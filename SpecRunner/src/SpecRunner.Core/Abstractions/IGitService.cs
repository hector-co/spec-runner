namespace SpecRunner.Core.Abstractions;

public interface IGitService
{
    Task CreateBranchAsync(string branchName, CancellationToken cancellationToken = default);

    Task SwitchBranchAsync(string branchName, CancellationToken cancellationToken = default);

    Task CommitAsync(string message, CancellationToken cancellationToken = default);

    Task PushAsync(string branchName, CancellationToken cancellationToken = default);

    Task PullAsync(CancellationToken cancellationToken = default);
}
