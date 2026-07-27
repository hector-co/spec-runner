using Microsoft.Extensions.Options;
using SpecRunner.Console;
using SpecRunner.Core.Configuration;
using SpecRunner.Core.Models;
using SpecRunner.Tests.Fakes;

namespace SpecRunner.Tests;

public class PrAdoptionServiceTests
{
    private static PrAdoptionService CreateService(FakeGitService git, FakeGitHubService gitHub)
        => new(git, gitHub, Options.Create(new SpecRunnerOptions { BaseBranchName = "main" }));

    [Fact]
    public async Task OneFolderAndNoIssueAdoptsWithoutAnIssueNumber()
    {
        var git = new FakeGitService { AddedSpecFolderNamesResult = new[] { "add-csv-export" } };
        var gitHub = new FakeGitHubService { ClosingIssueNumbersResult = Array.Empty<int>() };
        var service = CreateService(git, gitHub);

        var result = await service.TryAdoptAsync(12, "contributor/csv-export");

        Assert.True(result.Succeeded);
        Assert.Null(result.TrackedIssue!.IssueNumber);
        Assert.Equal("add-csv-export", result.TrackedIssue.SpecName);
        Assert.Equal("contributor/csv-export", result.TrackedIssue.BranchName);
        Assert.Equal(12, result.TrackedIssue.PrNumber);
    }

    [Fact]
    public async Task OneFolderAndOneIssueAdoptsWithThatIssueNumber()
    {
        var git = new FakeGitService { AddedSpecFolderNamesResult = new[] { "add-csv-export" } };
        var gitHub = new FakeGitHubService { ClosingIssueNumbersResult = new[] { 45 } };
        var service = CreateService(git, gitHub);

        var result = await service.TryAdoptAsync(12, "contributor/csv-export");

        Assert.True(result.Succeeded);
        Assert.Equal(45, result.TrackedIssue!.IssueNumber);
        Assert.Equal("add-csv-export", result.TrackedIssue.SpecName);
        Assert.Equal(12, result.TrackedIssue.PrNumber);
    }

    [Fact]
    public async Task ZeroFoldersFailsWithNoSpecFolderFound()
    {
        var git = new FakeGitService { AddedSpecFolderNamesResult = Array.Empty<string>() };
        var gitHub = new FakeGitHubService();
        var service = CreateService(git, gitHub);

        var result = await service.TryAdoptAsync(12, "contributor/csv-export");

        Assert.False(result.Succeeded);
        Assert.Equal(PrAdoptionFailureReason.NoSpecFolderFound, result.FailureReason);
    }

    [Fact]
    public async Task MultipleFoldersFailsWithCandidateNames()
    {
        var git = new FakeGitService { AddedSpecFolderNamesResult = new[] { "add-csv-export", "add-pdf-export" } };
        var gitHub = new FakeGitHubService();
        var service = CreateService(git, gitHub);

        var result = await service.TryAdoptAsync(12, "contributor/csv-export");

        Assert.False(result.Succeeded);
        Assert.Equal(PrAdoptionFailureReason.MultipleSpecFoldersFound, result.FailureReason);
        Assert.Equal(new[] { "add-csv-export", "add-pdf-export" }, result.CandidateSpecFolderNames);
    }

    [Fact]
    public async Task MultipleIssuesFailsWithCandidateNumbers()
    {
        var git = new FakeGitService { AddedSpecFolderNamesResult = new[] { "add-csv-export" } };
        var gitHub = new FakeGitHubService { ClosingIssueNumbersResult = new[] { 45, 46 } };
        var service = CreateService(git, gitHub);

        var result = await service.TryAdoptAsync(12, "contributor/csv-export");

        Assert.False(result.Succeeded);
        Assert.Equal(PrAdoptionFailureReason.MultipleIssuesFound, result.FailureReason);
        Assert.Equal(new[] { 45, 46 }, result.CandidateIssueNumbers);
    }
}
