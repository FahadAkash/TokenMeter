using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TokenMeter.Auth;
using TokenMeter.Auth.Runners;
using TokenMeter.Auth.Stores;
using TokenMeter.Core.Data;
using TokenMeter.Probes;
using TokenMeter.Probes.Impl;
using TokenMeter.Probes.Pipeline;
using TokenMeter.Auth.OAuth;
using TokenMeter.UI.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.IO;
// Squirrel removed temporarily due to package issues

namespace TokenMeter.UI;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private TaskbarIcon? _trayIcon;
    private MainViewModel? _viewModel;

    protected override async void OnStartup(StartupEventArgs e)
    {
        this.DispatcherUnhandledException += (s, args) =>
        {
            System.Windows.MessageBox.Show($"TokenMeter Unhandled Exception: {args.Exception.Message}\n\n{args.Exception.StackTrace}", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                System.Windows.MessageBox.Show($"Background Task Error: {args.Exception.Message}", "Background Error"));
            args.SetObserved();
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = (Exception)args.ExceptionObject;
            System.Windows.MessageBox.Show($"TokenMeter Fatal AppDomain Error: {ex.Message}\n\n{ex.StackTrace}", "Fatal Error");
        };

        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddHttpClient();
                services.AddSingleton<ITokenStore, WindowsCredentialStore>();
                services.AddSingleton<ClaudeCookieLoginRunner>();
                services.AddSingleton<ChatGPTCookieLoginRunner>();
                services.AddSingleton<CursorCookieLoginRunner>();

                // OAuth / GitHub
                services.AddSingleton<GitHubDeviceFlow>();
                services.AddSingleton<CopilotOAuthRunner>();

                // Auth Registry
                services.AddSingleton<AuthRegistry>(sp => new AuthRegistry(new IAuthRunner[]
                {
                    sp.GetRequiredService<ClaudeCookieLoginRunner>(),
                    sp.GetRequiredService<ChatGPTCookieLoginRunner>(),
                    sp.GetRequiredService<CursorCookieLoginRunner>(),
                    sp.GetRequiredService<CopilotOAuthRunner>()
                }));

                // Phase 3: SQLite Persistence
                services.AddDbContext<AppDbContext>(options =>
                {
                    var appData = Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
                    var dbDir = Path.Combine(appData, "TokenMeter");
                    if (!Directory.Exists(dbDir)) Directory.CreateDirectory(dbDir);
                    var dbPath = Path.Combine(dbDir, "usage.db");
                    options.UseSqlite($"Data Source={dbPath}");
                });

                // Register Probes
                services.AddTransient<AnthropicApiProbe>();
                services.AddTransient<OpenAIApiProbe>();
                services.AddTransient<CopilotApiProbe>();
                services.AddTransient<CursorApiProbe>();

                // Register Pipeline
                services.AddSingleton<ProviderFetchPipeline>(sp =>
                {
                    return new ProviderFetchPipeline(async context =>
                    {
                        IReadOnlyList<IProviderFetchStrategy> probes = [
                            sp.GetRequiredService<AnthropicApiProbe>(),
                            sp.GetRequiredService<OpenAIApiProbe>(), // Now used for ChatGPT
                            sp.GetRequiredService<CopilotApiProbe>(),
                            sp.GetRequiredService<CursorApiProbe>()
                        ];
                        return probes;
                    });
                });

                // UI mapping
                services.AddSingleton<MainViewModel>();
                services.AddTransient<SettingsViewModel>();
            })
            .Build();

        // Initialize SQLite Database
        using (var scope = _host.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Database.EnsureCreated();
        }

        await _host.StartAsync();

        var services = _host.Services;
        _viewModel = services.GetRequiredService<MainViewModel>();

        // Run the background cookie extraction silently on startup for all providers
#pragma warning disable CS4014
        Task.Run(() => services.GetRequiredService<ClaudeCookieLoginRunner>().TryExtractAndSaveSilentlyAsync());
        Task.Run(() => services.GetRequiredService<ChatGPTCookieLoginRunner>().TryExtractAndSaveSilentlyAsync());
        Task.Run(() => services.GetRequiredService<CursorCookieLoginRunner>().TryExtractAndSaveSilentlyAsync());
#pragma warning restore CS4014

        // Check for updates in the background (fire and forget)
        _ = Task.Run(CheckForUpdatesAsync);

        // Create system-tray icon

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "TokenMeter — Provider Usage Monitor",
            Icon = CreateDefaultIcon(),
            ContextMenu = BuildContextMenu(),
        };

        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowMainWindow();
    }

    private async Task CheckForUpdatesAsync()
    {
        // Placeholder for Clowd.Squirrel / GitHub updates
        await Task.Delay(1000);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        base.OnExit(e);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    public void ShowMainWindow()
    {
        var disp = this.Dispatcher ?? System.Windows.Application.Current?.Dispatcher;
        if (disp != null)
        {
            disp.Invoke(() =>
            {
                try
                {
                    if (MainWindow is null)
                    {
                        if (_viewModel == null && _host != null)
                        {
                            _viewModel = _host.Services.GetRequiredService<MainViewModel>();
                        }
                        MainWindow = new MainWindow { DataContext = _viewModel };
                        try { System.Windows.Application.Current.MainWindow = MainWindow; } catch { }
                    }

                    MainWindow.ShowActivated = true;
                    MainWindow.WindowState = WindowState.Normal;
                    MainWindow.Show();
                    MainWindow.Activate();
                    MainWindow.Focus();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to show dashboard: {ex.Message}\n{ex.StackTrace}");
                }
            });
        }
    }

    public void ShowSettingsWindow()
    {
        if (_host == null) return;

        var settingsVm = _host.Services.GetRequiredService<SettingsViewModel>();
        var window = new SettingsWindow { DataContext = settingsVm };

        // Trigger initial load on a background thread to stay responsive
        Task.Run(() => settingsVm.LoadTokensCommand.Execute(null));

        try
        {
            window.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to open settings: {ex.Message}");
        }
    }

    private System.Windows.Controls.ContextMenu BuildContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var showItem = new System.Windows.Controls.MenuItem { Header = "Show _Dashboard" };
        showItem.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(showItem);

        var settingsItem = new System.Windows.Controls.MenuItem { Header = "_Settings" };
        settingsItem.Click += (_, _) => ShowSettingsWindow();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var exitItem = new System.Windows.Controls.MenuItem { Header = "E_xit" };
        exitItem.Click += (_, _) => Shutdown();
        menu.Items.Add(exitItem);

        return menu;
    }

    /// <summary>
    /// Generate a simple coloured square icon at runtime (no .ico file needed).
    /// </summary>
    private static System.Drawing.Icon CreateDefaultIcon()
    {
        using var bmp = new System.Drawing.Bitmap(16, 16);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.FromArgb(137, 180, 250)); // Catppuccin blue
        g.DrawString("T",
            new System.Drawing.Font(System.Drawing.FontFamily.GenericSansSerif, 9, System.Drawing.FontStyle.Bold),
            System.Drawing.Brushes.White,
            new System.Drawing.PointF(-1, 0));
        var hIcon = bmp.GetHicon();
        return System.Drawing.Icon.FromHandle(hIcon);
    }
}
