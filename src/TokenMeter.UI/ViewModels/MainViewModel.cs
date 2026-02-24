using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using TokenMeter.Core.Models;
using TokenMeter.Probes;
using TokenMeter.Probes.Pipeline;
using TokenMeter.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace TokenMeter.UI.ViewModels;

public partial class ProviderItemViewModel : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _statusText = "Checking…";
    [ObservableProperty] private string _statusBadge = "•";
    [ObservableProperty] private System.Windows.Media.Brush _statusColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(108, 112, 134));

    // Current month totals for the summary cards
    [ObservableProperty] private double _totalCostUsd = 0.0;
    [ObservableProperty] private long _inputTokens = 0;
    [ObservableProperty] private long _outputTokens = 0;

    // LiveCharts Series
    [ObservableProperty] private ISeries[] _chartSeries = [];
    [ObservableProperty] private Axis[] _xAxes = [new Axis { Labels = [] }];

    public void UpdateFromHistory(IReadOnlyList<UsageEntry> history)
    {
        var entries = history.OrderBy(e => e.Date).ToList();
        if (!entries.Any()) return;

        var labels = entries.Select(e => e.Date.ToString("MMM dd")).ToArray();
        var inputData = entries.Select(e => (double)e.InputTokens).ToArray();
        var outputData = entries.Select(e => (double)e.OutputTokens).ToArray();
        var costData = entries.Select(e => e.TotalCost).ToArray();

        // Update current summary values with the latest record
        var latest = entries.Last();
        TotalCostUsd = latest.TotalCost;
        InputTokens = latest.InputTokens;
        OutputTokens = latest.OutputTokens;

        XAxes = [
            new Axis
            {
                Labels = labels,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#A5ADCB")),
                TextSize = 12
            }
        ];

        ChartSeries = [
            new ColumnSeries<double>
            {
                Values = inputData,
                Name = "Input",
                Fill = new SolidColorPaint(SKColor.Parse("#8AADF4")),
                MaxBarWidth = 12,
                Rx = 4, Ry = 4
            },
            new ColumnSeries<double>
            {
                Values = outputData,
                Name = "Output",
                Fill = new SolidColorPaint(SKColor.Parse("#ED8796")),
                MaxBarWidth = 12,
                Rx = 4, Ry = 4
            },
            new LineSeries<double>
            {
                Values = costData,
                Name = "Cost ($)",
                Fill = null,
                Stroke = new SolidColorPaint(SKColor.Parse("#A6DA95")) { StrokeThickness = 3 },
                GeometrySize = 8,
                GeometryFill = new SolidColorPaint(SKColor.Parse("#24273A")),
                GeometryStroke = new SolidColorPaint(SKColor.Parse("#A6DA95")) { StrokeThickness = 3 }
            }
        ];
    }
}

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private string _providerCountText = "0";
    [ObservableProperty] private double _totalCostUsdToday = 0.0;

    // LiveCharts Series for the Total App Aggregated Cost
    [ObservableProperty] private ISeries[] _totalCostSeries = [];
    [ObservableProperty] private Axis[] _totalCostXAxes = [new Axis { Labels = [] }];

    public ObservableCollection<ProviderItemViewModel> Providers { get; } = [];

    [ObservableProperty]
    private ProviderItemViewModel? _selectedProvider;

    private readonly ProviderFetchPipeline? _pipeline;
    private readonly TokenMeter.Auth.ITokenStore? _tokenStore;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly System.Windows.Threading.DispatcherTimer? _timer;

    public MainViewModel(ProviderFetchPipeline? pipeline = null, TokenMeter.Auth.ITokenStore? tokenStore = null, IServiceScopeFactory? scopeFactory = null)
    {
        _pipeline = pipeline;
        _tokenStore = tokenStore;
        _scopeFactory = scopeFactory;

        var overviewVm = new ProviderItemViewModel
        {
            Name = "Overview",
            StatusText = "App global aggregated cost",
            StatusBadge = "Total",
            StatusColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(166, 218, 149)),
        };
        Providers.Add(overviewVm);

        foreach (var p in Enum.GetValues<UsageProvider>())
        {
            var vm = new ProviderItemViewModel
            {
                Name = p.ToString(),
                StatusText = "Connecting...",
                StatusBadge = "Idle",
                StatusColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(108, 112, 134)),
            };
            // Historical data will be loaded via LoadHistoricalDataAsync
            Providers.Add(vm);
        }

        ProviderCountText = (Providers.Count - 1).ToString(); // Subtract 1 for Overview
        SelectedProvider = Providers.FirstOrDefault();

        if (_pipeline != null)
        {
            _timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _timer.Tick += async (s, e) => await RefreshProvidersAsync();
            _timer.Start();

            // Initial load from DB
            Task.Run(LoadHistoricalDataAsync);
            Task.Run(LoadAggregateHistoryAsync);
        }
    }

    private async Task LoadAggregateHistoryAsync()
    {
        if (_scopeFactory == null) return;
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var history = await db.CostHistory
            .OrderByDescending(e => e.Date)
            .Take(7)
            .ToListAsync();

        if (history.Any())
        {
            var entries = history.OrderBy(e => e.Date).ToList();
            var labels = entries.Select(e => e.Date.ToString("MMM dd")).ToArray();
            var costData = entries.Select(e => (double)e.TotalCostUsd).ToArray();

            var latest = entries.Last();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                TotalCostUsdToday = (double)latest.TotalCostUsd;

                TotalCostXAxes = [
                    new Axis
                    {
                        Labels = labels,
                        LabelsPaint = new SolidColorPaint(SKColor.Parse("#A5ADCB")),
                        TextSize = 12
                    }
                ];

                TotalCostSeries = [
                    new ColumnSeries<double>
                    {
                        Values = costData,
                        Name = "Total Cost ($)",
                        Fill = new SolidColorPaint(SKColor.Parse("#A6DA95")),
                        MaxBarWidth = 24,
                        Rx = 4, Ry = 4
                    }
                ];
            });
        }
    }

    private async Task LoadHistoricalDataAsync()
    {
        if (_scopeFactory == null) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        foreach (var providerVm in Providers)
        {
            if (!Enum.TryParse<UsageProvider>(providerVm.Name, out var providerEnum)) continue;

            var history = await db.UsageEntries
                .Where(e => e.Provider == providerEnum)
                .OrderByDescending(e => e.Date)
                .Take(7)
                .ToListAsync();

            if (history.Any())
            {
                // Update chart on UI thread
                System.Windows.Application.Current.Dispatcher.Invoke(() => providerVm.UpdateFromHistory(history));
            }
        }

        // Let's also refresh the Total App Cost Historical chart here when implemented
    }

    private async Task RefreshProvidersAsync()
    {
        var pipeline = _pipeline;
        var tokenStore = _tokenStore;
        if (pipeline == null || tokenStore == null) return;

        foreach (var providerVm in Providers)
        {
            if (!Enum.TryParse<UsageProvider>(providerVm.Name, out var providerEnum)) continue;

            string? token = null;
            var sourceMode = global::TokenMeter.Probes.ProviderSourceMode.Auto;

            // Read per-provider source mode preference from token store (if available)
            try
            {
                string key = providerEnum switch
                {
                    UsageProvider.Claude => "claude_source_mode",
                    UsageProvider.Codex => "chatgpt_source_mode",
                    UsageProvider.Cursor => "cursor_source_mode",
                    UsageProvider.Copilot => "copilot_source_mode",
                    _ => $"{providerEnum.ToString().ToLowerInvariant()}_source_mode"
                };

                if (!string.IsNullOrEmpty(key) && tokenStore != null)
                {
                    var modeStr = await tokenStore.GetAsync(key) ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(modeStr) && Enum.TryParse<global::TokenMeter.Probes.ProviderSourceMode>(modeStr, true, out var parsed))
                        sourceMode = parsed;
                }
            }
            catch { /* ignore and keep default Auto */ }

            // Modern credential discovery using TokenAccountManager
            // For now, instantiate it directly to reuse logic, ideally inject it.
            var tokenAccountManager = new TokenMeter.Auth.Stores.TokenAccountManager(tokenStore!);
            token = await tokenAccountManager.GetPrimaryCredentialsAsync(providerEnum);

            if (providerEnum == UsageProvider.Copilot)
            {
                sourceMode = global::TokenMeter.Probes.ProviderSourceMode.Api;
            }

            // For Copilot, we continue even if token is null because the probe can auto-detect
            if (string.IsNullOrEmpty(token) && providerEnum != UsageProvider.Copilot)
            {
                providerVm.StatusText = "No active session";
                providerVm.StatusBadge = "Unauthenticated";
                providerVm.StatusColor = new SolidColorBrush(Colors.SlateGray);
                continue;
            }

            var context = new global::TokenMeter.Probes.ProviderFetchContext
            {
                Runtime = global::TokenMeter.Probes.ProviderRuntime.App,
                SourceMode = sourceMode,
                ApiToken = token ?? "",
                TargetProvider = providerEnum
            };

            var outcome = await pipeline.FetchAsync(context, providerEnum);
            if (outcome.IsSuccess && outcome.Result != null)
            {
                var usage = outcome.Result.Usage;
                providerVm.StatusText = "Connected";
                providerVm.StatusBadge = usage.PlanName ?? "Active";
                providerVm.StatusColor = new SolidColorBrush(Colors.MediumSeaGreen);

                if (usage.TokenCost != null)
                {
                    providerVm.TotalCostUsd = usage.TokenCost.SessionCostUsd ?? 0.0;
                    providerVm.InputTokens = usage.TokenCost.SessionTokens ?? 0;
                    providerVm.OutputTokens = usage.TokenCost.SessionOutputTokens ?? 0;

                    // Save to DB
                    await SaveToHistoryAsync(providerEnum, usage.TokenCost);

                    // Refresh chart for this provider
                    await RefreshOneProviderHistoryAsync(providerVm, providerEnum);
                }
            }
            else
            {
                providerVm.StatusText = outcome.Error?.Message ?? "Check failed";
                providerVm.StatusBadge = "Error";
                providerVm.StatusColor = new SolidColorBrush(Colors.OrangeRed);
            }
        }
    }

    private async Task SaveToHistoryAsync(UsageProvider provider, CostUsageTokenSnapshot snapshot)
    {
        if (_scopeFactory == null) return;
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var today = DateTime.Today;
        var entry = await db.UsageEntries.FirstOrDefaultAsync(e => e.Provider == provider && e.Date == today);

        if (entry == null)
        {
            entry = new UsageEntry
            {
                Provider = provider,
                Date = today,
                InputTokens = snapshot.SessionTokens ?? 0,
                OutputTokens = snapshot.SessionOutputTokens ?? 0,
                TotalCost = snapshot.SessionCostUsd ?? 0.0
            };
            db.UsageEntries.Add(entry);
        }
        else
        {
            // Update with latest (assume session is cumulative for the day)
            entry.InputTokens = (long)Math.Max(entry.InputTokens, snapshot.SessionTokens ?? 0);
            entry.OutputTokens = (long)Math.Max(entry.OutputTokens, snapshot.SessionOutputTokens ?? 0);
            entry.TotalCost = Math.Max(entry.TotalCost, snapshot.SessionCostUsd ?? 0.0);
        }

        // Aggregate across all providers for today's CostHistorySnapshot
        var snapshotEntry = await db.CostHistory.FirstOrDefaultAsync(e => e.Date == today);
        if (snapshotEntry == null)
        {
            snapshotEntry = new CostHistorySnapshot
            {
                Date = today,
                TotalCostUsd = (decimal)entry.TotalCost
            };
            db.CostHistory.Add(snapshotEntry);
        }
        else
        {
            // Sum all provider costs for today manually (could be optimized, but ok for small N)
            var allTodayProviderCosts = await db.UsageEntries
                .Where(e => e.Date == today && e.Provider != provider)
                .SumAsync(e => e.TotalCost);

            db.CostHistory.Remove(snapshotEntry);
            var updatedSnapshotEntry = new CostHistorySnapshot
            {
                Id = snapshotEntry.Id,
                Date = snapshotEntry.Date,
                TotalCostUsd = (decimal)(allTodayProviderCosts + entry.TotalCost)
            };
            db.CostHistory.Add(updatedSnapshotEntry);
        }

        await db.SaveChangesAsync();
    }

    private async Task RefreshOneProviderHistoryAsync(ProviderItemViewModel vm, UsageProvider provider)
    {
        if (_scopeFactory == null) return;
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var history = await db.UsageEntries
            .Where(e => e.Provider == provider)
            .OrderByDescending(e => e.Date)
            .Take(7)
            .ToListAsync();

        System.Windows.Application.Current.Dispatcher.Invoke(() => vm.UpdateFromHistory(history));
    }
}
