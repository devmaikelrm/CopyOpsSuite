using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using CopyOpsSuite.Core.Models;

namespace CopyOpsSuite.App.WinUI.ViewModels
{
    public partial class HistoryViewModel : ObservableObject
    {
        private readonly AppServices _services;

        public ObservableCollection<JobHistoryRow> Jobs { get; } = new();

        [ObservableProperty]
        private string statusMessage = "Cargando historial...";

        public HistoryViewModel(AppServices services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _ = RefreshHistoryAsync();
        }

        [RelayCommand]
        private async Task RefreshHistoryAsync()
        {
            await InvokeOnDispatcherAsync(() => StatusMessage = "Cargando historial...").ConfigureAwait(false);
            var jobs = (await _services.Repositories.Jobs.GetAllAsync().ConfigureAwait(false))
                .Where(j => j.Status == JobStatus.Completed || j.Status == JobStatus.Error)
                .OrderByDescending(j => j.StartedAt)
                .ToList();

            var rows = new List<JobHistoryRow>();
            foreach (var job in jobs)
            {
                var targets = await _services.Repositories.Targets.GetByJobAsync(job.JobId).ConfigureAwait(false);
                var sale = await _services.Repositories.Sales.GetByJobAsync(job.JobId).ConfigureAwait(false);
                var snapshots = targets.Select(t => new TargetSnapshotRow(t)).ToList();
                rows.Add(new JobHistoryRow(job, targets.Count(), sale, snapshots));
            }

            await InvokeOnDispatcherAsync(() =>
            {
                Jobs.Clear();
                foreach (var row in rows)
                {
                    Jobs.Add(row);
                }

                StatusMessage = $"Mostrando {rows.Count} jobs.";
            }).ConfigureAwait(false);
        }

        private Task InvokeOnDispatcherAsync(Action action)
        {
            var dispatcher = DispatcherQueue.GetForCurrentThread();
            if (dispatcher == null)
            {
                action();
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<bool>();
            dispatcher.TryEnqueue(() =>
            {
                action();
                tcs.SetResult(true);
            });
            return tcs.Task;
        }
    }

    public sealed class JobHistoryRow
    {
        public TransferJob Job { get; }
        public int TargetCount { get; }
        public IReadOnlyList<TargetSnapshotRow> TargetSnapshots { get; }
        public Guid JobId => Job.JobId;
        public string Status => Job.Status.ToString();
        public string Source => string.IsNullOrWhiteSpace(Job.SourcePath) ? "Sin origen" : Path.GetFileName(Job.SourcePath);
        public long BytesOk => Job.BytesOk;
        public long BytesFailed => Job.BytesFailed;
        public decimal ExpectedAmount { get; }
        public decimal PaidAmount { get; }
        public decimal Difference => ExpectedAmount - PaidAmount;
        public bool HasPaymentGap => Math.Abs(Difference) > 0.01m;
        public string Currency => "CUP+";
        public Guid? SaleId => Sale?.SaleId;
        public string StartedAt => Job.StartedAt.ToString("g");
        public string Summary => $"{TargetCount} destinos - OK {BytesOk:N0} B - Err {BytesFailed:N0} B";

        private readonly Sale? Sale;

        public JobHistoryRow(TransferJob job, int targetCount, Sale? sale, IReadOnlyList<TargetSnapshotRow> snapshots)
        {
            Job = job;
            TargetCount = targetCount;
            Sale = sale;
            ExpectedAmount = sale?.AmountExpected ?? 0;
            PaidAmount = sale?.AmountPaid ?? 0;
            TargetSnapshots = snapshots;
        }
    }

    public sealed class TargetSnapshotRow
    {
        public string DeviceId { get; }
        public string Status { get; }
        public long BytesOk { get; }
        public long BytesFailed { get; }
        public bool HasErrors => BytesFailed > 0;
        public string Summary => $"{DeviceId} OK {BytesOk / (1024d * 1024d):0.##} MB Err {BytesFailed / (1024d * 1024d):0.##} MB";

        public TargetSnapshotRow(TransferTarget target)
        {
            DeviceId = target.DeviceId;
            Status = target.Status.ToString();
            BytesOk = target.BytesOk;
            BytesFailed = target.BytesFailed;
        }
    }
}
