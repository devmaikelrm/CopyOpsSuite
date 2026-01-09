using System;
using System.Collections.Generic;
using System.Linq;

namespace CopyOpsSuite.App.WinUI
{
    public sealed class CopySelectionState
    {
        private readonly object _sync = new();
        private List<string> _selectedDeviceIds = new();

        public string SourcePath { get; private set; } = string.Empty;
        public IReadOnlyList<string> SelectedDeviceIds
        {
            get
            {
                lock (_sync)
                {
                    return _selectedDeviceIds.AsReadOnly();
                }
            }
        }

        public DateTime LastUpdatedUtc { get; private set; } = DateTime.MinValue;

        public event EventHandler? SelectionChanged;

        public void Update(string sourcePath, IReadOnlyList<string>? deviceIds)
        {
            IReadOnlyList<string> snapshot;
            lock (_sync)
            {
                SourcePath = sourcePath?.Trim() ?? string.Empty;
                _selectedDeviceIds = (deviceIds ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                LastUpdatedUtc = DateTime.UtcNow;
                snapshot = _selectedDeviceIds.AsReadOnly();
            }

            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
