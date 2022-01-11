using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ConcurrencyAnalyzers.IntegrationTests.Utils;
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

        private static AnalysisResult AnalyzeCurrentProcess()
        {
            int processId = Process.GetCurrentProcess().Id;
            using TargetWithRuntime targetWithRuntime = ConcurrencyAnalyzer.AttachTo(processId).GetValueOrThrow();

            var analysisOptions = new AnalysisOptions(AnalysisScope.All, DegreeOfParallelism: Environment.ProcessorCount);
            return ConcurrencyAnalyzer.Analyze(analysisOptions, targetWithRuntime.Runtime);
        }

        private static AnalysisResult AnalyzeDumpFile(string dumpFile)
        {
            using TargetWithRuntime targetWithRuntime = ConcurrencyAnalyzer.OpenDump(dumpFile).GetValueOrThrow();
            var analysisOptions = new AnalysisOptions(AnalysisScope.All, DegreeOfParallelism: Environment.ProcessorCount);
            return ConcurrencyAnalyzer.Analyze(analysisOptions, targetWithRuntime.Runtime);
        }

        protected static void LogToOutput(AnalysisResult parallelThreads, bool renderRawStackFrames = false)
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

        protected async Task<AnalysisResult> AnalyzeWithMemoryDumpAsync(
            Action<CancellationToken> stateEstablishing,
            [CallerMemberName] string testCaseName = "")
        {
            var dumpFile = Path.Combine(Directory.GetCurrentDirectory(), "Dumps", $"{GetType().Name}.{testCaseName}.dmp");
            Output.WriteLine($"Using dump file: {dumpFile}");

            await GenerateDumpFileIfNeeded(dumpFile, stateEstablishing);

            return AnalyzeDumpFile(dumpFile);
        }

        protected static async Task<AnalysisResult> AnalyzeForCurrentProcessAsync(
            Action<CancellationToken> stateEstablishing)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

            var task = Task.Factory.StartNew(
                () => stateEstablishing(cts.Token),
                TaskCreationOptions.LongRunning);

            // A small delay to block the execution.
            await Task.Delay(400);

            var result = AnalyzeCurrentProcess();

            // Need to await the task produced by the callback, because it is responsible for the cleanup.
            await task;

            return result;
        }

        protected Task<AnalysisResult> AnalyzeTestCase(
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