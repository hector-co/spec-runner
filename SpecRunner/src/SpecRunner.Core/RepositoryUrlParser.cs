using System.Text.RegularExpressions;

namespace SpecRunner.Core;

public static partial class RepositoryUrlParser
{
    public static bool TryParse(string repositoryUrl, out string owner, out string repo)
    {
        owner = string.Empty;
        repo = string.Empty;

        if (string.IsNullOrWhiteSpace(repositoryUrl))
        {
            return false;
        }

        var match = GitHubUrlRegex().Match(repositoryUrl);
        if (!match.Success)
        {
            return false;
        }

        owner = match.Groups["owner"].Value;
        repo = match.Groups["repo"].Value;
        return true;
    }

    [GeneratedRegex(@"^https://github\.com/(?<owner>[^/\s]+)/(?<repo>[^/\s]+?)(\.git)?/?$")]
    private static partial Regex GitHubUrlRegex();
}
