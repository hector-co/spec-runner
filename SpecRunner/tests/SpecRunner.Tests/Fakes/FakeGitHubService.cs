using SpecRunner.Core.Abstractions;
using SpecRunner.Core.Models;

namespace SpecRunner.Tests.Fakes;

public class FakeGitHubService : IGitHubService
{
    public Task<string> GetAuthenticatedLoginAsync(CancellationToken cancellationToken = default)
        => Task.FromResult("spec-runner-bot");

    public Task<IReadOnlyList<GitHubIssue>> ListOpenIssuesWithCommentsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<GitHubIssue>>(Array.Empty<GitHubIssue>());

    public Task<IReadOnlyList<GitHubReaction>> ListCommentReactionsAsync(long commentId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<GitHubReaction>>(Array.Empty<GitHubReaction>());

    public Task AddCommentReactionAsync(long commentId, string reactionType, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task CreateIssueCommentAsync(int issueNumber, string body, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<int> CreatePullRequestAsync(string title, string body, string headBranch, string baseBranch, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<int> CreateDraftPullRequestAsync(string title, string body, string headBranch, string baseBranch, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<IReadOnlyList<GitHubPullRequest>> ListOpenPullRequestsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<GitHubPullRequest>>(Array.Empty<GitHubPullRequest>());

    public Task<IReadOnlyList<PrComment>> ReadPrCommentsAsync(int prNumber, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PrComment>>(Array.Empty<PrComment>());

    public Task WritePrCommentAsync(int prNumber, string body, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task MarkPrReadyForReviewAsync(int prNumber, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task UpdatePullRequestDescriptionAsync(int prNumber, string body, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task UpdatePullRequestTitleAsync(int prNumber, string title, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public IReadOnlyList<int> ClosingIssueNumbersResult { get; set; } = Array.Empty<int>();

    public Task<IReadOnlyList<int>> ListClosingIssueNumbersAsync(int prNumber, CancellationToken cancellationToken = default)
        => Task.FromResult(ClosingIssueNumbersResult);
}
