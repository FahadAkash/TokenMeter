using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TokenMeter.Core.Models;

namespace TokenMeter.Probes.Impl;

/// <summary>
/// Lightweight CLI fallback for Claude. Attempts to run a local `claude` binary
/// to obtain usage info. This is intentionally conservative: if the CLI is not
/// present the probe returns unavailable; if the CLI fails the pipeline will
/// fall through to the next strategy.
/// </summary>
public sealed class ClaudeCliProbe : IProviderFetchStrategy
{
    public string Id => "claude_cli";
    public ProviderFetchKind Kind => ProviderFetchKind.Cli;
    public UsageProvider Provider => UsageProvider.Claude;

    private readonly ILogger<ClaudeCliProbe> _logger;

    public ClaudeCliProbe(ILogger<ClaudeCliProbe> logger)
    {
        _logger = logger;
    }

    public Task<bool> IsAvailableAsync(ProviderFetchContext context, CancellationToken ct = default)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "claude",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return Task.FromResult(false);

            // Wait briefly for a response; if it exits quickly it's present
            proc.WaitForExit(1500);
            return Task.FromResult(proc.ExitCode == 0);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Claude CLI not available");
            return Task.FromResult(false);
        }
    }

    public async Task<ProviderFetchResult> FetchAsync(ProviderFetchContext context, CancellationToken ct = default)
    {
        // Conservative implementation: try `claude usage --json` if present.
        var psi = new ProcessStartInfo
        {
            FileName = "claude",
            Arguments = "usage --json",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi) ?? throw new Exception("Failed to start claude CLI");

        var output = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode != 0)
        {
            var err = await proc.StandardError.ReadToEndAsync();
            throw new Exception($"Claude CLI returned non-zero exit code: {err}");
        }

        try
        {
            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;

            // Basic parse: expect { "used": 12345, "limit": 100000 }
            int? used = null, limit = null;
            if (root.TryGetProperty("used", out var usedElem) && usedElem.TryGetInt32(out var u)) used = u;
            if (root.TryGetProperty("limit", out var limitElem) && limitElem.TryGetInt32(out var l)) limit = l;

            double percent = 0;
            if (used.HasValue && limit.HasValue && limit.Value > 0)
                percent = (used.Value / (double)limit.Value) * 100.0;

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
            throw new Exception("Failed to parse claude CLI output", ex);
        }
    }

    public bool ShouldFallback(Exception ex, ProviderFetchContext context)
    {
        // If the CLI returned a non-json output, allow fallback to next strategy
        return true;
    }
}
