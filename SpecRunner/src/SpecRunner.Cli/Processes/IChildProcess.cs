namespace SpecRunner.Cli.Processes;

internal interface IChildProcess : IAsyncDisposable
{
    bool HasExited { get; }

    int ExitCode { get; }

    event Action<string>? OutputLineReceived;

    event Action<int>? Exited;

    void Start();

    Task WriteLineAsync(string line, CancellationToken cancellationToken = default);

    void CloseStandardInput();

    Task WaitForExitAsync(CancellationToken cancellationToken = default);

    string ReadStandardErrorToEnd();

    void Kill();
}
