using SpecRunner.Core.Abstractions;

namespace SpecRunner.Git;

public class NotImplementedGitService : IGitService
{
    public Task CreateBranchAsync(string branchName, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task SwitchBranchAsync(string branchName, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task CommitAsync(string message, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task PushAsync(string branchName, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task PullAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
