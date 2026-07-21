using SpecRunner.Core.Models;

namespace SpecRunner.Core.Abstractions;

public interface ICliAgentSession : IAsyncDisposable
{
    CliAgentSessionState State { get; }

    Task StartAsync(string initialPrompt, CancellationToken cancellationToken = default);

    IAsyncEnumerable<CliAgentEvent> ReadEventsAsync(CancellationToken cancellationToken = default);

    Task SendCommandAsync(string text, CancellationToken cancellationToken = default);

    Task CancelCurrentRequestAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
