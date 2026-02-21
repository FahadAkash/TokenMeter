namespace TokenMeter.Probes;

/// <summary>
/// Interface for a single provider fetch strategy.
/// Mirrors the Swift <c>ProviderFetchStrategy</c> protocol.
/// </summary>
public interface IProviderFetchStrategy
{
    /// <summary>Unique identifier for this strategy (e.g. "claude-oauth", "codex-cli").</summary>
    string Id { get; }

    /// <summary>The kind of mechanism this strategy uses.</summary>
    ProviderFetchKind Kind { get; }

    /// <summary>
    /// Check whether this strategy is currently available/applicable.
    /// </summary>
    Task<bool> IsAvailableAsync(ProviderFetchContext context, CancellationToken ct = default);

    /// <summary>
    /// Execute the fetch, returning a result or throwing.
    /// </summary>
    Task<ProviderFetchResult> FetchAsync(ProviderFetchContext context, CancellationToken ct = default);

    /// <summary>
    /// Decide whether the pipeline should fall back to the next strategy after this one fails.
    /// </summary>
    bool ShouldFallback(Exception error, ProviderFetchContext context) => true;
}
