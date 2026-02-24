using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TokenMeter.Core.Models;

namespace TokenMeter.Probes.Impl;

public sealed class CursorCliProbe : IProviderFetchStrategy
{
    public string Id => "cursor_cli";
    public ProviderFetchKind Kind => ProviderFetchKind.Cli;
    public UsageProvider Provider => UsageProvider.Cursor;

    private readonly ILogger<CursorCliProbe> _logger;

    public CursorCliProbe(ILogger<CursorCliProbe> logger) => _logger = logger;

    public Task<bool> IsAvailableAsync(ProviderFetchContext context, CancellationToken ct = default)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cursor",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return Task.FromResult(false);
            proc.WaitForExit(1500);
            return Task.FromResult(proc.ExitCode == 0);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Cursor CLI not available");
            return Task.FromResult(false);
        }
    }

    public async Task<ProviderFetchResult> FetchAsync(ProviderFetchContext context, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cursor",
            Arguments = "usage --json",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi) ?? throw new Exception("Failed to start cursor CLI");
        var output = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode != 0)
        {
            var err = await proc.StandardError.ReadToEndAsync();
            throw new Exception($"Cursor CLI returned non-zero exit code: {err}");
        }

        try
        {
            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;

            int? used = null, limit = null;
            if (root.TryGetProperty("used", out var usedElem) && usedElem.TryGetInt32(out var u)) used = u;
            if (root.TryGetProperty("limit", out var limitElem) && limitElem.TryGetInt32(out var l)) limit = l;

            var snapshot = new UsageSnapshot
            {
                Provider = Provider,
                CapturedAt = DateTimeOffset.UtcNow,
                Status = UsageStatus.Ok,
                PlanName = "CLI",
                SessionUsage = used,
                SessionLimit = limit,
                TokenCost = new CostUsageTokenSnapshot
                {
                    SessionTokens = used,
                    Last30DaysTokens = used,
                    Daily = [],
                    UpdatedAt = DateTimeOffset.UtcNow
                }
            };

            return new ProviderFetchResult
            {
                Usage = snapshot,
                SourceLabel = "CLI",
                StrategyId = Id,
                StrategyKind = Kind
            };
        }
        catch (JsonException ex)
        {
            throw new Exception("Failed to parse cursor CLI output", ex);
        }
    }

    public bool ShouldFallback(Exception ex, ProviderFetchContext context) => true;
}
