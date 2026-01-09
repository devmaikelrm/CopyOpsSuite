using CopyOpsSuite.App.WinUI.ViewModels;
using CopyOpsSuite.App.WinUI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using System;
using CopyOpsSuite.App.WinUI.Services;
using System.IO;

namespace CopyOpsSuite.App.WinUI
{
    public sealed partial class MainWindow : Window
    {
        private readonly ShellViewModel _shellViewModel = new();
        private readonly TrayIconService _trayIcon;

        public MainWindow()
        {
            InitializeComponent();
            ApplyWindowIcon();
            DataContext = _shellViewModel;
            _shellViewModel.PropertyChanged += ShellViewModel_PropertyChanged;
            NavigateTo(_shellViewModel.CurrentPageType);
            _trayIcon = new TrayIconService(App.Services, this);
            Closing += MainWindow_Closing;
            AppWindow.Changed += AppWindow_Changed;
        }

        private void NavigateTo(Type? pageType)
        {
            if (pageType == null || ContentFrame.CurrentSourcePageType == pageType)
            {
                return;
            }

            ContentFrame.Navigate(pageType);
        }

        private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItemContainer?.Tag is string tag)
            {
                _shellViewModel.SelectedTag = tag;
            }
        }

        private void ShellViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ShellViewModel.CurrentPageType))
            {
                NavigateTo(_shellViewModel.CurrentPageType);
            }
        }

        private void MainWindow_Closing(object sender, WindowClosingEventArgs args)
        {
            if (_trayIcon.IsExitRequested)
            {
                return;
            }

            args.Cancel = true;
            _trayIcon.HideWindow();
        }

        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (sender.Presenter is OverlappedPresenter presenter
                && presenter.State == OverlappedPresenterState.Minimized
                && !_trayIcon.IsExitRequested)
            {
                _trayIcon.HideWindow();
            }
        }

        private void ApplyWindowIcon()
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "CopyOpsSuite.ico");
            if (File.Exists(iconPath))
            {
                AppWindow.SetIcon(iconPath);
            }
        }
    }
}
