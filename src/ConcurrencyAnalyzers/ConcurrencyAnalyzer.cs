using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

using ConcurrencyAnalyzers.ParallelThreadsAnalysis;
using ConcurrencyAnalyzers.Utilities;

using Microsoft.Diagnostics.Runtime;

namespace ConcurrencyAnalyzers
{
    /// <summary>
    /// A type-safe pair of <see cref="DataTarget"/> and <see cref="ClrRuntime"/>.
    /// </summary>
    public record TargetWithRuntime(DataTarget Target, ClrRuntime Runtime) : IDisposable
    {
        public void Dispose()
        {
            Runtime.Dispose();
            Target.Dispose();
        }
    }

    // TODO: Analyzers should be like a set of decorators that we should be able to add to a chain of analysis?

    /// <summary>
    /// An entry point for all concurrency analyzers implemented in this project.
    /// </summary>
    public class ConcurrencyAnalyzer
    {
        /// <summary>
        /// Opens a dump file at a given <paramref name="dumpPath"/>.
        /// </summary>
        public static Result<TargetWithRuntime> OpenDump(string dumpPath, string? dacFilePath = null, CacheOptions? cacheOptions = null)
        {
            if (!File.Exists(dumpPath))
            {
                string error = $"Specified dump file is not found at '{dumpPath}'.";
                return Result.Error<TargetWithRuntime>(error);
            }

            try
            {
                var target = DataTarget.LoadDump(dumpPath, cacheOptions);
                return CreateRuntime(target, dacFilePath);
            }
            catch (IOException e)
            {
                return Result.Error<TargetWithRuntime>(
                    $"IO error occurred when processing dump file at '{dumpPath}': {e.Message}");
            }
            catch (Exception e)
            {
                return Result.Error<TargetWithRuntime>(
                    $"Error processing dump file at '{dumpPath}': {e}");
            }
        }

        /// <summary>
        /// Gets the <see cref="TargetWithRuntime"/> from a running process by <paramref name="processId"/>.
        /// </summary>
        public static Result<TargetWithRuntime> AttachTo(int processId)
        {
            try
            {
                Process.GetProcessById(processId);
            }
            catch (ArgumentException e)
            {
                return Result.Error<TargetWithRuntime>($"Can't find process by id {processId}. {e.Message}");
            }

            return AttachToCore(processId);
        }

        private static Result<TargetWithRuntime> AttachToCore(int processId)
        {
            try
            {
                // TODO: add tracing.
                var dataTarget = DataTarget.CreateSnapshotAndAttach(processId);
                return CreateRuntime(dataTarget, dacFilePath: null);
            }
            catch (Exception e)
            {
                return Result.Error<TargetWithRuntime>($"Failed attaching to process {processId}. Error: {e}");
            }
        }

        /// <summary>
        /// Gets the <see cref="TargetWithRuntime"/> from a running process by <paramref name="processName"/>.
        /// </summary>
        public static Result<TargetWithRuntime> AttachTo(string processName)
        {
            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName(processName);
            }
            catch (InvalidOperationException e)
            {
                return Result.Error<TargetWithRuntime>($"Error getting processes by name '{processName}'. {e.Message}");
            }

            if (processes.Length == 0)
            {
                return Result.Error<TargetWithRuntime>($"Can't find any process with the name '{processName}'.");
            }

            if (processes.Length > 1)
            {
                return Result.Error<TargetWithRuntime>($"Find more then one process that matches the name '{processName}': {string.Join(", ", processes.Select(p => p.Id))}. Attach to the process by Id instead.");
            }

            return AttachToCore(processes.First().Id);
        }

        private static Result<TargetWithRuntime> CreateRuntime(DataTarget target, string? dacFilePath = null)
        {
            var runtime = CreateRuntimeCore(target, dacFilePath);
            if (!runtime.Success)
            {
                target.Dispose();
                return Result<TargetWithRuntime>.FromError(runtime);
            }

            return Result.Success(new TargetWithRuntime(target, runtime.Value));

            static Result<ClrRuntime> CreateRuntimeCore(DataTarget target, string? dacFilePath)
            {
                const string BaseErrorMessage = "Failed to create a runtime from a dump file";
                bool isTarget64Bit = target.DataReader.PointerSize == 8;
                if (Environment.Is64BitProcess != isTarget64Bit)
                {
                    string error =
                        $"{BaseErrorMessage}. Architecture mismatch. The tool is {Bitness(Environment.Is64BitProcess)} but target dump is {Bitness(isTarget64Bit)}.";
                    return Result.Error<ClrRuntime>(error);

                    static string Bitness(bool is64Bit) => is64Bit ? "64 bit" : "32 bit";
                }

                if (target.ClrVersions.IsEmpty)
                {
                    return Result.Error<ClrRuntime>($"{BaseErrorMessage}. Can't find any CLR instances in a dump file.");
                }

                var runtimeInfo = target.ClrVersions[0]; // just using the first runtime

                return Result.Success(
                    !string.IsNullOrEmpty(dacFilePath)
                        ? runtimeInfo.CreateRuntime(dacFilePath)
                        : runtimeInfo.CreateRuntime()
                );
            }
        }

        public static ParallelThreads AnalyzeParallelThreads(ClrRuntime runtime, ThreadRegistry? threadRegistry)
        {
            return ParallelThreads.Create(runtime, threadRegistry);
        }

        // TODO: use a full set of logging/tracing features! Like etw event source etc.

        public static AnalysisResult Analyze(AnalysisOptions options, ClrRuntime runtime)
        {
            AnalysisResult result = new AnalysisResult();
            if (options.Scope.HasFlag(AnalysisScope.ThreadPool))
            {
                result.ThreadPoolStats = ThreadPoolAnalyzer.TryAnalyzeThreadPoolStats(runtime);
            }

            if (options.Scope.HasFlag(AnalysisScope.Threads))
            {
                result.ThreadRegistry = ThreadRegistry.Create(runtime, options.DegreeOfParallelism);
            }

            if (options.Scope.HasFlag(AnalysisScope.ParallelThreads))
            {
                // TODO: add a try/catch block to avoid crashes.
                result.ParallelThreads = Result.Success(AnalyzeParallelThreads(runtime, result.ThreadRegistry));
            }

            return result;
        }
    }

    [Flags]
    public enum AnalysisScope
    {
        None = 0,
        Threads = 1 << 1,
        ParallelThreads = 1 << 2,
        ThreadPool = 1 << 3,
        Tasks = 1 << 4,
        TasksCausality = 1 << 5,

        All = Threads | ParallelThreads | ThreadPool | Tasks | TasksCausality,
    }

    public record AnalysisOptions(
        AnalysisScope Scope,
        int? DegreeOfParallelism);

    public record AnalysisResult
    {
        public ThreadRegistry? ThreadRegistry { get; set; }
        public Result<ParallelThreads>? ParallelThreads { get; set; }
        public Result<ThreadPoolStats>? ThreadPoolStats { get; set; }
    }
}

