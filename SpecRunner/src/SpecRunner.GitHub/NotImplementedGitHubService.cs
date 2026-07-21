using SpecRunner.Core.Abstractions;
using SpecRunner.Core.Models;

namespace SpecRunner.GitHub;

public class NotImplementedGitHubService : IGitHubService
{
    public Task<int> CreatePullRequestAsync(string title, string body, string headBranch, string baseBranch, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<int> CreateDraftPullRequestAsync(string title, string body, string headBranch, string baseBranch, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<IReadOnlyList<PrComment>> ReadPrCommentsAsync(int prNumber, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task WritePrCommentAsync(int prNumber, string body, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task MarkPrReadyForReviewAsync(int prNumber, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
