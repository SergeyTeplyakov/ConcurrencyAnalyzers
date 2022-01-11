using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConcurrencyAnalyzers.Utilities;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace ConcurrencyAnalyzers.IntegrationTests
{
    /// <summary>
    /// High level integration tests that checks that generate a dump file and attach to a running process.
    /// </summary>
    public class ParallelThreadsIntegrationTests : IntegrationTestBase
    {
        public ParallelThreadsIntegrationTests(ITestOutputHelper helper) : base(helper)
        {
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ParallelForBlockedOnLock(bool useDumpFile)
        {
            int threadCount = 42;
            var analysisResult = await AnalyzeTestCase(
                useDumpFile,
                token => ParallelForBlockedOnLockCase.Run(threadCount, token).GetAwaiter().GetResult());
            LogToOutput(analysisResult);

            AssertNoStrangeNameInStackTraces(analysisResult);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task BlockThreadPoolThreads(bool useDumpFile)
        {
            int threadCount = 42;
            var parallelThreads = await AnalyzeTestCase(
                useDumpFile,
                token => BlockThreadPoolThreadsCase.Run(threadCount).GetAwaiter().GetResult());
            LogToOutput(parallelThreads);

            AssertNoStrangeNameInStackTraces(parallelThreads);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ExceptionOnStack(bool useDumpFile)
        {
            // It seems that the lock count is not working in .net core! But works for net472!
            var exceptionMessage = "My exception message";
            var exception = new InvalidOperationException(exceptionMessage);
            var parallelThreads = await AnalyzeTestCase(
                useDumpFile,
                // Adding a static local function to create a more complicated stack frame.
                token => TestCase(exception, token));
            LogToOutput(parallelThreads);

            AssertNoStrangeNameInStackTraces(parallelThreads);

            static void TestCase(Exception exception, CancellationToken token)
                => ExceptionInsideTheLockCase.BlockingMethodWithLockAndException(exception, token);
        }

        static void AssertNoStrangeNameInStackTraces(AnalysisResult result)
        {
            // Checking that the following set of symbols is not present in stack traces:
            string[] invalidSubStrings = new[] { ".<", ".>", "<>", "`", "|" };
            var threads = result.ParallelThreads.AssertNotNull().AssertSuccess();

            foreach (var stackTrace in threads.GroupedThreads.SelectMany(pt => pt.ThreadInfo.StackFrames))
            {
                stackTrace.Signature.Should().NotContainAny(invalidSubStrings);
            }
        }
    }
}

