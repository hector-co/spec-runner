namespace SpecRunner.Core.Models;

public record GitHubIssue(int Number, string Title, string Body, IReadOnlyList<GitHubIssueComment> Comments);
