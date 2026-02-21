using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using TokenMeter.Core.Models;

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

    public void UpdateDemoData()
    {
        var random = new Random();

        TotalCostUsd = random.NextDouble() * 15.0;
        InputTokens = random.Next(10000, 500000);
        OutputTokens = random.Next(5000, 250000);

        // Generate 7 days of fake historical data for the chart
        var inputData = new double[7];
        var outputData = new double[7];
        var costData = new double[7];
        var labels = new string[7];

        for (int i = 6; i >= 0; i--)
        {
            var date = DateTime.Today.AddDays(-i);
            labels[6 - i] = date.ToString("MMM dd");
            inputData[6 - i] = random.Next(1000, 50000);
            outputData[6 - i] = random.Next(500, 25000);
            costData[6 - i] = random.NextDouble() * 2.0;
        }

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
                Name = "Input Tokens",
                Fill = new SolidColorPaint(SKColor.Parse("#8AADF4")),
                MaxBarWidth = 16,
                Rx = 4,
                Ry = 4
            },
            new ColumnSeries<double>
            {
                Values = outputData,
                Name = "Output Tokens",
                Fill = new SolidColorPaint(SKColor.Parse("#ED8796")),
                MaxBarWidth = 16,
                Rx = 4,
                Ry = 4
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

    public MainViewModel()
    {
        foreach (var p in Enum.GetValues<UsageProvider>())
        {
            var vm = new ProviderItemViewModel
            {
                Name = p.ToString(),
                StatusText = "Connecting...",
                StatusBadge = "Idle",
                StatusColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(108, 112, 134)),
            };
            vm.UpdateDemoData();
            Providers.Add(vm);
        }

        ProviderCountText = Providers.Count.ToString();
        SelectedProvider = Providers.FirstOrDefault();
    }
}
