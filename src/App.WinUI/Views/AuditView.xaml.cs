using System;
using CopyOpsSuite.App.WinUI.ViewModels;
using CopyOpsSuite.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace CopyOpsSuite.App.WinUI.Views
{
    public sealed partial class AuditView : Page
    {
        public AuditViewModel ViewModel { get; }

        public AuditView()
        {
            InitializeComponent();
            ViewModel = new AuditViewModel(App.Services);
            DataContext = ViewModel;
        }

        private void OnEventOpenJob(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.DataContext is AppEvent evt && evt.JobId.HasValue)
            {
                _ = App.Services.ShowJobDetailAsync(evt.JobId.Value);
            }
        }

        private void OnEventOpenSale(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.DataContext is AppEvent evt && evt.SaleId.HasValue)
            {
                App.Services.CashSelectionState.FocusSale(evt.SaleId.Value, evt.JobId ?? Guid.Empty);
                if (App.MainWindowInstance?.DataContext is ViewModels.ShellViewModel shell)
                {
                    shell.SelectedTag = "Cash";
                }
            }
        }

        private void OnEventCopyDetail(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.DataContext is AppEvent evt)
            {
                var pack = new DataPackage();
                pack.SetText($"[{evt.Ts:yyyy-MM-dd HH:mm:ss}] {evt.Type}: {evt.Message}");
                Clipboard.SetContent(pack);
            }
        }

        private void OnAlertOpenJob(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Guid jobId)
            {
                _ = App.Services.ShowJobDetailAsync(jobId);
            }
        }

        private async void OnAlertOpenSale(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Guid jobId)
            {
                var sale = await App.Services.Repositories.Sales.GetByJobAsync(jobId);
                if (sale != null)
                {
                    App.Services.CashSelectionState.FocusSale(sale.SaleId, jobId);
                }

                if (App.MainWindowInstance?.DataContext is ViewModels.ShellViewModel shell)
                {
                    shell.SelectedTag = "Cash";
                }
            }
        }
    }
}
