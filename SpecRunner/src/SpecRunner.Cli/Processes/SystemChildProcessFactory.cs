namespace SpecRunner.Cli.Processes;

internal sealed class SystemChildProcessFactory : IChildProcessFactory
{
    public IChildProcess Create(string executable, IReadOnlyList<string> arguments, string workingDirectory)
        => new SystemChildProcess(executable, arguments, workingDirectory);
}
