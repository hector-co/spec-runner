using SpecRunner.Core;

namespace SpecRunner.Tests;

public class RepositoryUrlParserTests
{
    [Fact]
    public void ParsesValidGitHubUrl()
    {
        var success = RepositoryUrlParser.TryParse("https://github.com/owner/repo", out var owner, out var repo);

        Assert.True(success);
        Assert.Equal("owner", owner);
        Assert.Equal("repo", repo);
    }

    [Fact]
    public void ParsesValidGitHubUrlWithTrailingGitSuffix()
    {
        var success = RepositoryUrlParser.TryParse("https://github.com/owner/repo.git", out var owner, out var repo);

        Assert.True(success);
        Assert.Equal("owner", owner);
        Assert.Equal("repo", repo);
    }

    [Fact]
    public void RejectsSshUrl()
    {
        var success = RepositoryUrlParser.TryParse("git@github.com:owner/repo.git", out _, out _);

        Assert.False(success);
    }

    [Fact]
    public void RejectsNonGitHubHost()
    {
        var success = RepositoryUrlParser.TryParse("https://gitlab.com/owner/repo", out _, out _);

        Assert.False(success);
    }

    [Fact]
    public void RejectsEmptyString()
    {
        var success = RepositoryUrlParser.TryParse(string.Empty, out _, out _);

        Assert.False(success);
    }
}
