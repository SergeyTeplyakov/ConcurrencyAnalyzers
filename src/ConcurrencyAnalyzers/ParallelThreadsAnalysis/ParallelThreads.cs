using System;
using System.Collections.Generic;
using System.Diagnostics.ContractsLight;
using System.Linq;

using ConcurrencyAnalyzers.Utilities;

using Microsoft.Diagnostics.Runtime;

namespace ConcurrencyAnalyzers.ParallelThreadsAnalysis
{
    public record StackFrame(string TypeName, string Method, string Arguments, string Signature)
    {
        /// <inheritdoc />
        public override string ToString() => Signature;
    }

    public record ThreadId(string? Name, int ManagedId, uint OsId);

    public record ExceptionObject(string TypeName, string? Message);

    public record ThreadInfo(StackFrame[] StackFrames, string[] RawStackFrames, ThreadId ThreadId,
        ExceptionObject? Exception, LockCount LockCount);

    /// <summary>
    /// A lightweight wrapper that represents the lock count.
    /// </summary>
    /// <remarks>
    /// It seems that lock count is not supported for .net core processes!
    /// </remarks>
    public readonly struct LockCount
    {
        private readonly int _count;

        public LockCount(uint count)
        {
            if (count == uint.MaxValue)
            {
                _count = -1;
            }
            else
            {
                checked
                {
                    _count = (int)count;
                }
            }
        }

        public bool IsValid => _count != -1;

        public bool IsEmpty => !IsValid || _count == 0;

        public int Count
        {
            get
            {
                Contract.Requires(IsValid);
                return _count;
            }
        }

        public override string ToString()
        {
            return IsValid ? Count.ToString() : "Invalid";
        }
    }

    public class ParallelThreads
    {
        public ParallelThread[] GroupedThreads { get; }

        public int ThreadCount => GroupedThreads.Aggregate(0, (c, t) => c + t switch
        {
            GroupedParallelThread gpt => gpt.GroupedThreads.Length,
            SingleParallelThread => 1,
            _ => throw new InvalidOperationException($"Unknown type {t.GetType()}.")
        });

        private ParallelThreads(ParallelThread[] groupedThreads)
        {
            GroupedThreads = groupedThreads;
        }

        public static ParallelThreads Create(ClrRuntime runtime, IThreadRegistry? threadRegistry = null)
        {
            var threads = new List<ThreadInfo>();

            foreach (var thread in runtime.Threads)
            {
                if (!thread.IsAlive)
                {
                    continue;
                }

                var stackFrames = thread.EnumerateStackTrace()
                    .Select(sf => (raw: sf, enhanced: StackFrameSanitizer.StackFrameToDisplayString(sf)))
                    .ToArray();

                if (stackFrames.Length > 0)
                {
                    ExceptionObject? exception = null;
                    if (thread.CurrentException is not null)
                    {
                        exception = new ExceptionObject(thread.CurrentException.Type.Name ?? "UnknownExceptionType",
                            thread.CurrentException.Message);
                    }

                    var threadInfo = new ThreadInfo(
                        stackFrames.Where(tpl => tpl.enhanced is not null).Select(tpl => tpl.enhanced!).ToArray(),
                        RawStackFrames: stackFrames.Select(sf => sf.raw.ToString()!).ToArray(),
                        new ThreadId(threadRegistry?.TryGetThreadName(thread.ManagedThreadId), thread.ManagedThreadId, thread.OSThreadId),
                        exception,
                        new LockCount(thread.LockCount));

                    // Skipping the threads with no managed stack traces
                    if (threadInfo.StackFrames.Length > 0)
                    {
                        threads.Add(threadInfo);
                    }
                }
            }

            return new ParallelThreads(ParallelThread.Group(threads));
        }
    }

    /// <summary>
    /// Represents a single 'item' in a "parallel stacks" view.
    /// </summary>
    public abstract class ParallelThread
    {
        public abstract string Header { get; }
        public abstract string Body { get; }

        public abstract ThreadInfo ThreadInfo { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{Header}{Environment.NewLine}{Body}";
        }

        public static ParallelThread[] Group(IEnumerable<ThreadInfo> threadInfos)
        {
            // Grouping is quite naive.
            // We check that the entire stack traces are exactly the same.

            // Grouping all thread information based on their hash codes.
            // This is still may not be the final thing, but can be close.
            var threadInfoHashCodeGroup = threadInfos.ToMultiDictionary(ti => ti, ti => ti, ThreadInfoEqualityComparer.Instance);

            return threadInfoHashCodeGroup
                .Select(kvp => Create(kvp.Value)!)
                .Where(r => r != null)
                // Sorting from the groups with the most number of threads down.
                .OrderByDescending(GetThreadCount)
                .ToArray();

            static int GetThreadCount(ParallelThread thread) =>
                thread switch
                {
                    GroupedParallelThread groupedParallelThread => groupedParallelThread.GroupedThreads.Length,
                    SingleParallelThread => 1,
                    _ => throw new ArgumentOutOfRangeException(nameof(thread))
                };
        }

        protected static string ThreadInfoToString(ThreadInfo threadInfo) => string.Join(Environment.NewLine,
            threadInfo.StackFrames.Select(sf => sf.ToString()));

        private static ParallelThread? Create(List<ThreadInfo> info)
        {
            return info.Count switch
            {
                0 => null,
                1 => new SingleParallelThread(info[0]),
                _ => new GroupedParallelThread(info.ToArray()),
            };
        }

        private class ThreadInfoEqualityComparer : IEqualityComparer<ThreadInfo>
        {
            public static ThreadInfoEqualityComparer Instance { get; } = new ThreadInfoEqualityComparer();
            public bool Equals(ThreadInfo? x, ThreadInfo? y)
            {
                if (x == y)
                {
                    return true;
                }

                if (x is null || y is null)
                {
                    return false;
                }

                if (x.Exception != y.Exception)
                {
                    // Can't group if one of the threads has an exception.
                    return false;
                }

                return x.StackFrames.SequenceEqual(y.StackFrames);
            }

            public int GetHashCode(ThreadInfo obj)
            {
                int result = 0;

                foreach (var stackFrame in obj.StackFrames)
                {
                    result = HashCode.Combine(result, stackFrame.GetHashCode());
                }

                return result;
            }
        }
    }

    public sealed class GroupedParallelThread : ParallelThread
    {
        /// <inheritdoc />
        public override string Header { get; }

        /// <inheritdoc />
        public override string Body { get; }

        /// <inheritdoc />
        public override ThreadInfo ThreadInfo { get; }

        public ThreadInfo[] GroupedThreads { get; }

        public GroupedParallelThread(ThreadInfo[] groupedThreads)
        {
            Contract.Requires(groupedThreads.Length != 0);

            GroupedThreads = groupedThreads;
            ThreadInfo = groupedThreads[0];

            int idsToTake = 10;
            string optionalTripleDots = groupedThreads.Length > idsToTake ? "..." : string.Empty;
            Header = $"{groupedThreads.Length} Threads. (Ids: {string.Join(", ", groupedThreads.Take(idsToTake).Select(t => t.ThreadId.ManagedId))}{optionalTripleDots})";

            Body = ThreadInfoToString(ThreadInfo);
        }
    }

    public sealed class SingleParallelThread : ParallelThread
    {
        /// <inheritdoc />
        public override string Header { get; }

        /// <inheritdoc />
        public override string Body { get; }

        /// <inheritdoc />
        public override ThreadInfo ThreadInfo { get; }

        public SingleParallelThread(ThreadInfo singleThread)
        {
            ThreadInfo = singleThread;
            string name = string.Empty;

            if (!string.IsNullOrEmpty(singleThread.ThreadId.Name))
            {
                name = $" ({singleThread.ThreadId.Name})";
            }

            Header = $"Thread #{singleThread.ThreadId.ManagedId} (OsId: #{singleThread.ThreadId.OsId}){name}";
            Body = ThreadInfoToString(singleThread);
        }
    }
}