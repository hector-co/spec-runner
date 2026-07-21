using SpecRunner.Cli.Processes;

namespace SpecRunner.Tests.Fakes;

internal sealed class FakeChildProcess : IChildProcess
{
    private readonly TaskCompletionSource _exitTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private string _stdErr = string.Empty;

    public List<string> WrittenLines { get; } = new();

    public bool Started { get; private set; }

    public bool StandardInputClosed { get; private set; }

    public bool Killed { get; private set; }

    public event Action<string>? OutputLineReceived;

    public event Action<int>? Exited;

    public bool HasExited { get; private set; }

    public int ExitCode { get; private set; }

    public void Start() => Started = true;

    public Task WriteLineAsync(string line, CancellationToken cancellationToken = default)
    {
        WrittenLines.Add(line);
        return Task.CompletedTask;
    }

    public void CloseStandardInput() => StandardInputClosed = true;

    public Task WaitForExitAsync(CancellationToken cancellationToken = default)
        => HasExited ? Task.CompletedTask : _exitTcs.Task.WaitAsync(cancellationToken);

    public string ReadStandardErrorToEnd() => _stdErr;

    public void Kill()
    {
        Killed = true;
        if (!HasExited)
        {
            SimulateExit(-1);
        }
    }

    public void EmitOutputLine(string line) => OutputLineReceived?.Invoke(line);

    public void SetStandardError(string text) => _stdErr = text;

    public void SimulateExit(int exitCode)
    {
        if (HasExited)
        {
            return;
        }

        HasExited = true;
        ExitCode = exitCode;
        _exitTcs.TrySetResult();
        Exited?.Invoke(exitCode);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
