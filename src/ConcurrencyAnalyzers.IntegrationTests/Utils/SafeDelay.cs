using System;
using System.Threading;
using System.Threading.Tasks;

namespace ConcurrencyAnalyzers.IntegrationTests
{
    public static class SafeDelay
    {
        public static async Task Delay(TimeSpan delay, CancellationToken token)
        {
            try
            {
                await Task.Delay(delay, token);
            }
            catch (TaskCanceledException)
            {

            }
        }
    }
}