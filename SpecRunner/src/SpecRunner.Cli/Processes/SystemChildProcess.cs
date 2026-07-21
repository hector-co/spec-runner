using System.Diagnostics;
using System.Text;

namespace SpecRunner.Cli.Processes;

internal sealed class SystemChildProcess : IChildProcess
{
    private readonly Process _process;
    private readonly StringBuilder _stdErr = new();
    private readonly object _stdErrLock = new();
    private bool _exitedRaised;

    public SystemChildProcess(string executable, IReadOnlyList<string> arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                OutputLineReceived?.Invoke(e.Data);
            }
        };
        _process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (_stdErrLock)
                {
                    _stdErr.AppendLine(e.Data);
                }
            }
        };
        _process.Exited += (_, _) =>
        {
            if (_exitedRaised)
            {
                return;
            }

            _exitedRaised = true;

            // Guarantees redirected stdout/stderr callbacks have fully drained before
            // we report the exit code, avoiding a race between Exited and the async reads.
            _process.WaitForExit();
            Exited?.Invoke(_process.ExitCode);
        };
    }

    public event Action<string>? OutputLineReceived;

    public event Action<int>? Exited;

    public bool HasExited => _process.HasExited;

    public int ExitCode => _process.ExitCode;

    public void Start()
    {
        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    public async Task WriteLineAsync(string line, CancellationToken cancellationToken = default)
    {
        await _process.StandardInput.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
        await _process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public void CloseStandardInput() => _process.StandardInput.Close();

    public Task WaitForExitAsync(CancellationToken cancellationToken = default) => _process.WaitForExitAsync(cancellationToken);

    public string ReadStandardErrorToEnd()
    {
        lock (_stdErrLock)
        {
            return _stdErr.ToString();
        }
    }

    public void Kill()
    {
        try
        {
            _process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Process already exited.
        }
    }

    public ValueTask DisposeAsync()
    {
        _process.Dispose();
        return ValueTask.CompletedTask;
    }
}
