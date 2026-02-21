using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TokenMeter.Auth.Stores;
using TokenMeter.Core.Models;

namespace TokenMeter.Probes.Impl;

public sealed class CopilotApiProbe : HttpProviderFetchStrategy
{
    public override string Id => "copilot_api";
    public override ProviderFetchKind Kind => ProviderFetchKind.ApiToken;
    public override UsageProvider Provider => UsageProvider.Copilot;

    public CopilotApiProbe(IHttpClientFactory httpClientFactory, ILogger<CopilotApiProbe> logger)
        : base(httpClientFactory, logger) { }

    public override Task<bool> IsAvailableAsync(ProviderFetchContext context, CancellationToken ct = default)
    {
        // For Copilot, we can either use the context token OR try to auto-detect it from Credential Manager
        if (!string.IsNullOrWhiteSpace(context.ApiToken)) return Task.FromResult(true);

        var autoToken = GitHubCredentialHelper.ReadGitHubToken();
        return Task.FromResult(!string.IsNullOrEmpty(autoToken));
    }

    public override async Task<ProviderFetchResult> FetchAsync(ProviderFetchContext context, CancellationToken ct = default)
    {
        var token = context.ApiToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            token = GitHubCredentialHelper.ReadGitHubToken();
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new Exception("GitHub Copilot token not found. Please log in via GitHub CLI or store a token in Credential Manager.");
        }

        using var client = CreateClient();
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.DefaultRequestHeaders.Add("Editor-Version", "vscode/1.96.2");
        client.DefaultRequestHeaders.Add("Editor-Plugin-Version", "copilot-chat/0.26.7");
        client.DefaultRequestHeaders.Add("User-Agent", "GitHubCopilotChat/0.26.7");
        client.DefaultRequestHeaders.Add("X-Github-Api-Version", "2025-04-01");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", token);

        var url = "https://api.github.com/copilot_internal/user";
        using var response = await client.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"GitHub Copilot API returned {response.StatusCode}");
        }

        var content = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        // Parsing logic based on Rust implementation
        double usedPercent = 0;
        string planName = "Copilot";

        if (root.TryGetProperty("quota_snapshots", out var snapshots))
        {
            if (snapshots.TryGetProperty("premium_interactions", out var premium))
            {
                if (premium.TryGetProperty("percent_remaining", out var remainingElem))
                {
                    usedPercent = 100.0 - remainingElem.GetDouble();
                }
            }
        }

        if (root.TryGetProperty("copilot_plan", out var planElem))
        {
            var plan = planElem.GetString() ?? "";
            planName = $"Copilot {char.ToUpper(plan[0])}{plan.Substring(1)}";
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
            SourceLabel = "GitHub API",
            StrategyId = Id,
            StrategyKind = Kind
        };
    }
}
