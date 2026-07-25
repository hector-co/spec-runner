using Microsoft.Extensions.Logging;

namespace SpecRunner.Console;

internal static class ProgressIndicator
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    public static async Task RunAsync(ILogger logger, string message, CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await Task.Delay(Interval, cancellationToken).ConfigureAwait(false);
                logger.LogInformation(message);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected once the awaited work completes and the linked token is cancelled.
        }
    }
}
