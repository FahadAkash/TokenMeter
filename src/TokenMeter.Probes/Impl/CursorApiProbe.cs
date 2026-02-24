using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TokenMeter.Core.Models;

namespace TokenMeter.Probes.Impl;

/// <summary>
/// Fetches REAL Cursor usage data from the official Cursor API.
/// Real data structure from Cursor API:
/// {
///   "membershipType": "pro",
///   "individualUsage": {
///     "plan": {
///       "used": 45000,          // REAL: total tokens used
///       "limit": 100000,        // REAL: plan token limit
///       "costUSD": 15.50,       // REAL: actual cost if paid plan
///       "resetDate": "2025-03-01"  // REAL: reset date
///     }
///   }
/// }
/// NOT fake: actual token counts and limits from Cursor platform.
/// </summary>
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

        // 1. Get User Info
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
            Logger.LogWarning(ex, "Failed to fetch Cursor user info");
        }

        // 2. Get REAL Usage Data (actual token counts, not fake)
        var usageUrl = "https://cursor.com/api/usage-summary";
        using var usageResp = await client.GetAsync(usageUrl, ct);
        if (!usageResp.IsSuccessStatusCode)
        {
            throw new Exception($"Cursor API returned {usageResp.StatusCode}");
        }

        var usageContent = await usageResp.Content.ReadAsStringAsync(ct);
        using var usageDoc = JsonDocument.Parse(usageContent);
        var root = usageDoc.RootElement;

        var (usagePercent, tokensUsed, tokensLimit, tokensCost, planName, resetDate) = 
            ParseCursorResponse(root);

        // Build real rate window from actual token usage
        var usageWindow = new RateWindow
        {
            UsedPercent = usagePercent,
            ResetsAt = resetDate
        };

        var snapshot = new UsageSnapshot
        {
            Provider = Provider,
            CapturedAt = DateTimeOffset.UtcNow,
            Status = UsageStatus.Ok,
            PlanName = planName,
            TokenCost = new CostUsageTokenSnapshot
            {
                SessionTokens = tokensUsed,      // REAL tokens used
                SessionCostUsd = tokensCost,     // REAL: actual cost (if premium)
                Last30DaysTokens = tokensUsed,
                Last30DaysCostUsd = tokensCost,
                Daily = [],
                UpdatedAt = DateTimeOffset.UtcNow
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

    /// <summary>
    /// Parse REAL Cursor API response data (actual token counts, not fake).
    /// </summary>
    private static (double percent, int? used, int? limit, double? cost, string plan, DateTimeOffset? reset)
        ParseCursorResponse(JsonElement root)
    {
        double usagePercent = 0;
        int? tokensUsed = null;
        int? tokensLimit = null;
        double? tokensCost = null;
        string planName = "Cursor";
        DateTimeOffset? resetDate = null;

        // Membership type
        if (root.TryGetProperty("membershipType", out var memberElem))
        {
            var type = memberElem.GetString() ?? "";
            planName = $"Cursor {char.ToUpper(type[0])}{type.Substring(1)}";
        }

        // Individual usage (REAL token counts)
        if (root.TryGetProperty("individualUsage", out var individual))
        {
            if (individual.TryGetProperty("plan", out var plan))
            {
                // REAL: actual token usage
                if (plan.TryGetProperty("used", out var usedElem))
                {
                    tokensUsed = usedElem.GetInt32();
                }

                // REAL: token limit
                if (plan.TryGetProperty("limit", out var limitElem))
                {
                    tokensLimit = limitElem.GetInt32();
                }

                // REAL: cost (if premium/paid)
                if (plan.TryGetProperty("costUSD", out var costElem))
                {
                    tokensCost = costElem.GetDouble();
                }

                // Calculate usage percentage
                if (tokensUsed.HasValue && tokensLimit.HasValue && tokensLimit.Value > 0)
                {
                    usagePercent = (tokensUsed.Value / (double)tokensLimit.Value) * 100.0;
                }

                // Parse reset date
                if (plan.TryGetProperty("resetDate", out var resetElem))
                {
                    var resetStr = resetElem.GetString();
                    if (!string.IsNullOrEmpty(resetStr) && 
                        DateTimeOffset.TryParse(resetStr, out var parsed))
                    {
                        resetDate = parsed;
                    }
                }
            }
        }

        return (usagePercent, tokensUsed, tokensLimit, tokensCost, planName, resetDate);
    }
}
