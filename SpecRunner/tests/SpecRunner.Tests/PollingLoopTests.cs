using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using SpecRunner.Console;
using SpecRunner.Core.Abstractions;

namespace SpecRunner.Tests;

public class PollingLoopTests
{
    [Fact]
    public async Task ExceptionFromScanPassIsCaughtAndLoopContinuesToNextPass()
    {
        using var cts = new CancellationTokenSource();
        var callCount = 0;
        var runner = new FakeProposeWorkflowRunner(() =>
        {
            callCount++;
            if (callCount == 1)
            {
                throw new InvalidOperationException("boom");
            }

            cts.Cancel();
        });

        await PollingLoop.RunAsync(runner, TimeSpan.Zero, cts.Token, NullLogger.Instance);

        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task CancelledTokenStopsLoopBeforeFirstScanPass()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var callCount = 0;
        var runner = new FakeProposeWorkflowRunner(() => callCount++);

        await PollingLoop.RunAsync(runner, TimeSpan.FromSeconds(30), cts.Token, NullLogger.Instance);

        Assert.Equal(0, callCount);
    }

    [Fact]
    public async Task CancellationDuringPollingDelayStopsLoopPromptly()
    {
        using var cts = new CancellationTokenSource();
        var callCount = 0;
        var runner = new FakeProposeWorkflowRunner(() =>
        {
            callCount++;
            cts.CancelAfter(TimeSpan.FromMilliseconds(20));
        });

        var stopwatch = Stopwatch.StartNew();
        await PollingLoop.RunAsync(runner, TimeSpan.FromSeconds(30), cts.Token, NullLogger.Instance);
        stopwatch.Stop();

        Assert.Equal(1, callCount);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Expected prompt cancellation, took {stopwatch.Elapsed}");
    }

    private sealed class FakeProposeWorkflowRunner : IProposeWorkflowRunner
    {
        private readonly Action _onRun;

        public FakeProposeWorkflowRunner(Action onRun)
        {
            _onRun = onRun;
        }

        public Task RunOnceAsync(CancellationToken cancellationToken = default)
        {
            _onRun();
            return Task.CompletedTask;
        }
    }
}
