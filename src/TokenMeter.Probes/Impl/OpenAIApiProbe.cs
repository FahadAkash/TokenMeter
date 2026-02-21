using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using TokenMeter.Core.Models;

namespace TokenMeter.Probes.Impl;

public sealed class OpenAIApiProbe : HttpProviderFetchStrategy
{
    public override string Id => "openai_api";
    public override ProviderFetchKind Kind => ProviderFetchKind.ApiToken;
    public override UsageProvider Provider => UsageProvider.Codex;

    public OpenAIApiProbe(IHttpClientFactory httpClientFactory, ILogger<OpenAIApiProbe> logger)
        : base(httpClientFactory, logger) { }

    public override Task<bool> IsAvailableAsync(ProviderFetchContext context, CancellationToken ct = default)
    {
        // Require API key in the context
        return Task.FromResult(!string.IsNullOrWhiteSpace(context.ApiToken));
    }

    public override async Task<ProviderFetchResult> FetchAsync(ProviderFetchContext context, CancellationToken ct = default)
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", context.ApiToken);

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var firstOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).ToString("yyyy-MM-dd");

        // Fetch usage
        using var usageResponse = await client.GetAsync($"https://api.openai.com/v1/dashboard/billing/usage?start_date={firstOfMonth}&end_date={today}", ct);
        var usageData = await DeserializeAsync<CostUsageDailyReport>(usageResponse, ct);

        // Map onto our standard snapshot model
        var snapshot = new UsageSnapshot
        {
            Provider = UsageProvider.Codex,
            CapturedAt = DateTimeOffset.UtcNow,
            Status = UsageStatus.Ok,
            TokenCost = new CostUsageTokenSnapshot
            {
                SessionCostUsd = usageData?.Summary?.TotalCostUsd ?? 0.0,
                SessionTokens = usageData?.Summary?.TotalTokens,
                Daily = usageData?.Data ?? []
            }
        };

        return new ProviderFetchResult
        {
            Usage = snapshot,
            SourceLabel = "OpenAI API",
            StrategyId = Id,
            StrategyKind = Kind
        };
    }
}
