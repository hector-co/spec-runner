namespace SpecRunner.Core.Models;

public record PrComment(long CommentId, string Author, string AuthorAssociation, string Body, DateTimeOffset CreatedAt);
