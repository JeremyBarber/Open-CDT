using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;

namespace CDT_Benchmarks;

internal static class Program
{
    private static void Main(string[] args)
    {
        IConfig config = ManualConfig.CreateEmpty()
            .AddLogger(ConsoleLogger.Default)
            .AddColumnProvider(DefaultColumnProviders.Instance)
            .AddExporter(MarkdownExporter.GitHub)
            .AddDiagnoser(new MemoryDiagnoser(new MemoryDiagnoserConfig(displayGenColumns: true)));

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
    }
}