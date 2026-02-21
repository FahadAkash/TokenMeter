using System.Windows;
using System.Windows.Input;

namespace TokenMeter.UI
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CopyAuthCode_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.SettingsViewModel vm && !string.IsNullOrEmpty(vm.AuthCode))
            {
                System.Windows.Clipboard.SetText(vm.AuthCode);
            }
        }
    }
}
