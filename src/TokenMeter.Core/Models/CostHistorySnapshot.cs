using System;

namespace TokenMeter.Core.Models;

/// <summary>
/// Represents an aggregated daily snapshot of total inferred cost across all providers, used for plotting history charts.
/// </summary>
public record CostHistorySnapshot
{
    public int Id { get; init; }

    /// <summary>
    /// The date this snapshot represents (stored without time component).
    /// </summary>
    public DateTime Date { get; init; }

    /// <summary>
    /// The total estimated cost accrued across all models on this date, tracked in USD.
    /// </summary>
    public decimal TotalCostUsd { get; init; }
}
