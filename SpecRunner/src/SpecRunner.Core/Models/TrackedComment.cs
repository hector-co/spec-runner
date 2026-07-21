namespace SpecRunner.Core.Models;

public record TrackedComment(long CommentId, CommentKind CommentKind, CommentStatus Status)
{
    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }
}
