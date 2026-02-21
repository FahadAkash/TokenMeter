using System.Text.Json.Serialization;

namespace TokenMeter.Core.Models;

/// <summary>
/// A single credit-usage event from a provider.
/// </summary>
public sealed record CreditEvent
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; } = Guid.NewGuid();

    [JsonPropertyName("date")]
    public DateTimeOffset Date { get; init; }

    [JsonPropertyName("service")]
    public required string Service { get; init; }

    [JsonPropertyName("creditsUsed")]
    public double CreditsUsed { get; init; }
}

/// <summary>
/// Snapshot of remaining credits and recent credit events for a provider.
/// </summary>
public sealed record CreditsSnapshot
{
    public double Remaining { get; init; }
    public IReadOnlyList<CreditEvent> Events { get; init; } = [];
    public DateTimeOffset UpdatedAt { get; init; }
}
