using System.Runtime.CompilerServices;
using System.Threading;
using Xunit;

[assembly: CollectionBehavior(MaxParallelThreads = 1)]

namespace ConcurrencyAnalyzers.IntegrationTests;

public class IntegrationTestsInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        // Setting a bit more threads then the default value of Environment.ProcessorCount
        ThreadPool.SetMinThreads(50, 50);
    }
}