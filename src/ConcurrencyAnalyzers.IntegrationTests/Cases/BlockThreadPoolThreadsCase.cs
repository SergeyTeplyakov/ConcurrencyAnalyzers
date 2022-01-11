using System;
using System.Linq;
using System.Threading.Tasks;

#nullable disable

namespace ConcurrencyAnalyzers.IntegrationTests
{
    // This test demonstrates the new features of the managed thread pool.
    public class BlockThreadPoolThreadsCase
    {
        public static async Task Run(int degreeOfParallelism)
        {
            // Detaching from the calling thread since the ForEachAsync is actually a blocking call.
            await Task.Yield();

            var tcs = new TaskCompletionSource();
            _ = Task.Delay(TimeSpan.FromSeconds(1)).ContinueWith(_ => tcs.SetResult());

            var tasks = Enumerable.Range(1, degreeOfParallelism)
                .Select(_ => Task.Run(async () =>
                {
                    await Task.Yield();
                    tcs.Task.GetAwaiter().GetResult();
                }));

            await Task.WhenAll(tasks);
        }
    }
}