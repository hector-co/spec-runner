using SpecRunner.Cli.Processes;

namespace SpecRunner.Tests.Fakes;

internal sealed class FakeChildProcessFactory : IChildProcessFactory
{
    public FakeChildProcess? LastCreated { get; private set; }

    public string? Executable { get; private set; }

    public IReadOnlyList<string>? Arguments { get; private set; }

    public string? WorkingDirectory { get; private set; }

    public int? NextAutoExitCode { get; set; }

    public Exception? NextStartException { get; set; }

    public IChildProcess Create(string executable, IReadOnlyList<string> arguments, string workingDirectory)
    {
        Executable = executable;
        Arguments = arguments;
        WorkingDirectory = workingDirectory;
        LastCreated = new FakeChildProcess
        {
            AutoExitCode = NextAutoExitCode,
            StartException = NextStartException
        };
        return LastCreated;
    }
}
