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
        var proposeRunner = new FakeProposeWorkflowRunner(() =>
        {
            callCount++;
            if (callCount == 1)
            {
                throw new InvalidOperationException("boom");
            }

            cts.Cancel();
        });
        var implementRunner = new FakeImplementWorkflowRunner(() => { });

        await PollingLoop.RunAsync(proposeRunner, implementRunner, TimeSpan.Zero, cts.Token, NullLogger.Instance);

        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task CancelledTokenStopsLoopBeforeFirstScanPass()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var proposeCallCount = 0;
        var implementCallCount = 0;
        var proposeRunner = new FakeProposeWorkflowRunner(() => proposeCallCount++);
        var implementRunner = new FakeImplementWorkflowRunner(() => implementCallCount++);

        await PollingLoop.RunAsync(proposeRunner, implementRunner, TimeSpan.FromSeconds(30), cts.Token, NullLogger.Instance);

        Assert.Equal(0, proposeCallCount);
        Assert.Equal(0, implementCallCount);
    }

    [Fact]
    public async Task CancellationDuringPollingDelayStopsLoopPromptly()
    {
        using var cts = new CancellationTokenSource();
        var callCount = 0;
        var proposeRunner = new FakeProposeWorkflowRunner(() =>
        {
            callCount++;
            cts.CancelAfter(TimeSpan.FromMilliseconds(20));
        });
        var implementRunner = new FakeImplementWorkflowRunner(() => { });

        var stopwatch = Stopwatch.StartNew();
        await PollingLoop.RunAsync(proposeRunner, implementRunner, TimeSpan.FromSeconds(30), cts.Token, NullLogger.Instance);
        stopwatch.Stop();

        Assert.Equal(1, callCount);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Expected prompt cancellation, took {stopwatch.Elapsed}");
    }

    [Fact]
    public async Task EachCycleRunsProposeThenImplementSequentially()
    {
        using var cts = new CancellationTokenSource();
        var callOrder = new List<string>();
        var proposeRunner = new FakeProposeWorkflowRunner(() => callOrder.Add("propose"));
        var implementRunner = new FakeImplementWorkflowRunner(() =>
        {
            callOrder.Add("implement");
            cts.Cancel();
        });

        await PollingLoop.RunAsync(proposeRunner, implementRunner, TimeSpan.Zero, cts.Token, NullLogger.Instance);

        Assert.Equal(new[] { "propose", "implement" }, callOrder);
    }

    [Fact]
    public async Task ExceptionFromProposeWorkflowDoesNotPreventImplementWorkflowRunningThatCycle()
    {
        using var cts = new CancellationTokenSource();
        var implementCallCount = 0;
        var proposeRunner = new FakeProposeWorkflowRunner(() => throw new InvalidOperationException("boom"));
        var implementRunner = new FakeImplementWorkflowRunner(() =>
        {
            implementCallCount++;
            cts.Cancel();
        });

        await PollingLoop.RunAsync(proposeRunner, implementRunner, TimeSpan.Zero, cts.Token, NullLogger.Instance);

        Assert.Equal(1, implementCallCount);
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

    private sealed class FakeImplementWorkflowRunner : IImplementWorkflowRunner
    {
        private readonly Action _onRun;

        public FakeImplementWorkflowRunner(Action onRun)
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
