using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TokenMeter.Auth;
using TokenMeter.Auth.Runners;
using TokenMeter.Auth.Stores;
using TokenMeter.Auth.OAuth;
using TokenMeter.Core.Data;
using TokenMeter.Core.Models;
using TokenMeter.Probes;
using TokenMeter.Probes.Impl;
using TokenMeter.Probes.Pipeline;
using Microsoft.EntityFrameworkCore;

namespace TokenMeter.CLI;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        try
        {
            switch (args[0].ToLowerInvariant())
            {
                case "usage":
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine("Error: Missing provider name.");
                        PrintHelp();
                        return 1;
                    }

                    if (Enum.TryParse<UsageProvider>(args[1], true, out var provider))
                    {
                        await RunUsageCommand(provider);
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: Invalid provider '{args[1]}'.");
                        ListProviders();
                        return 1;
                    }
                    return 0;

                case "providers":
                    ListProviders();
                    return 0;

                default:
                    Console.Error.WriteLine($"Unknown command: {args[0]}");
                    PrintHelp();
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal Error: {ex.Message}");
            return 1;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            TokenMeter CLI — AI Provider Usage Monitor

            Usage:
              tokenmeter <command> [arguments]

            Commands:
              usage <provider>   Fetch usage for a specific provider (e.g. Copilot, Claude)
              providers          List all supported providers
              --help             Show this help text
            """);
    }

    private static async Task RunUsageCommand(UsageProvider provider)
    {
        var host = CreateHost();
        using var scope = host.Services.CreateScope();
        var sp = scope.ServiceProvider;

        var pipeline = sp.GetRequiredService<ProviderFetchPipeline>();
        var tokenStore = sp.GetRequiredService<ITokenStore>();
        var tokenAccountManager = sp.GetRequiredService<TokenAccountManager>();

        Console.WriteLine($"Fetching usage for {provider}...");

        var token = await tokenAccountManager.GetPrimaryCredentialsAsync(provider);
        var sourceMode = provider == UsageProvider.Copilot ? ProviderSourceMode.Api : ProviderSourceMode.Auto;

        var context = new ProviderFetchContext
        {
            Runtime = ProviderRuntime.Cli,
            SourceMode = sourceMode,
            ApiToken = token ?? "",
            TargetProvider = provider
        };

        var outcome = await pipeline.FetchAsync(context, provider);

        if (outcome.IsSuccess && outcome.Result != null)
        {
            var usage = outcome.Result.Usage;
            Console.WriteLine(new string('─', 40));
            Console.WriteLine($"Provider:  {provider}");
            Console.WriteLine($"Status:    {usage.Status}");
            Console.WriteLine($"Plan:      {usage.PlanName ?? "Active"}");
            Console.WriteLine($"Source:    {outcome.Result.SourceLabel}");

            if (usage.TokenCost != null)
            {
                Console.WriteLine(new string('─', 40));
                Console.WriteLine($"Cost (USD): {usage.TokenCost.SessionCostUsd:C2}");
                Console.WriteLine($"Input Tokens:  {usage.TokenCost.SessionTokens:N0}");
                Console.WriteLine($"Output Tokens: {usage.TokenCost.SessionOutputTokens:N0}");
            }
        }
        else
        {
            Console.Error.WriteLine($"Error: {outcome.Error?.Message ?? "Fetch failed"}");
            foreach (var attempt in outcome.Attempts)
            {
                if (!attempt.WasAvailable) continue;
                Console.Error.WriteLine($"  - {attempt.StrategyId}: {attempt.ErrorDescription}");
            }
        }
    }

    private static IHost CreateHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureLogging(logging => logging.ClearProviders()) // Minimal CLI output
            .ConfigureServices((context, services) =>
            {
                services.AddHttpClient();
                services.AddSingleton<ITokenStore, WindowsCredentialStore>();
                services.AddSingleton<CookieCacheService>();
                services.AddSingleton<TokenAccountManager>();

                // Runners
                services.AddSingleton<ClaudeCookieLoginRunner>();
                services.AddSingleton<ChatGPTCookieLoginRunner>();
                services.AddSingleton<CursorCookieLoginRunner>();
                services.AddSingleton<CopilotOAuthRunner>();
                services.AddSingleton<GitHubDeviceFlow>();

                // Probes
                services.AddTransient<AnthropicApiProbe>();
                services.AddTransient<OpenAIApiProbe>();
                services.AddTransient<CopilotApiProbe>();
                services.AddTransient<CursorApiProbe>();
                services.AddTransient<GenericApiProbe>();
                services.AddTransient<ClaudeCliProbe>();
                services.AddTransient<OpenAICliProbe>();
                services.AddTransient<CopilotCliProbe>();
                services.AddTransient<CursorCliProbe>();

                // Database
                services.AddDbContext<AppDbContext>(options =>
                {
                    var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    var dbDir = Path.Combine(appData, "TokenMeter");
                    var dbPath = Path.Combine(dbDir, "usage.db");
                    options.UseSqlite($"Data Source={dbPath}");
                });

                // Pipeline
                services.AddSingleton<ProviderFetchPipeline>(sp =>
                {
                    return new ProviderFetchPipeline(async ctx =>
                    {
                        var probes = new List<IProviderFetchStrategy>
                        {
                            sp.GetRequiredService<AnthropicApiProbe>(),
                            sp.GetRequiredService<OpenAIApiProbe>(),
                            sp.GetRequiredService<CopilotApiProbe>(),
                            sp.GetRequiredService<CursorApiProbe>(),
                            sp.GetRequiredService<GenericApiProbe>(),
                            sp.GetRequiredService<ClaudeCliProbe>(),
                            sp.GetRequiredService<OpenAICliProbe>(),
                            sp.GetRequiredService<CopilotCliProbe>(),
                            sp.GetRequiredService<CursorCliProbe>()
                        };
                        return await Task.FromResult(probes);
                    });
                });
            })
            .Build();
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
