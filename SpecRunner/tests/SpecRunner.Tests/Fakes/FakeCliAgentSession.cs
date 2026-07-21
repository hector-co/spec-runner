using System.Runtime.CompilerServices;
using SpecRunner.Core.Abstractions;
using SpecRunner.Core.Models;

namespace SpecRunner.Tests.Fakes;

public class FakeCliAgentSession : ICliAgentSession
{
    private readonly CliAgentSessionState _finalState;
    private readonly TimeSpan _readEventsDelay;

    public FakeCliAgentSession(CliAgentSessionState finalState, TimeSpan readEventsDelay = default)
    {
        _finalState = finalState;
        _readEventsDelay = readEventsDelay;
    }

    public CliAgentSessionState State { get; private set; } = CliAgentSessionState.NotStarted;

    public string? LastPrompt { get; private set; }

    public bool StopCalled { get; private set; }

    public bool Disposed { get; private set; }

    public Task StartAsync(string initialPrompt, CancellationToken cancellationToken = default)
    {
        LastPrompt = initialPrompt;
        State = CliAgentSessionState.Running;
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<CliAgentEvent> ReadEventsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_readEventsDelay > TimeSpan.Zero)
        {
            await Task.Delay(_readEventsDelay, cancellationToken).ConfigureAwait(false);
        }

        State = _finalState;
        yield break;
    }

    public Task SendCommandAsync(string text, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public bool CloseInputCalled { get; private set; }

    public Task CloseInputAsync(CancellationToken cancellationToken = default)
    {
        CloseInputCalled = true;
        return Task.CompletedTask;
    }

    public Task CancelCurrentRequestAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        StopCalled = true;
        State = CliAgentSessionState.Stopped;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}
