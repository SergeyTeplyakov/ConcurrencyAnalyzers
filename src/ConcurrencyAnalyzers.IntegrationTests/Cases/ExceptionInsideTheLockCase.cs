using System;
using System.Threading;

namespace ConcurrencyAnalyzers.IntegrationTests;

public class ExceptionInsideTheLockCase
{
    public static void BlockingMethodWithLockAndException(Exception exception, CancellationToken token)
    {
        object o = new object();
        lock (o)
        {
            // For some reason when we attach to the running process, lock count is not properly available, but exceptions are!
            try
            {
                throw exception;
            }
            catch (Exception)
            {
                // Blocking the exception handling block to see the exception in one of the stacks.
                SafeDelay.Delay(TimeSpan.FromSeconds(42), token).GetAwaiter().GetResult();
            }
        }
    }
}
