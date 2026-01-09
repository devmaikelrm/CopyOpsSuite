using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace CopyOpsSuite.App.WinUI.Converters
{
    public sealed class HealthBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value switch
            {
                ViewModels.HealthState health => health switch
                {
                    ViewModels.HealthState.Critical => new SolidColorBrush(Windows.UI.Colors.IndianRed),
                    ViewModels.HealthState.Warn => new SolidColorBrush(Windows.UI.Colors.Goldenrod),
                    _ => new SolidColorBrush(Windows.UI.Colors.Transparent)
                },
                _ => new SolidColorBrush(Windows.UI.Colors.Transparent)
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => Binding.DoNothing;
    }
}
