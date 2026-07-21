using SpecRunner.Core.Abstractions;

namespace SpecRunner.Tests.Fakes;

public class FakeGitService : IGitService
{
    public Task CreateBranchAsync(string branchName, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SwitchBranchAsync(string branchName, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task CommitAsync(string message, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task PushAsync(string branchName, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task PullAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
