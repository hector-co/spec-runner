namespace SpecRunner.Core.Models;

public record GitHubIssueComment(long CommentId, string Author, string Body, DateTimeOffset CreatedAt);
