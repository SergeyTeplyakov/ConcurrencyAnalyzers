using System;
using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;

using ConcurrencyAnalyzers.Rendering;
using ConcurrencyAnalyzers.Utilities;

using Microsoft.Diagnostics.Runtime;
using Microsoft.Extensions.Logging;

namespace ConcurrencyAnalyzers
{
    public class Foo
    {

    }
    internal class Program
    {

        //private static void TestLogging()
        //{
        //    LoggerFactory.Create()

        //}
        static void Main(string[] args)
        {
            //string? s = null;


            //s ??= "12";

            //string s2 = s;

            //Foo foo;
            //if (args == null)
            //{
            //    foo = new Foo();
            //}
            //else
            //{
            //    foo = null;
            //}

            //// A warning! I was expecting only a warning on 'foo = null'
            //Foo bar = foo;




            //Console.WriteLine(foo);
            //Foo? bar;

            //for (int i = 0; i < 1000; i++)
            //{
            //    foo = new Foo();
            //    bar = new Foo();
            //}

            //// tooltip: 'foo' is not null here
            //Console.WriteLine(foo);
            //// tooltip: 'bar' may be null here
            //Console.WriteLine(bar);

            //TestLogging();
            //return;

            if (args.Length == 0)
            {
                // A local testing scenario. Will work if the integration tests ran at least once.
                var dumpFileOptions = new ProcessDumpOptions()
                {
                    DumpFile = @"..\..\..\..\ConcurrencyAnalyzers.IntegrationTests\bin\Debug\net6.0\Dumps\ParallelThreadsIntegrationTests.BlockThreadPoolThreads.dmp",
                    DiscoverThreadNames = true,
                };

                Analyze(dumpFileOptions);
                return;
            }

            var parsedArguments = Parser.Default.ParseArguments<ProcessDumpOptions, AttachOptions>(args);

            if (parsedArguments is Parsed<object> parsed)
            {
                var result = Analyze((VerbOptions)parsed.Value);
                if (!result.Success)
                {
                    Console.WriteLine(result);
                }

                return;
            }

            // Error case.
            Console.WriteLine(HelpText.AutoBuild(parsedArguments));
        }

        

        /// <summary>
        /// Run the analyzer for a given <paramref name="options"/>.
        /// </summary>
        static Result<Unit> Analyze(VerbOptions options)
        {
            // Ok, now we can open a dump (or attach to a running process).
            CacheOptions? cacheOptions = options is ProcessDumpOptions { DisableCaching: true } ? DisabledCacheOptions() : null;

            var (runtime, error) = options switch
            {
                ProcessDumpOptions pdo => ConcurrencyAnalyzer.OpenDump(pdo.DumpFile!, pdo.DacFilePath, cacheOptions),
                AttachOptions ao => ao.ProcessId is { } pid ? ConcurrencyAnalyzer.AttachTo(pid) : ConcurrencyAnalyzer.AttachTo(ao.ProcessName.AssertNotNull()),
                _ => throw new InvalidOperationException($"Unknown options {options.GetType()}"),
            };

            if (error is not null)
            {
                return Result.Error<Unit>(error);
            }

            using (runtime.AssertNotNull())
            {
                var analysisOptions = FromCommandLineOptions(options);

                var analysisResult = ConcurrencyAnalyzer.Analyze(analysisOptions, runtime.Runtime);
                var render = CreateRenderer(options);
                render.Render(analysisResult);

                return Unit.VoidSuccess;
            }

            static AnalysisOptions FromCommandLineOptions(VerbOptions options)
            {
                AnalysisScope scope = AnalysisScope.None;

                if (options.DiscoverThreadNames || options.StopAfterThreadNameDiscovery)
                {
                    scope |= AnalysisScope.Threads;
                }

                if (!options.StopAfterThreadNameDiscovery)
                {
                    scope |= (AnalysisScope.ParallelThreads | AnalysisScope.ThreadPool);
                }

                return new AnalysisOptions(scope, options.DegreeOfParallelism);
            }

            static CacheOptions DisabledCacheOptions() => new CacheOptions()
            {
                //CacheFields = false,
                //CacheMethods = false,
                //CacheTypes = false,
                //MaxDumpCacheSize = 1_000,
                UseOSMemoryFeatures = false
            };

            static TextRenderer CreateRenderer(VerbOptions options)
            {
                var renderers = new List<TextRenderer> { new ConsoleRenderer() };
                string? outputFileName = GetOutputFileName(options);
                if (outputFileName is not null)
                {
                    renderers.Add(FileRenderer.Create(outputFileName));
                }

                return new MultiTargetRenderer(renderers.ToArray());
            }

            static string? GetOutputFileName(VerbOptions options)
            {
                // Can return null in the future if we'll decide to add a flag to disable writing to a file.
                if (options.OutputFile is not null)
                {
                    return options.OutputFile;
                }

                return options switch
                {
                    ProcessDumpOptions pdo => $"{pdo.DumpFile.AssertNotNull()}.txt",
                    AttachOptions ao => ao.ProcessId is { } pid
                        ? $"PID_{pid}.txt"
                        : $"ProcessName_{ao.ProcessName}.txt",
                    _ => throw new InvalidOperationException($"Unknown options {options.GetType()}"),
                };
            }
        }
    }
}
