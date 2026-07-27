using SpecRunner.Core.Models;

namespace SpecRunner.Core.Abstractions;

public interface IStateStore
{
    Task<TrackedIssue?> FindByIssueNumberAsync(int issueNumber, CancellationToken cancellationToken = default);

    Task<TrackedIssue?> FindByPrNumberAsync(int prNumber, CancellationToken cancellationToken = default);

    Task<TrackedIssue?> FindByCommentIdAsync(long commentId, CancellationToken cancellationToken = default);

    Task<TrackedIssue> UpsertTrackedIssueAsync(TrackedIssue issue, CancellationToken cancellationToken = default);

    Task<TrackedComment> UpsertCommentAsync(int prNumber, TrackedComment comment, CancellationToken cancellationToken = default);
}
