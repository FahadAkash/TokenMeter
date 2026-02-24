using System.Text.Json.Serialization;

namespace TokenMeter.Core.Models;

// ── Provider cost snapshot (real cost/credit data) ──────────────────────

/// <summary>
/// Real cost or credit usage snapshot from a provider (e.g., Claude Extra Usage).
/// Values are in the provider's native currency (USD, CNY, etc).
/// Used: amount consumed in the current period.
/// Limit: monthly/period limit (if applicable).
/// </summary>
public sealed record ProviderCostSnapshot
{
    public required double Used { get; init; }
    public double? Limit { get; init; }
    public required string CurrencyCode { get; init; }
    public required string Period { get; init; }
    public DateTimeOffset? ResetsAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

// ── Token-cost snapshot (aggregated view) ──────────────────────────────

public sealed record CostUsageTokenSnapshot
{
    public int? SessionTokens { get; init; }
    public int? SessionOutputTokens { get; init; }
    public double? SessionCostUsd { get; init; }
    public int? Last30DaysTokens { get; init; }
    public double? Last30DaysCostUsd { get; init; }
    public ProviderCostSnapshot? ProviderCost { get; init; }
    public IReadOnlyList<DailyEntry> Daily { get; init; } = [];
    public DateTimeOffset UpdatedAt { get; init; }
}

// ── Daily report ───────────────────────────────────────────────────────

public sealed record ModelBreakdown
{
    [JsonPropertyName("modelName")]
    public required string ModelName { get; init; }

    [JsonPropertyName("costUSD")]
    public double? CostUsd { get; init; }
}

public sealed record DailyEntry
{
    [JsonPropertyName("date")]
    public required string Date { get; init; }

    [JsonPropertyName("inputTokens")]
    public int? InputTokens { get; init; }

    [JsonPropertyName("cacheReadTokens")]
    public int? CacheReadTokens { get; init; }

    [JsonPropertyName("cacheCreationTokens")]
    public int? CacheCreationTokens { get; init; }

    [JsonPropertyName("outputTokens")]
    public int? OutputTokens { get; init; }

    [JsonPropertyName("totalTokens")]
    public int? TotalTokens { get; init; }

    [JsonPropertyName("costUSD")]
    public double? CostUsd { get; init; }

    [JsonPropertyName("modelsUsed")]
    public IReadOnlyList<string>? ModelsUsed { get; init; }

    [JsonPropertyName("modelBreakdowns")]
    public IReadOnlyList<ModelBreakdown>? ModelBreakdowns { get; init; }
}

public sealed record DailySummary
{
    [JsonPropertyName("totalInputTokens")]
    public int? TotalInputTokens { get; init; }

    [JsonPropertyName("totalOutputTokens")]
    public int? TotalOutputTokens { get; init; }

    [JsonPropertyName("cacheReadTokens")]
    public int? CacheReadTokens { get; init; }

    [JsonPropertyName("cacheCreationTokens")]
    public int? CacheCreationTokens { get; init; }

    [JsonPropertyName("totalTokens")]
    public int? TotalTokens { get; init; }

    [JsonPropertyName("totalCostUSD")]
    public double? TotalCostUsd { get; init; }
}

public sealed record CostUsageDailyReport
{
    [JsonPropertyName("data")]
    public IReadOnlyList<DailyEntry> Data { get; init; } = [];

    [JsonPropertyName("summary")]
    public DailySummary? Summary { get; init; }
}

// ── Session report ─────────────────────────────────────────────────────

public sealed record SessionEntry
{
    [JsonPropertyName("session")]
    public required string Session { get; init; }

    [JsonPropertyName("inputTokens")]
    public int? InputTokens { get; init; }

    [JsonPropertyName("outputTokens")]
    public int? OutputTokens { get; init; }

    [JsonPropertyName("totalTokens")]
    public int? TotalTokens { get; init; }

    [JsonPropertyName("costUSD")]
    public double? CostUsd { get; init; }

    [JsonPropertyName("lastActivity")]
    public string? LastActivity { get; init; }
}

public sealed record SessionSummary
{
    [JsonPropertyName("totalCostUSD")]
    public double? TotalCostUsd { get; init; }
}

public sealed record CostUsageSessionReport
{
    [JsonPropertyName("data")]
    public IReadOnlyList<SessionEntry> Data { get; init; } = [];

    [JsonPropertyName("summary")]
    public SessionSummary? Summary { get; init; }
}

// ── Monthly report ─────────────────────────────────────────────────────

public sealed record MonthlyEntry
{
    [JsonPropertyName("month")]
    public required string Month { get; init; }

    [JsonPropertyName("totalTokens")]
    public int? TotalTokens { get; init; }

    [JsonPropertyName("costUSD")]
    public double? CostUsd { get; init; }
}

public sealed record MonthlySummary
{
    [JsonPropertyName("totalTokens")]
    public int? TotalTokens { get; init; }

    [JsonPropertyName("totalCostUSD")]
    public double? TotalCostUsd { get; init; }
}

public sealed record CostUsageMonthlyReport
{
    [JsonPropertyName("data")]
    public IReadOnlyList<MonthlyEntry> Data { get; init; } = [];

    [JsonPropertyName("summary")]
    public MonthlySummary? Summary { get; init; }
}
