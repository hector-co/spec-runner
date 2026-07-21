using SpecRunner.Cli;
using SpecRunner.Core.Configuration;
using SpecRunner.Core.Models;
using SpecRunner.Tests.Fakes;

namespace SpecRunner.Tests;

public class ClaudeCliAgentSessionTests
{
    private static (ClaudeCliAgentSession Session, FakeChildProcessFactory Factory) CreateSession(
        CliAgentOptions? cliAgentOptions = null,
        SpecRunnerOptions? specRunnerOptions = null)
    {
        var factory = new FakeChildProcessFactory();
        var session = new ClaudeCliAgentSession(
            cliAgentOptions ?? new CliAgentOptions(),
            specRunnerOptions ?? new SpecRunnerOptions { LocalRepositoryPath = "/repo" },
            factory);
        return (session, factory);
    }

    [Fact]
    public async Task StartAsyncLaunchesProcessAndTransitionsToRunning()
    {
        var (session, factory) = CreateSession();

        await session.StartAsync("propose a change for issue 45");

        Assert.Equal(CliAgentSessionState.Running, session.State);
        Assert.True(factory.LastCreated!.Started);
        Assert.Contains("--input-format", factory.Arguments!);
        Assert.Contains("stream-json", factory.Arguments!);
        Assert.Single(factory.LastCreated.WrittenLines);
    }

    [Fact]
    public async Task StartAsyncFallsBackToLocalRepositoryPathWhenWorkingDirectoryUnset()
    {
        var (session, factory) = CreateSession(
            cliAgentOptions: new CliAgentOptions { WorkingDirectory = null },
            specRunnerOptions: new SpecRunnerOptions { LocalRepositoryPath = "/repo/path" });

        await session.StartAsync("prompt");

        Assert.Equal("/repo/path", factory.WorkingDirectory);
    }

    [Fact]
    public async Task StartingAnAlreadyStartedSessionThrows()
    {
        var (session, _) = CreateSession();
        await session.StartAsync("prompt");

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.StartAsync("prompt again"));
    }

    [Fact]
    public async Task EventsAreObservableBeforeProcessExits()
    {
        var (session, factory) = CreateSession();
        await session.StartAsync("prompt");

        factory.LastCreated!.EmitOutputLine("""{"type":"assistant","message":{"content":[{"type":"text","text":"hello"}]}}""");

        await using var enumerator = session.ReadEventsAsync().GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(CliAgentEventKind.AssistantMessage, enumerator.Current.Kind);
        Assert.Equal("hello", enumerator.Current.Payload);

        Assert.Equal(CliAgentSessionState.Running, session.State);
    }

    [Fact]
    public async Task UnparseableOutputYieldsErrorEventInsteadOfThrowing()
    {
        var (session, factory) = CreateSession();
        await session.StartAsync("prompt");

        factory.LastCreated!.EmitOutputLine("not valid json");

        await using var enumerator = session.ReadEventsAsync().GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(CliAgentEventKind.Error, enumerator.Current.Kind);
    }

    [Fact]
    public async Task SendCommandSucceedsWhileRunning()
    {
        var (session, factory) = CreateSession();
        await session.StartAsync("prompt");

        await session.SendCommandAsync("also update the tests");

        Assert.Equal(2, factory.LastCreated!.WrittenLines.Count);
        Assert.Contains("also update the tests", factory.LastCreated.WrittenLines[1]);
    }

    [Fact]
    public async Task SendCommandThrowsWhenNotStarted()
    {
        var (session, _) = CreateSession();

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.SendCommandAsync("text"));
    }

    [Fact]
    public async Task SendCommandThrowsWhenCompleted()
    {
        var (session, factory) = CreateSession();
        await session.StartAsync("prompt");
        factory.LastCreated!.SimulateExit(0);

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.SendCommandAsync("text"));
    }

    [Fact]
    public async Task SendCommandThrowsWhenFailed()
    {
        var (session, factory) = CreateSession();
        await session.StartAsync("prompt");
        factory.LastCreated!.SimulateExit(1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.SendCommandAsync("text"));
    }

    [Fact]
    public async Task SendCommandThrowsWhenStopped()
    {
        var (session, _) = CreateSession();
        await session.StartAsync("prompt");
        await session.StopAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.SendCommandAsync("text"));
    }

    [Fact]
    public async Task CancelWritesInterruptMessageWithoutEndingSession()
    {
        var (session, factory) = CreateSession();
        await session.StartAsync("prompt");

        await session.CancelCurrentRequestAsync();

        Assert.Equal(2, factory.LastCreated!.WrittenLines.Count);
        Assert.Contains("interrupt", factory.LastCreated.WrittenLines[1]);
        Assert.False(factory.LastCreated.Killed);
        Assert.Equal(CliAgentSessionState.Running, session.State);
    }

    [Fact]
    public async Task CancelWhenIdleIsANoOp()
    {
        var (session, _) = CreateSession();
        await session.StartAsync("prompt");

        await session.CancelCurrentRequestAsync();

        Assert.Equal(CliAgentSessionState.Running, session.State);
    }

    [Fact]
    public async Task StopAsyncTerminatesProcessAndTransitionsToStopped()
    {
        var (session, factory) = CreateSession();
        await session.StartAsync("prompt");

        await session.StopAsync();

        Assert.Equal(CliAgentSessionState.Stopped, session.State);
        Assert.True(factory.LastCreated!.StandardInputClosed);
        Assert.True(factory.LastCreated.Killed);
    }

    [Fact]
    public async Task DisposingRunningSessionWithoutStopAsyncTerminatesProcess()
    {
        var (session, factory) = CreateSession();
        await session.StartAsync("prompt");

        await session.DisposeAsync();

        Assert.Equal(CliAgentSessionState.Stopped, session.State);
        Assert.True(factory.LastCreated!.Killed);
    }

    [Fact]
    public async Task CleanExitReachesCompleted()
    {
        var (session, factory) = CreateSession();
        await session.StartAsync("prompt");

        factory.LastCreated!.EmitOutputLine("""{"type":"result","subtype":"success"}""");
        factory.LastCreated.SimulateExit(0);

        await using var enumerator = session.ReadEventsAsync().GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(CliAgentEventKind.ResultCompleted, enumerator.Current.Kind);
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(CliAgentEventKind.ResultCompleted, enumerator.Current.Kind);
        Assert.Equal(0, enumerator.Current.ExitCode);
        Assert.False(await enumerator.MoveNextAsync());

        Assert.Equal(CliAgentSessionState.Completed, session.State);
    }

    [Fact]
    public async Task NonZeroExitReachesFailedWithExitCodeAndStderr()
    {
        var (session, factory) = CreateSession();
        await session.StartAsync("prompt");

        factory.LastCreated!.SetStandardError("boom");
        factory.LastCreated.SimulateExit(1);

        await using var enumerator = session.ReadEventsAsync().GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(CliAgentEventKind.Error, enumerator.Current.Kind);
        Assert.Equal(1, enumerator.Current.ExitCode);
        Assert.Equal("boom", enumerator.Current.StdErr);
        Assert.False(await enumerator.MoveNextAsync());

        Assert.Equal(CliAgentSessionState.Failed, session.State);
    }

    [Fact]
    public void CreateSessionReturnsIndependentSessionsInNotStartedState()
    {
        var factory = new ClaudeCliAgentSessionFactory(
            Microsoft.Extensions.Options.Options.Create(new CliAgentOptions()),
            Microsoft.Extensions.Options.Options.Create(new SpecRunnerOptions { LocalRepositoryPath = "/repo" }));

        var first = factory.CreateSession();
        var second = factory.CreateSession();

        Assert.NotSame(first, second);
        Assert.Equal(CliAgentSessionState.NotStarted, first.State);
        Assert.Equal(CliAgentSessionState.NotStarted, second.State);
    }
}
