using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace CopyOpsSuite.App.WinUI.Converters
{
    public sealed class BoolToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var highlightBrush = Application.Current.Resources["SystemControlHighlightListAccentLowBrush"] as Brush
                                 ?? Application.Current.Resources["SystemControlHighlightListAccentMediumBrush"] as Brush;
            var baseBrush = Application.Current.Resources["SystemControlBackgroundBaseLowBrush"] as Brush
                            ?? Application.Current.Resources["SystemControlBackgroundAltLowBrush"] as Brush;

            if (value is bool flag && flag)
            {
                return highlightBrush ?? new SolidColorBrush(Colors.LightGoldenrodYellow);
            }

            return baseBrush ?? new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return DependencyProperty.UnsetValue;
        }
    }
}
