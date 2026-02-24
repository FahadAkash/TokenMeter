using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TokenMeter.Core.Models;

namespace TokenMeter.Probes.Impl;

/// <summary>
/// A universal probe that can query any API endpoint that conforms to standard billing/usage patterns.
/// It uses the ProviderDescriptorRegistry to know what URL to hit and what JSON paths to extract.
/// </summary>
public class GenericApiProbe : IProviderFetchStrategy
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GenericApiProbe> _logger;

    public string Id => "generic-api";
    public ProviderFetchKind Kind => ProviderFetchKind.ApiToken;

    public GenericApiProbe(HttpClient httpClient, ILogger<GenericApiProbe> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<bool> IsAvailableAsync(ProviderFetchContext context, CancellationToken ct = default)
    {
        if (context.TargetProvider == null || string.IsNullOrWhiteSpace(context.ApiToken))
        {
            return Task.FromResult(false);
        }

        var descriptor = ProviderDescriptorRegistry.Get(context.TargetProvider.Value);
        bool canExecute = descriptor.Api.IsSupported && !string.IsNullOrWhiteSpace(descriptor.Api.Endpoint);

        return Task.FromResult(canExecute);
    }

    public async Task<ProviderFetchResult> FetchAsync(ProviderFetchContext context, CancellationToken ct = default)
    {
        if (context.TargetProvider == null)
        {
            throw new InvalidOperationException("GenericApiProbe requires a TargetProvider in the context.");
        }

        var provider = context.TargetProvider.Value;
        var descriptor = ProviderDescriptorRegistry.Get(provider);

        if (!descriptor.Api.IsSupported || string.IsNullOrEmpty(descriptor.Api.Endpoint))
        {
            throw new InvalidOperationException($"No API endpoint configured for {provider}.");
        }

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, descriptor.Api.Endpoint);

            if (string.IsNullOrEmpty(descriptor.Api.AuthHeaderPrefix))
            {
                request.Headers.TryAddWithoutValidation("Authorization", context.ApiToken);
            }
            else
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(descriptor.Api.AuthHeaderPrefix, context.ApiToken);
            }

            var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Generic API failed for {Provider}: {Status} - {Body}", provider, response.StatusCode, errorBody);
                throw new InvalidOperationException($"API request failed with status {response.StatusCode} - {errorBody}");
            }

            var jsonNode = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: ct);
            if (jsonNode == null)
            {
                throw new InvalidOperationException("Empty or invalid JSON response");
            }

            var usage = ParseUsageFromJson(jsonNode, descriptor, provider);
            return new ProviderFetchResult
            {
                Usage = usage,
                SourceLabel = "Generic API",
                StrategyId = Id,
                StrategyKind = Kind
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception querying generic API for {Provider}", provider);
            throw;
        }
    }

    public bool ShouldFallback(Exception error, ProviderFetchContext context) => true;

    private UsageSnapshot ParseUsageFromJson(JsonNode node, ProviderDescriptor descriptor, UsageProvider provider)
    {
        var api = descriptor.Api;

        int inputTokens = 0;
        int outputTokens = 0;
        double costUsd = 0;

        // Input Tokens
        if (!string.IsNullOrEmpty(api.InputTokensJsonPath))
        {
            var val = ExtractDoubleField(node, api.InputTokensJsonPath);
            if (val.HasValue) inputTokens = (int)val.Value;
        }

        // Output Tokens
        if (!string.IsNullOrEmpty(api.OutputTokensJsonPath))
        {
            var val = ExtractDoubleField(node, api.OutputTokensJsonPath);
            if (val.HasValue) outputTokens = (int)val.Value;
        }

        // Total Cost (usually in USD)
        if (!string.IsNullOrEmpty(api.CostUsdJsonPath))
        {
            var val = ExtractDoubleField(node, api.CostUsdJsonPath);
            if (val.HasValue) costUsd = (double)val.Value;
        }

        var snapshot = new UsageSnapshot
        {
            Provider = provider,
            PlanName = ExtractStringField(node, api.PlanJsonPath) ?? "Active",
            TokenCost = new CostUsageTokenSnapshot
            {
                SessionTokens = inputTokens,
                SessionOutputTokens = outputTokens,
                SessionCostUsd = costUsd
            }
        };

        return snapshot;
    }

    private string? ExtractStringField(JsonNode root, string? path)
    {
        var node = NavigatePath(root, path);
        return node?.GetValueKind() == JsonValueKind.String ? node.ToString() : null;
    }

    private double? ExtractDoubleField(JsonNode root, string? path)
    {
        var node = NavigatePath(root, path);
        if (node == null) return null;

        var kind = node.GetValueKind();
        if (kind == JsonValueKind.Number)
        {
            return (double)node;
        }
        else if (kind == JsonValueKind.String && double.TryParse(node.ToString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private JsonNode? NavigatePath(JsonNode root, string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        var segments = path.Split('.');
        JsonNode? current = root;

        foreach (var segment in segments)
        {
            if (current == null) return null;
            if (current is JsonObject obj)
            {
                if (!obj.TryGetPropertyValue(segment, out current))
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        return current;
    }
}
