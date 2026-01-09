using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CopyOpsSuite.App.WinUI;
using CopyOpsSuite.Core.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace CopyOpsSuite.App.WinUI.ViewModels
{
    public partial class JobDetailViewModel : ObservableObject
    {
        private readonly AppServices _services;

        [ObservableProperty]
        private Guid jobId;

        [ObservableProperty]
        private string status = "Preparando";

        [ObservableProperty]
        private string ramSummary = "Esperando datos de RAM...";

        [ObservableProperty]
        private string timelineSummary = string.Empty;

        [ObservableProperty]
        private string detailsSummary = string.Empty;

        public ObservableCollection<TransferItemLog> TransferItems { get; } = new();
        public ObservableCollection<ValidationLog> Validations { get; } = new();
        public ObservableCollection<ErrorLog> Errors { get; } = new();
        public ObservableCollection<RamBufferStats> RamStats { get; } = new();
        public ObservableCollection<AppEvent> TimelineEvents { get; } = new();

        public JobDetailViewModel(AppServices services)
        {
            _services = services;
        }

        public async Task LoadJobAsync(Guid id)
        {
            JobId = id;
            await LoadTransferItemsAsync(id).ConfigureAwait(false);
            await LoadValidationsAsync(id).ConfigureAwait(false);
            await LoadErrorsAsync(id).ConfigureAwait(false);
            await LoadRamStatsAsync(id).ConfigureAwait(false);
            await LoadTimelineAsync(id).ConfigureAwait(false);

            var job = await _services.Repositories.Jobs.GetByIdAsync(id).ConfigureAwait(false);
            Status = job?.Status.ToString() ?? "Pendiente";
        }

        private async Task LoadTransferItemsAsync(Guid id)
        {
            var items = await _services.Repositories.Items.GetByJobAsync(id).ConfigureAwait(false);
            await DispatchAsync(() =>
            {
                TransferItems.Clear();
                foreach (var item in items)
                {
                    TransferItems.Add(item);
                }
            });
        }

        private async Task LoadValidationsAsync(Guid id)
        {
            var validations = await _services.Repositories.Validations.GetByJobAsync(id).ConfigureAwait(false);
            await DispatchAsync(() =>
            {
                Validations.Clear();
                foreach (var validation in validations)
                {
                    Validations.Add(validation);
                }
            });
        }

        private async Task LoadErrorsAsync(Guid id)
        {
            var errors = await _services.Repositories.Errors.GetByJobAsync(id).ConfigureAwait(false);
            await DispatchAsync(() =>
            {
                Errors.Clear();
                foreach (var error in errors)
                {
                    Errors.Add(error);
                }
            });
        }

        private async Task LoadRamStatsAsync(Guid id)
        {
            var stats = await _services.Repositories.RamStats.GetByJobAsync(id).ConfigureAwait(false);
            await DispatchAsync(() =>
            {
                RamStats.Clear();
                foreach (var stat in stats.OrderByDescending(s => s.Ts).Take(120))
                {
                    RamStats.Add(stat);
                }

                var last = stats.OrderByDescending(s => s.Ts).FirstOrDefault();
                RamSummary = last == null ? "Sin datos" : $"{last.BytesBuffered / (1024d * 1024d):0.##} MB buffered · Queue {last.QueueDepth}";
            });
        }

        private async Task LoadTimelineAsync(Guid id)
        {
            var from = DateTime.UtcNow.AddDays(-7);
            var to = DateTime.UtcNow.AddHours(1);
            var events = await _services.Repositories.Events.QueryAsync(from, to).ConfigureAwait(false);
            var jobEvents = events.Where(evt => evt.JobId == id).OrderByDescending(evt => evt.Ts).ToList();
            await DispatchAsync(() =>
            {
                TimelineEvents.Clear();
                foreach (var evt in jobEvents)
                {
                    TimelineEvents.Add(evt);
                }

                TimelineSummary = jobEvents.Any() ? $"Último evento: {jobEvents.First().Ts:HH:mm}" : "Sin eventos";
            });
        }

        private Task DispatchAsync(Action action)
        {
            var tcs = new TaskCompletionSource<bool>();
            Application.Current.DispatcherQueue.TryEnqueue(() =>
            {
                action();
                tcs.SetResult(true);
            });
            return tcs.Task;
        }
    }
}
