using System.Text.Json;
using TokenMeter.Core.Models;
using TokenMeter.Core.Parsing;
using TokenMeter.Probes;

namespace TokenMeter.Tests;

// ── Provider enum tests ───────────────────────────────────────────────

public class UsageProviderTests
{
    [Fact]
    public void AllProviders_HaveExpectedCount()
    {
        var providers = Enum.GetValues<UsageProvider>();
        Assert.Equal(21, providers.Length);
    }

    [Theory]
    [InlineData("Codex", UsageProvider.Codex)]
    [InlineData("Claude", UsageProvider.Claude)]
    [InlineData("Cursor", UsageProvider.Cursor)]
    [InlineData("Gemini", UsageProvider.Gemini)]
    [InlineData("Copilot", UsageProvider.Copilot)]
    [InlineData("OpenRouter", UsageProvider.OpenRouter)]
    public void Provider_ParsesFromString(string name, UsageProvider expected)
    {
        Assert.True(Enum.TryParse<UsageProvider>(name, out var result));
        Assert.Equal(expected, result);
    }
}

// ── Cost usage model tests ────────────────────────────────────────────

public class CostUsageModelsTests
{
    [Fact]
    public void DailyEntry_DeserializesFromJson()
    {
        var json = """
        {
            "date": "2025-01-15",
            "inputTokens": 1500,
            "outputTokens": 500,
            "totalTokens": 2000,
            "costUSD": 0.05,
            "modelsUsed": ["claude-sonnet-4-20250514", "gpt-4o"]
        }
        """;

        var entry = JsonSerializer.Deserialize<DailyEntry>(json);

        Assert.NotNull(entry);
        Assert.Equal("2025-01-15", entry.Date);
        Assert.Equal(1500, entry.InputTokens);
        Assert.Equal(500, entry.OutputTokens);
        Assert.Equal(2000, entry.TotalTokens);
        Assert.Equal(0.05, entry.CostUsd);
        Assert.Equal(2, entry.ModelsUsed!.Count);
    }

    [Fact]
    public void DailyEntry_HandlesNullOptionals()
    {
        var json = """{ "date": "2025-02-01" }""";
        var entry = JsonSerializer.Deserialize<DailyEntry>(json);

        Assert.NotNull(entry);
        Assert.Equal("2025-02-01", entry.Date);
        Assert.Null(entry.InputTokens);
        Assert.Null(entry.CostUsd);
        Assert.Null(entry.ModelsUsed);
    }

    [Fact]
    public void MonthlyEntry_DeserializesFromJson()
    {
        var json = """
        {
            "month": "Jan 2025",
            "totalTokens": 100000,
            "costUSD": 12.50
        }
        """;

        var entry = JsonSerializer.Deserialize<MonthlyEntry>(json);

        Assert.NotNull(entry);
        Assert.Equal("Jan 2025", entry.Month);
        Assert.Equal(100000, entry.TotalTokens);
        Assert.Equal(12.50, entry.CostUsd);
    }

    [Fact]
    public void SessionEntry_DeserializesFromJson()
    {
        var json = """
        {
            "session": "abc-123",
            "inputTokens": 300,
            "outputTokens": 150,
            "totalTokens": 450,
            "costUSD": 0.01,
            "lastActivity": "2025-01-15T10:30:00Z"
        }
        """;

        var entry = JsonSerializer.Deserialize<SessionEntry>(json);

        Assert.NotNull(entry);
        Assert.Equal("abc-123", entry.Session);
        Assert.Equal(450, entry.TotalTokens);
    }
}

// ── Credits model tests ───────────────────────────────────────────────

public class CreditsModelsTests
{
    [Fact]
    public void CreditEvent_DeserializesFromJson()
    {
        var json = """
        {
            "date": "2025-01-15T00:00:00Z",
            "service": "claude",
            "creditsUsed": 2.5
        }
        """;

        var ev = JsonSerializer.Deserialize<CreditEvent>(json);

        Assert.NotNull(ev);
        Assert.Equal("claude", ev.Service);
        Assert.Equal(2.5, ev.CreditsUsed);
    }

    [Fact]
    public void CreditsSnapshot_RoundTrips()
    {
        var snapshot = new CreditsSnapshot
        {
            Remaining = 47.5,
            Events = [new CreditEvent { Service = "codex", CreditsUsed = 2.5, Date = DateTimeOffset.UtcNow }],
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        var json = JsonSerializer.Serialize(snapshot);
        var restored = JsonSerializer.Deserialize<CreditsSnapshot>(json);

        Assert.NotNull(restored);
        Assert.Equal(47.5, restored.Remaining);
        Assert.Single(restored.Events);
    }
}

// ── Date parser tests ─────────────────────────────────────────────────

public class DateParserTests
{
    [Theory]
    [InlineData("2025-01-15")]
    [InlineData("2025-01-15T10:30:00Z")]
    [InlineData("Jan 15, 2025")]
    public void ParseDay_ValidFormats(string input)
    {
        var result = DateParser.ParseDay(input);
        Assert.NotNull(result);
        Assert.Equal(2025, result.Value.Year);
        Assert.Equal(1, result.Value.Month);
        Assert.Equal(15, result.Value.Day);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-date")]
    public void ParseDay_Invalid_ReturnsNull(string? input)
    {
        Assert.Null(DateParser.ParseDay(input));
    }

    [Theory]
    [InlineData("Jan 2025")]
    [InlineData("January 2025")]
    [InlineData("2025-01")]
    public void ParseMonth_ValidFormats(string input)
    {
        var result = DateParser.ParseMonth(input);
        Assert.NotNull(result);
        Assert.Equal(2025, result.Value.Year);
        Assert.Equal(1, result.Value.Month);
    }
}

// ── Provider descriptor registry tests ────────────────────────────────

public class ProviderDescriptorRegistryTests
{
    [Fact]
    public void Register_And_Retrieve()
    {
        var descriptor = new ProviderDescriptor
        {
            Id = UsageProvider.Claude,
            Metadata = new ProviderMetadata
            {
                Id = UsageProvider.Claude,
                DisplayName = "Claude",
                SessionLabel = "Session",
                WeeklyLabel = "Weekly",
                ToggleTitle = "Claude",
                CliName = "claude",
            },
            Branding = new ProviderBranding { ColorHex = "#D97706", Icon = Core.Models.IconStyle.Claude },
            TokenCost = new ProviderTokenCostConfig { SupportsTokenCost = true },
            Cli = new ProviderCliConfig { Name = "claude", Aliases = ["anthropic"] },
        };

        ProviderDescriptorRegistry.Register(descriptor);
        var found = ProviderDescriptorRegistry.Get(UsageProvider.Claude);

        Assert.Equal("Claude", found.Metadata.DisplayName);

        var cliMap = ProviderDescriptorRegistry.CliNameMap;
        Assert.True(cliMap.ContainsKey("claude"));
        Assert.True(cliMap.ContainsKey("anthropic"));
    }
}

// ── Pipeline tests ────────────────────────────────────────────────────

public class ProviderFetchPipelineTests
{
    private sealed class AlwaysFailStrategy : IProviderFetchStrategy
    {
        public string Id => "fail";
        public ProviderFetchKind Kind => ProviderFetchKind.ApiToken;
        public Task<bool> IsAvailableAsync(ProviderFetchContext ctx, CancellationToken ct) => Task.FromResult(true);
        public Task<ProviderFetchResult> FetchAsync(ProviderFetchContext ctx, CancellationToken ct)
            => throw new InvalidOperationException("Simulated failure");
    }

    private sealed class AlwaysSucceedStrategy : IProviderFetchStrategy
    {
        public string Id => "succeed";
        public ProviderFetchKind Kind => ProviderFetchKind.ApiToken;
        public Task<bool> IsAvailableAsync(ProviderFetchContext ctx, CancellationToken ct) => Task.FromResult(true);
        public Task<ProviderFetchResult> FetchAsync(ProviderFetchContext ctx, CancellationToken ct)
            => Task.FromResult(new ProviderFetchResult
            {
                Usage = new UsageSnapshot { Provider = UsageProvider.Claude, Status = UsageStatus.Ok },
                SourceLabel = "test",
                StrategyId = "succeed",
                StrategyKind = ProviderFetchKind.ApiToken,
            });
    }

    [Fact]
    public async Task Pipeline_TriesFallback_ThenSucceeds()
    {
        var pipeline = new Probes.Pipeline.ProviderFetchPipeline(
            _ => Task.FromResult<IReadOnlyList<IProviderFetchStrategy>>(
                [new AlwaysFailStrategy(), new AlwaysSucceedStrategy()]));

        var context = new ProviderFetchContext { Runtime = ProviderRuntime.App };
        var outcome = await pipeline.FetchAsync(context, UsageProvider.Claude);

        Assert.True(outcome.IsSuccess);
        Assert.NotNull(outcome.Result);
        Assert.Equal(2, outcome.Attempts.Count);
        Assert.NotNull(outcome.Attempts[0].ErrorDescription);
        Assert.Null(outcome.Attempts[1].ErrorDescription);
    }

    [Fact]
    public async Task Pipeline_AllFail_ReturnsFailure()
    {
        var pipeline = new Probes.Pipeline.ProviderFetchPipeline(
            _ => Task.FromResult<IReadOnlyList<IProviderFetchStrategy>>(
                [new AlwaysFailStrategy()]));

        var context = new ProviderFetchContext { Runtime = ProviderRuntime.Cli };
        var outcome = await pipeline.FetchAsync(context, UsageProvider.Codex);

        Assert.False(outcome.IsSuccess);
        Assert.NotNull(outcome.Error);
    }
}