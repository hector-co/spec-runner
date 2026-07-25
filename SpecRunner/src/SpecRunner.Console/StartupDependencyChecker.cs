using Microsoft.Extensions.Options;
using SpecRunner.Core.Abstractions;
using SpecRunner.Core.Configuration;
using SpecRunner.Core.Models;

namespace SpecRunner.Console;

public class StartupDependencyChecker : IStartupDependencyChecker
{
    private readonly ICliToolAvailabilityChecker _cliToolAvailabilityChecker;
    private readonly IRepositoryConnectionTester _repositoryConnectionTester;
    private readonly CliAgentOptions _cliAgentOptions;
    private readonly OpenSpecCliOptions _openSpecCliOptions;

    public StartupDependencyChecker(
        ICliToolAvailabilityChecker cliToolAvailabilityChecker,
        IRepositoryConnectionTester repositoryConnectionTester,
        IOptions<CliAgentOptions> cliAgentOptions,
        IOptions<OpenSpecCliOptions> openSpecCliOptions)
    {
        _cliToolAvailabilityChecker = cliToolAvailabilityChecker;
        _repositoryConnectionTester = repositoryConnectionTester;
        _cliAgentOptions = cliAgentOptions.Value;
        _openSpecCliOptions = openSpecCliOptions.Value;
    }

    public async Task<IReadOnlyList<DependencyCheckResult>> CheckAllAsync(CancellationToken cancellationToken = default)
    {
        var claudeCliResult = await _cliToolAvailabilityChecker
            .CheckAsync(_cliAgentOptions.Executable, cancellationToken)
            .ConfigureAwait(false);

        var openSpecCliResult = await _cliToolAvailabilityChecker
            .CheckAsync(_openSpecCliOptions.Executable, cancellationToken)
            .ConfigureAwait(false);

        var connectionResult = await _repositoryConnectionTester
            .TestConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        return new[]
        {
            ToDependencyCheckResult("Claude CLI", claudeCliResult),
            ToDependencyCheckResult("OpenSpec CLI", openSpecCliResult),
            new DependencyCheckResult(
                "GitHub connection",
                connectionResult.Status == RepositoryConnectionStatus.Connected,
                connectionResult.Message)
        };
    }

    private static DependencyCheckResult ToDependencyCheckResult(string name, ToolAvailabilityResult result)
        => new(name, result.Status == ToolAvailabilityStatus.Available, result.Message);
}
