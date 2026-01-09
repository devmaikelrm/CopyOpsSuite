using System;
using CopyOpsSuite.App.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CopyOpsSuite.App.WinUI.Views
{
    public sealed partial class HistoryView : Page
    {
        public HistoryViewModel ViewModel { get; }

        public HistoryView()
        {
            InitializeComponent();
            ViewModel = new HistoryViewModel(App.Services);
            DataContext = ViewModel;
        }

        private void OnHistoryOpenJob(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.DataContext is JobHistoryRow row)
            {
                _ = App.Services.ShowJobDetailAsync(row.JobId);
            }
        }

        private void OnHistoryOpenSale(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.DataContext is JobHistoryRow row && row.SaleId.HasValue)
            {
                App.Services.CashSelectionState.FocusSale(row.SaleId.Value, row.JobId);
                if (App.MainWindowInstance?.DataContext is ViewModels.ShellViewModel shell)
                {
                    shell.SelectedTag = "Cash";
                }
            }
        }

        private void OnHistoryOpenEvents(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.DataContext is JobHistoryRow row)
            {
                App.Services.AuditFilterState.Set(jobId: row.JobId);
                if (App.MainWindowInstance?.DataContext is ViewModels.ShellViewModel shell)
                {
                    shell.SelectedTag = "Audit";
                }
            }
        }
    }
}
