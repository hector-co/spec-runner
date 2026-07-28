using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpecRunner.Core;
using SpecRunner.Core.Abstractions;
using SpecRunner.Core.Configuration;
using SpecRunner.Core.Models;

namespace SpecRunner.Console;

public class UpdateWorkflowRunner : IUpdateWorkflowRunner
{
    private const string UpdateTrigger = "/update";
    private static readonly string[] BotStatusReactionTypes = { "eyes", "+1", "confused" };

    private readonly IGitHubService _gitHub;
    private readonly IGitService _git;
    private readonly IStateStore _stateStore;
    private readonly IPrAdoptionService _prAdoptionService;
    private readonly ICliAgentSessionFactory _cliAgentSessionFactory;
    private readonly ITasksFileReader _tasksFileReader;
    private readonly ICommandTemplateRenderer _commandTemplateRenderer;
    private readonly SpecRunnerOptions _options;
    private readonly ILogger<UpdateWorkflowRunner> _logger;

    public UpdateWorkflowRunner(
        IGitHubService gitHub,
        IGitService git,
        IStateStore stateStore,
        IPrAdoptionService prAdoptionService,
        ICliAgentSessionFactory cliAgentSessionFactory,
        ITasksFileReader tasksFileReader,
        ICommandTemplateRenderer commandTemplateRenderer,
        IOptions<SpecRunnerOptions> options,
        ILogger<UpdateWorkflowRunner> logger)
    {
        _gitHub = gitHub;
        _git = git;
        _stateStore = stateStore;
        _prAdoptionService = prAdoptionService;
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

        var eligibleComments = new List<EligibleUpdateComment>();
        foreach (var pr in pullRequests)
        {
            var comments = await _gitHub.ReadPrCommentsAsync(pr.Number, cancellationToken).ConfigureAwait(false);
            foreach (var comment in comments)
            {
                if (!TryGetInstructions(comment.Body, out var instructions))
                {
                    continue;
                }

                if (!CommentAuthorization.IsAuthorized(comment.Author, comment.AuthorAssociation, _options))
                {
                    _logger.LogWarning(
                        "Ignoring /update comment {CommentId} on PR #{PrNumber} from unauthorized author {Author}",
                        comment.CommentId,
                        pr.Number,
                        comment.Author);
                    continue;
                }

                eligibleComments.Add(new EligibleUpdateComment(pr.Number, pr.HeadBranch, comment.CommentId, instructions, comment.Author, comment.AuthorAssociation));
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
        if (trimmed == UpdateTrigger)
        {
            instructions = string.Empty;
            return true;
        }

        if (trimmed.StartsWith(UpdateTrigger, StringComparison.Ordinal)
            && trimmed.Length > UpdateTrigger.Length
            && char.IsWhiteSpace(trimmed[UpdateTrigger.Length]))
        {
            instructions = trimmed[UpdateTrigger.Length..].TrimStart();
            return true;
        }

        instructions = string.Empty;
        return false;
    }

    private async Task ProcessCommentAsync(EligibleUpdateComment comment, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting /update flow for PR #{PrNumber} (comment {CommentId})", comment.PrNumber, comment.CommentId);

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
                var adoptionResult = await _prAdoptionService.TryAdoptAsync(comment.PrNumber, comment.PrHeadBranch, timeoutCts.Token).ConfigureAwait(false);
                if (!adoptionResult.Succeeded)
                {
                    await ReportAdoptionFailureAsync(comment, adoptionResult, timeoutCts.Token).ConfigureAwait(false);
                    return;
                }

                trackedIssue = await _stateStore.UpsertTrackedIssueAsync(adoptionResult.TrackedIssue!, timeoutCts.Token).ConfigureAwait(false);
            }

            _logger.LogDebug("Starting git sync for PR #{PrNumber}", comment.PrNumber);
            await _git.ResetHardAsync("HEAD", timeoutCts.Token).ConfigureAwait(false);
            await _git.FetchAsync(trackedIssue.BranchName, timeoutCts.Token).ConfigureAwait(false);
            await _git.SwitchBranchAsync(trackedIssue.BranchName, timeoutCts.Token).ConfigureAwait(false);
            await _git.ResetHardAsync($"origin/{trackedIssue.BranchName}", timeoutCts.Token).ConfigureAwait(false);
            _logger.LogDebug("Finished git sync for PR #{PrNumber}", comment.PrNumber);

            _logger.LogDebug("Starting prompt rendering for PR #{PrNumber}", comment.PrNumber);
            var prompt = await _commandTemplateRenderer.RenderAsync(
                "update",
                new Dictionary<string, string>
                {
                    ["spec_name"] = trackedIssue.SpecName,
                    ["instructions"] = comment.Instructions
                },
                timeoutCts.Token).ConfigureAwait(false);
            _logger.LogDebug("Finished prompt rendering for PR #{PrNumber}", comment.PrNumber);

            session = _cliAgentSessionFactory.CreateSession();
            _logger.LogDebug("Starting CLI agent session for PR #{PrNumber}", comment.PrNumber);
            await session.StartAsync($"\"{prompt}\"", timeoutCts.Token).ConfigureAwait(false);
            await session.CloseInputAsync(timeoutCts.Token).ConfigureAwait(false);
            _logger.LogDebug("Finished starting CLI agent session for PR #{PrNumber}", comment.PrNumber);

            using var progressCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token);
            var progressTask = ProgressIndicator.RunAsync(
                _logger,
                $"/update flow for PR #{comment.PrNumber} still in progress",
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
                throw new InvalidOperationException($"CLI agent session for PR #{comment.PrNumber} ended in state {session.State}.");
            }

            _logger.LogDebug("Starting commit for PR #{PrNumber}", comment.PrNumber);
            var commitMessage = trackedIssue.IssueNumber is int issueNumberForCommit
                ? $"updating specs for #{issueNumberForCommit}"
                : $"updating specs for PR #{comment.PrNumber}";
            await _git.CommitAsync(commitMessage, timeoutCts.Token).ConfigureAwait(false);
            _logger.LogDebug("Finished commit for PR #{PrNumber}", comment.PrNumber);

            _logger.LogDebug("Starting push for PR #{PrNumber}", comment.PrNumber);
            await _git.PushAsync(trackedIssue.BranchName, timeoutCts.Token).ConfigureAwait(false);
            _logger.LogDebug("Finished push for PR #{PrNumber}", comment.PrNumber);

            _logger.LogDebug("Starting tasks file read for PR #{PrNumber}", comment.PrNumber);
            var tasksContent = await _tasksFileReader.ReadCurrentAsync(trackedIssue.SpecName, timeoutCts.Token).ConfigureAwait(false);
            _logger.LogDebug("Finished tasks file read for PR #{PrNumber}", comment.PrNumber);
            if (tasksContent is not null)
            {
                _logger.LogDebug("Starting pull request description update for PR #{PrNumber}", comment.PrNumber);
                await _gitHub.UpdatePullRequestDescriptionAsync(comment.PrNumber, tasksContent, timeoutCts.Token).ConfigureAwait(false);
                _logger.LogDebug("Finished pull request description update for PR #{PrNumber}", comment.PrNumber);
            }

            await ReportSuccessAsync(comment, trackedIssue, timeoutCts.Token).ConfigureAwait(false);

            _logger.LogInformation("Finished /update flow for PR #{PrNumber} (comment {CommentId})", comment.PrNumber, comment.CommentId);
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

    private async Task ReportAdoptionFailureAsync(EligibleUpdateComment comment, PrAdoptionResult adoptionResult, CancellationToken cancellationToken)
    {
        await _gitHub.WritePrCommentAsync(comment.PrNumber, adoptionResult.FailureMessage, cancellationToken).ConfigureAwait(false);
        await _gitHub.AddCommentReactionAsync(comment.CommentId, "confused", cancellationToken).ConfigureAwait(false);
    }

    private async Task ReportSuccessAsync(EligibleUpdateComment comment, TrackedIssue trackedIssue, CancellationToken cancellationToken)
    {
        await _gitHub.AddCommentReactionAsync(comment.CommentId, "+1", cancellationToken).ConfigureAwait(false);
        await _gitHub.WritePrCommentAsync(comment.PrNumber, "Pushed changes for this comment.", cancellationToken).ConfigureAwait(false);

        await _stateStore.UpsertCommentAsync(
            comment.PrNumber,
            new TrackedComment(comment.CommentId, CommentKind.PrIssueComment, CommentStatus.Done),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task ReportTimeoutAsync(EligibleUpdateComment comment, TrackedIssue? trackedIssue, CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Processing /update comment {CommentId} on PR #{PrNumber} timed out after {Timeout}",
            comment.CommentId,
            comment.PrNumber,
            _options.TaskTimeout);

        await _gitHub.AddCommentReactionAsync(comment.CommentId, "confused", cancellationToken).ConfigureAwait(false);
        await _gitHub.WritePrCommentAsync(comment.PrNumber, "Processing this comment timed out.", cancellationToken).ConfigureAwait(false);
        await RecordCommentStatusAsync(comment, trackedIssue, CommentStatus.Error, cancellationToken).ConfigureAwait(false);
    }

    private async Task ReportErrorAsync(EligibleUpdateComment comment, TrackedIssue? trackedIssue, Exception ex, CancellationToken cancellationToken)
    {
        _logger.LogError(ex, "Error processing /update comment {CommentId} on PR #{PrNumber}", comment.CommentId, comment.PrNumber);

        await _gitHub.AddCommentReactionAsync(comment.CommentId, "confused", cancellationToken).ConfigureAwait(false);
        await _gitHub.WritePrCommentAsync(comment.PrNumber, $"Processing this comment failed: {ex.Message}", cancellationToken).ConfigureAwait(false);
        await RecordCommentStatusAsync(comment, trackedIssue, CommentStatus.Error, cancellationToken).ConfigureAwait(false);
    }

    private async Task RecordCommentStatusAsync(EligibleUpdateComment comment, TrackedIssue? trackedIssue, CommentStatus status, CancellationToken cancellationToken)
    {
        if (trackedIssue is null)
        {
            return;
        }

        await _stateStore.UpsertCommentAsync(
            comment.PrNumber,
            new TrackedComment(comment.CommentId, CommentKind.PrIssueComment, status),
            cancellationToken).ConfigureAwait(false);
    }
}
