using SpecRunner.Core.Models;

namespace SpecRunner.Core.Abstractions;

public interface IStateStore
{
    Task<SpecRunnerState> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(SpecRunnerState state, CancellationToken cancellationToken = default);

    Task<TrackedIssue?> FindByIssueNumberAsync(int issueNumber, CancellationToken cancellationToken = default);

    Task<TrackedIssue?> FindByPrNumberAsync(int prNumber, CancellationToken cancellationToken = default);
}
