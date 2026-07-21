using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpecRunner.Core.Abstractions;
using SpecRunner.Core.Configuration;
using SpecRunner.Core.Models;

namespace SpecRunner.Console;

public class ProposeWorkflowRunner : IProposeWorkflowRunner
{
    private const string ProposeTrigger = "/propose";
    private static readonly string[] BotStatusReactionTypes = { "eyes", "rocket", "confused" };

    private readonly IGitHubService _gitHub;
    private readonly IGitService _git;
    private readonly IStateStore _stateStore;
    private readonly ICliAgentSessionFactory _cliAgentSessionFactory;
    private readonly ISpecNameResolver _specNameResolver;
    private readonly SpecRunnerOptions _options;
    private readonly ILogger<ProposeWorkflowRunner> _logger;

    public ProposeWorkflowRunner(
        IGitHubService gitHub,
        IGitService git,
        IStateStore stateStore,
        ICliAgentSessionFactory cliAgentSessionFactory,
        ISpecNameResolver specNameResolver,
        IOptions<SpecRunnerOptions> options,
        ILogger<ProposeWorkflowRunner> logger)
    {
        _gitHub = gitHub;
        _git = git;
        _stateStore = stateStore;
        _cliAgentSessionFactory = cliAgentSessionFactory;
        _specNameResolver = specNameResolver;
        _options = options.Value;
        _logger = logger;
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var botLogin = await _gitHub.GetAuthenticatedLoginAsync(cancellationToken).ConfigureAwait(false);
        var issues = await _gitHub.ListOpenIssuesWithCommentsAsync(cancellationToken).ConfigureAwait(false);

        var eligibleComments = new List<EligibleProposeComment>();
        foreach (var issue in issues)
        {
            foreach (var comment in issue.Comments)
            {
                if (IsEligibleTrigger(comment.Body))
                {
                    eligibleComments.Add(new EligibleProposeComment(issue.Number, issue.Title, issue.Body, comment.CommentId));
                }
            }
        }

        foreach (var comment in eligibleComments)
        {
            var reactions = await _gitHub.ListCommentReactionsAsync(comment.CommentId, cancellationToken).ConfigureAwait(false);
            var alreadyHandled = reactions.Any(reaction =>
                reaction.AuthorLogin == botLogin && BotStatusReactionTypes.Contains(reaction.ReactionType));

            if (alreadyHandled)
            {
                continue;
            }

            await ProcessCommentAsync(comment, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsEligibleTrigger(string body)
    {
        var trimmed = body.Trim();
        if (trimmed == ProposeTrigger)
        {
            return true;
        }

        return trimmed.StartsWith(ProposeTrigger, StringComparison.Ordinal)
            && trimmed.Length > ProposeTrigger.Length
            && char.IsWhiteSpace(trimmed[ProposeTrigger.Length]);
    }

    private async Task ProcessCommentAsync(EligibleProposeComment comment, CancellationToken cancellationToken)
    {
        await _gitHub.AddCommentReactionAsync(comment.CommentId, "eyes", cancellationToken).ConfigureAwait(false);

        TrackedIssue? existingIssue = null;
        ICliAgentSession? session = null;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.TaskTimeout);

        try
        {
            existingIssue = await _stateStore.FindByIssueNumberAsync(comment.IssueNumber, timeoutCts.Token).ConfigureAwait(false);
            if (existingIssue?.PrNumber is int existingPrNumber)
            {
                await ReportAlreadyHasPrAsync(comment, existingPrNumber, timeoutCts.Token).ConfigureAwait(false);
                return;
            }

            await _git.PullAsync(timeoutCts.Token).ConfigureAwait(false);
            await _git.ResetHardAsync(_options.BaseBranchName, timeoutCts.Token).ConfigureAwait(false);

            var branchName = $"feature/{comment.IssueNumber}";
            await _git.CreateBranchAsync(branchName, timeoutCts.Token).ConfigureAwait(false);
            await _git.SwitchBranchAsync(branchName, timeoutCts.Token).ConfigureAwait(false);

            var specName = _specNameResolver.Resolve(comment.IssueNumber, comment.IssueTitle);

            session = _cliAgentSessionFactory.CreateSession();
            await session.StartAsync($"/opsx-propose {specName}\n{comment.IssueBody}", timeoutCts.Token).ConfigureAwait(false);

            await foreach (var _ in session.ReadEventsAsync(timeoutCts.Token).ConfigureAwait(false))
            {
                // Drain events; the channel completes once the session reaches a terminal state.
            }

            if (session.State != CliAgentSessionState.Completed)
            {
                throw new InvalidOperationException($"CLI agent session for issue #{comment.IssueNumber} ended in state {session.State}.");
            }

            await _git.CommitAsync($"adding specs for #{comment.IssueNumber}", timeoutCts.Token).ConfigureAwait(false);
            await _git.PushAsync(branchName, timeoutCts.Token).ConfigureAwait(false);

            var prNumber = await _gitHub.CreateDraftPullRequestAsync(
                $"Proposal for #{comment.IssueNumber}: {comment.IssueTitle}",
                comment.IssueBody,
                branchName,
                _options.BaseBranchName,
                timeoutCts.Token).ConfigureAwait(false);

            await ReportSuccessAsync(comment, specName, prNumber, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            if (session is not null)
            {
                await session.StopAsync(cancellationToken).ConfigureAwait(false);
            }

            await ReportTimeoutAsync(comment, existingIssue, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await ReportErrorAsync(comment, existingIssue, ex, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (session is not null)
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task ReportAlreadyHasPrAsync(EligibleProposeComment comment, int prNumber, CancellationToken cancellationToken)
    {
        await _gitHub.CreateIssueCommentAsync(
            comment.IssueNumber,
            $"This issue already has an active Draft PR: #{prNumber}. Please add /update to the PR instead.",
            cancellationToken).ConfigureAwait(false);
        await _gitHub.AddCommentReactionAsync(comment.CommentId, "rocket", cancellationToken).ConfigureAwait(false);
    }

    private async Task ReportSuccessAsync(EligibleProposeComment comment, string specName, int prNumber, CancellationToken cancellationToken)
    {
        await _gitHub.AddCommentReactionAsync(comment.CommentId, "rocket", cancellationToken).ConfigureAwait(false);
        await _gitHub.CreateIssueCommentAsync(comment.IssueNumber, $"Created Draft PR #{prNumber} for this issue.", cancellationToken).ConfigureAwait(false);

        await _stateStore.UpsertTrackedIssueAsync(new TrackedIssue(comment.IssueNumber, specName) { PrNumber = prNumber }, cancellationToken).ConfigureAwait(false);
        await _stateStore.UpsertCommentAsync(
            comment.IssueNumber,
            new TrackedComment(comment.CommentId, CommentKind.IssueComment, CommentStatus.Done),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task ReportTimeoutAsync(EligibleProposeComment comment, TrackedIssue? existingIssue, CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Processing /propose comment {CommentId} on issue #{IssueNumber} timed out after {Timeout}",
            comment.CommentId,
            comment.IssueNumber,
            _options.TaskTimeout);

        await _gitHub.AddCommentReactionAsync(comment.CommentId, "confused", cancellationToken).ConfigureAwait(false);
        await _gitHub.CreateIssueCommentAsync(comment.IssueNumber, "Processing this comment timed out.", cancellationToken).ConfigureAwait(false);
        await RecordCommentStatusAsync(comment, existingIssue, CommentStatus.Error, cancellationToken).ConfigureAwait(false);
    }

    private async Task ReportErrorAsync(EligibleProposeComment comment, TrackedIssue? existingIssue, Exception ex, CancellationToken cancellationToken)
    {
        _logger.LogError(ex, "Error processing /propose comment {CommentId} on issue #{IssueNumber}", comment.CommentId, comment.IssueNumber);

        await _gitHub.AddCommentReactionAsync(comment.CommentId, "confused", cancellationToken).ConfigureAwait(false);
        await _gitHub.CreateIssueCommentAsync(comment.IssueNumber, $"Processing this comment failed: {ex.Message}", cancellationToken).ConfigureAwait(false);
        await RecordCommentStatusAsync(comment, existingIssue, CommentStatus.Error, cancellationToken).ConfigureAwait(false);
    }

    private async Task RecordCommentStatusAsync(EligibleProposeComment comment, TrackedIssue? existingIssue, CommentStatus status, CancellationToken cancellationToken)
    {
        var specName = existingIssue?.SpecName ?? _specNameResolver.Resolve(comment.IssueNumber, comment.IssueTitle);
        var issueToUpsert = existingIssue ?? new TrackedIssue(comment.IssueNumber, specName);

        await _stateStore.UpsertTrackedIssueAsync(issueToUpsert, cancellationToken).ConfigureAwait(false);
        await _stateStore.UpsertCommentAsync(
            comment.IssueNumber,
            new TrackedComment(comment.CommentId, CommentKind.IssueComment, status),
            cancellationToken).ConfigureAwait(false);
    }
}
