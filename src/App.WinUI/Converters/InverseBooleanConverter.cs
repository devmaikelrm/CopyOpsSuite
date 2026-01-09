using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace CopyOpsSuite.App.WinUI.Converters
{
    public sealed class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool b)
            {
                return !b;
            }

            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return Convert(value, targetType, parameter, language);
        }
    }
}
