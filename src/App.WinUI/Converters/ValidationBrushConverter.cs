using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace CopyOpsSuite.App.WinUI.Converters
{
    public sealed class ValidationBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isValid && !isValid)
            {
                return Application.Current.Resources["SystemControlCriticalBaseLowBrush"] as Brush
                       ?? new SolidColorBrush(Colors.MistyRose);
            }

            return Application.Current.Resources["SystemControlBackgroundBaseLowBrush"] as Brush
                   ?? new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return DependencyProperty.UnsetValue;
        }
    }
}
