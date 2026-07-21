using Microsoft.Extensions.Logging;
using SpecRunner.Core.Abstractions;

namespace SpecRunner.Console;

public static class PollingLoop
{
    public static async Task RunAsync(
        IProposeWorkflowRunner proposeWorkflowRunner,
        TimeSpan pollingInterval,
        CancellationToken cancellationToken,
        ILogger logger)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await proposeWorkflowRunner.RunOnceAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception during propose-workflow scan pass");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await Task.Delay(pollingInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
