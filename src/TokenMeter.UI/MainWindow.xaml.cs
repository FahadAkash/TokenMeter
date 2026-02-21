using System.Windows;
using System.Windows.Input;

namespace TokenMeter.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Allows dragging the custom window since WindowStyle=None
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ((App)System.Windows.Application.Current).ShowSettingsWindow();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // Only hide, let the tray icon handle closing
        Hide();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}