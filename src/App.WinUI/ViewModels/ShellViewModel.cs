using CommunityToolkit.Mvvm.ComponentModel;
using CopyOpsSuite.App.WinUI.Views;
using System;
using System.Collections.Generic;

namespace CopyOpsSuite.App.WinUI.ViewModels
{
    public partial class ShellViewModel : ObservableObject
    {
        private static readonly IReadOnlyDictionary<string, Type> PageMap = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            ["Copy"] = typeof(CopyView),
            ["Cash"] = typeof(CashView),
            ["Audit"] = typeof(AuditView),
            ["History"] = typeof(HistoryView),
            ["Settings"] = typeof(SettingsView)
        };

        [ObservableProperty]
        private string header = "CopyOps Suite";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CurrentPageType))]
        private string selectedTag = "Copy";

        public Type? CurrentPageType => PageMap.TryGetValue(SelectedTag, out var pageType) ? pageType : null;
    }
}
