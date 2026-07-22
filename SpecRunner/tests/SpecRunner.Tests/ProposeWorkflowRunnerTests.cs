using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SpecRunner.Console;
using SpecRunner.Core;
using SpecRunner.Core.Abstractions;
using SpecRunner.Core.Configuration;
using SpecRunner.Core.Models;
using SpecRunner.State;
using SpecRunner.Tests.Fakes;

namespace SpecRunner.Tests;

public class ProposeWorkflowRunnerTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _stateFilePath;

    public ProposeWorkflowRunnerTests()
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

    private (ProposeWorkflowRunner Runner, RecordingGitService Git, RecordingGitHubService GitHub, FakeCliAgentSessionFactory CliFactory, SqliteStateStore StateStore, FakeTasksFileReader TasksFileReader) CreateRunner(
        TimeSpan? taskTimeout = null,
        Func<ICliAgentSession>? sessionFactory = null)
    {
        var git = new RecordingGitService();
        var gitHub = new RecordingGitHubService();
        var cliFactory = new FakeCliAgentSessionFactory(sessionFactory ?? (() => new FakeCliAgentSession(CliAgentSessionState.Completed)));
        var stateStore = new SqliteStateStore(_stateFilePath);
        var specNameResolver = new SpecNameResolver();
        var tasksFileReader = new FakeTasksFileReader();
        var commandTemplateRenderer = new CommandTemplateRenderer();
        var options = Options.Create(new SpecRunnerOptions
        {
            BaseBranchName = "main",
            TaskTimeout = taskTimeout ?? TimeSpan.FromSeconds(30)
        });

        var runner = new ProposeWorkflowRunner(gitHub, git, stateStore, cliFactory, specNameResolver, tasksFileReader, commandTemplateRenderer, options, NullLogger<ProposeWorkflowRunner>.Instance);
        return (runner, git, gitHub, cliFactory, stateStore, tasksFileReader);
    }

    private static GitHubIssue Issue(int number, string title, string body, params GitHubIssueComment[] comments)
        => new(number, title, body, comments);

    private static GitHubIssueComment Comment(long id, string body, string author = "someone")
        => new(id, author, body, DateTimeOffset.UtcNow);

    [Theory]
    [InlineData("/propose")]
    [InlineData("/propose\nAdditional context")]
    [InlineData("/propose please handle this")]
    [InlineData("  /propose  ")]
    public async Task EligibleCommentBodiesReceiveEyesReaction(string body)
    {
        var (runner, _, gitHub, _, _, _) = CreateRunner();
        gitHub.Issues.Add(Issue(45, "Add Login Page", "We need a login page.", Comment(9001, body)));

        await runner.RunOnceAsync();

        Assert.Contains((9001L, "eyes"), gitHub.AddedReactions);
    }

    [Theory]
    [InlineData("/proposed")]
    [InlineData("please /propose this")]
    [InlineData("/proposework")]
    [InlineData("some text mentioning /propose mid-sentence")]
    public async Task NonEligibleCommentBodiesAreIgnored(string body)
    {
        var (runner, _, gitHub, _, _, _) = CreateRunner();
        gitHub.Issues.Add(Issue(45, "Add Login Page", "We need a login page.", Comment(9001, body)));

        await runner.RunOnceAsync();

        Assert.Empty(gitHub.AddedReactions);
    }

    [Fact]
    public async Task CommentWithExistingBotReactionIsSkipped()
    {
        var (runner, git, gitHub, cliFactory, _, _) = CreateRunner();
        gitHub.Issues.Add(Issue(45, "Title", "Body", Comment(9001, "/propose")));
        gitHub.Reactions[9001] = new List<GitHubReaction> { new(gitHub.Login, "rocket") };

        await runner.RunOnceAsync();

        Assert.DoesNotContain(gitHub.AddedReactions, r => r.CommentId == 9001);
        Assert.Empty(git.Calls);
        Assert.Empty(cliFactory.CreatedSessions);
    }

    [Fact]
    public async Task CommentWithOnlyHumanReactionIsStillProcessed()
    {
        var (runner, _, gitHub, _, _, _) = CreateRunner();
        gitHub.Issues.Add(Issue(45, "Title", "Body", Comment(9001, "/propose")));
        gitHub.Reactions[9001] = new List<GitHubReaction> { new("a-human", "eyes") };

        await runner.RunOnceAsync();

        Assert.Contains((9001L, "eyes"), gitHub.AddedReactions);
    }

    [Fact]
    public async Task IssueWithExistingPrShortCircuitsWorkflow()
    {
        var (runner, git, gitHub, cliFactory, stateStore, _) = CreateRunner();
        await stateStore.UpsertTrackedIssueAsync(new TrackedIssue(45, "45-title") { PrNumber = 12 });
        gitHub.Issues.Add(Issue(45, "Title", "Body", Comment(9001, "/propose")));

        await runner.RunOnceAsync();

        Assert.Contains(
            gitHub.CreatedComments,
            c => c.IssueNumber == 45 && c.Body == "This issue already has an active Draft PR: #12. Please add /update to the PR instead.");
        Assert.Contains((9001L, "rocket"), gitHub.AddedReactions);
        Assert.Empty(git.Calls);
        Assert.Empty(cliFactory.CreatedSessions);
    }

    [Fact]
    public async Task SuccessfulRunCommitsPushesCreatesDraftPrAndUpdatesState()
    {
        var (runner, git, gitHub, cliFactory, stateStore, tasksFileReader) = CreateRunner();
        gitHub.NextPrNumber = 12;
        tasksFileReader.CurrentContentBySpecName["45-add-login-page"] = "## 1. Tasks\n- [ ] 1.1 Do the thing";
        gitHub.Issues.Add(Issue(45, "Add Login Page", "We need a login page.", Comment(9001, "/propose")));

        await runner.RunOnceAsync();

        Assert.Equal(
            new[] { "Pull", "ResetHard:main", "CreateBranch:feature/45", "SwitchBranch:feature/45", "Commit:adding specs for #45", "Push:feature/45" },
            git.Calls);

        var session = Assert.IsType<FakeCliAgentSession>(Assert.Single(cliFactory.CreatedSessions));
        Assert.Equal(
            "\"/opsx-propose 45-add-login-page\nWe need a login page.\n\n" +
            "This is an unattended run — do not ask for confirmation or clarification\n" +
            "at any step. If something is ambiguous, make the most reasonable\n" +
            "assumption, note it in proposal.md under a brief \"Assumptions\" note, and\n" +
            "continue.\"",
            session.LastPrompt);
        Assert.True(session.CloseInputCalled);

        var pr = Assert.Single(gitHub.CreatedDraftPrs);
        Assert.Equal("feature/45", pr.HeadBranch);
        Assert.Equal("main", pr.BaseBranch);
        Assert.Equal("## 1. Tasks\n- [ ] 1.1 Do the thing", pr.Body);

        Assert.Contains((9001L, "rocket"), gitHub.AddedReactions);
        Assert.Contains(gitHub.CreatedComments, c => c.IssueNumber == 45 && c.Body == "Created Draft PR #12 for this issue.");

        var tracked = await stateStore.FindByIssueNumberAsync(45);
        Assert.NotNull(tracked);
        Assert.Equal(12, tracked!.PrNumber);
        Assert.Equal("45-add-login-page", tracked.SpecName);
        var comment = Assert.Single(tracked.Comments);
        Assert.Equal(CommentStatus.Done, comment.Status);
    }

    [Fact]
    public async Task MissingTasksFileResultsInEmptyDraftPrBody()
    {
        var (runner, _, gitHub, _, _, _) = CreateRunner();
        gitHub.NextPrNumber = 12;
        gitHub.Issues.Add(Issue(45, "Add Login Page", "We need a login page.", Comment(9001, "/propose")));

        await runner.RunOnceAsync();

        var pr = Assert.Single(gitHub.CreatedDraftPrs);
        Assert.Equal(string.Empty, pr.Body);
        Assert.Contains(gitHub.CreatedComments, c => c.IssueNumber == 45 && c.Body == "Created Draft PR #12 for this issue.");
    }

    [Fact]
    public async Task ThrownExceptionDuringProcessingIsReportedAndProcessingContinuesToNextComment()
    {
        var (runner, _, gitHub, cliFactory, stateStore, _) = CreateRunner();
        gitHub.ThrowOnCreateDraftPr = new InvalidOperationException("GitHub API call failed with HTTP 422: validation failed");
        gitHub.ThrowOnCreateDraftPrOnlyOnce = true;
        gitHub.Issues.Add(Issue(45, "Title A", "Body A", Comment(9001, "/propose")));
        gitHub.Issues.Add(Issue(46, "Title B", "Body B", Comment(9002, "/propose")));

        await runner.RunOnceAsync();

        Assert.Contains((9001L, "confused"), gitHub.AddedReactions);
        Assert.Contains(
            gitHub.CreatedComments,
            c => c.IssueNumber == 45 && c.Body == "Processing this comment failed: GitHub API call failed with HTTP 422: validation failed");

        var trackedFailed = await stateStore.FindByIssueNumberAsync(45);
        Assert.NotNull(trackedFailed);
        var failedComment = Assert.Single(trackedFailed!.Comments);
        Assert.Equal(CommentStatus.Error, failedComment.Status);

        Assert.Contains((9002L, "rocket"), gitHub.AddedReactions);
        var trackedSucceeded = await stateStore.FindByIssueNumberAsync(46);
        Assert.NotNull(trackedSucceeded);
        Assert.NotNull(trackedSucceeded!.PrNumber);

        Assert.Equal(2, cliFactory.CreatedSessions.Count);
        Assert.All(cliFactory.CreatedSessions, s => Assert.True(Assert.IsType<FakeCliAgentSession>(s).CloseInputCalled));
    }

    [Fact]
    public async Task ExceedingTaskTimeoutStopsSessionAndReportsTimeout()
    {
        var (runner, _, gitHub, cliFactory, stateStore, _) = CreateRunner(
            taskTimeout: TimeSpan.FromMilliseconds(50),
            sessionFactory: () => new FakeCliAgentSession(CliAgentSessionState.Completed, readEventsDelay: TimeSpan.FromSeconds(5)));
        gitHub.Issues.Add(Issue(45, "Title", "Body", Comment(9001, "/propose")));

        await runner.RunOnceAsync();

        var session = Assert.IsType<FakeCliAgentSession>(Assert.Single(cliFactory.CreatedSessions));
        Assert.True(session.StopCalled);
        Assert.True(session.CloseInputCalled);
        Assert.Contains((9001L, "confused"), gitHub.AddedReactions);
        Assert.Contains(gitHub.CreatedComments, c => c.IssueNumber == 45 && c.Body == "Processing this comment timed out.");

        var tracked = await stateStore.FindByIssueNumberAsync(45);
        Assert.NotNull(tracked);
        var comment = Assert.Single(tracked!.Comments);
        Assert.Equal(CommentStatus.Error, comment.Status);
    }

    [Fact]
    public async Task EligibleCommentsAreProcessedSequentiallyNotConcurrently()
    {
        var tracker = new ConcurrencyTracker();
        var (runner, _, gitHub, cliFactory, _, _) = CreateRunner(
            sessionFactory: () => new TrackingCliAgentSession(tracker, TimeSpan.FromMilliseconds(30)));
        gitHub.Issues.Add(Issue(45, "Title A", "Body A", Comment(9001, "/propose")));
        gitHub.Issues.Add(Issue(46, "Title B", "Body B", Comment(9002, "/propose")));

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
