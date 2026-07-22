namespace SpecRunner.Core.Models;

public record GitHubPullRequest(int Number, string Title, string Body, string HeadBranch);
