using SpecRunner.Core.Abstractions;

namespace SpecRunner.Tests.Fakes;

public class RecordingGitService : IGitService
{
    public List<string> Calls { get; } = new();

    public Exception? ThrowOnCommit { get; set; }

    public Task PullAsync(CancellationToken cancellationToken = default)
    {
        Calls.Add("Pull");
        return Task.CompletedTask;
    }

    public Task ResetHardAsync(string targetRef, CancellationToken cancellationToken = default)
    {
        Calls.Add($"ResetHard:{targetRef}");
        return Task.CompletedTask;
    }

    public Task CreateBranchAsync(string branchName, CancellationToken cancellationToken = default)
    {
        Calls.Add($"CreateBranch:{branchName}");
        return Task.CompletedTask;
    }

    public Task SwitchBranchAsync(string branchName, CancellationToken cancellationToken = default)
    {
        Calls.Add($"SwitchBranch:{branchName}");
        return Task.CompletedTask;
    }

    public Task CommitAsync(string message, CancellationToken cancellationToken = default)
    {
        Calls.Add($"Commit:{message}");
        if (ThrowOnCommit is not null)
        {
            throw ThrowOnCommit;
        }

        return Task.CompletedTask;
    }

    public Task PushAsync(string branchName, CancellationToken cancellationToken = default)
    {
        Calls.Add($"Push:{branchName}");
        return Task.CompletedTask;
    }
}
