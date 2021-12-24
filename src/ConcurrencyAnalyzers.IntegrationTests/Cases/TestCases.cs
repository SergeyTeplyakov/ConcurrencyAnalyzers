using System;
using System.Threading;

namespace ConcurrencyAnalyzers.IntegrationTests;

public static class TestCases
{
    public static void ExceptionInsideLock(Exception exception, CancellationToken token)
        => ExceptionInsideTheLockCase.BlockingMethodWithLockAndException(exception, token);

    public static void ParallelForWithLock(int threadCount, CancellationToken token)
        => ParallelForBlockedOnLockCase.Run(threadCount, token).GetAwaiter().GetResult();
}