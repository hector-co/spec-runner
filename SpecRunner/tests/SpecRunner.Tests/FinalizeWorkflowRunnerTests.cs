using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SpecRunner.Console;
using SpecRunner.Core.Abstractions;
using SpecRunner.Core.Configuration;
using SpecRunner.Core.Models;
using SpecRunner.State;
using SpecRunner.Tests.Fakes;

namespace SpecRunner.Tests;

public class FinalizeWorkflowRunnerTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _stateFilePath;

    public FinalizeWorkflowRunnerTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _stateFilePath = Path.Combine(_tempDirectory, "state.db");
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private (FinalizeWorkflowRunner Runner, RecordingGitService Git, RecordingGitHubService GitHub, FakeCliAgentSessionFactory CliFactory, SqliteStateStore StateStore, FakeTasksFileReader TasksFileReader, FakePrAdoptionService PrAdoptionService) CreateRunner(
        TimeSpan? taskTimeout = null,
        Func<ICliAgentSession>? sessionFactory = null)
    {
        var git = new RecordingGitService();
        var gitHub = new RecordingGitHubService();
        var cliFactory = new FakeCliAgentSessionFactory(sessionFactory ?? (() => new FakeCliAgentSession(CliAgentSessionState.Completed)));
        var stateStore = new SqliteStateStore(_stateFilePath);
        var tasksFileReader = new FakeTasksFileReader();
        var commandTemplateRenderer = new CommandTemplateRenderer();
        var options = Options.Create(new SpecRunnerOptions
        {
            BaseBranchName = "main",
            TaskTimeout = taskTimeout ?? TimeSpan.FromSeconds(30)
        });

        var prAdoptionService = new FakePrAdoptionService();

        var runner = new FinalizeWorkflowRunner(gitHub, git, stateStore, prAdoptionService, cliFactory, tasksFileReader, commandTemplateRenderer, options, NullLogger<FinalizeWorkflowRunner>.Instance);
        return (runner, git, gitHub, cliFactory, stateStore, tasksFileReader, prAdoptionService);
    }

    private static GitHubPullRequest PullRequest(int number, string headBranch, string title = "Title", string body = "Body")
        => new(number, title, body, headBranch);

    private static PrComment Comment(long id, string body, string author = "someone", string authorAssociation = "OWNER")
        => new(id, author, authorAssociation, body, DateTimeOffset.UtcNow);

    [Theory]
    [InlineData("/finalize")]
    [InlineData("/finalize\nAdditional context")]
    [InlineData("/finalize please handle this")]
    [InlineData("  /finalize  ")]
    public async Task EligibleCommentBodiesReceiveEyesReaction(string body)
    {
        var (runner, _, gitHub, _, _, _, _) = CreateRunner();
        gitHub.PullRequests.Add(PullRequest(12, "feature/45"));
        gitHub.PrComments[12] = new List<PrComment> { Comment(9001, body) };

        await runner.RunOnceAsync();

        Assert.Contains((9001L, "eyes"), gitHub.AddedReactions);
    }

    [Theory]
    [InlineData("/finalized")]
    [InlineData("please /finalize this")]
    [InlineData("/finalizework")]
    [InlineData("some text mentioning /finalize mid-sentence")]
    public async Task NonEligibleCommentBodiesAreIgnored(string body)
    {
        var (runner, _, gitHub, _, _, _, _) = CreateRunner();
        gitHub.PullRequests.Add(PullRequest(12, "feature/45"));
        gitHub.PrComments[12] = new List<PrComment> { Comment(9001, body) };

        await runner.RunOnceAsync();

        Assert.Empty(gitHub.AddedReactions);
    }

    [Fact]
    public async Task InstructionsStripLeadingTriggerTokenAndSeparatingWhitespace()
    {
        var (runner, _, gitHub, cliFactory, stateStore, _, _) = CreateRunner();
        await stateStore.UpsertTrackedIssueAsync(new TrackedIssue(45, "45-add-login-page") { PrNumber = 12 });
        gitHub.PullRequests.Add(PullRequest(12, "feature/45"));
        gitHub.PrComments[12] = new List<PrComment> { Comment(9001, "/finalize the export button task was implemented under a different name") };

        await runner.RunOnceAsync();

        var session = Assert.IsType<FakeCliAgentSession>(Assert.Single(cliFactory.CreatedSessions));
        Assert.Equal(
            $"\"Run `openspec archive \"45-add-login-page\" --yes`. Mark missing tasks as completed and continue.{Environment.NewLine}" +
            $"the export button task was implemented under a different name{Environment.NewLine}{Environment.NewLine}" +
            $"This is an unattended run — do not ask for confirmation or clarification{Environment.NewLine}" +
            $"at any step. If something is ambiguous, make the most reasonable{Environment.NewLine}" +
            $"assumption, note it in proposal.md under a brief \"Assumptions\" note, and{Environment.NewLine}" +
            $"continue.\"",
            session.LastPrompt);
    }

    [Fact]
    public async Task UnauthorizedAuthorCommentIsIgnored()
    {
        var (runner, git, gitHub, cliFactory, _, _, _) = CreateRunner();
        gitHub.PullRequests.Add(PullRequest(12, "feature/45"));
        gitHub.PrComments[12] = new List<PrComment> { Comment(9001, "/finalize", author: "rando", authorAssociation: "NONE") };

        await runner.RunOnceAsync();

        Assert.Empty(gitHub.AddedReactions);
        Assert.Empty(gitHub.WrittenPrComments);
        Assert.Empty(git.Calls);
        Assert.Empty(cliFactory.CreatedSessions);
    }

    [Fact]
    public async Task CommentWithExistingBotReactionIsSkipped()
    {
        var (runner, git, gitHub, cliFactory, _, _, _) = CreateRunner();
        gitHub.PullRequests.Add(PullRequest(12, "feature/45"));
        gitHub.PrComments[12] = new List<PrComment> { Comment(9001, "/finalize") };
        gitHub.Reactions[9001] = new List<GitHubReaction> { new(gitHub.Login, "+1") };

        await runner.RunOnceAsync();

        Assert.DoesNotContain(gitHub.AddedReactions, r => r.CommentId == 9001);
        Assert.Empty(git.Calls);
        Assert.Empty(cliFactory.CreatedSessions);
    }

    [Fact]
    public async Task CommentWithOnlyHumanReactionIsStillProcessed()
    {
        var (runner, _, gitHub, _, _, _, _) = CreateRunner();
        gitHub.PullRequests.Add(PullRequest(12, "feature/45"));
        gitHub.PrComments[12] = new List<PrComment> { Comment(9001, "/finalize") };
        gitHub.Reactions[9001] = new List<GitHubReaction> { new("a-human", "eyes") };

        await runner.RunOnceAsync();

        Assert.Contains((9001L, "eyes"), gitHub.AddedReactions);
    }

    [Fact]
    public async Task UntrackedPrGetsExplanatoryReplyAndConfusedReactionWithNoFurtherWork()
    {
        var (runner, git, gitHub, cliFactory, stateStore, _, _) = CreateRunner();
        gitHub.PullRequests.Add(PullRequest(12, "feature/45"));
        gitHub.PrComments[12] = new List<PrComment> { Comment(9001, "/finalize") };

        await runner.RunOnceAsync();

        Assert.Contains((9001L, "confused"), gitHub.AddedReactions);
        Assert.Contains(gitHub.WrittenPrComments, c => c.PrNumber == 12);
        Assert.Empty(git.Calls);
        Assert.Empty(cliFactory.CreatedSessions);
        Assert.Empty(gitHub.MarkedReadyForReview);

        var tracked = await stateStore.FindByPrNumberAsync(12);
        Assert.Null(tracked);
    }

    [Fact]
    public async Task SuccessfulAdoptionWithIssueProceedsLikeATrackedPr()
    {
        var (runner, git, gitHub, cliFactory, stateStore, _, prAdoptionService) = CreateRunner();
        prAdoptionService.Result = PrAdoptionResult.Success(
            new TrackedIssue(45, "add-csv-export") { PrNumber = 12, BranchName = "contributor/csv-export" });
        gitHub.PullRequests.Add(PullRequest(12, "contributor/csv-export", title: "Add CSV export"));
        gitHub.PrComments[12] = new List<PrComment> { Comment(9001, "/finalize") };

        await runner.RunOnceAsync();

        var adoptionCall = Assert.Single(prAdoptionService.Calls);
        Assert.Equal((12, "contributor/csv-export"), adoptionCall);
        Assert.Contains("Commit:finalizing specs for #45", git.Calls);
        Assert.Contains(gitHub.UpdatedPullRequestDescriptions, d => d.PrNumber == 12 && d.Body.EndsWith("\n\nCloses #45", StringComparison.Ordinal));
        Assert.Contains(12, gitHub.MarkedReadyForReview);
        Assert.Contains((9001L, "+1"), gitHub.AddedReactions);

        var tracked = await stateStore.FindByPrNumberAsync(12);
        Assert.NotNull(tracked);
        Assert.Equal(45, tracked!.IssueNumber);
    }

    [Fact]
    public async Task SuccessfulAdoptionWithoutIssueProceedsWithoutOne()
    {
        var (runner, git, gitHub, cliFactory, stateStore, _, prAdoptionService) = CreateRunner();
        prAdoptionService.Result = PrAdoptionResult.Success(
            new TrackedIssue(null, "add-csv-export") { PrNumber = 12, BranchName = "contributor/csv-export" });
        gitHub.PullRequests.Add(PullRequest(12, "contributor/csv-export", title: "Add CSV export"));
        gitHub.PrComments[12] = new List<PrComment> { Comment(9001, "/finalize") };

        await runner.RunOnceAsync();

        Assert.Contains("Commit:finalizing specs for PR #12", git.Calls);
        Assert.DoesNotContain(gitHub.UpdatedPullRequestDescriptions, d => d.Body.Contains("Closes #"));
        Assert.DoesNotContain(gitHub.UpdatedPullRequestTitles, t => t.PrNumber == 12);
        Assert.Contains(12, gitHub.MarkedReadyForReview);
        Assert.Contains((9001L, "+1"), gitHub.AddedReactions);

        var tracked = await stateStore.FindByPrNumberAsync(12);
        Assert.NotNull(tracked);
        Assert.Null(tracked!.IssueNumber);
    }

    [Fact]
    public async Task AdoptionFailureForMultipleSpecFoldersRefusesWithCandidateList()
    {
        var (runner, git, gitHub, cliFactory, stateStore, _, prAdoptionService) = CreateRunner();
        prAdoptionService.Result = PrAdoptionResult.MultipleSpecFoldersFound(new[] { "add-csv-export", "add-pdf-export" });
        gitHub.PullRequests.Add(PullRequest(12, "feature/45"));
        gitHub.PrComments[12] = new List<PrComment> { Comment(9001, "/finalize") };

        await runner.RunOnceAsync();

        Assert.Contains((9001L, "confused"), gitHub.AddedReactions);
        Assert.Contains(gitHub.WrittenPrComments, c => c.PrNumber == 12 && c.Body.Contains("add-csv-export") && c.Body.Contains("add-pdf-export"));
        Assert.Empty(git.Calls);
        Assert.Empty(cliFactory.CreatedSessions);
        Assert.Empty(gitHub.MarkedReadyForReview);

        var tracked = await stateStore.FindByPrNumberAsync(12);
        Assert.Null(tracked);
    }

    [Fact]
    public async Task AdoptionFailureForMultipleIssuesRefusesWithCandidateList()
    {
        var (runner, git, gitHub, cliFactory, stateStore, _, prAdoptionService) = CreateRunner();
        prAdoptionService.Result = PrAdoptionResult.MultipleIssuesFound(new[] { 45, 46 });
        gitHub.PullRequests.Add(PullRequest(12, "feature/45"));
        gitHub.PrComments[12] = new List<PrComment> { Comment(9001, "/finalize") };

        await runner.RunOnceAsync();

        Assert.Contains((9001L, "confused"), gitHub.AddedReactions);
        Assert.Contains(gitHub.WrittenPrComments, c => c.PrNumber == 12 && c.Body.Contains("#45") && c.Body.Contains("#46"));
        Assert.Empty(git.Calls);
        Assert.Empty(cliFactory.CreatedSessions);
        Assert.Empty(gitHub.MarkedReadyForReview);

        var tracked = await stateStore.FindByPrNumberAsync(12);
        Assert.Null(tracked);
    }

    [Fact]
    public async Task SuccessfulRunRefreshesBranchCommitsPushesMarksReadyAndUpdatesState()
    {
        var (runner, git, gitHub, cliFactory, stateStore, tasksFileReader, _) = CreateRunner();
        await stateStore.UpsertTrackedIssueAsync(new TrackedIssue(45, "45-add-login-page") { PrNumber = 12, BranchName = "feature/45" });
        tasksFileReader.ArchivedContentBySpecName["45-add-login-page"] = "## 1. Tasks\n- [x] 1.1 Done";
        gitHub.PullRequests.Add(PullRequest(12, "feature/45", title: "Implementations for #45: Add login page"));
        gitHub.PrComments[12] = new List<PrComment> { Comment(9001, "/finalize the export button was implemented under a different name") };

        await runner.RunOnceAsync();

        Assert.Equal(
            new[] { "ResetHard:HEAD", "Fetch:feature/45", "SwitchBranch:feature/45", "ResetHard:origin/feature/45", "Commit:finalizing specs for #45", "Push:feature/45" },
            git.Calls);

        var session = Assert.IsType<FakeCliAgentSession>(Assert.Single(cliFactory.CreatedSessions));
        Assert.Equal(
            $"\"Run `openspec archive \"45-add-login-page\" --yes`. Mark missing tasks as completed and continue.{Environment.NewLine}" +
            $"the export button was implemented under a different name{Environment.NewLine}{Environment.NewLine}" +
            $"This is an unattended run — do not ask for confirmation or clarification{Environment.NewLine}" +
            $"at any step. If something is ambiguous, make the most reasonable{Environment.NewLine}" +
            $"assumption, note it in proposal.md under a brief \"Assumptions\" note, and{Environment.NewLine}" +
            $"continue.\"",
            session.LastPrompt);
        Assert.True(session.CloseInputCalled);

        Assert.Contains(12, gitHub.MarkedReadyForReview);
        Assert.Contains((9001L, "+1"), gitHub.AddedReactions);
        Assert.Contains(gitHub.WrittenPrComments, c => c.PrNumber == 12);
        Assert.Contains((12, "## 1. Tasks\n- [x] 1.1 Done\n\nCloses #45"), gitHub.UpdatedPullRequestDescriptions);
        Assert.Contains((12, "#45: Add login page"), gitHub.UpdatedPullRequestTitles);
        Assert.Equal(
            new[] { "UpdatePullRequestDescription", "UpdatePullRequestTitle", "MarkPrReadyForReview" },
            gitHub.PrMutationCallOrder);

        var tracked = await stateStore.FindByIssueNumberAsync(45);
        Assert.NotNull(tracked);
        var comment = Assert.Single(tracked!.Comments);
        Assert.Equal(CommentStatus.Done, comment.Status);
        Assert.Equal(CommentKind.PrIssueComment, comment.CommentKind);
    }

    [Fact]
    public async Task MissingArchivedTasksFileStillAppendsClosingLink()
    {
        var (runner, _, gitHub, _, stateStore, _, _) = CreateRunner();
        await stateStore.UpsertTrackedIssueAsync(new TrackedIssue(45, "45-add-login-page") { PrNumber = 12 });
        gitHub.PullRequests.Add(PullRequest(12, "feature/45"));
        gitHub.PrComments[12] = new List<PrComment> { Comment(9001, "/finalize") };

        await runner.RunOnceAsync();

        Assert.Contains((12, "\n\nCloses #45"), gitHub.UpdatedPullRequestDescriptions);
        Assert.Contains(12, gitHub.MarkedReadyForReview);
    }

    [Fact]
    public async Task TrackedBranchNameIsUsedEvenWhenItDiffersFromPrHeadBranch()
    {
        var (runner, git, gitHub, _, stateStore, _, _) = CreateRunner();
        await stateStore.UpsertTrackedIssueAsync(new TrackedIssue(45, "45-add-login-page") { PrNumber = 12, BranchName = "feature/45-2" });
        gitHub.PullRequests.Add(PullRequest(12, "feature/45"));
        gitHub.PrComments[12] = new List<PrComment> { Comment(9001, "/finalize") };

        await runner.RunOnceAsync();

        Assert.Equal(
            new[] { "ResetHard:HEAD", "Fetch:feature/45-2", "SwitchBranch:feature/45-2", "ResetHard:origin/feature/45-2", "Commit:finalizing specs for #45", "Push:feature/45-2" },
            git.Calls);
    }

    [Fact]
    public async Task FailureMarkingPrReadyForReviewIsReportedAsError()
    {
        var (runner, git, gitHub, cliFactory, stateStore, _, _) = CreateRunner();
        await stateStore.UpsertTrackedIssueAsync(new TrackedIssue(45, "45-add-login-page") { PrNumber = 12, BranchName = "feature/45" });
        gitHub.PullRequests.Add(PullRequest(12, "feature/45"));
        gitHub.PrComments[12] = new List<PrComment> { Comment(9001, "/finalize") };
        gitHub.ThrowOnMarkPrReadyForReview = new InvalidOperationException("PR is not a draft");

        await runner.RunOnceAsync();

        Assert.Equal(new[] { "Push:feature/45" }, git.Calls.Where(c => c.StartsWith("Push", StringComparison.Ordinal)));
        Assert.Contains((9001L, "confused"), gitHub.AddedReactions);
        Assert.DoesNotContain((9001L, "+1"), gitHub.AddedReactions);

        var tracked = await stateStore.FindByIssueNumberAsync(45);
        Assert.NotNull(tracked);
        var comment = Assert.Single(tracked!.Comments);
        Assert.Equal(CommentStatus.Error, comment.Status);
    }

    [Fact]
    public async Task ThrownExceptionDuringProcessingIsReportedAndProcessingContinuesToNextComment()
    {
        var callCount = 0;
        var (runner, _, gitHub, cliFactory, stateStore, _, _) = CreateRunner(
            sessionFactory: () =>
            {
                callCount++;
                return callCount == 1
                    ? new FakeCliAgentSession(CliAgentSessionState.Failed)
                    : new FakeCliAgentSession(CliAgentSessionState.Completed);
            });
        await stateStore.UpsertTrackedIssueAsync(new TrackedIssue(45, "45-spec") { PrNumber = 12 });
        await stateStore.UpsertTrackedIssueAsync(new TrackedIssue(46, "46-spec") { PrNumber = 13 });
        gitHub.PullRequests.Add(PullRequest(12, "feature/45"));
        gitHub.PrComments[12] = new List<PrComment> { Comment(9001, "/finalize") };
        gitHub.PullRequests.Add(PullRequest(13, "feature/46"));
        gitHub.PrComments[13] = new List<PrComment> { Comment(9002, "/finalize") };

        await runner.RunOnceAsync();

        Assert.Contains((9001L, "confused"), gitHub.AddedReactions);
        var trackedFailed = await stateStore.FindByIssueNumberAsync(45);
        Assert.NotNull(trackedFailed);
        var failedComment = Assert.Single(trackedFailed!.Comments);
        Assert.Equal(CommentStatus.Error, failedComment.Status);

        Assert.Contains((9002L, "+1"), gitHub.AddedReactions);
        var trackedSucceeded = await stateStore.FindByIssueNumberAsync(46);
        Assert.NotNull(trackedSucceeded);
        var succeededComment = Assert.Single(trackedSucceeded!.Comments);
        Assert.Equal(CommentStatus.Done, succeededComment.Status);

        Assert.Equal(2, cliFactory.CreatedSessions.Count);
    }

    [Fact]
    public async Task ExceedingTaskTimeoutStopsSessionAndReportsTimeout()
    {
        var (runner, _, gitHub, cliFactory, stateStore, _, _) = CreateRunner(
            taskTimeout: TimeSpan.FromMilliseconds(50),
            sessionFactory: () => new FakeCliAgentSession(CliAgentSessionState.Completed, readEventsDelay: TimeSpan.FromSeconds(5)));
        await stateStore.UpsertTrackedIssueAsync(new TrackedIssue(45, "45-spec") { PrNumber = 12 });
        gitHub.PullRequests.Add(PullRequest(12, "feature/45"));
        gitHub.PrComments[12] = new List<PrComment> { Comment(9001, "/finalize") };

        await runner.RunOnceAsync();

        var session = Assert.IsType<FakeCliAgentSession>(Assert.Single(cliFactory.CreatedSessions));
        Assert.True(session.StopCalled);
        Assert.Contains((9001L, "confused"), gitHub.AddedReactions);
        Assert.Contains(gitHub.WrittenPrComments, c => c.PrNumber == 12 && c.Body == "Processing this comment timed out.");
        Assert.Empty(gitHub.MarkedReadyForReview);

        var tracked = await stateStore.FindByIssueNumberAsync(45);
        Assert.NotNull(tracked);
        var comment = Assert.Single(tracked!.Comments);
        Assert.Equal(CommentStatus.Error, comment.Status);
    }

    [Fact]
    public async Task EligibleCommentsAreProcessedSequentiallyNotConcurrently()
    {
        var tracker = new ConcurrencyTracker();
        var (runner, _, gitHub, cliFactory, stateStore, _, _) = CreateRunner(
            sessionFactory: () => new TrackingCliAgentSession(tracker, TimeSpan.FromMilliseconds(30)));
        await stateStore.UpsertTrackedIssueAsync(new TrackedIssue(45, "45-spec") { PrNumber = 12 });
        await stateStore.UpsertTrackedIssueAsync(new TrackedIssue(46, "46-spec") { PrNumber = 13 });
        gitHub.PullRequests.Add(PullRequest(12, "feature/45"));
        gitHub.PrComments[12] = new List<PrComment> { Comment(9001, "/finalize") };
        gitHub.PullRequests.Add(PullRequest(13, "feature/46"));
        gitHub.PrComments[13] = new List<PrComment> { Comment(9002, "/finalize") };

        await runner.RunOnceAsync();

        Assert.Equal(2, cliFactory.CreatedSessions.Count);
        Assert.Equal(1, tracker.MaxConcurrent);
    }

    private sealed class ConcurrencyTracker
    {
        private readonly object _gate = new();
        private int _current;

        public int MaxConcurrent { get; private set; }

        public void Enter()
        {
            lock (_gate)
            {
                _current++;
                MaxConcurrent = Math.Max(MaxConcurrent, _current);
            }
        }

        public void Exit()
        {
            lock (_gate)
            {
                _current--;
            }
        }
    }

    private sealed class TrackingCliAgentSession : ICliAgentSession
    {
        private readonly ConcurrencyTracker _tracker;
        private readonly TimeSpan _delay;

        public TrackingCliAgentSession(ConcurrencyTracker tracker, TimeSpan delay)
        {
            _tracker = tracker;
            _delay = delay;
        }

        public CliAgentSessionState State { get; private set; } = CliAgentSessionState.NotStarted;

        public Task StartAsync(string initialPrompt, CancellationToken cancellationToken = default)
        {
            State = CliAgentSessionState.Running;
            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<CliAgentEvent> ReadEventsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _tracker.Enter();
            try
            {
                await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _tracker.Exit();
            }

            State = CliAgentSessionState.Completed;
            yield break;
        }

        public Task SendCommandAsync(string text, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CloseInputAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CancelCurrentRequestAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            State = CliAgentSessionState.Stopped;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
