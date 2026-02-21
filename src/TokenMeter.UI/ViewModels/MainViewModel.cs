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

        ProviderCountText = Providers.Count.ToString();
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
            var sourceMode = global::TokenMeter.Probes.ProviderSourceMode.Web;

            // Map provider to its stored credential key
            switch (providerEnum)
            {
                case UsageProvider.Claude:
                    token = await tokenStore.GetAsync("claude_cookie");
                    break;
                case UsageProvider.Codex: // OpenAI (ChatGPT)
                    token = await tokenStore.GetAsync("openai_cookie");
                    break;
                case UsageProvider.Cursor:
                    token = await tokenStore.GetAsync("cursor_cookie");
                    break;
                case UsageProvider.Copilot:
                    token = await tokenStore.GetAsync("copilot_token");
                    sourceMode = global::TokenMeter.Probes.ProviderSourceMode.Api;
                    break;
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
                ApiToken = token ?? ""
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
