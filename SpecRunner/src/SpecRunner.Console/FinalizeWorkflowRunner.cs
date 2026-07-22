using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpecRunner.Core.Abstractions;
using SpecRunner.Core.Configuration;
using SpecRunner.Core.Models;

namespace SpecRunner.Console;

public class FinalizeWorkflowRunner : IFinalizeWorkflowRunner
{
    private const string FinalizeTrigger = "/finalize";
    private static readonly string[] BotStatusReactionTypes = { "eyes", "+1", "confused" };

    private readonly IGitHubService _gitHub;
    private readonly IGitService _git;
    private readonly IStateStore _stateStore;
    private readonly ICliAgentSessionFactory _cliAgentSessionFactory;
    private readonly ITasksFileReader _tasksFileReader;
    private readonly ICommandTemplateRenderer _commandTemplateRenderer;
    private readonly SpecRunnerOptions _options;
    private readonly ILogger<FinalizeWorkflowRunner> _logger;

    public FinalizeWorkflowRunner(
        IGitHubService gitHub,
        IGitService git,
        IStateStore stateStore,
        ICliAgentSessionFactory cliAgentSessionFactory,
        ITasksFileReader tasksFileReader,
        ICommandTemplateRenderer commandTemplateRenderer,
        IOptions<SpecRunnerOptions> options,
        ILogger<FinalizeWorkflowRunner> logger)
    {
        _gitHub = gitHub;
        _git = git;
        _stateStore = stateStore;
        _cliAgentSessionFactory = cliAgentSessionFactory;
        _tasksFileReader = tasksFileReader;
        _commandTemplateRenderer = commandTemplateRenderer;
        _options = options.Value;
        _logger = logger;
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var botLogin = await _gitHub.GetAuthenticatedLoginAsync(cancellationToken).ConfigureAwait(false);
        var pullRequests = await _gitHub.ListOpenPullRequestsAsync(cancellationToken).ConfigureAwait(false);

        var eligibleComments = new List<EligibleFinalizeComment>();
        foreach (var pr in pullRequests)
        {
            var comments = await _gitHub.ReadPrCommentsAsync(pr.Number, cancellationToken).ConfigureAwait(false);
            foreach (var comment in comments)
            {
                if (TryGetInstructions(comment.Body, out var instructions))
                {
                    eligibleComments.Add(new EligibleFinalizeComment(pr.Number, pr.HeadBranch, comment.CommentId, instructions));
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

    private static bool TryGetInstructions(string body, out string instructions)
    {
        var trimmed = body.Trim();
        if (trimmed == FinalizeTrigger)
        {
            instructions = string.Empty;
            return true;
        }

        if (trimmed.StartsWith(FinalizeTrigger, StringComparison.Ordinal)
            && trimmed.Length > FinalizeTrigger.Length
            && char.IsWhiteSpace(trimmed[FinalizeTrigger.Length]))
        {
            instructions = trimmed[FinalizeTrigger.Length..].TrimStart();
            return true;
        }

        instructions = string.Empty;
        return false;
    }

    private async Task ProcessCommentAsync(EligibleFinalizeComment comment, CancellationToken cancellationToken)
    {
        await _gitHub.AddCommentReactionAsync(comment.CommentId, "eyes", cancellationToken).ConfigureAwait(false);

        TrackedIssue? trackedIssue = null;
        ICliAgentSession? session = null;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.TaskTimeout);

        try
        {
            trackedIssue = await _stateStore.FindByPrNumberAsync(comment.PrNumber, timeoutCts.Token).ConfigureAwait(false);
            if (trackedIssue is null)
            {
                await ReportUntrackedPrAsync(comment, timeoutCts.Token).ConfigureAwait(false);
                return;
            }

            await _git.ResetHardAsync("HEAD", timeoutCts.Token).ConfigureAwait(false);
            await _git.FetchAsync(trackedIssue.BranchName, timeoutCts.Token).ConfigureAwait(false);
            await _git.SwitchBranchAsync(trackedIssue.BranchName, timeoutCts.Token).ConfigureAwait(false);
            await _git.ResetHardAsync($"origin/{trackedIssue.BranchName}", timeoutCts.Token).ConfigureAwait(false);

            var prompt = await _commandTemplateRenderer.RenderAsync(
                "archive",
                new Dictionary<string, string>
                {
                    ["spec_name"] = trackedIssue.SpecName,
                    ["instructions"] = comment.Instructions
                },
                timeoutCts.Token).ConfigureAwait(false);

            session = _cliAgentSessionFactory.CreateSession();
            await session.StartAsync($"\"{prompt}\"", timeoutCts.Token).ConfigureAwait(false);
            await session.CloseInputAsync(timeoutCts.Token).ConfigureAwait(false);

            await foreach (var _ in session.ReadEventsAsync(timeoutCts.Token).ConfigureAwait(false))
            {
                // Drain events; the channel completes once the session reaches a terminal state.
            }

            if (session.State != CliAgentSessionState.Completed)
            {
                throw new InvalidOperationException($"CLI agent session for PR #{comment.PrNumber} ended in state {session.State}.");
            }

            await _git.CommitAsync($"finalizing specs for #{trackedIssue.IssueNumber}", timeoutCts.Token).ConfigureAwait(false);
            await _git.PushAsync(trackedIssue.BranchName, timeoutCts.Token).ConfigureAwait(false);

            var archivedTasksContent = await _tasksFileReader.ReadArchivedAsync(trackedIssue.SpecName, timeoutCts.Token).ConfigureAwait(false);
            var finalBody = $"{archivedTasksContent ?? string.Empty}\n\nCloses #{trackedIssue.IssueNumber}";
            await _gitHub.UpdatePullRequestDescriptionAsync(comment.PrNumber, finalBody, timeoutCts.Token).ConfigureAwait(false);

            await _gitHub.MarkPrReadyForReviewAsync(comment.PrNumber, timeoutCts.Token).ConfigureAwait(false);

            await ReportSuccessAsync(comment, trackedIssue, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            if (session is not null)
            {
                await session.StopAsync(cancellationToken).ConfigureAwait(false);
            }

            await ReportTimeoutAsync(comment, trackedIssue, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await ReportErrorAsync(comment, trackedIssue, ex, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (session is not null)
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task ReportUntrackedPrAsync(EligibleFinalizeComment comment, CancellationToken cancellationToken)
    {
        await _gitHub.WritePrCommentAsync(
            comment.PrNumber,
            "No associated spec/change was found for this PR. /finalize only works on a PR opened by /propose.",
            cancellationToken).ConfigureAwait(false);
        await _gitHub.AddCommentReactionAsync(comment.CommentId, "confused", cancellationToken).ConfigureAwait(false);
    }

    private async Task ReportSuccessAsync(EligibleFinalizeComment comment, TrackedIssue trackedIssue, CancellationToken cancellationToken)
    {
        await _gitHub.AddCommentReactionAsync(comment.CommentId, "+1", cancellationToken).ConfigureAwait(false);
        await _gitHub.WritePrCommentAsync(comment.PrNumber, "Finalized this change and marked the PR ready for review.", cancellationToken).ConfigureAwait(false);

        await _stateStore.UpsertCommentAsync(
            trackedIssue.IssueNumber,
            new TrackedComment(comment.CommentId, CommentKind.PrIssueComment, CommentStatus.Done),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task ReportTimeoutAsync(EligibleFinalizeComment comment, TrackedIssue? trackedIssue, CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Processing /finalize comment {CommentId} on PR #{PrNumber} timed out after {Timeout}",
            comment.CommentId,
            comment.PrNumber,
            _options.TaskTimeout);

        await _gitHub.AddCommentReactionAsync(comment.CommentId, "confused", cancellationToken).ConfigureAwait(false);
        await _gitHub.WritePrCommentAsync(comment.PrNumber, "Processing this comment timed out.", cancellationToken).ConfigureAwait(false);
        await RecordCommentStatusAsync(comment, trackedIssue, CommentStatus.Error, cancellationToken).ConfigureAwait(false);
    }

    private async Task ReportErrorAsync(EligibleFinalizeComment comment, TrackedIssue? trackedIssue, Exception ex, CancellationToken cancellationToken)
    {
        _logger.LogError(ex, "Error processing /finalize comment {CommentId} on PR #{PrNumber}", comment.CommentId, comment.PrNumber);

        await _gitHub.AddCommentReactionAsync(comment.CommentId, "confused", cancellationToken).ConfigureAwait(false);
        await _gitHub.WritePrCommentAsync(comment.PrNumber, $"Processing this comment failed: {ex.Message}", cancellationToken).ConfigureAwait(false);
        await RecordCommentStatusAsync(comment, trackedIssue, CommentStatus.Error, cancellationToken).ConfigureAwait(false);
    }

    private async Task RecordCommentStatusAsync(EligibleFinalizeComment comment, TrackedIssue? trackedIssue, CommentStatus status, CancellationToken cancellationToken)
    {
        if (trackedIssue is null)
        {
            return;
        }

        await _stateStore.UpsertCommentAsync(
            trackedIssue.IssueNumber,
            new TrackedComment(comment.CommentId, CommentKind.PrIssueComment, status),
            cancellationToken).ConfigureAwait(false);
    }
}
