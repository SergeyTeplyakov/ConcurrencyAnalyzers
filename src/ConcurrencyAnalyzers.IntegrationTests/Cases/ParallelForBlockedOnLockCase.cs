using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#nullable disable

namespace ConcurrencyAnalyzers.IntegrationTests
{
    // The code looks weird just to check that the tool can 'decrypt' the stack traces properly.
    public class ParallelForBlockedOnLockCase
    {
        private static readonly object s_globalSyncLock = new object();

        public static async Task Run(int threadCount, CancellationToken token)
        {
            // Detaching from the calling thread since the ForEachAsync is actually a blocking call.
            await Task.Yield();
            var source = Enumerable.Range(1, 1000);
            source.AsParallel().WithDegreeOfParallelism(threadCount).Select(n =>
                LongRunningAndBlockingTask<int>(token)).ToList();
        }

        private static Task<T> LongRunningAndBlockingTask<T>(CancellationToken token)
        {
            return Task.FromResult(FooBar<T>(token));
        }

        private static T FooBar<T>(CancellationToken token)
        {
            return FooBaz<T>(token);

            static T FooBaz<U>(CancellationToken token)
            {
                lock (s_globalSyncLock)
                {
                    // This will allow breaking the method once token is requested.
                    if (!token.IsCancellationRequested)
                    {
                        Console.WriteLine("Sleep inside the lock!");
                        Thread.Sleep(1000);
                    }
                }

                return default;
            }
        }
    }
}