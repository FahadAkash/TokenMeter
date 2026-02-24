using System;
using System.Globalization;
using System.Windows.Data;
using TokenMeter.Core.Models;

namespace TokenMeter.UI.Converters;

public class ProviderToUrlConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is UsageProvider provider)
        {
            return provider switch
            {
                UsageProvider.Claude => "https://claude.ai/login",
                UsageProvider.ChatGPT => "https://chatgpt.com/auth/login",
                UsageProvider.Cursor => "https://cursor.com/login",
                UsageProvider.Copilot => "https://github.com/login",
                _ => string.Empty
            };
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
