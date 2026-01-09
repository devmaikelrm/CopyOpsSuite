using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using CopyOpsSuite.App.WinUI.ViewModels;

namespace CopyOpsSuite.App.WinUI.Converters
{
    public sealed class StateBrushConverter : IValueConverter
    {
        private static readonly Brush IdleBrush = new SolidColorBrush(Color.FromArgb(255, 221, 226, 255));
        private static readonly Brush BusyBrush = new SolidColorBrush(Color.FromArgb(255, 199, 230, 255));
        private static readonly Brush PausedBrush = new SolidColorBrush(Color.FromArgb(255, 255, 230, 180));
        private static readonly Brush DoneBrush = new SolidColorBrush(Color.FromArgb(255, 200, 255, 215));

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is TransferStateLabel state)
            {
                return state switch
                {
                    TransferStateLabel.Reading => BusyBrush,
                    TransferStateLabel.Writing => BusyBrush,
                    TransferStateLabel.BufferWait => BusyBrush,
                    TransferStateLabel.Paused => PausedBrush,
                    TransferStateLabel.DoneOk or TransferStateLabel.DoneErrors => DoneBrush,
                    _ => IdleBrush
                };
            }

            return IdleBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => Binding.DoNothing;
    }
}
