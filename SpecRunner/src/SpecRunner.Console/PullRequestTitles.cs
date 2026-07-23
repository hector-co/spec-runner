namespace SpecRunner.Console;

internal static class PullRequestTitles
{
    internal static string ExtractIssueName(string currentTitle, int issueNumber)
    {
        var marker = $"#{issueNumber}: ";
        var index = currentTitle.IndexOf(marker, StringComparison.Ordinal);
        return index < 0 ? currentTitle : currentTitle[(index + marker.Length)..];
    }
}
