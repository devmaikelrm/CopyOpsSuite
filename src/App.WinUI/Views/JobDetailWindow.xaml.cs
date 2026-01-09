using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CopyOpsSuite.App.WinUI.ViewModels;

namespace CopyOpsSuite.App.WinUI.Views
{
    public sealed partial class JobDetailWindow : Window
    {
        public JobDetailViewModel ViewModel { get; }

        public JobDetailWindow()
        {
            InitializeComponent();
            ViewModel = new JobDetailViewModel(App.Services);
            DataContext = ViewModel;
        }

        public async Task LoadJobAsync(Guid jobId)
        {
            await ViewModel.LoadJobAsync(jobId);
        }
    }
}
