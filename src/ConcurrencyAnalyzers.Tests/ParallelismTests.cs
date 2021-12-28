using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ConcurrencyAnalyzers.Tests;

public class ParallelismTests
{
    private readonly ITestOutputHelper _helper;

    public ParallelismTests(ITestOutputHelper helper)
    {
        _helper = helper;
    }

    [Fact]
    public void Test()
    {
        var input = Enumerable.Range(1, 100)
            .ToList()
            .AsParallel()
            .WithDegreeOfParallelism(Environment.ProcessorCount);
    }
}