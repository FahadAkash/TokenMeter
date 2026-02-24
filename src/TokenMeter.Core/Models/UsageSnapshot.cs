namespace TokenMeter.Core.Models;

/// <summary>
/// Represents a rate limit window (e.g., 5-hour session, 7-day weekly).
/// Stores real usage percentage and reset timing from provider APIs.
/// </summary>
public sealed record RateWindow
{
    /// <summary>Percentage of the window that has been used (0-100).</summary>
    public required double UsedPercent { get; init; }

    /// <summary>When the window resets (if known).</summary>
    public DateTimeOffset? ResetsAt { get; init; }

    /// <summary>Calculate remaining percentage.</summary>
    public double RemainingPercent => Math.Max(0, 100.0 - UsedPercent);

    /// <summary>Check if the window is exhausted (&gt;= 100%).</summary>
    public bool IsExhausted => UsedPercent >= 100.0;
}

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


