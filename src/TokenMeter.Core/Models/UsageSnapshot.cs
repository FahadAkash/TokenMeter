namespace TokenMeter.Core.Models;

/// <summary>
/// The status of a provider probe result.
/// </summary>
public enum UsageStatus
{
    Unknown,
    Ok,
    Warning,
    Error,
    RateLimited,
    Expired,
    Disabled,
}

/// <summary>
/// Snapshot of a provider's current usage, returned by a probe.
/// </summary>
public sealed record UsageSnapshot
{
    public required UsageProvider Provider { get; init; }
    public UsageStatus Status { get; init; } = UsageStatus.Unknown;

    /// <summary>Human-readable status message (e.g. "3 of 50 requests used").</summary>
    public string? StatusMessage { get; init; }

    /// <summary>Session-level usage count (requests or tokens).</summary>
    public int? SessionUsage { get; init; }

    /// <summary>Session-level usage limit.</summary>
    public int? SessionLimit { get; init; }

    /// <summary>Weekly / rolling-period usage count.</summary>
    public int? PeriodUsage { get; init; }

    /// <summary>Weekly / rolling-period usage limit.</summary>
    public int? PeriodLimit { get; init; }

    /// <summary>Time until the session/period resets.</summary>
    public TimeSpan? ResetIn { get; init; }

    /// <summary>Plan or tier name (e.g. "Pro", "Team", "Free").</summary>
    public string? PlanName { get; init; }

    /// <summary>Active model name (e.g. "claude-sonnet-4-20250514").</summary>
    public string? ActiveModel { get; init; }

    /// <summary>Token cost data, if available.</summary>
    public CostUsageTokenSnapshot? TokenCost { get; init; }

    /// <summary>When this snapshot was captured.</summary>
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// OpenAI dashboard-specific snapshot (billing, organisation info).
/// </summary>
public sealed record OpenAIDashboardSnapshot
{
    public double? CreditBalance { get; init; }
    public double? CreditLimit { get; init; }
    public string? OrganisationName { get; init; }
    public DateTimeOffset? BillingCycleEnd { get; init; }
}

/// <summary>
/// Aggregate cost snapshot for a single provider.
/// </summary>
public sealed record ProviderCostSnapshot
{
    public required UsageProvider Provider { get; init; }
    public double TotalCostUsd { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
