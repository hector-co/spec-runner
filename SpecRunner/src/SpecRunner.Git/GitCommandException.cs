namespace SpecRunner.Git;

public class GitCommandException : Exception
{
    public GitCommandException(string command, int exitCode, string standardError)
        : base($"Git command '{command}' failed with exit code {exitCode}: {standardError}")
    {
        Command = command;
        ExitCode = exitCode;
        StandardError = standardError;
    }

    public string Command { get; }

    public int ExitCode { get; }

    public string StandardError { get; }
}
