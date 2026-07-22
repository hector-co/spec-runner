using SpecRunner.Core.Abstractions;

namespace SpecRunner.Tests.Fakes;

public class RecordingGitService : IGitService
{
    public List<string> Calls { get; } = new();

    public Exception? ThrowOnCommit { get; set; }

    public Dictionary<string, bool> BranchExistsResults { get; } = new();

    public Task PullAsync(CancellationToken cancellationToken = default)
    {
        Calls.Add("Pull");
        return Task.CompletedTask;
    }

    public Task FetchAsync(string branchName, CancellationToken cancellationToken = default)
    {
        Calls.Add($"Fetch:{branchName}");
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

    public Task<bool> BranchExistsAsync(string branchName, CancellationToken cancellationToken = default)
    {
        Calls.Add($"BranchExists:{branchName}");
        return Task.FromResult(BranchExistsResults.GetValueOrDefault(branchName, false));
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
