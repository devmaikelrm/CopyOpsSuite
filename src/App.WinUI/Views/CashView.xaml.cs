using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CopyOpsSuite.App.WinUI.ViewModels;
using CopyOpsSuite.App.WinUI.Views;

namespace CopyOpsSuite.App.WinUI.Views
{
    public sealed partial class CashView : Page
    {
        public CashViewModel ViewModel { get; }

        public CashView()
        {
            InitializeComponent();
            ViewModel = new CashViewModel(App.Services);
            DataContext = ViewModel;
        }

        private void OnRowOpenJob(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.DataContext is SaleRowViewModel row)
            {
                _ = App.Services.ShowJobDetailAsync(row.Sale.JobId);
            }
        }

        private void OnRowOpenAudit(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.DataContext is SaleRowViewModel row)
            {
                App.Services.AuditFilterState.Set(jobId: row.Sale.JobId, saleId: row.Sale.SaleId);
                if (App.MainWindowInstance?.DataContext is ViewModels.ShellViewModel shell)
                {
                    shell.SelectedTag = "Audit";
                }
            }
        }

        private void OnDiagEventsClick(object sender, RoutedEventArgs e)
        {
            App.Services.AuditFilterState.Set(type: "DIAG_BUFFER_SUMMARY");
            if (App.MainWindowInstance?.DataContext is ViewModels.ShellViewModel shell)
            {
                shell.SelectedTag = "Audit";
            }
        }
    }
}
