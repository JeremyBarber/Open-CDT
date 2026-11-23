using Open_CDT;
using Open_CDT_CLI.ConsoleWriter;

using System.Text;

namespace Open_CDT_CLI;

internal static class Program
{
    internal static int Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch
        {
        }

        switch (args)
        {
            case ["simulate", var configPath]:
                using (var renderer = new ConsoleRenderer())
                {
                    return EntryPoint.Run(configPath, renderer.OnProgress);
                }

            case ["plot", var directory]:
                using (var renderer = new ConsoleRenderer())
                {
                    return EntryPoint.Plot(directory, renderer.OnProgress);
                }

            default:
                Console.WriteLine("Usage:");
                Console.WriteLine("  open-cdt simulate <config-file.cdt>");
                Console.WriteLine("  open-cdt plot <output-directory>");
                return 1;
        }
    }
}