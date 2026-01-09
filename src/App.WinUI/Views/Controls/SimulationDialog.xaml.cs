using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;
using CopyOpsSuite.App.WinUI.ViewModels;

namespace CopyOpsSuite.App.WinUI.Views.Controls
{
    public sealed partial class SimulationDialog : ContentDialog
    {
        public SimulationDialog()
        {
            InitializeComponent();
        }

        public void SetReport(string summary, IReadOnlyList<SimulationResult> results)
        {
            SummaryText.Text = summary;
            ResultsList.ItemsSource = results;
        }
    }
}
