using TokenMeter.Core.Models;

namespace TokenMeter.Probes;

/// <summary>
/// Branding information for a provider (color, icon key).
/// </summary>
public sealed record ProviderBranding
{
    public required string ColorHex { get; init; }
    public required IconStyle Icon { get; init; }
}

/// <summary>
/// CLI configuration for a provider.
/// </summary>
public sealed record ProviderCliConfig
{
    public required string Name { get; init; }
    public IReadOnlyList<string> Aliases { get; init; } = [];
}

/// <summary>
/// Api configuration for generic HTTP probes.
/// </summary>
public sealed record ProviderApiConfig
{
    public required bool IsSupported { get; init; }
    public string? Endpoint { get; init; }
    public string? AuthHeaderPrefix { get; init; } = "Bearer";

    // JSON paths for extraction
    public string? PlanJsonPath { get; init; }
    public string? InputTokensJsonPath { get; init; }
    public string? OutputTokensJsonPath { get; init; }
    public string? CostUsdJsonPath { get; init; }
}

/// <summary>
/// Token-cost feature configuration.
/// </summary>
public sealed record ProviderTokenCostConfig
{
    public bool SupportsTokenCost { get; init; }
    public string NoDataMessage { get; init; } = "No token cost data available.";
}

/// <summary>
/// Full descriptor for a provider — combines metadata, branding, fetch plan, and CLI config.
/// Mirrors the Swift <c>ProviderDescriptor</c>.
/// </summary>
public sealed record ProviderDescriptor
{
    public required UsageProvider Id { get; init; }
    public required ProviderMetadata Metadata { get; init; }
    public required ProviderBranding Branding { get; init; }
    public required ProviderTokenCostConfig TokenCost { get; init; }
    public required ProviderCliConfig Cli { get; init; }
    public ProviderApiConfig Api { get; init; } = new ProviderApiConfig { IsSupported = false };
}

/// <summary>
/// Thread-safe registry of all known provider descriptors.
/// </summary>
public static class ProviderDescriptorRegistry
{
    private static readonly object Lock = new();
    private static readonly List<ProviderDescriptor> Ordered = [];
    private static readonly Dictionary<UsageProvider, ProviderDescriptor> ById = [];
    private static readonly Dictionary<string, UsageProvider> CliMap = new(StringComparer.OrdinalIgnoreCase);

    static ProviderDescriptorRegistry()
    {
        // Register explicit known providers with specific branding
        Register(new ProviderDescriptor
        {
            Id = UsageProvider.Claude,
            Metadata = new ProviderMetadata
            {
                Id = UsageProvider.Claude,
                DisplayName = "Anthropic Claude",
                SessionLabel = "Session",
                WeeklyLabel = "Weekly",
                ToggleTitle = "Show Claude usage",
                CliName = "claude",
            },
            Branding = new ProviderBranding { ColorHex = "#C6A0F6", Icon = IconStyle.Claude },
            TokenCost = new ProviderTokenCostConfig { SupportsTokenCost = true },
            Cli = new ProviderCliConfig { Name = "claude" }
        });

        Register(new ProviderDescriptor
        {
            Id = UsageProvider.ChatGPT,
            Metadata = new ProviderMetadata
            {
                Id = UsageProvider.ChatGPT,
                DisplayName = "OpenAI / ChatGPT",
                SessionLabel = "Session",
                WeeklyLabel = "Weekly",
                ToggleTitle = "Show ChatGPT usage",
                CliName = "chatgpt",
            },
            Branding = new ProviderBranding { ColorHex = "#A6DA95", Icon = IconStyle.ChatGPT },
            TokenCost = new ProviderTokenCostConfig { SupportsTokenCost = true },
            Cli = new ProviderCliConfig { Name = "chatgpt", Aliases = ["openai"] }
        });

        Register(new ProviderDescriptor
        {
            Id = UsageProvider.Cursor,
            Metadata = new ProviderMetadata
            {
                Id = UsageProvider.Cursor,
                DisplayName = "Cursor",
                SessionLabel = "Session",
                WeeklyLabel = "Weekly",
                ToggleTitle = "Show Cursor usage",
                CliName = "cursor",
            },
            Branding = new ProviderBranding { ColorHex = "#8AADF4", Icon = IconStyle.Cursor },
            TokenCost = new ProviderTokenCostConfig { SupportsTokenCost = true },
            Cli = new ProviderCliConfig { Name = "cursor" }
        });

        Register(new ProviderDescriptor
        {
            Id = UsageProvider.Copilot,
            Metadata = new ProviderMetadata
            {
                Id = UsageProvider.Copilot,
                DisplayName = "GitHub Copilot",
                SessionLabel = "Session",
                WeeklyLabel = "Weekly",
                ToggleTitle = "Show Copilot usage",
                CliName = "copilot",
            },
            Branding = new ProviderBranding { ColorHex = "#F5A191", Icon = IconStyle.Copilot },
            TokenCost = new ProviderTokenCostConfig { SupportsTokenCost = true },
            Cli = new ProviderCliConfig { Name = "copilot", Aliases = ["gh", "github"] }
        });

        // Auto-register remaining providers with generic fallbacks
        foreach (UsageProvider p in Enum.GetValues<UsageProvider>())
        {
            if (!ById.ContainsKey(p))
            {
                // Map enum to a valid icon style if possible, else fallback to Combined
                IconStyle iconStyle = Enum.TryParse<IconStyle>(p.ToString(), out var parsedIcon) ? parsedIcon : IconStyle.Combined;

                // Configure standard generic API fallback attributes for generic AI providers
                var endpoint = p switch
                {
                    UsageProvider.OpenRouter => "https://openrouter.ai/api/v1/auth/key",
                    UsageProvider.Gemini => "https://generativelanguage.googleapis.com/v1beta/models?key=",
                    UsageProvider.Ollama => "http://localhost:11434/api/tags",
                    UsageProvider.Synthetic => "https://api.synthetic.ai/v1/usage",
                    UsageProvider.MiniMax => "https://api.minimax.chat/v1/user/info",
                    _ => null
                };

                // By default, assume standard OpenAI / OpenRouter style usage packet for others 
                // Alternatively, can leave empty and depend strictly on URL existence.
                var planPath = p == UsageProvider.OpenRouter ? "data.label" : "plan";
                var costUsdPath = p == UsageProvider.OpenRouter ? "data.usage" : "usage.total_cost";

                Register(new ProviderDescriptor
                {
                    Id = p,
                    Metadata = new ProviderMetadata
                    {
                        Id = p,
                        DisplayName = p.ToString(),
                        SessionLabel = "Session",
                        WeeklyLabel = "Weekly",
                        ToggleTitle = $"Show {p} usage",
                        CliName = p.ToString().ToLowerInvariant(),
                    },
                    Branding = new ProviderBranding { ColorHex = "#A5ADCB", Icon = iconStyle },
                    TokenCost = new ProviderTokenCostConfig { SupportsTokenCost = false },
                    Cli = new ProviderCliConfig { Name = p.ToString().ToLowerInvariant() },
                    Api = new ProviderApiConfig
                    {
                        IsSupported = endpoint != null,
                        Endpoint = endpoint,
                        AuthHeaderPrefix = "Bearer",
                        PlanJsonPath = planPath,
                        InputTokensJsonPath = "usage.prompt_tokens",
                        OutputTokensJsonPath = "usage.completion_tokens",
                        CostUsdJsonPath = costUsdPath
                    }
                });
            }
        }
    }

    /// <summary>Register or overwrite a provider descriptor.</summary>
    public static void Register(ProviderDescriptor descriptor)
    {
        lock (Lock)
        {
            // Diagnostic: log registrations (temporary)
            try
            {
                var a = descriptor.Cli.Aliases ?? Array.Empty<string>();
                Console.WriteLine($"[Register] Id={descriptor.Id} Name={descriptor.Cli.Name} Aliases={string.Join(',', a)}");
            }
            catch { }

            // Remove any existing ordered entry so the new descriptor replaces it and appears last
            if (ById.TryGetValue(descriptor.Id, out var previous))
            {
                // Remove previous CLI mappings for this provider
                try
                {
                    CliMap.Remove(previous.Cli.Name);
                    foreach (var a in previous.Cli.Aliases)
                        CliMap.Remove(a);
                }
                catch { }
            }

            Ordered.RemoveAll(d => d.Id == descriptor.Id);
            Ordered.Add(descriptor);
            ById[descriptor.Id] = descriptor;

            // Update CLI mappings for the new descriptor
            try
            {
                CliMap[descriptor.Cli.Name] = descriptor.Id;
                var aliases = descriptor.Cli.Aliases ?? Array.Empty<string>();
                foreach (var a in aliases)
                    CliMap[a] = descriptor.Id;
            }
            catch { }

            // Diagnostic: if tests manipulate Claude, print its CLI aliases (temporary)
            if (descriptor.Id == UsageProvider.Claude)
            {
                try
                {
                    var aliases = descriptor.Cli.Aliases ?? Array.Empty<string>();
                    Console.WriteLine($"[ProviderDescriptorRegistry] Registered {descriptor.Id} CLI='{descriptor.Cli.Name}' Aliases='{string.Join(',', aliases)}' OrderedCount={Ordered.Count}");
                }
                catch { /* ignore diagnostics failures */ }
            }
        }
    }

    /// <summary>All registered descriptors in registration order.</summary>
    public static IReadOnlyList<ProviderDescriptor> All
    {
        get { lock (Lock) return Ordered.ToList(); }
    }

    /// <summary>Look up a descriptor by provider ID.</summary>
    public static ProviderDescriptor Get(UsageProvider id)
    {
        lock (Lock)
        {
            if (ById.TryGetValue(id, out var d)) return d;
            throw new KeyNotFoundException($"Missing ProviderDescriptor for {id}.");
        }
    }

    /// <summary>CLI name → provider mapping (includes aliases).</summary>
    public static IReadOnlyDictionary<string, UsageProvider> CliNameMap
    {
        get
        {
            lock (Lock)
            {
                // Return a copy of the maintained CliMap so callers can't mutate it
                return new Dictionary<string, UsageProvider>(CliMap, StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}
