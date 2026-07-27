namespace SpecRunner.Core.Abstractions;

public interface IGitService
{
    Task PullAsync(CancellationToken cancellationToken = default);

    Task FetchAsync(string branchName, CancellationToken cancellationToken = default);

    Task ResetHardAsync(string targetRef, CancellationToken cancellationToken = default);

    Task CreateBranchAsync(string branchName, CancellationToken cancellationToken = default);

    Task SwitchBranchAsync(string branchName, CancellationToken cancellationToken = default);

    Task<bool> BranchExistsAsync(string branchName, CancellationToken cancellationToken = default);

    Task CommitAsync(string message, CancellationToken cancellationToken = default);

    Task PushAsync(string branchName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListAddedSpecFolderNamesAsync(string baseBranch, string headBranch, CancellationToken cancellationToken = default);
}
