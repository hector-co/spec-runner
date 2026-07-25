using SpecRunner.Cli.Processes;
using SpecRunner.Core.Abstractions;
using SpecRunner.Core.Models;

namespace SpecRunner.Cli;

public class ProcessCliToolAvailabilityChecker : ICliToolAvailabilityChecker
{
    private readonly IChildProcessFactory _processFactory;

    public ProcessCliToolAvailabilityChecker()
        : this(new SystemChildProcessFactory())
    {
    }

    internal ProcessCliToolAvailabilityChecker(IChildProcessFactory processFactory)
    {
        _processFactory = processFactory;
    }

    public async Task<ToolAvailabilityResult> CheckAsync(string executable, CancellationToken cancellationToken = default)
    {
        IChildProcess process;
        try
        {
            process = _processFactory.Create(executable, new[] { "--version" }, Directory.GetCurrentDirectory());
            process.Start();
        }
        catch (Exception ex)
        {
            return new ToolAvailabilityResult(
                ToolAvailabilityStatus.NotFound,
                $"Could not start '{executable}': {ex.Message}");
        }

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            return process.ExitCode == 0
                ? new ToolAvailabilityResult(ToolAvailabilityStatus.Available, $"'{executable} --version' exited with code 0.")
                : new ToolAvailabilityResult(
                    ToolAvailabilityStatus.LaunchFailed,
                    $"'{executable} --version' exited with code {process.ExitCode}.");
        }
        finally
        {
            await process.DisposeAsync().ConfigureAwait(false);
        }
    }
}
