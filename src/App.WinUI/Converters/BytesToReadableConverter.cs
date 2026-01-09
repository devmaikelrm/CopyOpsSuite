using System;
using Microsoft.UI.Xaml.Data;

namespace CopyOpsSuite.App.WinUI.Converters
{
    public sealed class BytesToReadableConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            double bytes = value switch
            {
                long l => l,
                int i => i,
                double d => d,
                float f => f,
                _ => 0
            };

            return FormatBytes(bytes);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return Binding.DoNothing;
        }

        private static string FormatBytes(double bytes)
        {
            if (bytes < 0)
            {
                bytes = 0;
            }

            var units = new[] { "B", "KB", "MB", "GB", "TB" };
            var order = 0;
            while (bytes >= 1024 && order < units.Length - 1)
            {
                order++;
                bytes /= 1024;
            }

            return $"{bytes:0.##} {units[order]}";
        }
    }
}
