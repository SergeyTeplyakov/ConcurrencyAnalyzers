using System;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace ConcurrencyAnalyzers.Tests;

public class TypeNameHelperTests
{
    private readonly ITestOutputHelper _helper;

    public TypeNameHelperTests(ITestOutputHelper helper)
    {
        _helper = helper;
    }

    [Theory]
    [InlineData("ManualResetEventSlimOnTheStack.Program+<RunAsync>d__1", "ManualResetEventSlimOnTheStack.Program.RunAsync.StateMachine__1")]
    [InlineData("AsyncReaderWriterLockDeadlock.Program+<Main>d__1.MoveNext", "AsyncReaderWriterLockDeadlock.Program.Main.StateMachine__1.MoveNext")]
    [InlineData("ManualResetEventSlimOnTheStack.Program+<>c+<<RunAsync>b__1_0>d", "ManualResetEventSlimOnTheStack.Program.RunAsync.AnonymousMethod__1_0.StateMachine")]
    [InlineData("ManualResetEventSlimOnTheStack.Program+<>c+<<RunAsync>b__2_1>d", "ManualResetEventSlimOnTheStack.Program.RunAsync.AnonymousMethod__2_1.StateMachine")]
    [InlineData("DumpSources.ParallelForBlocked+<>c__DisplayClass1_0+<<Run>b__0>d.MoveNext()", "DumpSources.ParallelForBlocked.Run.AnonymousMethod__0.StateMachine.MoveNext()")]
    [InlineData("ManualResetEventSlimOnTheStack.Program+<<RunAsync>g__local|1_2>d", "ManualResetEventSlimOnTheStack.Program.RunAsync.local1_2.StateMachine")]
    public void TestAsyncMethodNameSimplification(string input, string expected)
    {
        var prettifiedTypeName = StackFrameSanitizer.PrettifyTypeName(input).ToString();

        TraceResultsAndValidate(input, prettifiedTypeName, expected);
    }

    [Theory]
    [InlineData("System.Threading.Tasks.Parallel+ForEachAsyncState<System.Int32>+<>c", "System.Threading.Tasks.Parallel.ForEachAsyncState<System.Int32>")]
    [InlineData("System.Threading.Tasks.Parallel.ForEachAsyncState<System.Int32>+<>c", "System.Threading.Tasks.Parallel.ForEachAsyncState<System.Int32>")]
    [InlineData("System.Threading.Tasks.Parallel+ForEachAsyncState<System.Int32>", "System.Threading.Tasks.Parallel.ForEachAsyncState<System.Int32>")]
    public void SimplifyTypeName(string input, string expected)
    {
        var prettifiedTypeName = StackFrameSanitizer.PrettifyTypeName(input);

        TraceResultsAndValidate(input, prettifiedTypeName, expected);
    }

    [Theory]
    [InlineData("System.Threading.Tasks.Parallel+<>c__50`1+<<ForEachAsync>b__50_0>d[[System.Int32, System.Private.CoreLib]].MoveNext()",
        "System.Threading.Tasks.Parallel.ForEachAsync.AnonymousMethod__50_0.StateMachine<int>.MoveNext()",
        "System.Threading.Tasks.Parallel.ForEachAsync.AnonymousMethod__50_0.StateMachine<int>",
        "MoveNext",
        "")]
    
    [InlineData("System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncStateMachineBox`1[[System.Threading.Tasks.VoidTaskResult, System.Private.CoreLib],[StartupHook+<ReceiveDeltas>d__3, Microsoft.Extensions.DotNetDeltaApplier]].ExecutionContextCallback(System.Object)",
        "System.Runtime.CompilerServices.AsyncTaskMethodBuilder.AsyncStateMachineBox<System.Threading.Tasks.VoidTaskResult,StartupHook.ReceiveDeltas.StateMachine__3>.ExecutionContextCallback(object)",
        "System.Runtime.CompilerServices.AsyncTaskMethodBuilder.AsyncStateMachineBox<System.Threading.Tasks.VoidTaskResult,StartupHook.ReceiveDeltas.StateMachine__3>",
        "ExecutionContextCallback",
        "object")]
    // A case with more then one generic argument, like FooBar<int, int>
    [InlineData("DumpSources.ParallelForBlocked.<FooBar>g__FooBaz|3_0[[System.Int32, System.Private.CoreLib],[System.Int32, System.Private.CoreLib]]()",
        "DumpSources.ParallelForBlocked.FooBar.FooBaz3_0<int,int>()",
        "DumpSources.ParallelForBlocked.FooBar",
        "FooBaz3_0<int,int>",
        "")]
    // A case with display class
    [InlineData("DumpSources.ParallelForBlocked+<>c__DisplayClass1_0.<Run>b__0(Int32, System.Threading.CancellationToken)",
        "DumpSources.ParallelForBlocked.Run.AnonymousMethod__0(int, System.Threading.CancellationToken)",
        "DumpSources.ParallelForBlocked.Run",
        "AnonymousMethod__0",
        "int, System.Threading.CancellationToken")]
    
    [InlineData("ConcurrencyAnalyzers.IntegrationTests.ParallelThreadsIntegrationTests.<ExceptionOnStack>g__TestCase|2_0(System.Int32, System.Threading.CancellationToken)",
        "ConcurrencyAnalyzers.IntegrationTests.ParallelThreadsIntegrationTests.ExceptionOnStack.TestCase2_0(int, System.Threading.CancellationToken)",
        "ConcurrencyAnalyzers.IntegrationTests.ParallelThreadsIntegrationTests.ExceptionOnStack",
        "TestCase2_0",
        "int, System.Threading.CancellationToken")]

    [InlineData("System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start[[System.Threading.Tasks.Parallel+<>c__50`1+<<ForEachAsync>b__50_0>d[[System.Int32, System.Private.CoreLib]], System.Threading.Tasks.Parallel]](<<ForEachAsync>b__50_0>d<Int32> ByRef)",
        "System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start<System.Threading.Tasks.Parallel.ForEachAsync.AnonymousMethod__50_0.StateMachine<int>>(ref ForEachAsync.AnonymousMethod__50_0.StateMachine<Int32>)",
        "System.Runtime.CompilerServices.AsyncMethodBuilderCore",
        "Start<System.Threading.Tasks.Parallel.ForEachAsync.AnonymousMethod__50_0.StateMachine<int>>",
        "ref ForEachAsync.AnonymousMethod__50_0.StateMachine<Int32>")]
    public void SimplifyRawSignatures(string input, string expectedFull, string expectedType, string expectedMethod, string expectedArgs)
    {
        string prettifiedName = StackFrameSanitizer.RawStackFrameSignatureToDisplayString(input, out var type, out var method, out var args).AssertNotNull();

        TraceResultsAndValidate(input, prettifiedName, expectedFull);

        _helper.WriteLine($"Type: {type}\r\nMethod: {method}\r\nArgs: {args}");

        expectedType.Should().Be(type.ToString());
        expectedMethod.Should().Be(method.ToString());
        expectedArgs.Should().Be(args.ToString());
    }

    [Theory]
    [InlineData("Int32, System.Object", "int, object")]
    public void PrettifyArguments(string input, string expected)
    {
        var prettified = StackFrameSanitizer.PrettifyArguments(input);

        TraceResultsAndValidate(input, prettified, expected);
    }
    
    [Theory]
    [InlineData("System.Threading.Tasks.Parallel+<>c__50<System.Int32>+<<ForEachAsync>b__50_0>d", "System.Threading.Tasks.Parallel.ForEachAsync.AnonymousMethod__50_0.StateMachine")]
    public void PrettifyRawTypeNames(string input, string expected)
    {
        var prettified = StackFrameSanitizer.PrettifyTypeName((ReadOnlySpan<char>)input);

        TraceResultsAndValidate(input, prettified, expected);

        prettified.ToString().Should().Be(expected);
    }


    private void TraceResultsAndValidate(string original, ReadOnlySpan<char> prettified, string expected)
    {
        _helper.WriteLine($"  Original:\r\n{original}");
        _helper.WriteLine($"Prettified:\r\n{prettified}");
        _helper.WriteLine($"  Expected:\r\n{expected}");

        prettified.ToString().Should().Be(expected);
    }

}