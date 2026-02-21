using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TokenMeter.Core.Models;

namespace TokenMeter.Probes.Impl;

public sealed class OpenAIApiProbe : HttpProviderFetchStrategy
{
    public override string Id => "chatgpt_web";
    public override ProviderFetchKind Kind => ProviderFetchKind.WebDashboard;
    public override UsageProvider Provider => UsageProvider.ChatGPT;

    public OpenAIApiProbe(IHttpClientFactory httpClientFactory, ILogger<OpenAIApiProbe> logger)
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
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

        // 1. Get Access Token from session
        string accessToken = "";
        string email = "Unknown";
        try
        {
            var sessionUrl = "https://chatgpt.com/api/auth/session";
            using var sessionResp = await client.GetAsync(sessionUrl, ct);
            if (!sessionResp.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to get session: {sessionResp.StatusCode}");
            }

            var sessionContent = await sessionResp.Content.ReadAsStringAsync(ct);
            using var sessionDoc = JsonDocument.Parse(sessionContent);
            var root = sessionDoc.RootElement;

            if (root.TryGetProperty("accessToken", out var tokenElem))
                accessToken = tokenElem.GetString() ?? "";

            if (root.TryGetProperty("user", out var userElem) && userElem.TryGetProperty("email", out var emailElem))
                email = emailElem.GetString() ?? "Unknown";
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to get OpenAI session/accessToken.");
            throw;
        }

        if (string.IsNullOrEmpty(accessToken))
        {
            throw new Exception("OpenAI accessToken not found in session.");
        }

        // 2. Get Usage Data from backend-api
        var usageUrl = "https://chatgpt.com/backend-api/wham/usage";
        var request = new HttpRequestMessage(HttpMethod.Get, usageUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var usageResp = await client.SendAsync(request, ct);
        if (!usageResp.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to get usage from OpenAI: {usageResp.StatusCode}");
        }

        var usageContent = await usageResp.Content.ReadAsStringAsync(ct);
        using var usageDoc = JsonDocument.Parse(usageContent);
        var usageRoot = usageDoc.RootElement;

        double usedPercent = 0;
        string planName = "ChatGPT";

        if (usageRoot.TryGetProperty("used_percent", out var usedElem))
            usedPercent = usedElem.GetDouble();
        else if (usageRoot.TryGetProperty("usage_percent", out var usageElem))
            usedPercent = usageElem.GetDouble();

        if (usageRoot.TryGetProperty("plan_type", out var planElem))
        {
            planName = FormatPlan(planElem.GetString() ?? "");
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

    private static string FormatPlan(string plan)
    {
        return plan.ToLowerInvariant() switch
        {
            "free" => "ChatGPT Free",
            "plus" => "ChatGPT Plus",
            "pro" => "ChatGPT Pro",
            "team" => "ChatGPT Team",
            "enterprise" => "ChatGPT Enterprise",
            _ => $"ChatGPT ({plan})"
        };
    }
}
