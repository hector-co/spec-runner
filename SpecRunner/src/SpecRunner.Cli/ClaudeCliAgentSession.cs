using System.Text.Json;
using System.Threading.Channels;
using SpecRunner.Cli.Processes;
using SpecRunner.Core.Abstractions;
using SpecRunner.Core.Configuration;
using SpecRunner.Core.Models;

namespace SpecRunner.Cli;

public class ClaudeCliAgentSession : ICliAgentSession
{
    private static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(2);

    private readonly CliAgentOptions _cliAgentOptions;
    private readonly SpecRunnerOptions _specRunnerOptions;
    private readonly IChildProcessFactory _processFactory;
    private readonly Channel<CliAgentEvent> _events = Channel.CreateUnbounded<CliAgentEvent>();
    private readonly object _stateLock = new();

    private IChildProcess? _process;
    private volatile bool _stopRequested;

    public ClaudeCliAgentSession(CliAgentOptions cliAgentOptions, SpecRunnerOptions specRunnerOptions)
        : this(cliAgentOptions, specRunnerOptions, new SystemChildProcessFactory())
    {
    }

    internal ClaudeCliAgentSession(CliAgentOptions cliAgentOptions, SpecRunnerOptions specRunnerOptions, IChildProcessFactory processFactory)
    {
        _cliAgentOptions = cliAgentOptions;
        _specRunnerOptions = specRunnerOptions;
        _processFactory = processFactory;
    }

    public CliAgentSessionState State { get; private set; } = CliAgentSessionState.NotStarted;

    public async Task StartAsync(string initialPrompt, CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (State != CliAgentSessionState.NotStarted)
            {
                throw new InvalidOperationException($"Cannot start a session in state {State}; expected {CliAgentSessionState.NotStarted}.");
            }
        }

        var workingDirectory = string.IsNullOrWhiteSpace(_cliAgentOptions.WorkingDirectory)
            ? _specRunnerOptions.LocalRepositoryPath
            : _cliAgentOptions.WorkingDirectory;

        var arguments = new List<string>(_cliAgentOptions.Arguments)
        {
            "--print",
            "--verbose",
            "--input-format",
            "stream-json",
            "--output-format",
            "stream-json",
            "--dangerously-skip-permissions"
        };

        _process = _processFactory.Create(_cliAgentOptions.Executable, arguments, workingDirectory);
        _process.OutputLineReceived += OnOutputLineReceived;
        _process.Exited += OnProcessExited;
        _process.Start();

        lock (_stateLock)
        {
            State = CliAgentSessionState.Running;
        }

        await SendUserTurnAsync(initialPrompt, cancellationToken).ConfigureAwait(false);
    }

    public IAsyncEnumerable<CliAgentEvent> ReadEventsAsync(CancellationToken cancellationToken = default)
        => _events.Reader.ReadAllAsync(cancellationToken);

    public async Task SendCommandAsync(string text, CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (State != CliAgentSessionState.Running)
            {
                throw new InvalidOperationException($"Cannot send a command while the session is in state {State}.");
            }
        }

        await SendUserTurnAsync(text, cancellationToken).ConfigureAwait(false);
    }

    public Task CloseInputAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (State != CliAgentSessionState.Running)
            {
                throw new InvalidOperationException($"Cannot close input while the session is in state {State}.");
            }
        }

        _process!.CloseStandardInput();
        return Task.CompletedTask;
    }

    public async Task CancelCurrentRequestAsync(CancellationToken cancellationToken = default)
    {
        bool isRunning;
        lock (_stateLock)
        {
            isRunning = State == CliAgentSessionState.Running;
        }

        if (!isRunning || _process is null)
        {
            return;
        }

        await _process.WriteLineAsync(BuildInterruptJson(), cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (State is CliAgentSessionState.Stopped or CliAgentSessionState.Completed or CliAgentSessionState.Failed)
            {
                return;
            }
        }

        _stopRequested = true;

        if (_process is not null)
        {
            _process.CloseStandardInput();

            using var graceCts = new CancellationTokenSource(GracePeriod);
            try
            {
                await _process.WaitForExitAsync(graceCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Grace period elapsed; fall through to force-kill below.
            }

            if (!_process.HasExited)
            {
                _process.Kill();
            }
        }

        lock (_stateLock)
        {
            State = CliAgentSessionState.Stopped;
        }

        _events.Writer.TryComplete();
    }

    public async ValueTask DisposeAsync()
    {
        CliAgentSessionState current;
        lock (_stateLock)
        {
            current = State;
        }

        if (current is not (CliAgentSessionState.Completed or CliAgentSessionState.Failed or CliAgentSessionState.Stopped))
        {
            await StopAsync().ConfigureAwait(false);
        }

        if (_process is not null)
        {
            await _process.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task SendUserTurnAsync(string text, CancellationToken cancellationToken)
    {
        await _process!.WriteLineAsync(BuildUserTurnJson(text), cancellationToken).ConfigureAwait(false);
    }

    private void OnOutputLineReceived(string line)
    {
        _events.Writer.TryWrite(ParseLine(line));
    }

    private void OnProcessExited(int exitCode)
    {
        if (_stopRequested)
        {
            return;
        }

        var stdErr = _process?.ReadStandardErrorToEnd() ?? string.Empty;

        CliAgentSessionState newState;
        CliAgentEventKind kind;
        string payload;

        if (exitCode == 0)
        {
            newState = CliAgentSessionState.Completed;
            kind = CliAgentEventKind.ResultCompleted;
            payload = "Process exited normally.";
        }
        else
        {
            newState = CliAgentSessionState.Failed;
            kind = CliAgentEventKind.Error;
            payload = $"Process exited with code {exitCode}.";
        }

        lock (_stateLock)
        {
            if (State is CliAgentSessionState.Stopped or CliAgentSessionState.Completed or CliAgentSessionState.Failed)
            {
                return;
            }

            State = newState;
        }

        _events.Writer.TryWrite(new CliAgentEvent(kind, payload, string.Empty, DateTimeOffset.UtcNow)
        {
            ExitCode = exitCode,
            StdErr = stdErr
        });
        _events.Writer.TryComplete();
    }

    private static string BuildUserTurnJson(string text)
    {
        var message = new
        {
            type = "user",
            message = new
            {
                role = "user",
                content = new[] { new { type = "text", text } }
            }
        };
        return JsonSerializer.Serialize(message);
    }

    private static string BuildInterruptJson()
    {
        var message = new
        {
            type = "control_request",
            request_id = Guid.NewGuid().ToString("N"),
            request = new { subtype = "interrupt" }
        };
        return JsonSerializer.Serialize(message);
    }

    private static CliAgentEvent ParseLine(string line)
    {
        var timestamp = DateTimeOffset.UtcNow;

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;

            if (!root.TryGetProperty("type", out var typeProperty))
            {
                return new CliAgentEvent(CliAgentEventKind.Error, "Stream-json line missing 'type' field.", line, timestamp);
            }

            var type = typeProperty.GetString();
            return type switch
            {
                "assistant" => new CliAgentEvent(CliAgentEventKind.AssistantMessage, ExtractMessageText(root), line, timestamp),
                "tool_use" => new CliAgentEvent(CliAgentEventKind.ToolUse, ExtractToolUsePayload(root), line, timestamp),
                "tool_result" => new CliAgentEvent(CliAgentEventKind.ToolResult, ExtractMessageText(root), line, timestamp),
                "system" => new CliAgentEvent(CliAgentEventKind.SystemInfo, ExtractSubtypePayload(root, "System"), line, timestamp),
                "error" => new CliAgentEvent(CliAgentEventKind.Error, ExtractErrorPayload(root), line, timestamp),
                "result" => new CliAgentEvent(CliAgentEventKind.ResultCompleted, ExtractSubtypePayload(root, "Result"), line, timestamp),
                _ => new CliAgentEvent(CliAgentEventKind.Error, $"Unrecognized stream-json type '{type}'.", line, timestamp)
            };
        }
        catch (JsonException ex)
        {
            return new CliAgentEvent(CliAgentEventKind.Error, $"Failed to parse stream-json line: {ex.Message}", line, timestamp);
        }
    }

    private static string ExtractMessageText(JsonElement root)
    {
        if (root.TryGetProperty("message", out var message) &&
            message.TryGetProperty("content", out var content) &&
            content.ValueKind == JsonValueKind.Array)
        {
            var texts = new List<string>();
            foreach (var item in content.EnumerateArray())
            {
                if (item.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                {
                    texts.Add(text.GetString() ?? string.Empty);
                }
            }

            if (texts.Count > 0)
            {
                return string.Join(string.Empty, texts);
            }
        }

        return root.GetRawText();
    }

    private static string ExtractToolUsePayload(JsonElement root)
    {
        if (root.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
        {
            return $"Tool use: {name.GetString()}";
        }

        return root.GetRawText();
    }

    private static string ExtractSubtypePayload(JsonElement root, string label)
    {
        if (root.TryGetProperty("subtype", out var subtype) && subtype.ValueKind == JsonValueKind.String)
        {
            return $"{label}: {subtype.GetString()}";
        }

        return root.GetRawText();
    }

    private static string ExtractErrorPayload(JsonElement root)
    {
        if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
        {
            return message.GetString() ?? root.GetRawText();
        }

        return root.GetRawText();
    }
}
