using TokenMeter.Core.Models;

namespace TokenMeter.CLI;

/// <summary>
/// TokenMeter CLI — command-line access to provider usage data.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "providers":
                ListProviders();
                return 0;

            case "version":
                Console.WriteLine("TokenMeter CLI v0.1.0");
                return 0;

            default:
                Console.Error.WriteLine($"Unknown command: {args[0]}");
                PrintHelp();
                return 1;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            TokenMeter CLI — AI Provider Usage Monitor

            Usage:
              tokenmeter <command> [options]

            Commands:
              providers   List all supported providers
              version     Show version information
              --help      Show this help text
            """);
    }

    private static void ListProviders()
    {
        Console.WriteLine("Supported Providers:");
        Console.WriteLine(new string('─', 30));
        foreach (var p in Enum.GetValues<UsageProvider>())
        {
            Console.WriteLine($"  • {p}");
        }
        Console.WriteLine();
        Console.WriteLine($"Total: {Enum.GetValues<UsageProvider>().Length} providers");
    }
}
