using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TokenMeter.Core.Models;

namespace TokenMeter.Probes.Impl;

/// <summary>
/// Base class for all HTTP-based provider probes.
/// Extends IProviderFetchStrategy with common HttpClient infrastructure.
/// </summary>
public abstract class HttpProviderFetchStrategy : IProviderFetchStrategy
{
    private readonly IHttpClientFactory _httpClientFactory;
    protected readonly ILogger Logger;

    protected HttpProviderFetchStrategy(IHttpClientFactory httpClientFactory, ILogger logger)
    {
        _httpClientFactory = httpClientFactory;
        Logger = logger;
    }

    public abstract string Id { get; }
    public abstract ProviderFetchKind Kind { get; }
    public abstract UsageProvider Provider { get; }

    /// <summary>
    /// Checks if the credentials required for this probe exist in the context.
    /// </summary>
    public abstract Task<bool> IsAvailableAsync(ProviderFetchContext context, CancellationToken ct = default);

    /// <summary>
    /// Executes the actual HTTP fetch logic.
    /// </summary>
    public abstract Task<ProviderFetchResult> FetchAsync(ProviderFetchContext context, CancellationToken ct = default);

    public virtual bool ShouldFallback(Exception error, ProviderFetchContext context) => true;

    /// <summary>
    /// Creates an HttpClient configured for this specific provider (using Polly policies).
    /// </summary>
    protected HttpClient CreateClient() => _httpClientFactory.CreateClient(Provider.ToString());

    /// <summary>
    /// Helper to cleanly deserialize JSON from an HTTP response.
    /// </summary>
    protected async Task<T?> DeserializeAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var content = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            Logger.LogWarning("[{Provider}] HTTP {Status}: {Body}", Provider, response.StatusCode, content);
            response.EnsureSuccessStatusCode(); // throw
        }

        return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
}
