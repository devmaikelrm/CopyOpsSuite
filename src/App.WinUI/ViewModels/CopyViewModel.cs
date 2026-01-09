using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using CopyOpsSuite.Core.Models;
using CopyOpsSuite.MultiCopyEngine;
using CopyOpsSuite.App.WinUI.Services;
using CopyOpsSuite.App.WinUI.Views.Controls;
using CopyOpsSuite.CashOps;

namespace CopyOpsSuite.App.WinUI.ViewModels
{
    public partial class CopyViewModel : ObservableObject
    {
        private const int MaxLogLines = 500;

        private readonly AppServices _services;
        private readonly DispatcherQueue _dispatcher;
        private readonly ConcurrentDictionary<string, DeviceRowViewModel> _rows = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DeviceInfo> _deviceInfo = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, TargetSnapshot> _pendingSnapshots = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, (long BytesOk, long BytesPlanned)> _targetTotals = new(StringComparer.OrdinalIgnoreCase);
        private readonly DispatcherQueueTimer _throttleTimer;
        private readonly DispatcherQueueTimer _billingTimer;
        private readonly MiniWindowManager _miniWindowManager;
        private readonly object _tiersLock = new();
        private CancellationTokenSource? _currentJobCts;
        private Guid? _activeJobId;
        private readonly List<PricingTier> _tiers = new();
        private string _currency = "CUP+";
        private bool _capEventRaised;

        public ObservableCollection<DeviceRowViewModel> DeviceRows { get; } = new();
        public ObservableCollection<LogLineViewModel> Logs { get; } = new();

        [ObservableProperty]
        private string statusMessage = "Cargando dispositivos...";

        [ObservableProperty]
        private string sourcePath = string.Empty;

        [ObservableProperty]
        private string ramStatus = "RAM --";

        [ObservableProperty]
        private int selectedDevicesCount;

        [ObservableProperty]
        private string selectionSummary = "0/0 seleccionados";

        [ObservableProperty]
        private double overallProgress;

        [ObservableProperty]
        private string overallProgressText = "0%";

        [ObservableProperty]
        private bool showVerboseLogs;

        [ObservableProperty]
        private decimal expectedNow;

        [ObservableProperty]
        private decimal expectedFull;

        [ObservableProperty]
        private string globalExpectedNowDisplay = "Cobro ahora: 0 CUP+";

        [ObservableProperty]
        private string globalExpectedFinalDisplay = "Cobro final: 0 CUP+";

        [ObservableProperty]
        private decimal paidAmount;

        [ObservableProperty]
        private string paymentMethod = "Efectivo";

        [ObservableProperty]
        private string paymentStatus = "Pendiente";

        [ObservableProperty]
        private bool isJobRunning;

        public CopyViewModel(AppServices services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _dispatcher = DispatcherQueue.GetForCurrentThread() ?? throw new InvalidOperationException("DispatcherQueue no disponible.");
            _services.DriveWatcher.DevicesChanged += DriveWatcher_DevicesChanged;
            _services.RamMonitor.RamUpdated += RamMonitor_RamUpdated;
            _services.MultiCopyEngine.ProgressUpdated += Engine_ProgressUpdated;
            _services.PricingTiersUpdated += PricingTiersUpdated;
            _throttleTimer = DispatcherQueue.CreateTimer();
            _throttleTimer.Interval = TimeSpan.FromMilliseconds(300);
            _throttleTimer.Tick += (_, _) => FlushThrottledUpdates();
            _throttleTimer.Start();
            _billingTimer = DispatcherQueue.CreateTimer();
            _billingTimer.Interval = TimeSpan.FromSeconds(1);
            _billingTimer.Tick += (_, _) => UpdateOverallProgress();
            _billingTimer.Start();
            _ = LoadDevicesFromWatcherAsync();
            _ = LoadPricingTiersAsync();
            _miniWindowManager = new MiniWindowManager(_services);
        }

        partial void OnPaidAmountChanged(decimal value) => UpdatePaymentStatus();
        partial void OnExpectedFullChanged(decimal value) => UpdatePaymentStatus();
        partial void OnSourcePathChanged(string value) => UpdateSelectionState();

        [RelayCommand]
        private async Task RefreshDevicesAsync()
        {
            await LoadDevicesFromWatcherAsync().ConfigureAwait(false);
        }

        [RelayCommand]
        private async Task StartAsync()
        {
            if (string.IsNullOrWhiteSpace(SourcePath))
            {
                StatusMessage = "Selecciona una carpeta de origen valida.";
                AppendLog("Se intento iniciar sin carpeta seleccionada.", "Warn");
                return;
            }

            if (!_deviceInfo.Any())
            {
                StatusMessage = "No hay destinos disponibles.";
                AppendLog("No se encontraron dispositivos para copiar.", "Warn");
                return;
            }

            var selectedRows = DeviceRows.Where(r => r.IsSelected).ToList();
            if (!selectedRows.Any())
            {
                StatusMessage = "Selecciona al menos un destino.";
                AppendLog("No se seleccionaron destinos.", "Warn");
                return;
            }

            SourcePath = SourcePath.Trim();
            if (!Directory.Exists(SourcePath))
            {
                StatusMessage = "La ruta de origen no existe.";
                AppendLog("La ruta de origen no es accesible.", "Error");
                return;
            }

            SourceMetadata metadata;
            try
            {
                metadata = await ComputeSourceMetadataAsync(SourcePath);
            }
            catch (Exception ex)
            {
                StatusMessage = "No se pudo analizar la fuente.";
                AppendLog($"Error al escanear la carpeta: {ex.Message}", "Error");
                return;
            }

            if (metadata.TotalBytes == 0)
            {
                StatusMessage = "La carpeta de origen no contiene archivos.";
                AppendLog("No hay archivos para copiar.", "Warn");
                return;
            }

            var job = new TransferJob
            {
                JobId = Guid.NewGuid(),
                SourcePath = SourcePath,
                StartedAt = DateTime.UtcNow,
                OperatorName = "Operador",
                BytesPlanned = metadata.TotalBytes * selectedRows.Count,
                Status = JobStatus.Pending
            };

            await _services.Repositories.Jobs.UpsertAsync(job).ConfigureAwait(false);

            var precheck = RunPrechecks(selectedRows, metadata);
            await PersistPrecheckAsync(job.JobId, precheck).ConfigureAwait(false);

            var forced = false;
            if (precheck.Items.Any())
            {
                forced = await ShowPrecheckDialogAsync(precheck).ConfigureAwait(false);
            }

            if (precheck.FailCount > 0 && !forced)
            {
                StatusMessage = "Precheck bloqueado; requiere acceso admin para continuar.";
                AppendLog("Precheck bloqueado.", "Warn");
                _services.AuditService.RecordEvent("PRECHECK_BLOCKED", $"Job {job.JobId} bloqueado por precheck.", EventSeverity.Warn, job.JobId);
                job.Status = JobStatus.Canceled;
                job.EndedAt = DateTime.UtcNow;
                await _services.Repositories.Jobs.UpsertAsync(job).ConfigureAwait(false);
                return;
            }

            if (precheck.Items.Any())
            {
                var summary = BuildPrecheckSummary(precheck);
                _services.AuditService.RecordEvent(forced ? "PRECHECK_FORCED" : "PRECHECK_OK", summary, EventSeverity.Info, job.JobId);
            }

            _currentJobCts?.Cancel();
            _currentJobCts = new CancellationTokenSource();
            _targetTotals.Clear();
            _pendingSnapshots.Clear();
            _activeJobId = job.JobId;
            IsJobRunning = true;
            _capEventRaised = false;
            var jobDevices = selectedRows
                .Where(row => _deviceInfo.TryGetValue(row.DeviceId, out _))
                .Select(row => _deviceInfo[row.DeviceId])
                .ToList();

            foreach (var row in selectedRows)
            {
                _targetTotals[row.DeviceId] = (0, metadata.TotalBytes);
            }

            _miniWindowManager.OpenForDevices(selectedRows, job.JobId);
            AppendLog($"Job {job.JobId} iniciando ({selectedRows.Count} destinos).", "Info", "Job");
            StatusMessage = $"Iniciando job {job.JobId}";

            try
            {
                await _services.MultiCopyEngine.RunAsync(job, jobDevices, _currentJobCts.Token);
                AppendLog($"Job {job.JobId} finalizado con estado {job.Status}.", "Info", "Job");
            }
            catch (OperationCanceledException)
            {
                AppendLog($"Job {job.JobId} cancelado manualmente.", "Warn", "Job");
            }
            catch (Exception ex)
            {
                AppendLog($"Error en job {job.JobId}: {ex.Message}", "Error", "Job");
                StatusMessage = $"Error en job {job.JobId}";
            }
            finally
            {
                _currentJobCts = null;
                _activeJobId = null;
                IsJobRunning = false;
                _miniWindowManager.HandleJobCompletion(job);
            }
        }

        private async Task LoadDevicesFromWatcherAsync()
        {
            try
            {
                var devices = await _services.DriveWatcher.GetDevicesAsync().ConfigureAwait(false);
                _dispatcher.TryEnqueue(() =>
                {
                    UpdateDevices(devices);
                    StatusMessage = "Dispositivos actualizados";
                });
                AppendLog("Dispositivos refrescados.", "Info", "Devices");
            }
            catch (Exception ex)
            {
                StatusMessage = "Error al cargar dispositivos";
                AppendLog($"Error al listar unidades: {ex.Message}", "Error", "Devices");
            }
        }

        private void DriveWatcher_DevicesChanged(object? sender, IReadOnlyList<DeviceInfo> devices)
        {
            _dispatcher.TryEnqueue(() => UpdateDevices(devices));
            AppendLog("Cambios en dispositivos detectados.", "Info", "Devices");
        }

        private void RamMonitor_RamUpdated(object? sender, string status)
        {
            _dispatcher.TryEnqueue(() => RamStatus = status);
        }

        private void PricingTiersUpdated(object? sender, EventArgs e)
        {
            _ = LoadPricingTiersAsync();
        }

        private void Engine_ProgressUpdated(object? sender, EngineProgress progress)
        {
            foreach (var snapshot in progress.Targets)
            {
                _pendingSnapshots[snapshot.DeviceId] = snapshot;
                _targetTotals[snapshot.DeviceId] = (snapshot.BytesOk, snapshot.BytesPlanned);
            }
        }

        private void FlushThrottledUpdates()
        {
            foreach (var row in DeviceRows)
            {
                if (_pendingSnapshots.TryRemove(row.DeviceId, out var snapshot))
                {
                    row.ApplySnapshot(snapshot);
                }
                else if (_deviceInfo.TryGetValue(row.DeviceId, out var device))
                {
                    row.UpdateDeviceInfo(device);
                }

                UpdateRowPricing(row);
            }

            UpdateOverallProgress();
            _miniWindowManager.UpdateFromRows(DeviceRows);
        }

        private void UpdateOverallProgress()
        {
            var totalBytesOk = _targetTotals.Values.Sum(v => v.BytesOk);
            var totalBytesPlanned = Math.Max(1, _targetTotals.Values.Sum(v => v.BytesPlanned));
            OverallProgress = totalBytesOk / (double)totalBytesPlanned;
            OverallProgressText = $"{Math.Round(OverallProgress * 100)}%";
            ExpectedNow = CalculateExpectedNow();
            ExpectedFull = CalculateExpectedFull();
            GlobalExpectedNowDisplay = $"Cobro ahora: {ExpectedNow:N0} {_currency}";
            GlobalExpectedFinalDisplay = $"Cobro final: {ExpectedFull:N0} {_currency}";
            UpdatePaymentStatus();
        }

        private decimal CalculateExpectedNow()
        {
            if (_targetTotals.IsEmpty)
            {
                return 0m;
            }

            var totalOk = _targetTotals.Values.Sum(v => v.BytesOk);
            return CalculateCharge(totalOk);
        }

        private decimal CalculateExpectedFull()
        {
            if (_targetTotals.IsEmpty)
            {
                return 0m;
            }

            var total = 0L;
            foreach (var kvp in _targetTotals)
            {
                total += kvp.Value.BytesPlanned;
            }

            return CalculateCharge(total);
        }

        private decimal CalculateCharge(long bytes)
        {
            var result = TierPricingEngine.Calculate(bytes, GetTierSnapshot());
            if (result.IsCapApplied && !_capEventRaised && _activeJobId.HasValue)
            {
                _capEventRaised = true;
                _services.AuditService.RecordEvent("CAP_APPLIED", "Tarifa CAP aplicada.", EventSeverity.Info, _activeJobId);
            }

            return result.TotalCUP;
        }

        private void UpdateDevices(IReadOnlyList<DeviceInfo> devices)
        {
            var removed = _deviceInfo.Keys.Except(devices.Select(d => d.Id), StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var device in devices)
            {
                _deviceInfo[device.Id] = device;
                if (!_rows.TryGetValue(device.Id, out var row))
                {
                    row = new DeviceRowViewModel(device.Id)
                    {
                        IsSelected = true
                    };
                    row.PropertyChanged += DeviceRow_PropertyChanged;
                    _rows[device.Id] = row;
                    DeviceRows.Add(row);
                }

                row.UpdateDeviceInfo(device);
            }

            foreach (var id in removed)
            {
                if (_rows.Remove(id, out var row))
                {
                    row.PropertyChanged -= DeviceRow_PropertyChanged;
                    DeviceRows.Remove(row);
                }

                _deviceInfo.Remove(id);
                _pendingSnapshots.TryRemove(id, out _);
                _targetTotals.TryRemove(id, out _);
            }

            UpdateSelectionSummary();
        }

        private void DeviceRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DeviceRowViewModel.IsSelected))
            {
                UpdateSelectionSummary();
                UpdateSelectionState();
            }
        }

        private void UpdateSelectionSummary()
        {
            SelectedDevicesCount = DeviceRows.Count(r => r.IsSelected && r.State != TransferStateLabel.DoneOk);
            SelectionSummary = $"{SelectedDevicesCount}/{DeviceRows.Count} seleccionados";
            UpdateSelectionState();
        }

        private void UpdateSelectionState()
        {
            var selected = DeviceRows.Where(r => r.IsSelected).Select(r => r.DeviceId).ToList();
            _services.SelectionState.Update(SourcePath, selected);
        }

        private async Task<SourceMetadata> ComputeSourceMetadataAsync(string source)
        {
            return await Task.Run(() =>
            {
                var total = 0L;
                var hasLarge = false;
                var reader = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories);
                foreach (var file in reader)
                {
                    var info = new FileInfo(file);
                    total += info.Length;
                    if (!hasLarge && info.Length > 4L * 1024 * 1024 * 1024)
                    {
                        hasLarge = true;
                    }
                }

                return new SourceMetadata(total, hasLarge);
            }).ConfigureAwait(false);
        }

        private PrecheckResult RunPrechecks(IReadOnlyList<DeviceRowViewModel> targets, SourceMetadata metadata)
        {
            var items = new List<PrecheckDisplayItem>();
            var sourceLevel = metadata.TotalBytes > 0 ? PrecheckResultLevel.Pass : PrecheckResultLevel.Fail;
            items.Add(new PrecheckDisplayItem("Origen", $"{GetSourceLabel()} - {(metadata.TotalBytes > 0 ? "Accesible" : "Sin archivos")}", sourceLevel));

            foreach (var row in targets)
            {
                if (!_deviceInfo.TryGetValue(row.DeviceId, out var device))
                {
                    items.Add(new PrecheckDisplayItem(row.DriveLetter, "Dispositivo no disponible", PrecheckResultLevel.Fail));
                    continue;
                }

                var label = string.IsNullOrWhiteSpace(device.Label) ? device.DriveLetter : device.Label;
                var spaceLevel = device.FreeBytes >= metadata.TotalBytes ? PrecheckResultLevel.Pass : PrecheckResultLevel.Fail;
                var spaceText = $"Espacio libre {FormatBytes(device.FreeBytes)} / {FormatBytes(metadata.TotalBytes)}";
                items.Add(new PrecheckDisplayItem(label, $"Verificacion de espacio: {spaceText}", spaceLevel));

                var fat32Flag = device.FileSystem.Equals("FAT32", StringComparison.OrdinalIgnoreCase) && metadata.HasLargeFile;
                items.Add(new PrecheckDisplayItem(label, fat32Flag ? "FAT32 detectado y archivos >4GB" : "FAT32 OK", fat32Flag ? PrecheckResultLevel.Warn : PrecheckResultLevel.Pass));

                var usb2Flag = device.BusHint.Contains("USB 2", StringComparison.OrdinalIgnoreCase);
                if (usb2Flag)
                {
                    items.Add(new PrecheckDisplayItem(label, "Puerto USB 2.0 detectado; velocidad reducida", PrecheckResultLevel.Warn));
                }
                else
                {
                    items.Add(new PrecheckDisplayItem(label, "Bus moderno detectado", PrecheckResultLevel.Pass));
                }
            }

            return new PrecheckResult(items);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0)
            {
                return "0 B";
            }

            var units = new[] { "B", "KB", "MB", "GB", "TB" };
            var size = (double)bytes;
            var order = 0;
            while (size >= 1024 && order < units.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {units[order]}";
        }

        private string GetSourceLabel()
        {
            var normalized = (SourcePath ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var label = Path.GetFileName(normalized);
            if (string.IsNullOrWhiteSpace(label))
            {
                return string.IsNullOrEmpty(normalized) ? "Origen" : normalized;
            }

            return label;
        }

        private async Task<bool> ShowPrecheckDialogAsync(PrecheckResult result)
        {
            if (!result.Items.Any())
            {
                return true;
            }

            var dialog = new PrecheckDialog
            {
                Title = "Precheck de destinos"
            };
            dialog.SetItems(result.Items);
            dialog.ToggleForceButton(_services.SettingsService.IsAdminUnlocked && result.FailCount > 0);
            dialog.ResetForceFlag();
            await dialog.ShowAsync();
            return dialog.ForceRequested;
        }

        public async Task<SimulationReport?> BuildSimulationAsync()
        {
            if (string.IsNullOrWhiteSpace(SourcePath))
            {
                StatusMessage = "Selecciona una carpeta de origen valida.";
                return null;
            }

            if (!Directory.Exists(SourcePath))
            {
                StatusMessage = "La ruta de origen no existe.";
                return null;
            }

            var selectedRows = DeviceRows.Where(r => r.IsSelected).ToList();
            if (!selectedRows.Any())
            {
                StatusMessage = "Selecciona al menos un destino.";
                return null;
            }

            SourceMetadata metadata;
            try
            {
                metadata = await ComputeSourceMetadataAsync(SourcePath).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                StatusMessage = "No se pudo analizar la fuente.";
                AppendLog($"Error al escanear la carpeta: {ex.Message}", "Error");
                return null;
            }

            if (metadata.TotalBytes == 0)
            {
                StatusMessage = "La carpeta de origen no contiene archivos.";
                return null;
            }

            var perTargetBytes = metadata.TotalBytes / selectedRows.Count;
            var settings = _services.SettingsService.SimulationSettings;
            var results = new List<SimulationResult>();

            foreach (var row in selectedRows)
            {
                var display = string.IsNullOrWhiteSpace(row.Label) ? row.DriveLetter : $"{row.DriveLetter} {row.Label}";
                var baseline = Math.Max(1, DetermineBaseline(row.BusHint, settings));
                var seconds = perTargetBytes / (baseline * 1024 * 1024);
                var eta = TimeSpan.FromSeconds(Math.Max(1, seconds));
                var plannedGb = ConvertBytesToGb(perTargetBytes);
                results.Add(new SimulationResult(row.DeviceId, display, row.BusHint, baseline, plannedGb, eta));
            }

            var slowest = results.OrderByDescending(r => r.Eta).FirstOrDefault();
            if (slowest != null)
            {
                slowest.IsSlowest = true;
            }

            var overallEta = results.Max(r => r.Eta);
            var summary = $"Total {metadata.TotalBytes / (1024d * 1024d * 1024d):0.##} GB - ETA max {overallEta:hh\\:mm\\:ss}";
            _services.AuditService.RecordEvent("SIMULATION", summary, EventSeverity.Info);

            return new SimulationReport(summary, results);
        }

        public async Task AddPathsAsync(IReadOnlyList<string> paths)
        {
            if (!IsJobRunning || !_activeJobId.HasValue)
            {
                StatusMessage = "No hay un job activo.";
                return;
            }

            if (paths == null || paths.Count == 0)
            {
                return;
            }

            var selectedRows = DeviceRows.Where(r => r.IsSelected).ToList();
            if (!selectedRows.Any())
            {
                StatusMessage = "Selecciona al menos un destino.";
                return;
            }

            var targetIds = selectedRows.Select(r => r.DeviceId).ToList();
            var result = await _services.MultiCopyEngine.AddFilesToJobAsync(_activeJobId.Value, paths, targetIds).ConfigureAwait(false);
            if (result.BytesAdded <= 0)
            {
                StatusMessage = "No se agregaron archivos nuevos.";
                return;
            }

            foreach (var entry in result.BytesAddedByDevice)
            {
                if (_targetTotals.TryGetValue(entry.Key, out var totals))
                {
                    _targetTotals[entry.Key] = (totals.BytesOk, totals.BytesPlanned + entry.Value);
                }
                else
                {
                    _targetTotals[entry.Key] = (0, entry.Value);
                }
            }

            _dispatcher.TryEnqueue(() =>
            {
                foreach (var row in DeviceRows)
                {
                    UpdateRowPricing(row);
                }

                UpdateOverallProgress();
            });

            AppendLog($"Se agregaron {result.FileCount} archivos ({result.BytesAdded:N0} bytes).", "Info", "Job");
        }

        private void AppendLog(string message, string severity = "Info", string type = "Copy")
        {
            _dispatcher.TryEnqueue(() =>
            {
                Logs.Add(new LogLineViewModel(DateTime.Now, type, severity, message));
                if (Logs.Count > MaxLogLines)
                {
                    Logs.RemoveAt(0);
                }
            });
        }

        public void Log(string message)
        {
            AppendLog(message);
        }

        private void UpdatePaymentStatus()
        {
            if (ExpectedFull <= 0)
            {
                PaymentStatus = "Pendiente";
                return;
            }

            if (PaidAmount >= ExpectedFull)
            {
                PaymentStatus = "Pagado";
                return;
            }

            if (PaidAmount > 0)
            {
                PaymentStatus = "Parcial";
                return;
            }

            PaymentStatus = "Pendiente";
        }

        private async Task LoadPricingTiersAsync()
        {
            try
            {
                var tiers = await _services.Repositories.PricingTiers.GetAllAsync().ConfigureAwait(false);
                lock (_tiersLock)
                {
                    _tiers.Clear();
                    _tiers.AddRange(tiers);
                }
            }
            catch
            {
                // Keep defaults if profile data is unavailable.
            }

            _dispatcher.TryEnqueue(() =>
            {
                foreach (var row in DeviceRows)
                {
                    UpdateRowPricing(row);
                }

                UpdateOverallProgress();
            });
        }

        private async Task PersistPrecheckAsync(Guid jobId, PrecheckResult result)
        {
            foreach (var item in result.Items)
            {
                var log = new ValidationLog
                {
                    JobId = jobId,
                    Rule = "PRECHECK",
                    Result = item.LevelText,
                    Details = $"{item.Device}: {item.Message}",
                    Ts = DateTime.UtcNow
                };
                await _services.Repositories.Validations.AddAsync(log).ConfigureAwait(false);
            }
        }

        private static string BuildPrecheckSummary(PrecheckResult result)
        {
            var pass = result.Items.Count(i => i.Level == PrecheckResultLevel.Pass);
            var warn = result.Items.Count(i => i.Level == PrecheckResultLevel.Warn);
            var fail = result.Items.Count(i => i.Level == PrecheckResultLevel.Fail);
            return $"Precheck: PASS {pass} / WARN {warn} / FAIL {fail}";
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

        private static decimal ConvertBytesToGb(long bytes)
        {
            return Math.Round(bytes / 1024m / 1024m / 1024m, 2);
        }

        private IReadOnlyList<PricingTier> GetTierSnapshot()
        {
            lock (_tiersLock)
            {
                return _tiers.ToList();
            }
        }

        private void UpdateRowPricing(DeviceRowViewModel row)
        {
            var totals = _targetTotals.TryGetValue(row.DeviceId, out var value) ? value : (0L, 0L);
            var pricing = BuildPricing(totals.BytesOk, totals.BytesPlanned);
            row.UpdatePricing(pricing.ExpectedNow, pricing.ExpectedFinal, pricing.RealGbNow, pricing.BillableGbNow, pricing.RealGbFinal, pricing.BillableGbFinal, _currency, pricing.AppliedTierNow, pricing.AppliedTierFinal);
        }

        private PricingSnapshot BuildPricing(long bytesOk, long bytesPlanned)
        {
            var realNow = bytesOk / 1024m / 1024m / 1024m;
            var realFinal = bytesPlanned / 1024m / 1024m / 1024m;
            var tiers = GetTierSnapshot();
            var nowResult = TierPricingEngine.Calculate(bytesOk, tiers);
            var finalResult = TierPricingEngine.Calculate(bytesPlanned, tiers);

            if (nowResult.IsCapApplied && !_capEventRaised && _activeJobId.HasValue)
            {
                _capEventRaised = true;
                _services.AuditService.RecordEvent("CAP_APPLIED", "Tarifa CAP aplicada.", EventSeverity.Info, _activeJobId);
            }

            if (finalResult.IsCapApplied && !_capEventRaised && _activeJobId.HasValue)
            {
                _capEventRaised = true;
                _services.AuditService.RecordEvent("CAP_APPLIED", "Tarifa CAP aplicada.", EventSeverity.Info, _activeJobId);
            }

            return new PricingSnapshot(
                Math.Round(realNow, 2),
                Math.Round(realFinal, 2),
                nowResult.BillableGB,
                finalResult.BillableGB,
                nowResult.TotalCUP,
                finalResult.TotalCUP,
                nowResult.AppliedTierText,
                finalResult.AppliedTierText);
        }

        private sealed record SourceMetadata(long TotalBytes, bool HasLargeFile);

        private sealed class PrecheckResult
        {
            public IReadOnlyList<PrecheckDisplayItem> Items { get; }
            public int FailCount => Items.Count(i => i.Level == PrecheckResultLevel.Fail);

            public PrecheckResult(IReadOnlyList<PrecheckDisplayItem> items)
            {
                Items = items;
            }
        }

        private sealed record PricingSnapshot(
            decimal RealGbNow,
            decimal RealGbFinal,
            decimal BillableGbNow,
            decimal BillableGbFinal,
            decimal ExpectedNow,
            decimal ExpectedFinal,
            string AppliedTierNow,
            string AppliedTierFinal);

        public sealed record SimulationReport(string Summary, IReadOnlyList<SimulationResult> Results);
    }
}

