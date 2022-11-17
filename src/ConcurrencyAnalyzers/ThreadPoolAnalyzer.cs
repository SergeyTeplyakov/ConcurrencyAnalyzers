using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.ContractsLight;
using System.Linq;
using ConcurrencyAnalyzers.Utilities;
using Microsoft.Diagnostics.Runtime;

namespace ConcurrencyAnalyzers
{
    /// <summary>
    /// A resulting thread pool statistics.
    /// </summary>
    /// <param name="NumProcessingWork">A number of work items being processed right now.</param>
    /// <param name="NumBlockedThreads">A number of thread pool threads blocked by <code>Task.Wait</code> or <code>Task.GetAwaiter().GetResult()</code>.</param>
    /// <param name="NumThreadsAddedDueToBlocking">A number of threads that were added due to an excessive number of blocked threads.</param>
    /// <param name="MemoryUsageBytes">A memory usage of the thread pool.</param>
    /// <param name="ThreadCount">A number of threads currently available in the thread pool.</param>
    /// <param name="AvailableThreads">A maximum number of threads in the thread pool.</param>
    public record ThreadPoolStats
        (
            int NumProcessingWork,
            int NumBlockedThreads,
            int NumThreadsAddedDueToBlocking,
            long MemoryUsageBytes,
            int ThreadCount,
            int AvailableThreads
        );

    /// <summary>
    /// An analyzer that looks at the heap for thread pool stats if a portable (i.e. managed) thread pool implementation is used.
    /// </summary>
    /// <remarks>
    /// As of Jan 2022 ClrMD does not expose any API that allows getting a thread pool stats.
    /// The only option is to check the managed thread pool stats if it was used by the process.
    /// </remarks>
    public class ThreadPoolAnalyzer
    {
        private readonly ClrRuntime _runtime;
        private readonly Lazy<ClrModule?> _coreLib;

        public ThreadPoolAnalyzer(ClrRuntime runtime)
        {
            _runtime = runtime;
            _coreLib = new Lazy<ClrModule?>(() =>
                runtime.EnumerateModules().FirstOrDefault(m => m.Name?.EndsWith("System.Private.CoreLib.dll") == true));
        }

        public static Result<ThreadPoolStats> TryAnalyzeThreadPoolStats(ClrRuntime runtime)
        {
            var analyzer = new ThreadPoolAnalyzer(runtime);
            if (!analyzer.PortableThreadPoolIsUsed())
            {
                return Result.Error<ThreadPoolStats>(
                    "The target runtime does not use the portable thread pool implementation.");
            }

            return Result.Success(analyzer.GetThreadPoolStats());
        }

        public bool PortableThreadPoolIsUsed()
        {
            // We can just check if the System.Threading.PortableThreadPool
            // type is available in a dump, but its not enough, because
            // the portable thread pool may be off even when available (via a configuration).
            //
            // ThreadPool class have a static field UsePortableThreadPool that defines
            // whether the managed thread pool implementation was used or not.
            if (_coreLib.Value is { } coreLib)
            {
                var threadPoolType = coreLib.GetTypeByName("System.Threading.ThreadPool");

                if (threadPoolType is null)
                {
                    return false;
                }

                bool? isWorkerTrackingEnabledInConfig = threadPoolType.GetStaticFieldByName("UsePortableThreadPool").TryGetValue<bool>(_runtime);
                return isWorkerTrackingEnabledInConfig == true;
            }

            return false;
        }

        public ThreadPoolStats GetThreadPoolStats()
        {
            Contract.Requires(PortableThreadPoolIsUsed());
            // System.Threading.PortableThreadPool.BlockingConfig
            if (_coreLib.Value is { } coreLib)
            {
                // If 'PortableThreadPoolIsUsed' is true, the portable thread pool type must be present.
                var portableThreadPoolType = coreLib.GetTypeByName("System.Threading.PortableThreadPool").AssertNotNull();

                // The thread pool is a singleton.
                var threadPoolInstance =
                    portableThreadPoolType.GetStaticFieldByName("ThreadPoolInstance").AssertNotNull()
                        .TryReadObject(_runtime).AssertNotNull();

                // See PortableThreadPool.cs CacheLineSeparated struct for more details.
                // Getting 'ThreadCounts' struct from '_separated' field.
                var data =
                    threadPoolInstance.ReadValueTypeField("_separated").AssertNotNull()
                        .ReadValueTypeField("counts").AssertNotNull()
                        .ReadField<ulong>("_data");
                var threadCounts = new ThreadCounts(data);

                // Need to check, but the semantics of '_numBlockedThreads' is a bit weird. Its not incremented just when a thread pool's thread
                // is blocked, it is increased when 'ThreadPool.NotifyThreadBlocked' method is called, and its not called a lot (one example is when Task.Wait is called).
                // I think this is done to increase the number of threads in a thread pool when 'sync over async' pattern is used!
                short numBlockedThreads = threadPoolInstance.ReadField<short>("_numBlockedThreads");
                short numThreadsAddedDueToBlocking = threadPoolInstance.ReadField<short>("_numThreadsAddedDueToBlocking");
                long memoryUsageBytes = threadPoolInstance.ReadField<short>("_memoryUsageBytes");

                short maxThreads = threadPoolInstance.ReadField<short>("_maxThreads");

                // See PortableThreadPool.GetAvailableThreads method
                int availableThreads = maxThreads - threadCounts.NumProcessingWork;
                if (availableThreads < 0)
                {
                    availableThreads = 0;
                }

                return new ThreadPoolStats(
                    NumProcessingWork: threadCounts.NumProcessingWork,
                    NumBlockedThreads: numBlockedThreads,
                    NumThreadsAddedDueToBlocking: numThreadsAddedDueToBlocking,
                    MemoryUsageBytes: memoryUsageBytes,
                    ThreadCount: threadCounts.NumExistingThreads,
                    AvailableThreads: availableThreads);
            }

            return null!;
        }

        /// <summary>
        /// Tracks information on the number of threads we want/have in different states in our thread pool.
        /// </summary>
        /// <remarks>
        /// This is a copy of the struct from dotnet/runtime repository.
        /// </remarks>
        private struct ThreadCounts
        {
            // SOS's ThreadPool command depends on this layout
            private const byte NumProcessingWorkShift = 0;
            private const byte NumExistingThreadsShift = 16;
            private const byte NumThreadsGoalShift = 32;

            private ulong _data; // SOS's ThreadPool command depends on this name

            public ThreadCounts(ulong data) => _data = data;

            private short GetInt16Value(byte shift) => (short)(_data >> shift);
            private void SetInt16Value(short value, byte shift) =>
                _data = (_data & ~((ulong)ushort.MaxValue << shift)) | ((ulong)(ushort)value << shift);

            /// <summary>
            /// Number of threads processing work items.
            /// </summary>
            public short NumProcessingWork => GetInt16Value(NumProcessingWorkShift);

            /// <summary>
            /// Number of thread pool threads that currently exist.
            /// </summary>
            public short NumExistingThreads => GetInt16Value(NumExistingThreadsShift);

            /// <summary>
            /// Max possible thread pool threads we want to have.
            /// </summary>
            public short NumThreadsGoal => GetInt16Value(NumThreadsGoalShift);

            public static bool operator ==(ThreadCounts lhs, ThreadCounts rhs) => lhs._data == rhs._data;
            public static bool operator !=(ThreadCounts lhs, ThreadCounts rhs) => lhs._data != rhs._data;

            public override bool Equals([NotNullWhen(true)] object? obj) => obj is ThreadCounts other && _data == other._data;
            public override int GetHashCode() => (int)_data + (int)(_data >> 32);
        }
    }
}