using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;

namespace ConcurrencyAnalyzers;

public record EnumerateObjectsProgress(long TotalObjectCount, double DiscoveryRatePerSecond, long RelevantObjects);

/// <summary>
/// Helper class for enumerating objects faster.
/// </summary>
public class ObjectRetriever
{
    public static IEnumerable<ClrObject> EnumerateObjects(ClrRuntime runtime, Func<ClrObject, bool> objectSelector, int degreeOfParallelism, Action<EnumerateObjectsProgress>? progressReporter = null)
    {
        // TODO: still checking if parallel processing is worth it! It seems that on hdd it gives the same results actually!
        if (!runtime.IsThreadSafe)
        {
            throw new InvalidOperationException("Parallel enumeration is not possible. runtime.IsThreadSafe is false.");
            // The runtime is not thread-safe.
            // return runtime.Heap.EnumerateObjects();
        }

        Console.WriteLine($"Segments: {runtime.Heap.Segments.Length}, DoP: {degreeOfParallelism}, GCMode: {(GCSettings.IsServerGC ? "Server" : "Workstation")}");

        var blockingCollection = new BlockingCollection<ClrObject>();

        long discoveredObjectsCount = 0;
        long relevantObjectsCount = 0;
        CancellationTokenSource? cts = null;

        if (progressReporter is not null)
        {
            cts = new CancellationTokenSource();

            var reportingTask = Task.Run(async () =>
            {
                var periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
                while (!cts.IsCancellationRequested)
                {
                    long previousDiscoveredObjectsCount = Interlocked.Read(ref discoveredObjectsCount);

                    if (await periodicTimer.WaitForNextTickAsync(cts.Token))
                    {
                        long currentCount = Interlocked.Read(ref discoveredObjectsCount);

                        var rate = currentCount - previousDiscoveredObjectsCount;
                        previousDiscoveredObjectsCount = currentCount;
                        progressReporter(new EnumerateObjectsProgress(currentCount, rate, Interlocked.Read(ref relevantObjectsCount)));
                    }

                }
            });
        }

        var enumerateTask = Task.Factory.StartNew(() =>
        {
            var stats = runtime.Heap.Segments
                .AsParallel()
                .WithDegreeOfParallelism(degreeOfParallelism)
                .Select(segment =>
                {
                    if (!runtime.Heap.CanWalkHeap)
                    {
                        // The heap can be in a bad state if the dump is created during GC, for instance.
                        return (threadId: Thread.CurrentThread.ManagedThreadId, processedCount: 0);
                    }

                    int count = 0;
                    foreach (var clrObject in segment.EnumerateObjects())
                    {
                        if (clrObject.IsValid && !clrObject.IsNull)
                        {
                            Interlocked.Increment(ref discoveredObjectsCount);

                            if (objectSelector(clrObject))
                            {
                                count++;
                                Interlocked.Increment(ref relevantObjectsCount);
                                blockingCollection.Add(clrObject);
                            }
                        }
                    }

                    return (threadId: Thread.CurrentThread.ManagedThreadId, processedCount: count);
                })
                .ToList();

            string statsString =
                $"Processing stats: {string.Join(", ", stats.Select(tpl => $"ThreadId: {tpl.threadId}, Count: {tpl.processedCount}"))}";
            Console.WriteLine(statsString);

            blockingCollection.CompleteAdding();
        }, TaskCreationOptions.LongRunning);

        if (cts is not null)
        {
            enumerateTask.ContinueWith(_ =>
            {
                cts.Cancel();
            });
        }

        foreach (var clrObject in blockingCollection.GetConsumingEnumerable())
        {
            yield return clrObject;
        }
    }
}