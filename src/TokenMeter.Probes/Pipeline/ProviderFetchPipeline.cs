using TokenMeter.Core.Models;

namespace TokenMeter.Probes.Pipeline;

/// <summary>
/// Executes an ordered list of <see cref="IProviderFetchStrategy"/> instances,
/// falling through on failure until one succeeds.
/// Mirrors the Swift <c>ProviderFetchPipeline</c>.
/// </summary>
public sealed class ProviderFetchPipeline
{
    private readonly Func<ProviderFetchContext, Task<IReadOnlyList<IProviderFetchStrategy>>> _resolveStrategies;

    public ProviderFetchPipeline(
        Func<ProviderFetchContext, Task<IReadOnlyList<IProviderFetchStrategy>>> resolveStrategies)
    {
        _resolveStrategies = resolveStrategies;
    }

    public async Task<ProviderFetchOutcome> FetchAsync(
        ProviderFetchContext context,
        UsageProvider provider,
        CancellationToken ct = default)
    {
        var strategies = await _resolveStrategies(context);
        var attempts = new List<ProviderFetchAttempt>(strategies.Count);

        foreach (var strategy in strategies)
        {
            ct.ThrowIfCancellationRequested();

            var available = await strategy.IsAvailableAsync(context, ct);
            if (!available)
            {
                attempts.Add(new ProviderFetchAttempt
                {
                    StrategyId = strategy.Id,
                    Kind = strategy.Kind,
                    WasAvailable = false,
                });
                continue;
            }

            try
            {
                var result = await strategy.FetchAsync(context, ct);
                attempts.Add(new ProviderFetchAttempt
                {
                    StrategyId = strategy.Id,
                    Kind = strategy.Kind,
                    WasAvailable = true,
                });

                return new ProviderFetchOutcome
                {
                    IsSuccess = true,
                    Result = result,
                    Attempts = attempts,
                };
            }
            catch (Exception ex)
            {
                attempts.Add(new ProviderFetchAttempt
                {
                    StrategyId = strategy.Id,
                    Kind = strategy.Kind,
                    WasAvailable = true,
                    ErrorDescription = ex.Message,
                });

                if (strategy.ShouldFallback(ex, context))
                    continue;

                return new ProviderFetchOutcome
                {
                    IsSuccess = false,
                    Error = ex,
                    Attempts = attempts,
                };
            }
        }

        return new ProviderFetchOutcome
        {
            IsSuccess = false,
            Error = new NoAvailableStrategyException(provider),
            Attempts = attempts,
        };
    }
}
