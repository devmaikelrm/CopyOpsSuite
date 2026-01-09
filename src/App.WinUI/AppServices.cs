using System.Threading;
using System.Threading.Tasks;
using CopyOpsSuite.AuditOps;
using CopyOpsSuite.Core.Models;
using CopyOpsSuite.MultiCopyEngine;
using CopyOpsSuite.Storage;
using CopyOpsSuite.System;
using CopyOpsSuite.App.WinUI.Views;
using System;
using CopyOpsSuite.Core.System;

namespace CopyOpsSuite.App.WinUI
{
    public sealed class AppServices
    {
        public SqliteDb Database { get; }
        public Repositories Repositories { get; }
        public SettingsService SettingsService { get; }
        public DriveWatcher DriveWatcher { get; }
        public RamMonitor RamMonitor { get; }
        public EventBus EventBus { get; }
        public AuditService AuditService { get; }
        public Verifier Verifier { get; }
        public Encryptor Encryptor { get; }
        public AlertService AlertService { get; }
        public SessionService SessionService { get; }
        public Engine MultiCopyEngine { get; }
        public CopySelectionState SelectionState { get; } = new();
        public CashSelectionState CashSelectionState { get; } = new();
        public AuditFilterState AuditFilterState { get; } = new();

        public event EventHandler? DiagnosticsRequested;
        public event EventHandler? PricingTiersUpdated;

        private readonly CancellationTokenSource _eventSinkCts = new();
        private Task? _eventSinkTask;

        public AppServices()
        {
            Database = new SqliteDb();
            Repositories = new Repositories(Database);
            SettingsService = new SettingsService(Repositories.Settings);
            DriveWatcher = new DriveWatcher(Repositories);
            RamMonitor = new RamMonitor();
            EventBus = new EventBus();
            AuditService = new AuditService(EventBus);
            Verifier = new Verifier();
            Encryptor = new Encryptor();
            AlertService = new AlertService(Repositories.Alerts, EventBus);
            SessionService = new SessionService(Repositories);
            MultiCopyEngine = new Engine(Repositories, SettingsService, AuditService, AlertService, Verifier, Encryptor);
        }

        public void Start()
        {
            DriveWatcher.Start();
            RamMonitor.Start();
            _eventSinkTask ??= Task.Run(() => ProcessEventsAsync(_eventSinkCts.Token));
        }

        private async Task ProcessEventsAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                AppEvent? evt = null;
                try
                {
                    evt = EventBus.Events.Take(token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (evt == null)
                {
                    continue;
                }

                try
                {
                    await Repositories.Events.AddAsync(evt).ConfigureAwait(false);
                }
                catch
                {
                    // Swallow; logging will happen elsewhere.
                }
            }
        }

        public void Stop()
        {
            _eventSinkCts.Cancel();
            DriveWatcher.Stop();
            RamMonitor.Stop();
        }

        public async Task ShowJobDetailAsync(Guid jobId)
        {
            var window = new JobDetailWindow();
            await window.LoadJobAsync(jobId).ConfigureAwait(false);
            window.Activate();
        }

        public void RequestDiagnostics()
        {
            DiagnosticsRequested?.Invoke(this, EventArgs.Empty);
        }

        public void NotifyPricingTiersUpdated()
        {
            PricingTiersUpdated?.Invoke(this, EventArgs.Empty);
        }
    }
}
