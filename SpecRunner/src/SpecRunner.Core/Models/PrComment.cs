namespace SpecRunner.Core.Models;

public record PrComment(long CommentId, string Author, string Body, DateTimeOffset CreatedAt);
