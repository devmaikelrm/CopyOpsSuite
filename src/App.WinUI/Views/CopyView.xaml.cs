using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using CopyOpsSuite.App.WinUI.Views.Controls;

namespace CopyOpsSuite.App.WinUI.Views
{
    public sealed partial class CopyView : Page
    {
        public CopyViewModel ViewModel { get; }

        public CopyView()
        {
            InitializeComponent();
            ViewModel = new CopyViewModel(App.Services);
            DataContext = ViewModel;
        }

        private async void BrowseSource_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker();
            picker.SuggestedStartLocation = PickerLocationId.Desktop;
            picker.FileTypeFilter.Add("*");

            var hwnd = WindowNative.GetWindowHandle(App.MainWindowInstance ?? throw new InvalidOperationException("La ventana principal no esta disponible."));
            InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();
            if (folder is not null)
            {
                ViewModel.SourcePath = folder.Path;
                ViewModel.Log($"Origen seleccionado: {folder.Path}");
            }
        }

        private async void SourceDropZone_Drop(object sender, DragEventArgs e)
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                return;
            }

            var items = await e.DataView.GetStorageItemsAsync();
            var folder = items?.OfType<StorageFolder>().FirstOrDefault();
            if (folder is not null)
            {
                ViewModel.SourcePath = folder.Path;
                ViewModel.Log($"Origen arrastrado: {folder.Path}");
            }
        }

        private void SourceDropZone_DragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private async void Simulate_Click(object sender, RoutedEventArgs e)
        {
            var report = await ViewModel.BuildSimulationAsync();
            if (report == null)
            {
                return;
            }

            var dialog = new SimulationDialog();
            dialog.XamlRoot = XamlRoot;
            dialog.SetReport(report.Summary, report.Results);
            await dialog.ShowAsync();
        }

        private async void AddFiles_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.IsJobRunning)
            {
                return;
            }

            var selector = new ContentDialog
            {
                Title = "Agregar archivos",
                Content = "Selecciona si quieres agregar archivos sueltos o una carpeta.",
                PrimaryButtonText = "Archivos",
                SecondaryButtonText = "Carpeta",
                CloseButtonText = "Cancelar",
                XamlRoot = XamlRoot
            };

            var choice = await selector.ShowAsync();
            if (choice == ContentDialogResult.Primary)
            {
                var picker = new FileOpenPicker();
                picker.FileTypeFilter.Add("*");
                var hwnd = WindowNative.GetWindowHandle(App.MainWindowInstance ?? throw new InvalidOperationException("La ventana principal no esta disponible."));
                InitializeWithWindow.Initialize(picker, hwnd);
                var files = await picker.PickMultipleFilesAsync();
                if (files.Count > 0)
                {
                    await ViewModel.AddPathsAsync(files.Select(f => f.Path).ToList());
                }
            }
            else if (choice == ContentDialogResult.Secondary)
            {
                var picker = new FolderPicker();
                picker.FileTypeFilter.Add("*");
                var hwnd = WindowNative.GetWindowHandle(App.MainWindowInstance ?? throw new InvalidOperationException("La ventana principal no esta disponible."));
                InitializeWithWindow.Initialize(picker, hwnd);
                var folder = await picker.PickSingleFolderAsync();
                if (folder != null)
                {
                    await ViewModel.AddPathsAsync(new List<string> { folder.Path });
                }
            }
        }
    }
}
