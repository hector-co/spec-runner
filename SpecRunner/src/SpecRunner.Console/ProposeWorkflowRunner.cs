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
    private readonly ISpecFolderResolver _specFolderResolver;
    private readonly ITasksFileReader _tasksFileReader;
    private readonly ICommandTemplateRenderer _commandTemplateRenderer;
    private readonly SpecRunnerOptions _options;
    private readonly ILogger<ProposeWorkflowRunner> _logger;

    public ProposeWorkflowRunner(
        IGitHubService gitHub,
        IGitService git,
        IStateStore stateStore,
        ICliAgentSessionFactory cliAgentSessionFactory,
        ISpecNameResolver specNameResolver,
        ISpecFolderResolver specFolderResolver,
        ITasksFileReader tasksFileReader,
        ICommandTemplateRenderer commandTemplateRenderer,
        IOptions<SpecRunnerOptions> options,
        ILogger<ProposeWorkflowRunner> logger)
    {
        _gitHub = gitHub;
        _git = git;
        _stateStore = stateStore;
        _cliAgentSessionFactory = cliAgentSessionFactory;
        _specNameResolver = specNameResolver;
        _specFolderResolver = specFolderResolver;
        _tasksFileReader = tasksFileReader;
        _commandTemplateRenderer = commandTemplateRenderer;
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
        _logger.LogInformation("Starting /propose flow for issue #{IssueNumber} (comment {CommentId})", comment.IssueNumber, comment.CommentId);

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

            _logger.LogDebug("Starting git reset for issue #{IssueNumber}", comment.IssueNumber);
            await _git.ResetHardAsync("HEAD", timeoutCts.Token).ConfigureAwait(false);
            _logger.LogDebug("Finished git reset for issue #{IssueNumber}", comment.IssueNumber);

            _logger.LogDebug("Starting switch to base branch for issue #{IssueNumber}", comment.IssueNumber);
            await _git.SwitchBranchAsync(_options.BaseBranchName, timeoutCts.Token).ConfigureAwait(false);
            _logger.LogDebug("Finished switch to base branch for issue #{IssueNumber}", comment.IssueNumber);

            _logger.LogDebug("Starting git pull for issue #{IssueNumber}", comment.IssueNumber);
            await _git.PullAsync(timeoutCts.Token).ConfigureAwait(false);
            _logger.LogDebug("Finished git pull for issue #{IssueNumber}", comment.IssueNumber);

            _logger.LogDebug("Starting branch resolution for issue #{IssueNumber}", comment.IssueNumber);
            var branchName = $"feature/{comment.IssueNumber}";
            var suffix = 2;
            while (await _git.BranchExistsAsync(branchName, timeoutCts.Token).ConfigureAwait(false))
            {
                branchName = $"feature/{comment.IssueNumber}-{suffix}";
                suffix++;
            }

            await _git.CreateBranchAsync(branchName, timeoutCts.Token).ConfigureAwait(false);
            await _git.SwitchBranchAsync(branchName, timeoutCts.Token).ConfigureAwait(false);
            _logger.LogDebug("Finished branch resolution for issue #{IssueNumber}; using branch {BranchName}", comment.IssueNumber, branchName);

            var specName = _specNameResolver.Resolve(comment.IssueNumber, comment.IssueTitle);

            await _stateStore.UpsertTrackedIssueAsync(
                new TrackedIssue(comment.IssueNumber, specName) { BranchName = branchName },
                timeoutCts.Token).ConfigureAwait(false);

            _logger.LogDebug("Starting prompt rendering for issue #{IssueNumber}", comment.IssueNumber);
            var prompt = await _commandTemplateRenderer.RenderAsync(
                "propose",
                new Dictionary<string, string>
                {
                    ["spec_name"] = specName,
                    ["issue_title"] = comment.IssueTitle,
                    ["issue_body"] = comment.IssueBody
                },
                timeoutCts.Token).ConfigureAwait(false);
            _logger.LogDebug("Finished prompt rendering for issue #{IssueNumber}", comment.IssueNumber);

            session = _cliAgentSessionFactory.CreateSession();
            _logger.LogDebug("Starting CLI agent session for issue #{IssueNumber}", comment.IssueNumber);
            await session.StartAsync($"\"{prompt}\"", timeoutCts.Token).ConfigureAwait(false);
            await session.CloseInputAsync(timeoutCts.Token).ConfigureAwait(false);
            _logger.LogDebug("Finished starting CLI agent session for issue #{IssueNumber}", comment.IssueNumber);

            using var progressCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token);
            var progressTask = ProgressIndicator.RunAsync(
                _logger,
                $"/propose flow for issue #{comment.IssueNumber} still in progress",
                progressCts.Token);

            try
            {
                await foreach (var _ in session.ReadEventsAsync(timeoutCts.Token).ConfigureAwait(false))
                {
                    // Drain events; the channel completes once the session reaches a terminal state.
                }
            }
            finally
            {
                progressCts.Cancel();
                await progressTask.ConfigureAwait(false);
            }

            if (session.State != CliAgentSessionState.Completed)
            {
                throw new InvalidOperationException($"CLI agent session for issue #{comment.IssueNumber} ended in state {session.State}.");
            }

            _logger.LogDebug("Starting spec folder resolution for issue #{IssueNumber}", comment.IssueNumber);
            specName = await _specFolderResolver.ResolveAsync(specName, comment.IssueNumber, timeoutCts.Token).ConfigureAwait(false);
            _logger.LogDebug("Finished spec folder resolution for issue #{IssueNumber}", comment.IssueNumber);

            _logger.LogDebug("Starting commit for issue #{IssueNumber}", comment.IssueNumber);
            await _git.CommitAsync($"adding specs for #{comment.IssueNumber}", timeoutCts.Token).ConfigureAwait(false);
            _logger.LogDebug("Finished commit for issue #{IssueNumber}", comment.IssueNumber);

            _logger.LogDebug("Starting push for issue #{IssueNumber}", comment.IssueNumber);
            await _git.PushAsync(branchName, timeoutCts.Token).ConfigureAwait(false);
            _logger.LogDebug("Finished push for issue #{IssueNumber}", comment.IssueNumber);

            _logger.LogDebug("Starting tasks file read for issue #{IssueNumber}", comment.IssueNumber);
            var tasksContent = await _tasksFileReader.ReadCurrentAsync(specName, timeoutCts.Token).ConfigureAwait(false);
            _logger.LogDebug("Finished tasks file read for issue #{IssueNumber}", comment.IssueNumber);

            _logger.LogDebug("Starting draft pull request creation for issue #{IssueNumber}", comment.IssueNumber);
            var prNumber = await _gitHub.CreateDraftPullRequestAsync(
                $"Proposal for #{comment.IssueNumber}: {comment.IssueTitle}",
                tasksContent ?? string.Empty,
                branchName,
                _options.BaseBranchName,
                timeoutCts.Token).ConfigureAwait(false);
            _logger.LogDebug("Finished draft pull request creation for issue #{IssueNumber}", comment.IssueNumber);

            await ReportSuccessAsync(comment, specName, branchName, prNumber, timeoutCts.Token).ConfigureAwait(false);

            _logger.LogInformation("Finished /propose flow for issue #{IssueNumber} (comment {CommentId})", comment.IssueNumber, comment.CommentId);
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

    private async Task ReportSuccessAsync(EligibleProposeComment comment, string specName, string branchName, int prNumber, CancellationToken cancellationToken)
    {
        await _gitHub.AddCommentReactionAsync(comment.CommentId, "rocket", cancellationToken).ConfigureAwait(false);
        await _gitHub.CreateIssueCommentAsync(comment.IssueNumber, $"Created Draft PR #{prNumber} for this issue.", cancellationToken).ConfigureAwait(false);

        await _stateStore.UpsertTrackedIssueAsync(new TrackedIssue(comment.IssueNumber, specName) { BranchName = branchName, PrNumber = prNumber }, cancellationToken).ConfigureAwait(false);
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
