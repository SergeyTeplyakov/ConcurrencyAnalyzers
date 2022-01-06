using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ConcurrencyAnalyzers.IntegrationTests.Utils;
using ConcurrencyAnalyzers.ParallelThreadsAnalysis;
using ConcurrencyAnalyzers.Rendering;
using ConcurrencyAnalyzers.Utilities;

using Xunit.Abstractions;

namespace ConcurrencyAnalyzers.IntegrationTests
{
    public class IntegrationTestBase : RedirectingConsoleTest
    {
        public IntegrationTestBase(ITestOutputHelper helper) : base(helper)
        {
        }

        private static ParallelThreads GetParallelThreadsForCurrentProcess()
        {
            int processId = Process.GetCurrentProcess().Id;
            using TargetWithRuntime targetWithRuntime = ConcurrencyAnalyzer.AttachTo(processId).GetValueOrThrow();
            var threadRegistry = ThreadRegistry.Create(targetWithRuntime.Runtime, degreeOfParallelism: null);
            return ConcurrencyAnalyzer.AnalyzeParallelThreads(targetWithRuntime.Runtime, threadRegistry);
        }

        private static ParallelThreads GetParallelThreadsFromDumpFile(string dumpFile)
        {
            using TargetWithRuntime targetWithRuntime = ConcurrencyAnalyzer.OpenDump(dumpFile).GetValueOrThrow();
            var threadRegistry = ThreadRegistry.Create(targetWithRuntime.Runtime, Environment.ProcessorCount);
            return ConcurrencyAnalyzer.AnalyzeParallelThreads(targetWithRuntime.Runtime, threadRegistry);
        }

        protected static void LogToOutput(ParallelThreads parallelThreads, bool renderRawStackFrames = false)
        {
            var consoleRenderer = new ConsoleRenderer(renderRawStackFrames);
            consoleRenderer.Render(parallelThreads);
        }

        private static async Task GenerateDumpFileIfNeeded(string dumpPath, Action<CancellationToken> stateEstablishing)
        {
            if (!File.Exists(dumpPath))
            {
                // Need to create a dump file!

                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

                var task = Task.Factory.StartNew(
                    () => stateEstablishing(cts.Token),
                    TaskCreationOptions.LongRunning);

                // A small delay to block the execution.
                await Task.Delay(400);

                var currentProcess = Process.GetCurrentProcess();
                ProcessDumper.DumpProcess(currentProcess.Handle, currentProcess.Id, dumpPath);

                // Need to await the task produced by the callback, because it is responsible for the cleanup.
                await task;
            }
            else
            {
                Console.WriteLine($"Skipping dump file generation because file '{dumpPath}' already exists.");
            }
        }

        protected async Task<ParallelThreads> AnalyzeWithMemoryDumpAsync(
            Action<CancellationToken> stateEstablishing,
            [CallerMemberName] string testCaseName = "")
        {
            var dumpFile = Path.Combine(Directory.GetCurrentDirectory(), "Dumps", $"{GetType().Name}.{testCaseName}.dmp");
            Output.WriteLine($"Using dump file: {dumpFile}");

            await GenerateDumpFileIfNeeded(dumpFile, stateEstablishing);

            return GetParallelThreadsFromDumpFile(dumpFile);
        }

        protected static async Task<ParallelThreads> AnalyzeForCurrentProcessAsync(
            Action<CancellationToken> stateEstablishing)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

            var task = Task.Factory.StartNew(
                () => stateEstablishing(cts.Token),
                TaskCreationOptions.LongRunning);

            // A small delay to block the execution.
            await Task.Delay(400);

            var result = GetParallelThreadsForCurrentProcess();

            // Need to await the task produced by the callback, because it is responsible for the cleanup.
            await task;

            return result;
        }

        protected Task<ParallelThreads> AnalyzeTestCase(
            bool useDumpFile,
            Action<CancellationToken> stateEstablishing,
            [CallerMemberName] string testCaseName = "")
        {
            return useDumpFile
                ? AnalyzeWithMemoryDumpAsync(stateEstablishing, testCaseName)
                : AnalyzeForCurrentProcessAsync(stateEstablishing);
        }
    }
}