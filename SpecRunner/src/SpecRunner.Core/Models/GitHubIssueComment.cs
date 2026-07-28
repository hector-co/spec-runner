namespace SpecRunner.Core.Models;

public record GitHubIssueComment(long CommentId, string Author, string AuthorAssociation, string Body, DateTimeOffset CreatedAt);
