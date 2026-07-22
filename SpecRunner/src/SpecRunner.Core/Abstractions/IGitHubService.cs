using SpecRunner.Core.Models;

namespace SpecRunner.Core.Abstractions;

public interface IGitHubService
{
    Task<string> GetAuthenticatedLoginAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GitHubIssue>> ListOpenIssuesWithCommentsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GitHubReaction>> ListCommentReactionsAsync(long commentId, CancellationToken cancellationToken = default);

    Task AddCommentReactionAsync(long commentId, string reactionType, CancellationToken cancellationToken = default);

    Task CreateIssueCommentAsync(int issueNumber, string body, CancellationToken cancellationToken = default);

    Task<int> CreatePullRequestAsync(string title, string body, string headBranch, string baseBranch, CancellationToken cancellationToken = default);

    Task<int> CreateDraftPullRequestAsync(string title, string body, string headBranch, string baseBranch, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GitHubPullRequest>> ListOpenPullRequestsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PrComment>> ReadPrCommentsAsync(int prNumber, CancellationToken cancellationToken = default);

    Task WritePrCommentAsync(int prNumber, string body, CancellationToken cancellationToken = default);

    Task MarkPrReadyForReviewAsync(int prNumber, CancellationToken cancellationToken = default);

    Task UpdatePullRequestDescriptionAsync(int prNumber, string body, CancellationToken cancellationToken = default);
}
