using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using TokenMeter.UI.ViewModels;

namespace TokenMeter.UI;

public partial class App : System.Windows.Application
{
    private TaskbarIcon? _trayIcon;
    private MainViewModel? _viewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Commenting out Squirrel for now to fix build issues on .NET 8
        // SquirrelAwareApp.HandleEvents(
        //     onInitialInstall: v => _trayIcon?.ShowBalloonTip("TokenMeter Installed", $"Version {v} installed successfully.", BalloonIcon.Info),
        //     onAppUpdate: v => _trayIcon?.ShowBalloonTip("TokenMeter Updated", $"TokenMeter was updated to version {v}.", BalloonIcon.Info),
        //     onAppUninstall: v => _trayIcon?.Dispose()
        // );

        // Check for updates in the background
        Task.Run(CheckForUpdatesAsync);

        _viewModel = new MainViewModel();

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
        // try
        // {
        //     using var mgr = new UpdateManager("https://github.com/example/TokenMeter/releases/latest");
        //     var releaseEntry = await mgr.UpdateApp();
        //     if (releaseEntry != null)
        //     {
        //         _trayIcon?.ShowBalloonTip("Update Ready", "TokenMeter has been updated in the background. It will apply on next restart.", BalloonIcon.Info);
        //     }
        // }
        // catch (Exception ex)
        // {
        //     System.Diagnostics.Debug.WriteLine($"Squirrel update failed: {ex.Message}");
        // }
        await Task.CompletedTask;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private System.Windows.Controls.ContextMenu BuildContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var showItem = new System.Windows.Controls.MenuItem { Header = "Show _Dashboard" };
        showItem.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(showItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var exitItem = new System.Windows.Controls.MenuItem { Header = "E_xit" };
        exitItem.Click += (_, _) => Shutdown();
        menu.Items.Add(exitItem);

        return menu;
    }

    private void ShowMainWindow()
    {
        if (MainWindow is null)
        {
            MainWindow = new MainWindow { DataContext = _viewModel };
        }

        MainWindow.Show();
        MainWindow.Activate();
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
