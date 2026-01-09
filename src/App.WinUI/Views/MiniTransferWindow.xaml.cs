using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CopyOpsSuite.App.WinUI.ViewModels;

namespace CopyOpsSuite.App.WinUI.Views
{
    public sealed partial class MiniTransferWindow : Window
    {
        public MiniTransferViewModel ViewModel { get; }

        public MiniTransferWindow()
        {
            InitializeComponent();
            ViewModel = new MiniTransferViewModel();
            DataContext = ViewModel;
        }

        public void Bind(DeviceRowViewModel row)
        {
            ViewModel.UpdateFromRow(row);
        }
    }
}
