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
    public partial class AuditViewModel : ObservableObject
    {
        private readonly AppServices _services;
        private readonly DispatcherQueue _dispatcher;
        private bool _isRefreshingEvents;

        public ObservableCollection<AppEvent> Events { get; } = new();
        public ObservableCollection<AlertRecord> ActiveAlerts { get; } = new();
        public ObservableCollection<AlertRecord> ResolvedAlerts { get; } = new();
        public ObservableCollection<SimulationDestinationItem> SimulationTargets { get; } = new();
        public ObservableCollection<SimulationResult> SimulationResults { get; } = new();

        [ObservableProperty]
        private DateTime eventsFrom = DateTime.Today.AddDays(-7);

        [ObservableProperty]
        private DateTime eventsTo = DateTime.Today.AddDays(1);

        [ObservableProperty]
        private string? selectedType;

        [ObservableProperty]
        private EventSeverity? selectedSeverity;

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private bool showResolvedAlerts;

        [ObservableProperty]
        private string latestDiagSummary = string.Empty;

        [ObservableProperty]
        private DateTime? latestDiagTimestamp;

        [ObservableProperty]
        private string simulationSourcePath = string.Empty;

        [ObservableProperty]
        private string simulationStatus = "Listo";

        [ObservableProperty]
        private string simulationSummary = string.Empty;

        [ObservableProperty]
        private bool isSimulating;

        [ObservableProperty]
        private double simulationUsb2Baseline;

        [ObservableProperty]
        private double simulationUsb3Baseline;

        [ObservableProperty]
        private double simulationUnknownBaseline;

        public string LatestDiagDisplay =>
            string.IsNullOrWhiteSpace(LatestDiagSummary)
                ? "Sin diagnósticos recientes."
                : LatestDiagTimestamp.HasValue
                    ? $"{LatestDiagTimestamp:dd/MM HH:mm} - {LatestDiagSummary}"
                    : LatestDiagSummary;

        public IReadOnlyList<TextOption> EventTypeOptions { get; } = new List<TextOption>
        {
            new("Todos", null),
            new("Transferencias", "TRANSFER"),
            new("USB", "USB"),
            new("Cierre/Cash", "CASH"),
            new("Errores", "ERROR"),
            new("Sistema", "SYSTEM"),
            new("Diagnóstico", "DIAG"),
            new("Alertas", "ALERT"),
            new("Simulación", "SIMULATION")
        };

        public IReadOnlyList<SeverityOption> SeverityOptions { get; } = new List<SeverityOption>
        {
            new("Todos", null),
            new("Info", EventSeverity.Info),
            new("Advertencia", EventSeverity.Warn),
            new("Crítico", EventSeverity.Critical)
        };

        public AuditViewModel(AppServices services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _dispatcher = DispatcherQueue.GetForCurrentThread() ?? throw new InvalidOperationException("DispatcherQueue no disponible.");
            SimulationUsb2Baseline = services.SettingsService.SimulationSettings.Usb2BaselineMbps;
            SimulationUsb3Baseline = services.SettingsService.SimulationSettings.Usb3BaselineMbps;
            SimulationUnknownBaseline = services.SettingsService.SimulationSettings.UnknownBaselineMbps;
            SimulationSourcePath = _services.SelectionState.SourcePath;
            _services.AuditFilterState.Changed += AuditFilterState_Changed;
            _services.SelectionState.SelectionChanged += (_, _) => SimulationSourcePath = _services.SelectionState.SourcePath;
            _services.DriveWatcher.DevicesChanged += DriveWatcher_DevicesChanged;
            _ = RefreshEventsAsync();
            _ = LoadAlertsAsync();
            _ = LoadSimulationTargetsAsync();
        }

        partial void OnShowResolvedAlertsChanged(bool value)
        {
            if (value)
            {
                _ = LoadResolvedAlertsAsync();
            }
            else
            {
                ResolvedAlerts.Clear();
            }
        }

        partial void OnLatestDiagSummaryChanged(string value) => OnPropertyChanged(nameof(LatestDiagDisplay));
        partial void OnLatestDiagTimestampChanged(DateTime? value) => OnPropertyChanged(nameof(LatestDiagDisplay));

        [RelayCommand]
        private async Task RefreshEventsAsync()
        {
            if (_isRefreshingEvents)
            {
                return;
            }

            _isRefreshingEvents = true;
            try
            {
                await RefreshEventsCoreAsync().ConfigureAwait(false);
            }
            finally
            {
                _isRefreshingEvents = false;
            }
        }

        [RelayCommand]
        private async Task ResetEventFiltersAsync()
        {
            EventsFrom = DateTime.Today.AddDays(-7);
            EventsTo = DateTime.Today.AddDays(1);
            SelectedType = null;
            SelectedSeverity = null;
            SearchText = string.Empty;
            _services.AuditFilterState.Clear();
            await RefreshEventsAsync().ConfigureAwait(false);
        }

        [RelayCommand]
        private async Task ResolveAlertAsync(AlertRecord alert)
        {
            if (alert == null)
            {
                return;
            }

            await _services.AlertService.MarkResolvedAsync(alert.AlertId).ConfigureAwait(false);
            await LoadAlertsAsync().ConfigureAwait(false);
        }

        [RelayCommand]
        private async Task RunSimulationAsync()
        {
            if (IsSimulating)
            {
                return;
            }

            var source = SimulationSourcePath?.Trim();
            if (string.IsNullOrWhiteSpace(source))
            {
                SimulationStatus = "Selecciona la carpeta de origen.";
                return;
            }

            if (!Directory.Exists(source))
            {
                SimulationStatus = "La ruta de origen no existe.";
                return;
            }

            var targets = SimulationTargets.Where(t => t.IsSelected).ToList();
            if (!targets.Any())
            {
                SimulationStatus = "Selecciona al menos un destino para la simulación.";
                return;
            }

            IsSimulating = true;
            SimulationStatus = "Calculando estimación...";
            SimulationResults.Clear();

            try
            {
                var metadata = await EstimateSourceMetadataAsync(source).ConfigureAwait(false);
                if (metadata.TotalBytes == 0)
                {
                    SimulationStatus = "No se detectaron archivos en la ruta.";
                    return;
                }

                var perTargetBytes = metadata.TotalBytes / targets.Count;
                var settings = _services.SettingsService.SimulationSettings;
                var results = new List<SimulationResult>();

                foreach (var target in targets)
                {
                    var baseline = DetermineBaseline(target.BusHint, settings);
                    var seconds = perTargetBytes / (baseline * 1024 * 1024);
                    var eta = TimeSpan.FromSeconds(Math.Max(1, seconds));
                    var plannedGb = ConvertBytesToGb(perTargetBytes);
                    var result = new SimulationResult(target.DeviceId, target.DisplayName, target.BusHint, baseline, plannedGb, eta);
                    results.Add(result);
                }

                var slowest = results.OrderByDescending(r => r.Eta).FirstOrDefault();
                if (slowest != null)
                {
                    slowest.IsSlowest = true;
                }

                var overallEta = results.Max(r => r.Eta);
                var summary = $"Total {metadata.TotalBytes / (1024d * 1024d * 1024d):0.##} GB · ETA máximo {overallEta:hh\\:mm\\:ss}";
                SimulationSummary = summary;
                SimulationStatus = "Simulación completada";
                _services.AuditService.RecordEvent("SIMULATION", summary, EventSeverity.Info);

                await InvokeOnDispatcherAsync(() =>
                {
                    SimulationResults.Clear();
                    foreach (var result in results)
                    {
                        SimulationResults.Add(result);
                    }
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                SimulationStatus = $"Error en la simulación: {ex.Message}";
            }
            finally
            {
                IsSimulating = false;
            }
        }

        [RelayCommand]
        private async Task SaveSimulationBaselinesAsync()
        {
            await _services.SettingsService.SetSimulationBaselineAsync(SimulationUsb2Baseline, SimulationUsb3Baseline, SimulationUnknownBaseline).ConfigureAwait(false);
            SimulationStatus = "Bases de simulación guardadas";
        }

        private async Task RefreshEventsCoreAsync()
        {
            var typeFilter = string.IsNullOrWhiteSpace(SelectedType) ? null : SelectedType;
            var events = await _services.Repositories.Events.QueryAsync(EventsFrom, EventsTo, typeFilter, SelectedSeverity).ConfigureAwait(false);
            var filtered = events;

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var term = SearchText.Trim();
                filtered = filtered.Where(evt =>
                    evt.Message.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || evt.Type.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (evt.JobId?.ToString().Contains(term, StringComparison.OrdinalIgnoreCase) == true)
                    || (evt.SaleId?.ToString().Contains(term, StringComparison.OrdinalIgnoreCase) == true))
                    .ToList();
            }

            var state = _services.AuditFilterState;
            if (state.JobId.HasValue)
            {
                filtered = filtered.Where(evt => evt.JobId == state.JobId.Value).ToList();
            }
            else if (state.SaleId.HasValue)
            {
                filtered = filtered.Where(evt => evt.SaleId == state.SaleId.Value).ToList();
            }

            await InvokeOnDispatcherAsync(() =>
            {
                Events.Clear();
                foreach (var evt in filtered)
                {
                    Events.Add(evt);
                }
            }).ConfigureAwait(false);

            await UpdateLatestDiagSummaryAsync().ConfigureAwait(false);
        }

        private async Task LoadAlertsAsync()
        {
            await InvokeOnDispatcherAsync(() =>
            {
                ActiveAlerts.Clear();
                foreach (var alert in _services.AlertService.ActiveAlerts.OrderByDescending(a => a.RaisedAt))
                {
                    ActiveAlerts.Add(alert);
                }
            }).ConfigureAwait(false);

            if (ShowResolvedAlerts)
            {
                await LoadResolvedAlertsAsync().ConfigureAwait(false);
            }
        }

        private async Task LoadResolvedAlertsAsync()
        {
            var resolved = await _services.AlertService.GetResolvedAsync().ConfigureAwait(false);
            await InvokeOnDispatcherAsync(() =>
            {
                ResolvedAlerts.Clear();
                foreach (var alert in resolved)
                {
                    ResolvedAlerts.Add(alert);
                }
            }).ConfigureAwait(false);
        }

        private async Task LoadSimulationTargetsAsync()
        {
            var devices = await _services.DriveWatcher.GetDevicesAsync().ConfigureAwait(false);
            await UpdateSimulationTargetsAsync(devices).ConfigureAwait(false);
        }

        private async Task UpdateSimulationTargetsAsync(IReadOnlyList<DeviceInfo> devices)
        {
            await InvokeOnDispatcherAsync(() =>
            {
                var selected = SimulationTargets.Where(item => item.IsSelected).Select(item => item.DeviceId).ToHashSet(StringComparer.OrdinalIgnoreCase);
                SimulationTargets.Clear();
                foreach (var device in devices)
                {
                    var name = string.IsNullOrWhiteSpace(device.Label) ? device.DriveLetter : device.Label;
                    var label = $"{device.DriveLetter} {name}";
                    var isSelected = selected.Contains(device.Id) || _services.SelectionState.SelectedDeviceIds.Contains(device.Id);
                    var item = new SimulationDestinationItem(device.Id, label, device.BusHint)
                    {
                        IsSelected = isSelected
                    };
                    SimulationTargets.Add(item);
                }
            }).ConfigureAwait(false);
        }

        private async Task UpdateLatestDiagSummaryAsync()
        {
            var from = DateTime.UtcNow.AddDays(-7);
            var diagEvents = await _services.Repositories.Events.QueryAsync(from, DateTime.UtcNow, "DIAG_BUFFER_SUMMARY").ConfigureAwait(false);
            var latest = diagEvents.FirstOrDefault();
            if (latest != null)
            {
                await InvokeOnDispatcherAsync(() =>
                {
                    LatestDiagSummary = latest.Message;
                    LatestDiagTimestamp = latest.Ts;
                }).ConfigureAwait(false);
            }
            else
            {
                await InvokeOnDispatcherAsync(() =>
                {
                    LatestDiagSummary = string.Empty;
                    LatestDiagTimestamp = null;
                }).ConfigureAwait(false);
            }
        }

        private async Task<SourceMetadata> EstimateSourceMetadataAsync(string source)
        {
            return await Task.Run(() =>
            {
                var total = 0L;
                var reader = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories);
                foreach (var file in reader)
                {
                    var info = new FileInfo(file);
                    total += info.Length;
                }

                return new SourceMetadata(total);
            }).ConfigureAwait(false);
        }

        private static decimal ConvertBytesToGb(long bytes)
        {
            return Math.Round(bytes / 1024m / 1024m / 1024m, 2);
        }

        private static double DetermineBaseline(string? busHint, SimulationSettings settings)
        {
            var normalized = busHint?.ToLowerInvariant() ?? string.Empty;
            if (normalized.Contains("usb 2"))
            {
                return settings.Usb2BaselineMbps;
            }

            if (normalized.Contains("usb 3"))
            {
                return settings.Usb3BaselineMbps;
            }

            return settings.UnknownBaselineMbps;
        }

        private Task InvokeOnDispatcherAsync(Action action)
        {
            var tcs = new TaskCompletionSource<bool>();
            _dispatcher.TryEnqueue(() =>
            {
                action();
                tcs.SetResult(true);
            });
            return tcs.Task;
        }

        private async void AuditFilterState_Changed(object? sender, EventArgs e)
        {
            await RefreshEventsAsync().ConfigureAwait(false);
        }

        private async void DriveWatcher_DevicesChanged(object? sender, IReadOnlyList<DeviceInfo> devices)
        {
            await UpdateSimulationTargetsAsync(devices).ConfigureAwait(false);
        }

        private sealed record SourceMetadata(long TotalBytes);
    }

    public sealed class SimulationDestinationItem : ObservableObject
    {
        public string DeviceId { get; }
        public string DisplayName { get; }
        public string BusHint { get; }

        public SimulationDestinationItem(string deviceId, string displayName, string busHint)
        {
            DeviceId = deviceId;
            DisplayName = displayName;
            BusHint = busHint;
        }

        [ObservableProperty]
        private bool isSelected;
    }

    public sealed class SimulationResult
    {
        public string DeviceId { get; }
        public string DisplayName { get; }
        public string BusHint { get; }
        public double BaselineMbps { get; }
        public decimal PlannedGb { get; }
        public TimeSpan Eta { get; }
        public bool IsSlowest { get; set; }

        public string EtaDisplay => Eta.ToString(@"hh\:mm\:ss");
        public string PlannedDisplay => $"{PlannedGb:0.##} GB";
        public string BaselineDisplay => $"{BaselineMbps:0.#} MB/s";

        public SimulationResult(string deviceId, string displayName, string busHint, double baselineMbps, decimal plannedGb, TimeSpan eta)
        {
            DeviceId = deviceId;
            DisplayName = displayName;
            BusHint = busHint;
            BaselineMbps = baselineMbps;
            PlannedGb = plannedGb;
            Eta = eta;
        }
    }

    public sealed class TextOption
    {
        public string Label { get; }
        public string? Value { get; }

        public TextOption(string label, string? value)
        {
            Label = label;
            Value = value;
        }
    }

    public sealed class SeverityOption
    {
        public string Label { get; }
        public EventSeverity? Value { get; }

        public SeverityOption(string label, EventSeverity? value)
        {
            Label = label;
            Value = value;
        }
    }
}



