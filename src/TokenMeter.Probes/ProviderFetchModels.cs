using System.Net.Http;
using TokenMeter.Core.Models;

namespace TokenMeter.Probes;

/// <summary>
/// How the probe acquired the data.
/// </summary>
public enum ProviderFetchKind
{
    Cli,
    Web,
    OAuth,
    ApiToken,
    LocalProbe,
    WebDashboard,
}

/// <summary>
/// Runtime in which the probe is executing.
/// </summary>
public enum ProviderRuntime
{
    App,
    Cli,
}

/// <summary>
/// Source mode preference for fetching provider data.
/// </summary>
public enum ProviderSourceMode
{
    Auto,
    Web,
    Cli,
    OAuth,
    Api,
}

/// <summary>
/// Context passed into every probe fetch strategy.
/// </summary>
public sealed record ProviderFetchContext
{
    public required ProviderRuntime Runtime { get; init; }
    public ProviderSourceMode SourceMode { get; init; } = ProviderSourceMode.Auto;
    public bool IncludeCredits { get; init; }
    public TimeSpan WebTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public bool Verbose { get; init; }
    public IReadOnlyDictionary<string, string> Environment { get; init; }
        = new Dictionary<string, string>();
    public HttpClient? HttpClient { get; init; }
    public string? ApiToken { get; init; }
}

/// <summary>
/// The result of a successful provider probe fetch.
/// </summary>
public sealed record ProviderFetchResult
{
    public required UsageSnapshot Usage { get; init; }
    public CreditsSnapshot? Credits { get; init; }
    public OpenAIDashboardSnapshot? Dashboard { get; init; }
    public required string SourceLabel { get; init; }
    public required string StrategyId { get; init; }
    public required ProviderFetchKind StrategyKind { get; init; }
}

/// <summary>
/// Records a single attempt at a fetch strategy (used for debugging/telemetry).
/// </summary>
public sealed record ProviderFetchAttempt
{
    public required string StrategyId { get; init; }
    public required ProviderFetchKind Kind { get; init; }
    public bool WasAvailable { get; init; }
    public string? ErrorDescription { get; init; }
}

/// <summary>
/// Outcome of a full pipeline execution — success or failure, plus all attempts.
/// </summary>
public sealed record ProviderFetchOutcome
{
    public bool IsSuccess { get; init; }
    public ProviderFetchResult? Result { get; init; }
    public Exception? Error { get; init; }
    public IReadOnlyList<ProviderFetchAttempt> Attempts { get; init; } = [];
}

/// <summary>
/// Error thrown when no fetch strategy is available/applicable for a provider.
/// </summary>
public sealed class NoAvailableStrategyException(UsageProvider provider)
    : Exception($"No available fetch strategy for {provider}.")
{
    public UsageProvider Provider { get; } = provider;
}
