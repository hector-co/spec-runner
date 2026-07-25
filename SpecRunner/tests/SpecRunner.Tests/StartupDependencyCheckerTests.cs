using Microsoft.Extensions.Options;
using SpecRunner.Console;
using SpecRunner.Core.Configuration;
using SpecRunner.Core.Models;
using SpecRunner.Tests.Fakes;

namespace SpecRunner.Tests;

public class StartupDependencyCheckerTests
{
    private static StartupDependencyChecker CreateChecker(
        FakeCliToolAvailabilityChecker cliToolAvailabilityChecker,
        FakeRepositoryConnectionTester repositoryConnectionTester,
        CliAgentOptions? cliAgentOptions = null,
        OpenSpecCliOptions? openSpecCliOptions = null)
        => new(
            cliToolAvailabilityChecker,
            repositoryConnectionTester,
            Options.Create(cliAgentOptions ?? new CliAgentOptions { Executable = "claude" }),
            Options.Create(openSpecCliOptions ?? new OpenSpecCliOptions { Executable = "openspec" }));

    [Fact]
    public async Task AllChecksSucceedingReturnsThreeSuccessfulResults()
    {
        var cliChecker = new FakeCliToolAvailabilityChecker
        {
            DefaultResult = new ToolAvailabilityResult(ToolAvailabilityStatus.Available, "ok")
        };
        var connectionTester = new FakeRepositoryConnectionTester
        {
            Result = new RepositoryConnectionResult(RepositoryConnectionStatus.Connected, "connected")
        };
        var checker = CreateChecker(cliChecker, connectionTester);

        var results = await checker.CheckAllAsync();

        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.True(r.IsSuccessful));
    }

    [Fact]
    public async Task ResultsAreOrderedClaudeThenOpenSpecThenGitHub()
    {
        var cliChecker = new FakeCliToolAvailabilityChecker();
        var connectionTester = new FakeRepositoryConnectionTester();
        var checker = CreateChecker(
            cliChecker,
            connectionTester,
            cliAgentOptions: new CliAgentOptions { Executable = "claude" },
            openSpecCliOptions: new OpenSpecCliOptions { Executable = "openspec" });

        var results = await checker.CheckAllAsync();

        Assert.Equal("Claude CLI", results[0].Name);
        Assert.Equal("OpenSpec CLI", results[1].Name);
        Assert.Equal("GitHub connection", results[2].Name);
        Assert.Equal(new[] { "claude", "openspec" }, cliChecker.CheckedExecutables);
    }

    [Fact]
    public async Task OneCheckFailingDoesNotPreventTheOthersFromRunning()
    {
        var cliChecker = new FakeCliToolAvailabilityChecker();
        cliChecker.SetResult("claude", new ToolAvailabilityResult(ToolAvailabilityStatus.NotFound, "missing"));
        var connectionTester = new FakeRepositoryConnectionTester
        {
            Result = new RepositoryConnectionResult(RepositoryConnectionStatus.Connected, "connected")
        };
        var checker = CreateChecker(cliChecker, connectionTester);

        var results = await checker.CheckAllAsync();

        Assert.Equal(3, results.Count);
        Assert.False(results[0].IsSuccessful);
        Assert.True(results[1].IsSuccessful);
        Assert.True(results[2].IsSuccessful);
        Assert.Contains("openspec", cliChecker.CheckedExecutables);
        Assert.True(connectionTester.WasCalled);
    }
}
