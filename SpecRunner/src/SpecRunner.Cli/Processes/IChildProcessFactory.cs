namespace SpecRunner.Cli.Processes;

internal interface IChildProcessFactory
{
    IChildProcess Create(string executable, IReadOnlyList<string> arguments, string workingDirectory);
}
