using System;
using CopyOpsSuite.Core.Models;

namespace CopyOpsSuite.App.WinUI
{
    public sealed class AuditFilterState
    {
        private readonly object _sync = new();

        public Guid? JobId { get; private set; }
        public Guid? SaleId { get; private set; }
        public string? Type { get; private set; }
        public EventSeverity? Severity { get; private set; }
        public string? SearchText { get; private set; }
        public DateTime LastUpdatedUtc { get; private set; } = DateTime.MinValue;

        public event EventHandler? Changed;

        public void Set(Guid? jobId = null, Guid? saleId = null, string? type = null, EventSeverity? severity = null, string? searchText = null)
        {
            lock (_sync)
            {
                JobId = jobId;
                SaleId = saleId;
                Type = string.IsNullOrWhiteSpace(type) ? null : type.Trim();
                Severity = severity;
                SearchText = string.IsNullOrWhiteSpace(searchText) ? null : searchText.Trim();
                LastUpdatedUtc = DateTime.UtcNow;
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Clear() => Set();

        public bool HasFilters => JobId != null || SaleId != null || Type != null || Severity != null || SearchText != null;
    }
}
