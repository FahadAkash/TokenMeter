using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TokenMeter.Core.Models;

namespace TokenMeter.Probes.Impl;

public sealed class AnthropicApiProbe : HttpProviderFetchStrategy
{
    public override string Id => "anthropic_web";
    public override ProviderFetchKind Kind => ProviderFetchKind.WebDashboard;
    public override UsageProvider Provider => UsageProvider.Claude;

    public AnthropicApiProbe(IHttpClientFactory httpClientFactory, ILogger<AnthropicApiProbe> logger)
        : base(httpClientFactory, logger) { }

    public override Task<bool> IsAvailableAsync(ProviderFetchContext context, CancellationToken ct = default)
    {
        // Require the extracted cookie string stored in ApiToken to run
        return Task.FromResult(!string.IsNullOrWhiteSpace(context.ApiToken));
    }

    public override async Task<ProviderFetchResult> FetchAsync(ProviderFetchContext context, CancellationToken ct = default)
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", context.ApiToken);
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        // 1. Get Organization ID
        var orgUrl = "https://claude.ai/api/organizations";
        using var orgResp = await client.GetAsync(orgUrl, ct);
        if (!orgResp.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to get organizations: {orgResp.StatusCode}");
        }

        var orgContent = await orgResp.Content.ReadAsStringAsync(ct);
        using var orgDoc = JsonDocument.Parse(orgContent);
        var rootArray = orgDoc.RootElement;

        if (rootArray.ValueKind != JsonValueKind.Array || rootArray.GetArrayLength() == 0)
        {
            throw new Exception("No organizations found for this Claude account.");
        }

        var orgId = rootArray[0].GetProperty("uuid").GetString();

        // 2. Get Usage Data
        var usageUrl = $"https://claude.ai/api/organizations/{orgId}/usage";
        using var usageResp = await client.GetAsync(usageUrl, ct);
        if (!usageResp.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to get usage: {usageResp.StatusCode}");
        }

        var usageContent = await usageResp.Content.ReadAsStringAsync(ct);
        using var usageDoc = JsonDocument.Parse(usageContent);
        var usageRoot = usageDoc.RootElement;

        double usedPercent = 0;
        DateTimeOffset? resetsAt = null;

        if (usageRoot.TryGetProperty("five_hour", out var fiveHour) && fiveHour.ValueKind != JsonValueKind.Null)
        {
            if (fiveHour.TryGetProperty("utilization", out var util))
                usedPercent = util.GetDouble() * 100.0;

            if (fiveHour.TryGetProperty("resets_at", out var resetsAtElement) && resetsAtElement.ValueKind != JsonValueKind.Null)
            {
                if (DateTimeOffset.TryParse(resetsAtElement.GetString(), out var parsed))
                    resetsAt = parsed;
            }
        }

        // 3. Get Account Info
        string accountTier = "Claude";
        string email = "Unknown";
        try
        {
            var accUrl = "https://claude.ai/api/account";
            using var accResp = await client.GetAsync(accUrl, ct);
            if (accResp.IsSuccessStatusCode)
            {
                var accContent = await accResp.Content.ReadAsStringAsync(ct);
                using var accDoc = JsonDocument.Parse(accContent);
                var accRoot = accDoc.RootElement;

                if (accRoot.TryGetProperty("rate_limit_tier", out var tierElem))
                    accountTier = FormatTier(tierElem.GetString() ?? "");

                if (accRoot.TryGetProperty("email_address", out var emailElem))
                    email = emailElem.GetString() ?? "";
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to parse account / tier info.");
        }

        var snapshot = new UsageSnapshot
        {
            Provider = Provider,
            CapturedAt = DateTimeOffset.UtcNow,
            Status = UsageStatus.Ok,
            PlanName = accountTier,
            TokenCost = new CostUsageTokenSnapshot
            {
                SessionCostUsd = usedPercent, // Using this generically for the UI percentage
                SessionTokens = (int)(usedPercent * 1000), // Artificial just so chart renders something
                Daily = [] // No daily available in GraphQL unless we parse messages
            }
        };

        return new ProviderFetchResult
        {
            Usage = snapshot,
            SourceLabel = $"Web ({email})",
            StrategyId = Id,
            StrategyKind = Kind
        };
    }

    private static string FormatTier(string tier)
    {
        return tier.ToLowerInvariant() switch
        {
            "free" => "Claude Free",
            "pro" or "claude_pro" => "Claude Pro",
            "max" or "claude_max_5" => "Claude Max",
            "team" => "Claude Team",
            _ => $"Claude ({tier})"
        };
    }
}
