using System;

namespace TokenMeter.Core.Models;

/// <summary>
/// Represents a historical usage record for a specific date and provider.
/// Used for persistence in Phase 3.
/// </summary>
public class UsageEntry
{
    public int Id { get; set; }

    public UsageProvider Provider { get; set; }

    /// <summary>
    /// The local date for this usage (ignored time component).
    /// </summary>
    public DateTime Date { get; set; }

    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public double TotalCost { get; set; }

    public string? ModelName { get; set; }

    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}
