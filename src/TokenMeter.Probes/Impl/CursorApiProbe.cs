using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TokenMeter.Core.Models;

namespace TokenMeter.Probes.Impl;

public sealed class CursorApiProbe : HttpProviderFetchStrategy
{
    public override string Id => "cursor_web";
    public override ProviderFetchKind Kind => ProviderFetchKind.WebDashboard;
    public override UsageProvider Provider => UsageProvider.Cursor;

    public CursorApiProbe(IHttpClientFactory httpClientFactory, ILogger<CursorApiProbe> logger)
        : base(httpClientFactory, logger) { }

    public override Task<bool> IsAvailableAsync(ProviderFetchContext context, CancellationToken ct = default)
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(context.ApiToken));
    }

    public override async Task<ProviderFetchResult> FetchAsync(ProviderFetchContext context, CancellationToken ct = default)
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", context.ApiToken);
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        // 1. Get User Info (for email)
        string email = "Unknown";
        try
        {
            var userUrl = "https://cursor.com/api/auth/me";
            using var userResp = await client.GetAsync(userUrl, ct);
            if (userResp.IsSuccessStatusCode)
            {
                var userContent = await userResp.Content.ReadAsStringAsync(ct);
                using var userDoc = JsonDocument.Parse(userContent);
                if (userDoc.RootElement.TryGetProperty("email", out var emailElem))
                    email = emailElem.GetString() ?? "Unknown";
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to fetch Cursor user info.");
        }

        // 2. Get Usage Summary
        var usageUrl = "https://cursor.com/api/usage-summary";
        using var usageResp = await client.GetAsync(usageUrl, ct);
        if (!usageResp.IsSuccessStatusCode)
        {
            throw new Exception($"Cursor API returned {usageResp.StatusCode}");
        }

        var usageContent = await usageResp.Content.ReadAsStringAsync(ct);
        using var usageDoc = JsonDocument.Parse(usageContent);
        var root = usageDoc.RootElement;

        double usedPercent = 0;
        string planName = "Cursor";

        if (root.TryGetProperty("membershipType", out var memberElem))
        {
            var type = memberElem.GetString() ?? "";
            planName = $"Cursor {char.ToUpper(type[0])}{type.Substring(1)}";
        }

        if (root.TryGetProperty("individualUsage", out var individual))
        {
            if (individual.TryGetProperty("plan", out var plan))
            {
                if (plan.TryGetProperty("totalPercentUsed", out var percentElem))
                {
                    usedPercent = percentElem.GetDouble() * 100.0;
                }
                else if (plan.TryGetProperty("used", out var usedElem) && plan.TryGetProperty("limit", out var limitElem))
                {
                    var used = usedElem.GetDouble();
                    var limit = limitElem.GetDouble();
                    if (limit > 0) usedPercent = (used / limit) * 100.0;
                }
            }
        }

        var snapshot = new UsageSnapshot
        {
            Provider = Provider,
            CapturedAt = DateTimeOffset.UtcNow,
            Status = UsageStatus.Ok,
            PlanName = planName,
            TokenCost = new CostUsageTokenSnapshot
            {
                SessionCostUsd = usedPercent,
                SessionTokens = (int)(usedPercent * 1000),
                Daily = []
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
}
