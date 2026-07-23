using SpecRunner.Console;

namespace SpecRunner.Tests;

public class PullRequestTitlesTests
{
    [Fact]
    public void ExtractIssueNameReturnsTextFollowingTheIssueNumberMarker()
    {
        var issueName = PullRequestTitles.ExtractIssueName("Proposal for #45: Add login page", 45);

        Assert.Equal("Add login page", issueName);
    }

    [Fact]
    public void ExtractIssueNameFallsBackToWholeTitleWhenMarkerIsMissing()
    {
        var issueName = PullRequestTitles.ExtractIssueName("A manually renamed title", 45);

        Assert.Equal("A manually renamed title", issueName);
    }
}
