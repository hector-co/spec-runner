using SpecRunner.Cli;
using SpecRunner.Core.Models;
using SpecRunner.Tests.Fakes;

namespace SpecRunner.Tests;

public class ProcessCliToolAvailabilityCheckerTests
{
    private static (ProcessCliToolAvailabilityChecker Checker, FakeChildProcessFactory Factory) CreateChecker()
    {
        var factory = new FakeChildProcessFactory();
        var checker = new ProcessCliToolAvailabilityChecker(factory);
        return (checker, factory);
    }

    [Fact]
    public async Task ReturnsAvailableWhenProcessExitsWithZero()
    {
        var (checker, factory) = CreateChecker();
        factory.NextAutoExitCode = 0;

        var result = await checker.CheckAsync("claude");

        Assert.Equal(ToolAvailabilityStatus.Available, result.Status);
    }

    [Fact]
    public async Task ReturnsLaunchFailedWithExitCodeWhenProcessExitsNonZero()
    {
        var (checker, factory) = CreateChecker();
        factory.NextAutoExitCode = 3;

        var result = await checker.CheckAsync("claude");

        Assert.Equal(ToolAvailabilityStatus.LaunchFailed, result.Status);
        Assert.Contains("3", result.Message);
    }

    [Fact]
    public async Task ReturnsNotFoundWhenProcessFailsToStart()
    {
        var (checker, factory) = CreateChecker();
        factory.NextStartException = new InvalidOperationException("no such file");

        var result = await checker.CheckAsync("does-not-exist");

        Assert.Equal(ToolAvailabilityStatus.NotFound, result.Status);
        Assert.Contains("no such file", result.Message);
    }

    [Fact]
    public async Task PassesVersionFlagToTheConfiguredExecutable()
    {
        var (checker, factory) = CreateChecker();
        factory.NextAutoExitCode = 0;

        await checker.CheckAsync("openspec");

        Assert.Equal("openspec", factory.Executable);
        Assert.Contains("--version", factory.Arguments!);
    }
}
