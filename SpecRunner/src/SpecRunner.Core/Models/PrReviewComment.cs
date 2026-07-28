namespace SpecRunner.Core.Models;

public record PrReviewComment(long CommentId, string Path, string Author, string AuthorAssociation, string Body, DateTimeOffset CreatedAt);
