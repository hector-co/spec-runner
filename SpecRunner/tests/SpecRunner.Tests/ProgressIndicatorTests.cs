using Microsoft.Extensions.Logging;
using SpecRunner.Console;

namespace SpecRunner.Tests;

public class ProgressIndicatorTests
{
    [Fact]
    public async Task LogsAtLeastOnceWhenLeftRunningPastFiveSeconds()
    {
        var logger = new RecordingLogger();
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(30.5));

        await ProgressIndicator.RunAsync(logger, "still in progress", cts.Token);

        Assert.True(logger.Messages.Count >= 1);
        Assert.All(logger.Messages, message => Assert.Equal("still in progress", message));
    }

    [Fact]
    public async Task LogsNothingWhenCancelledBeforeFirstInterval()
    {
        var logger = new RecordingLogger();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await ProgressIndicator.RunAsync(logger, "still in progress", cts.Token);

        Assert.Empty(logger.Messages);
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
