using SpecRunner.Core.Abstractions;

namespace SpecRunner.Tests.Fakes;

public class FakeGitService : IGitService
{
    public Task PullAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task FetchAsync(string branchName, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ResetHardAsync(string targetRef, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task CreateBranchAsync(string branchName, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SwitchBranchAsync(string branchName, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public bool BranchExistsResult { get; set; } = false;

    public Task<bool> BranchExistsAsync(string branchName, CancellationToken cancellationToken = default) => Task.FromResult(BranchExistsResult);

    public Task CommitAsync(string message, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task PushAsync(string branchName, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public IReadOnlyList<string> AddedSpecFolderNamesResult { get; set; } = Array.Empty<string>();

    public Task<IReadOnlyList<string>> ListAddedSpecFolderNamesAsync(string baseBranch, string headBranch, CancellationToken cancellationToken = default)
        => Task.FromResult(AddedSpecFolderNamesResult);
}
