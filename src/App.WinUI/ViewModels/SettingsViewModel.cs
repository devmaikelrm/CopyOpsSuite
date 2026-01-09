using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CopyOpsSuite.App.WinUI;
using CopyOpsSuite.App.WinUI.Views.Controls;
using CopyOpsSuite.Core.Models;

namespace CopyOpsSuite.App.WinUI.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly AppServices _services;
        private readonly DispatcherQueue _dispatcher;
        private bool _isReloading;

        public ObservableCollection<DeviceChoice> DiagnosticsDevices { get; } = new();
        public ObservableCollection<string> DiagnosticsLog { get; } = new();
        public ObservableCollection<PricingTierRow> PricingTiers { get; } = new();

        [ObservableProperty]
        private bool operatorModeEnabled;

        [ObservableProperty]
        private bool adminUnlocked;

        [ObservableProperty]
        private bool showDefaultPinWarning;

        [ObservableProperty]
        private bool enableBuffering;

        [ObservableProperty]
        private int chunkSizeMb;

        [ObservableProperty]
        private int maxChunks;

        [ObservableProperty]
        private int writerWorkersPerTarget;

        [ObservableProperty]
        private bool pinMiniTopMost;

        [ObservableProperty]
        private bool isDiagnosticsRunning;

        [ObservableProperty]
        private string diagnosticsStatus = "Listo";

        [ObservableProperty]
        private string diagnosticsSummary = string.Empty;

        [ObservableProperty]
        private string selectedDiagnosticsDeviceId = string.Empty;

        [ObservableProperty]
        private bool infoBarIsOpen;

        [ObservableProperty]
        private string infoBarMessage = string.Empty;

        [ObservableProperty]
        private InfoBarSeverity infoBarSeverity = InfoBarSeverity.Informational;

        [ObservableProperty]
        private string selectedTheme = "Default";

        [ObservableProperty]
        private BillingMode selectedBillingMode = BillingMode.PerJob;

        [ObservableProperty]
        private string tierValidationMessage = string.Empty;

        [ObservableProperty]
        private bool canSaveTiers;

        public bool IsAdvancedRestricted => OperatorModeEnabled && !AdminUnlocked;

        public string BufferWarningPrimary => $"Max RAM por destino aprox {ChunkSizeMb * MaxChunks} MB";

        public string BufferWarningSecondary => "Total aprox por destino x destinos seleccionados";

        public IReadOnlyList<ThemeOption> ThemeOptions { get; } = new[]
        {
            new ThemeOption("Seguir sistema", "Default"),
            new ThemeOption("Claro", "Light"),
            new ThemeOption("Oscuro", "Dark")
        };

        public IReadOnlyList<BillingModeOption> BillingModeOptions { get; } = new[]
        {
            new BillingModeOption("Por job", BillingMode.PerJob),
            new BillingModeOption("Por destino", BillingMode.PerTarget)
        };

        public SettingsViewModel(AppServices services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _dispatcher = DispatcherQueue.GetForCurrentThread() ?? throw new InvalidOperationException("DispatcherQueue no disponible.");
            _services.DiagnosticsRequested += OnDiagnosticsRequested;
            LoadValues();
            _ = LoadDiagnosticDevicesAsync();
            _ = LoadPricingTiersAsync();
        }

        partial void OnOperatorModeEnabledChanged(bool value)
        {
            if (_isReloading)
            {
                OnPropertyChanged(nameof(IsAdvancedRestricted));
                return;
            }

            _ = UpdateOperatorModeAsync(value);
        }

        partial void OnAdminUnlockedChanged(bool value)
        {
            OnPropertyChanged(nameof(IsAdvancedRestricted));
        }

        partial void OnChunkSizeMbChanged(int value)
        {
            OnPropertyChanged(nameof(BufferWarningPrimary));
        }

        partial void OnMaxChunksChanged(int value)
        {
            OnPropertyChanged(nameof(BufferWarningPrimary));
        }

        partial void OnPinMiniTopMostChanged(bool value)
        {
            if (_isReloading)
            {
                return;
            }

            _ = _services.SettingsService.SetTopMostPinAsync(value);
        }

        partial void OnIsDiagnosticsRunningChanged(bool value)
        {
            OnPropertyChanged(nameof(DiagnosticsButtonText));
        }

        partial void OnSelectedThemeChanged(string value)
        {
            if (_isReloading)
            {
                return;
            }

            _ = UpdateThemeAsync(value);
        }

        partial void OnSelectedBillingModeChanged(BillingMode value)
        {
            if (_isReloading)
            {
                return;
            }

            _ = _services.SettingsService.SetBillingModeAsync(value);
        }

        public string DiagnosticsButtonText => IsDiagnosticsRunning ? "Ejecutando..." : "Ejecutar diagnostico";

        [RelayCommand]
        private async Task SwitchToAdminAsync()
        {
            var dialog = new PinDialog
            {
                Title = "Desbloquear Administracion"
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var pin = dialog.Pin;
                if (_services.SettingsService.TryUnlockAdmin(pin))
                {
                    AdminUnlocked = true;
                    ShowInfoBar("Modo admin desbloqueado", InfoBarSeverity.Success);
                }
                else
                {
                    ShowInfoBar("PIN incorrecto", InfoBarSeverity.Warning);
                }

                ReloadConstraints();
            }
        }

        [RelayCommand]
        private void LockAdmin()
        {
            _services.SettingsService.LockAdmin();
            AdminUnlocked = false;
            ShowInfoBar("Modo admin bloqueado", InfoBarSeverity.Informational);
            ReloadConstraints();
        }

        [RelayCommand]
        private async Task SaveBufferingAsync()
        {
            await _services.SettingsService.SetBufferingAsync(EnableBuffering, ChunkSizeMb, MaxChunks, WriterWorkersPerTarget);
            LoadValues();
            ShowInfoBar("Configuracion de buffering guardada", InfoBarSeverity.Success);
        }

        [RelayCommand]
        private async Task RunDiagnosticsAsync()
        {
            if (IsDiagnosticsRunning)
            {
                return;
            }

            IsDiagnosticsRunning = true;
            DiagnosticsStatus = "Ejecutando diagnostico...";
            DiagnosticsLog.Clear();
            DiagnosticsSummary = string.Empty;
            AppendDiagnosticLine("DIAG_START");
            _services.AuditService.RecordEvent("DIAG_START", "Inicio de diagnostico", EventSeverity.Info);

            try
            {
                var devices = await _services.DriveWatcher.GetDevicesAsync().ConfigureAwait(false);
                await UpdateDiagnosticDevicesAsync(devices).ConfigureAwait(false);
                var removable = devices.Where(d => d.Type == DeviceType.RemovableUsb).ToList();
                foreach (var device in removable)
                {
                    var detail = $"Drive {device.DriveLetter.TrimEnd('\\')} FS={device.FileSystem} Bus={device.BusHint}";
                    AppendDiagnosticLine($"DIAG_DEVICE: {detail}");
                    _services.AuditService.RecordEvent("DIAG_DEVICE", detail, EventSeverity.Info, deviceId: device.Id);
                }

                if (!string.IsNullOrEmpty(SelectedDiagnosticsDeviceId))
                {
                    var target = devices.FirstOrDefault(d => d.Id == SelectedDiagnosticsDeviceId);
                    if (target != null)
                    {
                        var summary = await RunBufferTestAsync(target).ConfigureAwait(false);
                        DiagnosticsSummary = summary;
                        AppendDiagnosticLine(summary);
                        _services.AuditService.RecordEvent("DIAG_BUFFER_SUMMARY", summary, EventSeverity.Info, deviceId: target.Id);
                    }
                }

                AppendDiagnosticLine("DIAG_COMPLETE");
                _services.AuditService.RecordEvent("DIAG_COMPLETE", "Diagnostico finalizado", EventSeverity.Info);
                ShowInfoBar("Diagnostico completado", InfoBarSeverity.Success);
                DiagnosticsStatus = "Diagnostico completado";
            }
            catch (Exception ex)
            {
                AppendDiagnosticLine($"DIAG_ERROR: {ex.Message}");
                ShowInfoBar($"Diagnostico fallo: {ex.Message}", InfoBarSeverity.Error);
                DiagnosticsStatus = "Error durante diagnostico";
            }
            finally
            {
                IsDiagnosticsRunning = false;
            }
        }

        private void OnDiagnosticsRequested(object? sender, EventArgs e)
        {
            if (!IsDiagnosticsRunning)
            {
                _ = RunDiagnosticsAsync();
            }
        }

        [RelayCommand]
        private void GenerateRecommendedTiers()
        {
            PricingTiers.Clear();
            foreach (var tier in BuildRecommendedTiers())
            {
                PricingTiers.Add(tier);
            }

            ValidateTiers();
        }

        [RelayCommand]
        private async Task SaveTiersAsync()
        {
            if (!ValidateTiers())
            {
                return;
            }

            var tiers = PricingTiers.Select(t => t.ToModel()).ToList();
            await _services.Repositories.PricingTiers.ReplaceAllAsync(tiers).ConfigureAwait(false);
            _services.AuditService.RecordEvent("TIERS_UPDATED", $"Se guardaron {tiers.Count} tiers.", EventSeverity.Info);
            _services.NotifyPricingTiersUpdated();
            TierValidationMessage = "Tiers guardados.";
        }

        [RelayCommand]
        private void SortTiersByMinGb()
        {
            if (IsAdvancedRestricted)
            {
                return;
            }

            var ordered = PricingTiers
                .OrderBy(t => t.MinGB)
                .ThenBy(t => t.MaxGB == 0 ? int.MaxValue : t.MaxGB)
                .ToList();

            for (var i = 0; i < ordered.Count; i++)
            {
                ordered[i].Order = i + 1;
            }

            for (var targetIndex = 0; targetIndex < ordered.Count; targetIndex++)
            {
                var item = ordered[targetIndex];
                var currentIndex = PricingTiers.IndexOf(item);
                if (currentIndex >= 0 && currentIndex != targetIndex)
                {
                    PricingTiers.Move(currentIndex, targetIndex);
                }
            }

            ValidateTiers();
        }

        private async Task UpdateOperatorModeAsync(bool value)
        {
            await _services.SettingsService.SetOperatorModeAsync(value).ConfigureAwait(false);
            _dispatcher.TryEnqueue(() =>
            {
                AdminUnlocked = _services.SettingsService.IsAdminUnlocked;
                LoadValues();
            });
        }

        private async Task UpdateThemeAsync(string value)
        {
            var normalized = value switch
            {
                "Light" => "Light",
                "Dark" => "Dark",
                _ => "Default"
            };

            await _services.SettingsService.SetAppThemeAsync(normalized).ConfigureAwait(false);
            App.ApplyTheme(ToElementTheme(normalized));
        }

        private void LoadValues()
        {
            _isReloading = true;
            var settings = _services.SettingsService;
            OperatorModeEnabled = settings.AccessSettings.OperatorModeEnabled;
            AdminUnlocked = settings.IsAdminUnlocked;
            ShowDefaultPinWarning = settings.HasDefaultPin;
            EnableBuffering = settings.BufferSettings.EnableBuffering;
            ChunkSizeMb = settings.BufferSettings.ChunkSizeMb;
            MaxChunks = settings.BufferSettings.MaxChunks;
            WriterWorkersPerTarget = settings.BufferSettings.MaxWriterWorkersPerTarget;
            PinMiniTopMost = settings.MiniWindowSettings.TopMostPin;
            SelectedTheme = settings.AppTheme;
            SelectedBillingMode = settings.BillingMode;
            OnPropertyChanged(nameof(IsAdvancedRestricted));
            OnPropertyChanged(nameof(BufferWarningPrimary));
            _isReloading = false;
        }

        private async Task LoadDiagnosticDevicesAsync()
        {
            try
            {
                var devices = await _services.DriveWatcher.GetDevicesAsync().ConfigureAwait(false);
                await UpdateDiagnosticDevicesAsync(devices).ConfigureAwait(false);
            }
            catch
            {
                AppendDiagnosticLine("No se pudieron cargar los dispositivos para diagnostico.");
            }
        }

        private async Task LoadPricingTiersAsync()
        {
            try
            {
                var tiers = await _services.Repositories.PricingTiers.GetAllAsync().ConfigureAwait(false);
                await InvokeOnDispatcherAsync(() =>
                {
                    PricingTiers.Clear();
                    foreach (var tier in tiers.OrderBy(t => t.Order).ThenBy(t => t.MinGB))
                    {
                        PricingTiers.Add(PricingTierRow.FromModel(tier));
                    }

                    ValidateTiers();
                }).ConfigureAwait(false);
            }
            catch
            {
                TierValidationMessage = "No se pudieron cargar los tiers.";
            }
        }

        private bool ValidateTiers()
        {
            TierValidationMessage = string.Empty;
            CanSaveTiers = false;

            var active = PricingTiers.Where(t => t.IsActive).ToList();
            if (active.Count == 0)
            {
                TierValidationMessage = "Debe existir al menos un tier activo.";
                foreach (var row in PricingTiers)
                {
                    row.ErrorMessage = "Inactivo";
                }
                return false;
            }

            var capCount = active.Count(t => t.MinGB >= 1024 && t.MaxGB == 0);
            if (capCount != 1)
            {
                TierValidationMessage = "Debe existir exactamente un tier CAP (MinGB >= 1024 y MaxGB = 0).";
                foreach (var row in PricingTiers)
                {
                    if (row.IsActive && row.IsCap)
                    {
                        row.ErrorMessage = "CAP duplicado";
                    }
                }
                return false;
            }

            foreach (var row in PricingTiers)
            {
                row.ErrorMessage = string.Empty;
            }

            foreach (var tier in active)
            {
                if (tier.MinGB < 1)
                {
                    TierValidationMessage = "MinGB debe ser >= 1.";
                    tier.ErrorMessage = "MinGB invalido";
                    return false;
                }

                if (tier.MaxGB != 0 && tier.MaxGB < tier.MinGB)
                {
                    TierValidationMessage = "MaxGB debe ser >= MinGB (o 0 para infinito).";
                    tier.ErrorMessage = "MaxGB invalido";
                    return false;
                }
            }

            var ordered = active.OrderBy(t => t.MinGB).ThenBy(t => t.MaxGB == 0 ? int.MaxValue : t.MaxGB).ToList();
            for (var i = 0; i < ordered.Count - 1; i++)
            {
                var currentMax = ordered[i].MaxGB == 0 ? int.MaxValue : ordered[i].MaxGB;
                var next = ordered[i + 1];
                var nextIsCap = next.MinGB >= 1024 && next.MaxGB == 0;
                if (currentMax >= next.MinGB && !(nextIsCap && currentMax == next.MinGB))
                {
                    TierValidationMessage = "Hay solapamientos entre tiers.";
                    ordered[i].ErrorMessage = "Solapamiento";
                    next.ErrorMessage = "Solapamiento";
                    return false;
                }
            }

            CanSaveTiers = !IsAdvancedRestricted;
            return true;
        }

        public void RevalidateTiers()
        {
            ValidateTiers();
        }

        private static IEnumerable<PricingTierRow> BuildRecommendedTiers()
        {
            var list = new List<PricingTierRow>();
            var order = 1;

            for (var gb = 1; gb <= 5; gb++)
            {
                list.Add(new PricingTierRow(gb, gb, 0, true, order++));
            }

            for (var start = 6; start <= 46; start += 5)
            {
                list.Add(new PricingTierRow(start, start + 4, 0, true, order++));
            }

            for (var start = 51; start <= 91; start += 10)
            {
                list.Add(new PricingTierRow(start, start + 9, 0, true, order++));
            }

            for (var start = 101; start <= 181; start += 20)
            {
                list.Add(new PricingTierRow(start, start + 19, 0, true, order++));
            }

            for (var start = 201; start <= 1001; start += 20)
            {
                var end = Math.Min(start + 19, 1024);
                list.Add(new PricingTierRow(start, end, 0, true, order++));
                if (end == 1024)
                {
                    break;
                }
            }

            list.Add(new PricingTierRow(1024, 0, 0, true, order++));
            return list;
        }

        private Task UpdateDiagnosticDevicesAsync(IReadOnlyList<DeviceInfo> devices)
        {
            return InvokeOnDispatcherAsync(() =>
            {
                DiagnosticsDevices.Clear();
                foreach (var device in devices)
                {
                    var name = string.IsNullOrWhiteSpace(device.Label) ? device.DriveLetter : device.Label;
                    DiagnosticsDevices.Add(new DeviceChoice(device.Id, $"{device.DriveLetter} {name}"));
                }
            });
        }

        private async Task<string> RunBufferTestAsync(DeviceInfo device)
        {
            var bufferSettings = _services.SettingsService.BufferSettings;
            var tempFile = Path.Combine(Path.GetTempPath(), $"copyops-diag-{Guid.NewGuid()}.tmp");
            var destRoot = Path.Combine(device.DriveLetter.TrimEnd('\\'), "CopyOpsDiag");
            Directory.CreateDirectory(destRoot);
            var destFile = Path.Combine(destRoot, $"diag-{Guid.NewGuid()}.tmp");
            var chunkSize = Math.Max(1, bufferSettings.ChunkSizeMb) * 1024 * 1024;

            try
            {
                var buffer = new byte[1024 * 1024];
                await using (var writer = File.Create(tempFile))
                {
                    for (var i = 0; i < 64; i++)
                    {
                        await writer.WriteAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(false);
                    }
                }

                var stopwatch = Stopwatch.StartNew();
                await using (var source = File.OpenRead(tempFile))
                await using (var dest = File.Create(destFile))
                {
                    var copyBuffer = new byte[chunkSize];
                    int read;
                    long total = 0;
                    while ((read = await source.ReadAsync(copyBuffer.AsMemory(0, copyBuffer.Length)).ConfigureAwait(false)) > 0)
                    {
                        await dest.WriteAsync(copyBuffer.AsMemory(0, read)).ConfigureAwait(false);
                        total += read;
                    }
                    stopwatch.Stop();
                    var seconds = Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001);
                    var avg = total / 1024d / 1024d / seconds;
                    return $"Buffer test {device.DriveLetter.TrimEnd('\\')} | {total / (1024d * 1024d):0.##} MB | Avg {avg:0.#} MB/s | Queue depth: N/A";
                }
            }
            catch (Exception ex)
            {
                return $"Buffer test fallo: {ex.Message}";
            }
            finally
            {
                TryDelete(tempFile);
                TryDelete(destFile);
            }
        }

        private void AppendDiagnosticLine(string line)
        {
            _dispatcher.TryEnqueue(() =>
            {
                DiagnosticsLog.Add($"{DateTime.Now:HH:mm:ss} {line}");
                if (DiagnosticsLog.Count > 200)
                {
                    DiagnosticsLog.RemoveAt(0);
                }
            });
        }

        private void ShowInfoBar(string message, InfoBarSeverity severity)
        {
            _dispatcher.TryEnqueue(() =>
            {
                InfoBarMessage = message;
                InfoBarSeverity = severity;
                InfoBarIsOpen = true;
            });
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

        private void ReloadConstraints()
        {
            OnPropertyChanged(nameof(IsAdvancedRestricted));
            ValidateTiers();
        }

        private static ElementTheme ToElementTheme(string value)
        {
            return value switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // best effort
            }
        }

        public sealed class DeviceChoice
        {
            public string DeviceId { get; }
            public string DisplayName { get; }

            public DeviceChoice(string deviceId, string displayName)
            {
                DeviceId = deviceId;
                DisplayName = displayName;
            }
        }

        public sealed class ThemeOption
        {
            public string Label { get; }
            public string Value { get; }

            public ThemeOption(string label, string value)
            {
                Label = label;
                Value = value;
            }
        }

        public sealed class BillingModeOption
        {
            public string Label { get; }
            public BillingMode Value { get; }

            public BillingModeOption(string label, BillingMode value)
            {
                Label = label;
                Value = value;
            }
        }

        public sealed class PricingTierRow : ObservableObject
        {
            [ObservableProperty]
            private int minGB;

            [ObservableProperty]
            private int maxGB;

            [ObservableProperty]
            private int priceCUP;

            [ObservableProperty]
            private bool isActive;

            [ObservableProperty]
            private int order;

            [ObservableProperty]
            private string errorMessage = string.Empty;

            public bool IsCap => MinGB >= 1024 && MaxGB == 0;
            public string CapLabel => IsCap ? "CAP" : string.Empty;
            public bool IsRowValid => string.IsNullOrWhiteSpace(ErrorMessage);

            public PricingTierRow(int minGB, int maxGB, int priceCUP, bool isActive, int order)
            {
                MinGB = minGB;
                MaxGB = maxGB;
                PriceCUP = priceCUP;
                IsActive = isActive;
                Order = order;
            }

            partial void OnMinGBChanged(int value)
            {
                OnPropertyChanged(nameof(IsCap));
                OnPropertyChanged(nameof(CapLabel));
            }

            partial void OnMaxGBChanged(int value)
            {
                OnPropertyChanged(nameof(IsCap));
                OnPropertyChanged(nameof(CapLabel));
            }

            partial void OnErrorMessageChanged(string value)
            {
                OnPropertyChanged(nameof(IsRowValid));
            }

            public PricingTier ToModel()
            {
                return new PricingTier
                {
                    TierId = $"{MinGB}-{MaxGB}-{Order}",
                    MinGB = MinGB,
                    MaxGB = MaxGB,
                    PriceCUP = PriceCUP,
                    IsActive = IsActive,
                    Order = Order
                };
            }

            public static PricingTierRow FromModel(PricingTier tier)
            {
                return new PricingTierRow(tier.MinGB, tier.MaxGB, tier.PriceCUP, tier.IsActive, tier.Order);
            }
        }
    }
}
