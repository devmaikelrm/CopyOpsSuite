using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CopyOpsSuite.App.WinUI.ViewModels;

namespace CopyOpsSuite.App.WinUI.Views.Controls
{
    public sealed partial class PrecheckDialog : ContentDialog
    {
        public bool ForceRequested { get; private set; }

        public PrecheckDialog()
        {
            InitializeComponent();
        }

        public void SetItems(IReadOnlyList<PrecheckDisplayItem> items)
        {
            PrecheckList.ItemsSource = items;
        }

        public void ToggleForceButton(bool isEnabled)
        {
            ForceButton.IsEnabled = isEnabled;
            ForceButton.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
        }

        public void ResetForceFlag()
        {
            ForceRequested = false;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            ForceRequested = false;
            Hide();
        }

        private void Force_Click(object sender, RoutedEventArgs e)
        {
            ForceRequested = true;
            Hide();
        }
    }
}
