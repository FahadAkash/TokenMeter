namespace TokenMeter.Core.Models;

/// <summary>
/// Static metadata for a provider — display labels, CLI aliases, dashboard links, etc.
/// </summary>
public sealed record ProviderMetadata
{
    public required UsageProvider Id { get; init; }
    public required string DisplayName { get; init; }
    public required string SessionLabel { get; init; }
    public required string WeeklyLabel { get; init; }
    public string? OpusLabel { get; init; }
    public bool SupportsOpus { get; init; }
    public bool SupportsCredits { get; init; }
    public string CreditsHint { get; init; } = string.Empty;
    public required string ToggleTitle { get; init; }
    public required string CliName { get; init; }
    public bool DefaultEnabled { get; init; }
    public bool IsPrimaryProvider { get; init; }
    public bool UsesAccountFallback { get; init; }
    public string? DashboardUrl { get; init; }
    public string? SubscriptionDashboardUrl { get; init; }

    /// <summary>Statuspage.io base URL for incident polling.</summary>
    public string? StatusPageUrl { get; init; }

    /// <summary>Browser-only status link (no API polling).</summary>
    public string? StatusLinkUrl { get; init; }

    /// <summary>Google Workspace product ID for status polling.</summary>
    public string? StatusWorkspaceProductId { get; init; }
}
