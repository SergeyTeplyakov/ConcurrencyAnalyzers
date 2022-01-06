using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Diagnostics.Runtime.Implementation;

namespace ConcurrencyAnalyzers;

public record EnumerateObjectsProgress(long TotalObjectCount, double DiscoveryRatePerSecond, long RelevantObjectsCount);

/// <summary>
/// Helper class for enumerating objects faster.
/// </summary>
public class ObjectsRetriever
{
    // it seems that 4-6 threads gives the best throughput.
    public const int DefaultDegreeOfParallelism = 5;
    
    private readonly int _degreeOfParallelism;
    private readonly Action<EnumerateObjectsProgress>? _reportProgress;

    public ObjectsRetriever(int? degreeOfParallelism = DefaultDegreeOfParallelism, Action<EnumerateObjectsProgress>? reportProgress = null)
    {
        _degreeOfParallelism = degreeOfParallelism ?? DefaultDegreeOfParallelism;
        _reportProgress = reportProgress;
    }

    /// <summary>
    /// Enumerates all the instances of <see cref="Thread"/> class.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="EnumerateObjects"/> this method just enumerates finalizable objects to discover threads.
    /// This is much faster because the number of finalizable objects is significantly smaller then the overall number of
    /// objects in the managed heap.
    /// </remarks>
    public IEnumerable<ClrObject> EnumerateThreads(ClrRuntime runtime)
    {
        if (!runtime.Heap.CanWalkHeap)
        {
            throw new InvalidOperationException($"Can't walk the heap!");
        }

        Console.WriteLine($"Discovering ClrThreads with {_degreeOfParallelism} threads...");

        IHeapHelpers? helpers = TryGetHeapHelpers(runtime);
        if (helpers is not null)
        {
            return EnumerateThreadsFast(runtime, helpers);
        }

        // Re-using 'EnumerateObjectsCore' for reporting purposes.
        return EnumerateObjectsCore(
            sequence: new[] { Unit.Void },
            map: _ => runtime.Heap.EnumerateFinalizableObjects(),
            predicate: clrInstance => clrInstance.Type?.Name == "System.Threading.Thread");
    }

    private IEnumerable<ClrObject> EnumerateObjectsCore<T>(
        IEnumerable<T> sequence, 
        Func<T, IEnumerable<ClrObject>> map,
        Func<ClrObject, bool> predicate)
    {
        long instanceCount = 0;
        long relevantInstanceCount = 0;
        
        CancellationTokenSource? cts = null;

        if (_reportProgress is not null)
        {
            cts = new CancellationTokenSource();
            StartReporter(cts.Token);
        }

        var relevantObjects = new BlockingCollection<ClrObject>();

        var enumerateTask = Task.Factory.StartNew(() =>
        {
            var stats = sequence.AsParallel().WithDegreeOfParallelism(_degreeOfParallelism)
                .Select(element =>
                {
                    int count = 0;
                    foreach (var clrInstance in map(element))
                    {
                        if (!clrInstance.IsValid || clrInstance.IsNull)
                        {
                            continue;
                        }

                        Interlocked.Increment(ref instanceCount);
                        if (predicate(clrInstance))
                        {
                            Interlocked.Increment(ref relevantInstanceCount);
                            count++;
                            relevantObjects.Add(clrInstance);
                        }
                    }

                    return (threadId: Environment.CurrentManagedThreadId, count);
                })
                .ToList();

            stats = stats
                .ToMultiDictionary(tpl => tpl.threadId, tpl => tpl)
                .Select(entry => (threadId: entry.Key, processedCount: entry.Value.Sum(e => e.count)))
                .ToList();

            string statsString =
                $"Processing stats: {string.Join(", ", stats.Select(tpl => $"ThreadId: {tpl.threadId}, Count: {tpl.count}"))}";
            Console.WriteLine(statsString);

            relevantObjects.CompleteAdding();
        }, TaskCreationOptions.LongRunning);

        // TODO: trace if the task fails?

        if (cts is not null)
        {
            // Enumeration is done. Cancelling reporting if its running.
            enumerateTask.ContinueWith(_ =>
            {
                cts.Cancel();
            });
        }

        foreach (var clrObject in relevantObjects.GetConsumingEnumerable())
        {
            yield return clrObject;
        }

        void StartReporter(CancellationToken token)
        {
            Task.Run(async () =>
            {
                var periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
                long previousDiscoveredObjectsCount = Interlocked.Read(ref instanceCount);

                while (!token.IsCancellationRequested)
                {
                    if (await periodicTimer.WaitForNextTickAsync(token))
                    {
                        long currentCount = Interlocked.Read(ref instanceCount);

                        var rate = currentCount - previousDiscoveredObjectsCount;
                        previousDiscoveredObjectsCount = currentCount;
                        _reportProgress?.Invoke(new EnumerateObjectsProgress(currentCount, rate, Interlocked.Read(ref relevantInstanceCount)));
                    }
                }
            });
        }
    }

    private IEnumerable<ClrObject> EnumerateThreadsFast(ClrRuntime runtime, IHeapHelpers helpers)
    {
        var success = helpers.CreateSegments(runtime.Heap, out _, out _, out _, out var fqObjects);

        // TODO: trace unsuccessful case!
        if (!success)
        {
            throw new InvalidOperationException($"Can't get finalizer queue segments.");
        }

        Console.WriteLine($"CreateSegments: {success}, Finalizer queue segments count: {fqObjects.Length}");

        return EnumerateObjectsCore(
            fqObjects,
            ReadObjectsFromFinalizerQueueSegment,
            clrInstance => clrInstance.Type?.Name == "System.Threading.Thread");

        IEnumerable<ClrObject> ReadObjectsFromFinalizerQueueSegment(FinalizerQueueSegment seg)
        {
            for (ulong ptr = seg.Start; ptr < seg.End; ptr += (uint)IntPtr.Size)
            {
                ulong obj = helpers.DataReader.ReadPointer(ptr);
                if (obj == 0)
                {
                    continue;
                }

                ulong mt = helpers.DataReader.ReadPointer(obj);
                ClrType? type = helpers.Factory.GetOrCreateType(mt, obj);
                yield return new ClrObject(obj, type);
            }
        }

        //{
        //    var threads = new BlockingCollection<ClrObject>();

        //    var success = helpers.CreateSegments(runtime.Heap, out _, out _, out _, out var fqObjects);
        //    Console.WriteLine($"CreateSegments: {success}, FqObjects.Length: {fqObjects.Length}");
        //    var enumerateTask = Task.Factory.StartNew(() =>
        //    {
        //        var stats = fqObjects.AsParallel().WithDegreeOfParallelism(degreeOfParallelism)
        //            .Select(seg =>
        //            {
        //                int count = 0;
        //                for (ulong ptr = seg.Start; ptr < seg.End; ptr += (uint)IntPtr.Size)
        //                {
        //                    ulong obj = helpers.DataReader.ReadPointer(ptr);
        //                    if (obj == 0)
        //                    {
        //                        continue;
        //                    }

        //                    ulong mt = helpers.DataReader.ReadPointer(obj);
        //                    ClrType? type = helpers.Factory.GetOrCreateType(mt, obj);
        //                    if (type?.Name == "System.Threading.Thread")
        //                    {
        //                        count++;
        //                        threads.Add(new ClrObject(obj, type));
        //                    }
        //                }

        //                return (threadId: Thread.CurrentThread.ManagedThreadId, processedCount: count);
        //            })
        //            .ToList();

        //        stats = stats
        //            .ToMultiDictionary(tpl => tpl.threadId, tpl => tpl)
        //            .Select(entry => (threadId: entry.Key, processedCount: entry.Value.Sum(e => e.processedCount)))
        //            .ToList();

        //        string statsString =
        //            $"Processing stats: {string.Join(", ", stats.Select(tpl => $"ThreadId: {tpl.threadId}, Count: {tpl.processedCount}"))}";
        //        Console.WriteLine(statsString);

        //        threads.CompleteAdding();
        //    }, TaskCreationOptions.LongRunning);

        //    foreach (var clrObject in threads.GetConsumingEnumerable())
        //    {
        //        yield return clrObject;
        //    }
        //}
    }

    private static IHeapHelpers? TryGetHeapHelpers(ClrRuntime runtime)
    {
        try
        {
            // A hacky way of getting 'IHeapHelpers'. This is the only way I've found so far.
            var heap = runtime.Heap;
            var helpersField = heap.GetType().GetField("_helpers", BindingFlags.Instance | BindingFlags.NonPublic);
            var result = (IHeapHelpers)helpersField!.GetValue(heap)!;
            return result;
        }
        catch (Exception ex)
        {
            // TODO: trace properly.
            Console.WriteLine($"Failed getting IHeapHelpers: {ex}");
            return null;
        }
    }

    public IEnumerable<ClrObject> EnumerateObjects(
        ClrRuntime runtime, 
        Func<ClrObject, bool> predicate)
    {
        // TODO: still checking if parallel processing is worth it! It seems that on hdd it gives the same results actually!
        if (!runtime.IsThreadSafe)
        {
            throw new InvalidOperationException("Parallel enumeration is not possible. runtime.IsThreadSafe is false.");
            // The runtime is not thread-safe.
            // return runtime.Heap.EnumerateObjects();
        }

        if (!runtime.Heap.CanWalkHeap)
        {
            throw new InvalidOperationException($"Can't walk the heap!");
        }

        // TODO: trace properly.
        Console.WriteLine($"Segments: {runtime.Heap.Segments.Length}, DoP: {_degreeOfParallelism}, GCMode: {(GCSettings.IsServerGC ? "Server" : "Workstation")}");

        return EnumerateObjectsCore(
            sequence: runtime.Heap.Segments,
            map: static segment => segment.EnumerateObjects(),
            predicate);
    }
}