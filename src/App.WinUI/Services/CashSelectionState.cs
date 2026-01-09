using System;

namespace CopyOpsSuite.App.WinUI
{
    public sealed class CashSelectionState
    {
        private readonly object _sync = new();
        private Guid? _saleId;
        private Guid? _jobId;

        public Guid? SaleId
        {
            get
            {
                lock (_sync)
                {
                    return _saleId;
                }
            }
        }

        public Guid? JobId
        {
            get
            {
                lock (_sync)
                {
                    return _jobId;
                }
            }
        }

        public bool HasFocus
        {
            get
            {
                lock (_sync)
                {
                    return _saleId != null;
                }
            }
        }

        public DateTime LastUpdatedUtc { get; private set; } = DateTime.MinValue;

        public event EventHandler? FocusChanged;

        public void FocusSale(Guid saleId, Guid jobId)
        {
            lock (_sync)
            {
                _saleId = saleId;
                _jobId = jobId;
                LastUpdatedUtc = DateTime.UtcNow;
            }

            FocusChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Clear()
        {
            lock (_sync)
            {
                _saleId = null;
                _jobId = null;
                LastUpdatedUtc = DateTime.UtcNow;
            }

            FocusChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
