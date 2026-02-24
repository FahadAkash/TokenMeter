using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TokenMeter.Auth.Stores;
using TokenMeter.Core.Models;

namespace TokenMeter.Probes.Impl;

/// <summary>
/// Fetches real Claude usage and cost data from the official Claude OAuth API.
/// Uses credentials from Claude CLI: ~/.claude/.credentials.json
/// Returns actual usage percentages + real cost/credit data.
/// </summary>
public sealed class ClaudeOAuthProbe : HttpProviderFetchStrategy
{
    public override string Id => "claude_oauth";
    public override ProviderFetchKind Kind => ProviderFetchKind.ApiToken;
    public override UsageProvider Provider => UsageProvider.Claude;

    private const string UsageApiUrl = "https://api.claude.ai/api/usage";

    public ClaudeOAuthProbe(IHttpClientFactory httpClientFactory, ILogger<ClaudeOAuthProbe> logger)
        : base(httpClientFactory, logger) { }

    public override Task<bool> IsAvailableAsync(ProviderFetchContext context, CancellationToken ct = default)
    {
        // Check for context token OR auto-detect from Claude CLI credentials file
        if (!string.IsNullOrWhiteSpace(context.ApiToken)) return Task.FromResult(true);

        var token = LoadClaudeOAuthToken();
        return Task.FromResult(!string.IsNullOrEmpty(token));
    }

    public override async Task<ProviderFetchResult> FetchAsync(ProviderFetchContext context, CancellationToken ct = default)
    {
        var token = context.ApiToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            token = LoadClaudeOAuthToken();
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new Exception("Claude OAuth token not found. Run `claude` to authenticate or set CODEXBAR_CLAUDE_OAUTH_TOKEN.");
        }

        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());

        var response = await client.GetAsync(UsageApiUrl, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Claude API returned {response.StatusCode}: {response.Content}");
        }

        var content = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        // Parse the response into structured usage data
        var (primaryWindow, secondaryWindow, opusWindow, providerCost, planName) = ParseOAuthResponse(root);

        var snapshot = new UsageSnapshot
        {
            Provider = Provider,
            CapturedAt = DateTimeOffset.UtcNow,
            Status = UsageStatus.Ok,
            PlanName = planName,
            TokenCost = new CostUsageTokenSnapshot
            {
                SessionCostUsd = primaryWindow?.UsedPercent,
                SessionTokens = primaryWindow != null ? (int)(primaryWindow.UsedPercent * 1000) : null,
                Last30DaysCostUsd = secondaryWindow?.UsedPercent,
                Last30DaysTokens = secondaryWindow != null ? (int)(secondaryWindow.UsedPercent * 1000) : null,
                ProviderCost = providerCost,
                Daily = [],
                UpdatedAt = DateTimeOffset.UtcNow
            }
        };

        return new ProviderFetchResult
        {
            Usage = snapshot,
            SourceLabel = "Claude OAuth",
            StrategyId = Id,
            StrategyKind = Kind
        };
    }

    /// <summary>
    /// Parses the OAuth API response into rate windows and cost data.
    /// Real data structure from Claude API:
    /// {
    ///   "fiveHour": { "utilization": 0.25, "resetsAt": "2025-02-24T15:30:00Z" },
    ///   "sevenDay": { "utilization": 0.50, "resetsAt": "2025-03-03T00:00:00Z" },
    ///   "sevenDayOpus": { "utilization": 0.10, "resetsAt": "2025-03-03T00:00:00Z" },
    ///   "extraUsage": {
    ///     "isEnabled": true,
    ///     "usedCredits": 5450,          // in cents
    ///     "monthlyLimit": 50000,        // in cents
    ///     "currency": "USD"
    ///   }
    /// }
    /// </summary>
    private static (RateWindow?, RateWindow?, RateWindow?, ProviderCostSnapshot?, string) ParseOAuthResponse(
        JsonElement root)
    {
        RateWindow? fiveHour = null;
        RateWindow? sevenDay = null;
        RateWindow? opus = null;
        ProviderCostSnapshot? cost = null;
        string planName = "Claude";

        // Parse 5-hour session window
        if (root.TryGetProperty("fiveHour", out var fiveHourElem))
        {
            fiveHour = ParseRateWindow(fiveHourElem);
        }

        // Parse 7-day window
        if (root.TryGetProperty("sevenDay", out var sevenDayElem))
        {
            sevenDay = ParseRateWindow(sevenDayElem);
        }

        // Parse model-specific (Opus) window
        if (root.TryGetProperty("sevenDayOpus", out var opusElem))
        {
            opus = ParseRateWindow(opusElem);
        }

        // Parse Extra Usage (real paid credits data)
        if (root.TryGetProperty("extraUsage", out var extraElem))
        {
            cost = ParseExtraUsage(extraElem);
            if (cost != null)
            {
                planName = "Claude Pro (with Extra Usage)";
            }
        }

        // Try to infer plan from rate limit tier
        if (root.TryGetProperty("rateLimitTier", out var tierElem))
        {
            var tier = tierElem.GetString() ?? "";
            planName = FormatPlan(tier);
        }

        return (fiveHour, sevenDay, opus, cost, planName);
    }

    /// <summary>
    /// Parse a single rate window from the OAuth response.
    /// </summary>
    private static RateWindow? ParseRateWindow(JsonElement elem)
    {
        if (elem.ValueKind == JsonValueKind.Null) return null;

        double utilization = 0;
        if (elem.TryGetProperty("utilization", out var util))
        {
            utilization = util.GetDouble() * 100.0;  // Convert 0-1 to 0-100
        }

        DateTimeOffset? resetsAt = null;
        if (elem.TryGetProperty("resetsAt", out var resetElem))
        {
            var resetStr = resetElem.GetString();
            if (!string.IsNullOrEmpty(resetStr) && DateTimeOffset.TryParse(resetStr, out var parsed))
            {
                resetsAt = parsed;
            }
        }

        return new RateWindow
        {
            UsedPercent = utilization,
            ResetsAt = resetsAt
        };
    }

    /// <summary>
    /// Parse Extra Usage (Claude Pro paid credits) from the response.
    /// Values are in cents; convert to dollars for display.
    /// </summary>
    private static ProviderCostSnapshot? ParseExtraUsage(JsonElement elem)
    {
        if (elem.ValueKind == JsonValueKind.Null) return null;

        // Check if extra usage is enabled
        if (elem.TryGetProperty("isEnabled", out var enabledElem))
        {
            if (!enabledElem.GetBoolean()) return null;
        }

        double? usedCredits = null;
        double? monthlyLimit = null;

        if (elem.TryGetProperty("usedCredits", out var usedElem))
        {
            var cents = usedElem.GetDouble();
            usedCredits = cents / 100.0;  // Convert cents to dollars
        }

        if (elem.TryGetProperty("monthlyLimit", out var limitElem))
        {
            var cents = limitElem.GetDouble();
            monthlyLimit = cents / 100.0;  // Convert cents to dollars
        }

        string currency = "USD";
        if (elem.TryGetProperty("currency", out var currElem))
        {
            var curr = currElem.GetString();
            if (!string.IsNullOrWhiteSpace(curr))
            {
                currency = curr.ToUpperInvariant();
            }
        }

        if (usedCredits.HasValue && monthlyLimit.HasValue)
        {
            return new ProviderCostSnapshot
            {
                Used = usedCredits.Value,
                Limit = monthlyLimit.Value,
                CurrencyCode = currency,
                Period = "Monthly",
                ResetsAt = null,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }

        return null;
    }

    private static string FormatPlan(string tier)
    {
        return tier.ToLowerInvariant() switch
        {
            var t when t.Contains("max") => "Claude Max",
            var t when t.Contains("pro") => "Claude Pro",
            var t when t.Contains("team") => "Claude Team",
            var t when t.Contains("enterprise") => "Claude Enterprise",
            _ => "Claude"
        };
    }

    /// <summary>
    /// Load Claude OAuth token from ~/.claude/.credentials.json or environment variable.
    /// </summary>
    private static string? LoadClaudeOAuthToken()
    {
        // Try environment variable first
        var envToken = Environment.GetEnvironmentVariable("CODEXBAR_CLAUDE_OAUTH_TOKEN");
        if (!string.IsNullOrWhiteSpace(envToken))
        {
            return envToken.Trim();
        }

        // Try reading from Claude CLI credentials file
        try
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var credPath = Path.Combine(homeDir, ".claude", ".credentials.json");

            if (!File.Exists(credPath)) return null;

            var content = File.ReadAllText(credPath);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.TryGetProperty("claudeAiOauth", out var oauthElem))
            {
                if (oauthElem.TryGetProperty("accessToken", out var tokenElem))
                {
                    var token = tokenElem.GetString();
                    return string.IsNullOrWhiteSpace(token) ? null : token.Trim();
                }
            }
        }
        catch
        {
            // Silently fail; token may not be available
        }

        return null;
    }
}
