using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.ContractsLight;
using System.Linq;
using Microsoft.Diagnostics.Runtime;

namespace ConcurrencyAnalyzers;

public interface IThreadRegistry
{
    string? TryGetThreadName(int managedThreadId);
}

public class EmptyThreadRegistry : IThreadRegistry
{
    public string? TryGetThreadName(int managedThreadId)
    {
        return null;
    }
}

/// <summary>
/// Contains extra information about the threads.
/// </summary>
/// <remarks>
/// This is not that useful class for the full framework case, because most of the threads there would have 'null' as the name.
/// </remarks>
public class ThreadRegistry : IThreadRegistry
{
    private readonly Dictionary<int, string?> _threadNames;

    private ThreadRegistry(Dictionary<int, string?> threadNames)
    {
        _threadNames = threadNames;
    }

    public int ThreadCount => _threadNames.Count;

    public string? TryGetThreadName(int managedThreadId)
    {
        _threadNames.TryGetValue(managedThreadId, out var result);
        return result;
    }

    public static ThreadRegistry Create(ClrRuntime runtime, int? degreeOfParallelism)
    {
        var activeThreads = runtime.Threads.Where(t => t.IsAlive).Select(t => t.ManagedThreadId).ToHashSet();
        var dictionary = new Dictionary<int, string?>();

        // TODO: use proper logging.
        var sw = Stopwatch.StartNew();

        var objectsRetriever = new ObjectsRetriever(
            degreeOfParallelism,
            progress =>
            {
                Console.WriteLine(
                    $"Processed {progress.TotalObjectCount}, rate: {progress.DiscoveryRatePerSecond}ops, Relevant Objects: {progress.RelevantObjectsCount}.");
            });

        var threads = objectsRetriever.EnumerateThreads(runtime);

        foreach (var threadObject in threads)
        {
            int managedThreadId = threadObject.ReadField<int>(GetManagedThreadIdFieldName(threadObject));
            string? threadName = threadObject.ReadStringField(GetNameFieldName(threadObject));

            dictionary[managedThreadId] = threadName;
            if (activeThreads.Count == dictionary.Count)
            {
                // No need to further look up the heap if all the threads were observed/discovered.
                break;
            }
        }

        Console.WriteLine($"Discovered the names for {dictionary.Count} threads in {sw.ElapsedMilliseconds}ms.");

        return new ThreadRegistry(dictionary);
    }

    private static string ManagedThreadIdFieldName = string.Empty;
    private static string NameFieldName = string.Empty;

    /// <summary>
    /// Gets the name of 'managed thread id' field at runtime because the field name is runtime specific.
    /// </summary>
    private static string GetManagedThreadIdFieldName(ClrObject threadObject)
    {
        Contract.Requires(threadObject.Type != null);

        if (string.IsNullOrEmpty(ManagedThreadIdFieldName))
        {
            var managedThreadIdField = threadObject.Type.Fields.FirstOrDefault(fn =>
                fn.Name?.Contains("managedThreadId", StringComparison.InvariantCultureIgnoreCase) == true);
            managedThreadIdField.AssertNotNull();

            ManagedThreadIdFieldName = managedThreadIdField.Name.AssertNotNull();
        }

        return ManagedThreadIdFieldName;
    }
    
    /// <summary>
    /// Gets the name of 'managed thread id' field at runtime because the field name is runtime specific.
    /// </summary>
    private static string GetNameFieldName(ClrObject threadObject)
    {
        Contract.Requires(threadObject.Type != null);

        if (string.IsNullOrEmpty(NameFieldName))
        {
            var nameField = threadObject.Type.Fields.FirstOrDefault(fn =>
                fn.Name?.Contains("name", StringComparison.InvariantCultureIgnoreCase) == true);
            nameField.AssertNotNull();

            NameFieldName = nameField.Name.AssertNotNull();
        }

        return NameFieldName;
    }
}