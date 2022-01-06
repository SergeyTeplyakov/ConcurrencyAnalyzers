using CommandLine;

namespace ConcurrencyAnalyzers;

/// <summary>
/// Base options used by all the supported verbs.
/// </summary>
public class VerbOptions
{
    [Option('n', "discoverThreadNames", Default = false, Group = "Threads", HelpText = "[Expensive] Whether or not to discover thread names.")]
    public bool DiscoverThreadNames { get; set; }

    [Option('t', "threadsOnly", Group = "Threads", HelpText = "[For testing purposes only] Do not analyze the dump after threads data is obtained.")]
    public bool StopAfterThreadNameDiscovery { get; set; }

    [Option('p', "degreeOfParallelism", Group = "Threads", HelpText = "[Expensive] Number of threads used for discovering thread names.")]
    public int? DegreeOfParallelism { get; set; }

    [Option('o', "outputFile", Required = false, HelpText = "The path to the output file where the analysis results will be produced.")]
    public string? OutputFile { get; set; }
}

/// <summary>
/// Command line options for 'dump' verb for processing a dump file.
/// </summary>
[Verb("dump", isDefault: true, HelpText = "Options for processing a dump file.")]
public class ProcessDumpOptions : VerbOptions
{
    [Option('d', "dumpPath", Group = "DumpFile", Required = true, HelpText = "A path to a dump file to analyze.")]
    public string? DumpFile { get; set; }

    [Option('a', "dacPath", Group = "DumpFile", Required = false, HelpText = "A path to a DAC file, useful only when a built-in resolution fails to discover a required DAC file.")]
    public string? DacFilePath { get; set; }

    [Option('c', "disableCaching", Group = "DumpFile", Required = false, HelpText = "Disable caching used by ClrMd by default.")]
    public bool DisableCaching { get; set; }
}

/// <summary>
/// Command line options for 'attach' verb for attaching to a running process.
/// </summary>
[Verb("attach", HelpText = "Options for attaching to a running process.")]
public class AttachOptions : VerbOptions
{
    [Option('i', "processId", Group = "Attach", Required = false, HelpText = "The Id of the process used for live analysis.")]
    public int? ProcessId { get; set; }

    [Option('a', "processName", Group = "Attach", Required = false, HelpText = "The name of the process used for live analysis.")]
    public string? ProcessName { get; set; }
}