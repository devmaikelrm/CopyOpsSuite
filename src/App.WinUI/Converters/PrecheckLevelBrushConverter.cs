using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using CopyOpsSuite.App.WinUI.ViewModels;

namespace CopyOpsSuite.App.WinUI.Converters
{
    public sealed class PrecheckLevelBrushConverter : IValueConverter
    {
        private static readonly Brush PassBrush = new SolidColorBrush(Color.FromArgb(255, 198, 239, 206));
        private static readonly Brush WarnBrush = new SolidColorBrush(Color.FromArgb(255, 255, 229, 153));
        private static readonly Brush FailBrush = new SolidColorBrush(Color.FromArgb(255, 255, 199, 206));

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is PrecheckResultLevel level)
            {
                return level switch
                {
                    PrecheckResultLevel.Fail => FailBrush,
                    PrecheckResultLevel.Warn => WarnBrush,
                    _ => PassBrush
                };
            }

            return PassBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return Binding.DoNothing;
        }
    }
}
