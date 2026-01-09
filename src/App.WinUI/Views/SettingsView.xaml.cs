using CopyOpsSuite.App.WinUI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace CopyOpsSuite.App.WinUI.Views
{
    public sealed partial class SettingsView : Page
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsView()
        {
            InitializeComponent();
            ViewModel = new SettingsViewModel(App.Services);
            DataContext = ViewModel;
        }

        private void OnTierCellEditEnded(object sender, CommunityToolkit.WinUI.UI.Controls.DataGridCellEditEndedEventArgs e)
        {
            ViewModel.RevalidateTiers();
        }
    }
}
