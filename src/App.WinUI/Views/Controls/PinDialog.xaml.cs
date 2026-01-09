using Microsoft.UI.Xaml.Controls;

namespace CopyOpsSuite.App.WinUI.Views.Controls
{
    public sealed partial class PinDialog : ContentDialog
    {
        public PinDialog()
        {
            InitializeComponent();
        }

        public string Pin => PinBox.Password;
    }
}
