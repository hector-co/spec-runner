using SpecRunner.Core.Abstractions;
using SpecRunner.Core.Models;

namespace SpecRunner.Tests.Fakes;

public class RecordingGitHubService : IGitHubService
{
    public string Login { get; set; } = "spec-runner-bot";

    public List<GitHubIssue> Issues { get; } = new();

    public List<GitHubPullRequest> PullRequests { get; } = new();

    public Dictionary<int, List<PrComment>> PrComments { get; } = new();

    public Dictionary<long, List<GitHubReaction>> Reactions { get; } = new();

    public List<(long CommentId, string ReactionType)> AddedReactions { get; } = new();

    public List<(int IssueNumber, string Body)> CreatedComments { get; } = new();

    public List<(int PrNumber, string Body)> WrittenPrComments { get; } = new();

    public List<(string Title, string Body, string HeadBranch, string BaseBranch)> CreatedDraftPrs { get; } = new();

    public int NextPrNumber { get; set; } = 12;

    public Exception? ThrowOnCreateDraftPr { get; set; }

    public bool ThrowOnCreateDraftPrOnlyOnce { get; set; }

    public List<int> MarkedReadyForReview { get; } = new();

    public Exception? ThrowOnMarkPrReadyForReview { get; set; }

    public List<(int PrNumber, string Body)> UpdatedPullRequestDescriptions { get; } = new();

    public Exception? ThrowOnUpdatePullRequestDescription { get; set; }

    public Task<string> GetAuthenticatedLoginAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Login);

    public Task<IReadOnlyList<GitHubIssue>> ListOpenIssuesWithCommentsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<GitHubIssue>>(Issues);

    public Task<IReadOnlyList<GitHubReaction>> ListCommentReactionsAsync(long commentId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<GitHubReaction>>(
            Reactions.TryGetValue(commentId, out var list) ? list : new List<GitHubReaction>());

    public Task AddCommentReactionAsync(long commentId, string reactionType, CancellationToken cancellationToken = default)
    {
        AddedReactions.Add((commentId, reactionType));

        if (!Reactions.TryGetValue(commentId, out var list))
        {
            list = new List<GitHubReaction>();
            Reactions[commentId] = list;
        }

        list.Add(new GitHubReaction(Login, reactionType));
        return Task.CompletedTask;
    }

    public Task CreateIssueCommentAsync(int issueNumber, string body, CancellationToken cancellationToken = default)
    {
        CreatedComments.Add((issueNumber, body));
        return Task.CompletedTask;
    }

    public Task<int> CreatePullRequestAsync(string title, string body, string headBranch, string baseBranch, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<int> CreateDraftPullRequestAsync(string title, string body, string headBranch, string baseBranch, CancellationToken cancellationToken = default)
    {
        if (ThrowOnCreateDraftPr is not null)
        {
            var exception = ThrowOnCreateDraftPr;
            if (ThrowOnCreateDraftPrOnlyOnce)
            {
                ThrowOnCreateDraftPr = null;
            }

            throw exception;
        }

        CreatedDraftPrs.Add((title, body, headBranch, baseBranch));
        return Task.FromResult(NextPrNumber);
    }

    public Task<IReadOnlyList<GitHubPullRequest>> ListOpenPullRequestsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<GitHubPullRequest>>(PullRequests);

    public Task<IReadOnlyList<PrComment>> ReadPrCommentsAsync(int prNumber, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PrComment>>(
            PrComments.TryGetValue(prNumber, out var list) ? list : new List<PrComment>());

    public Task WritePrCommentAsync(int prNumber, string body, CancellationToken cancellationToken = default)
    {
        WrittenPrComments.Add((prNumber, body));
        return Task.CompletedTask;
    }

    public Task MarkPrReadyForReviewAsync(int prNumber, CancellationToken cancellationToken = default)
    {
        if (ThrowOnMarkPrReadyForReview is not null)
        {
            throw ThrowOnMarkPrReadyForReview;
        }

        MarkedReadyForReview.Add(prNumber);
        return Task.CompletedTask;
    }

    public Task UpdatePullRequestDescriptionAsync(int prNumber, string body, CancellationToken cancellationToken = default)
    {
        if (ThrowOnUpdatePullRequestDescription is not null)
        {
            throw ThrowOnUpdatePullRequestDescription;
        }

        UpdatedPullRequestDescriptions.Add((prNumber, body));
        return Task.CompletedTask;
    }
}
