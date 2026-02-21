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
}

/// <summary>
/// Thread-safe registry of all known provider descriptors.
/// Mirrors the Swift <c>ProviderDescriptorRegistry</c>.
/// </summary>
public static class ProviderDescriptorRegistry
{
    private static readonly object Lock = new();
    private static readonly List<ProviderDescriptor> Ordered = [];
    private static readonly Dictionary<UsageProvider, ProviderDescriptor> ById = [];

    /// <summary>Register or overwrite a provider descriptor.</summary>
    public static void Register(ProviderDescriptor descriptor)
    {
        lock (Lock)
        {
            if (!ById.ContainsKey(descriptor.Id))
                Ordered.Add(descriptor);
            ById[descriptor.Id] = descriptor;
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
                var map = new Dictionary<string, UsageProvider>(StringComparer.OrdinalIgnoreCase);
                foreach (var d in Ordered)
                {
                    map[d.Cli.Name] = d.Id;
                    foreach (var alias in d.Cli.Aliases)
                        map[alias] = d.Id;
                }
                return map;
            }
        }
    }
}
