using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CopyOpsSuite.Core.Models;
using CopyOpsSuite.MultiCopyEngine;

namespace CopyOpsSuite.App.WinUI.ViewModels
{
    public enum HealthState
    {
        Ok,
        Warn,
        Critical
    }

    public enum TransferStateLabel
    {
        Idle,
        Reading,
        Writing,
        BufferWait,
        Paused,
        DoneOk,
        DoneErrors
    }

    public sealed class DeviceRowViewModel : INotifyPropertyChanged
    {
        public string DeviceId { get; }

        private string _driveLetter = string.Empty;
        private string _label = string.Empty;
        private string _fileSystem = string.Empty;
        private string _busHint = string.Empty;
        private bool _isSelected;
        private HealthState _health = HealthState.Ok;
        private string _healthText = "OK";
        private TransferStateLabel _state = TransferStateLabel.Idle;
        private string _stateText = "En espera";
        private int _activeWorkers;
        private int _workerSlots = 1;
        private int _queueCount;
        private string _workersText = "0/1";
        private string _queueText = "Cola: 0";
        private double _progressValue;
        private string _progressText = "0%";
        private double _currentMBps;
        private double _maxMBps;
        private double _avgMBps;
        private string _etaText = "--";
        private string _tooltipText = string.Empty;
        private bool _queueWarning;
        private string _dataText = "0 GB / 0 GB";
        private string _currentSpeedText = "--";
        private string _averageSpeedText = "--";
        private decimal _expectedNow;
        private decimal _expectedFinal;
        private decimal _realGbNow;
        private decimal _billableGbNow;
        private decimal _realGbFinal;
        private decimal _billableGbFinal;
        private string _currency = "CUP+";
        private string _appliedTierNow = string.Empty;
        private string _appliedTierFinal = string.Empty;
        private string _chargeNowDisplay = "Cobro ahora: 0 CUP+";
        private string _chargeFinalDisplay = "Cobro final: 0 CUP+";
        private string _chargeTooltip = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public DeviceRowViewModel(string deviceId)
        {
            DeviceId = deviceId;
        }

        public string DriveLetter
        {
            get => _driveLetter;
            set => SetProperty(ref _driveLetter, value);
        }

        public string Label
        {
            get => _label;
            set => SetProperty(ref _label, value);
        }

        public string FileSystem
        {
            get => _fileSystem;
            set => SetProperty(ref _fileSystem, value);
        }

        public string BusHint
        {
            get => _busHint;
            set => SetProperty(ref _busHint, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public HealthState Health
        {
            get => _health;
            private set
            {
                if (SetProperty(ref _health, value))
                {
                    HealthText = value switch
                    {
                        HealthState.Ok => "OK",
                        HealthState.Warn => "Lento",
                        HealthState.Critical => "Error",
                        _ => value.ToString()
                    };
                }
            }
        }

        public string HealthText
        {
            get => _healthText;
            private set => SetProperty(ref _healthText, value);
        }

        public TransferStateLabel State
        {
            get => _state;
            private set
            {
                if (SetProperty(ref _state, value))
                {
                    StateText = value switch
                    {
                        TransferStateLabel.Reading => "Leyendo",
                        TransferStateLabel.Writing => "Escribiendo",
                        TransferStateLabel.BufferWait => "En buffer",
                        TransferStateLabel.Paused => "Pausado",
                        TransferStateLabel.DoneOk => "Completado",
                        TransferStateLabel.DoneErrors => "Completado con errores",
                        _ => "En espera"
                    };
                }
            }
        }

        public string StateText
        {
            get => _stateText;
            private set => SetProperty(ref _stateText, value);
        }

        public int ActiveWorkers
        {
            get => _activeWorkers;
            private set
            {
                if (SetProperty(ref _activeWorkers, value))
                {
                    WorkersText = $"{_activeWorkers}/{Math.Max(1, _workerSlots)}";
                }
            }
        }

        public int WorkerSlots
        {
            get => _workerSlots;
            private set
            {
                if (SetProperty(ref _workerSlots, value))
                {
                    WorkersText = $"{_activeWorkers}/{Math.Max(1, _workerSlots)}";
                }
            }
        }

        public string WorkersText
        {
            get => _workersText;
            private set => SetProperty(ref _workersText, value);
        }

        public int QueueCount
        {
            get => _queueCount;
            private set
            {
                if (SetProperty(ref _queueCount, value))
                {
                    QueueText = $"Cola: {value}";
                }
            }
        }

        public string QueueText
        {
            get => _queueText;
            private set => SetProperty(ref _queueText, value);
        }

        public double ProgressValue
        {
            get => _progressValue;
            set
            {
                if (SetProperty(ref _progressValue, value))
                {
                    ProgressText = $"{Math.Round(value * 100)}%";
                }
            }
        }

        public string ProgressText
        {
            get => _progressText;
            private set => SetProperty(ref _progressText, value);
        }

        public double CurrentMBps
        {
            get => _currentMBps;
            private set => SetProperty(ref _currentMBps, value);
        }

        public double MaxMBps
        {
            get => _maxMBps;
            private set => SetProperty(ref _maxMBps, value);
        }

        public double AvgMBps
        {
            get => _avgMBps;
            private set => SetProperty(ref _avgMBps, value);
        }

        public string EtaText
        {
            get => _etaText;
            private set => SetProperty(ref _etaText, value);
        }

        public string TooltipText
        {
            get => _tooltipText;
            private set => SetProperty(ref _tooltipText, value);
        }

        public bool QueueWarning
        {
            get => _queueWarning;
            private set => SetProperty(ref _queueWarning, value);
        }

        public string DataText
        {
            get => _dataText;
            private set => SetProperty(ref _dataText, value);
        }

        public string CurrentSpeedText
        {
            get => _currentSpeedText;
            private set => SetProperty(ref _currentSpeedText, value);
        }

        public string AverageSpeedText
        {
            get => _averageSpeedText;
            private set => SetProperty(ref _averageSpeedText, value);
        }

        public decimal ExpectedNow
        {
            get => _expectedNow;
            private set => SetProperty(ref _expectedNow, value);
        }

        public decimal ExpectedFinal
        {
            get => _expectedFinal;
            private set => SetProperty(ref _expectedFinal, value);
        }

        public decimal RealGbNow
        {
            get => _realGbNow;
            private set => SetProperty(ref _realGbNow, value);
        }

        public decimal BillableGbNow
        {
            get => _billableGbNow;
            private set => SetProperty(ref _billableGbNow, value);
        }

        public decimal RealGbFinal
        {
            get => _realGbFinal;
            private set => SetProperty(ref _realGbFinal, value);
        }

        public decimal BillableGbFinal
        {
            get => _billableGbFinal;
            private set => SetProperty(ref _billableGbFinal, value);
        }

        public string Currency
        {
            get => _currency;
            private set => SetProperty(ref _currency, value);
        }

        public string AppliedTierNow
        {
            get => _appliedTierNow;
            private set => SetProperty(ref _appliedTierNow, value);
        }

        public string AppliedTierFinal
        {
            get => _appliedTierFinal;
            private set => SetProperty(ref _appliedTierFinal, value);
        }

        public string ChargeNowDisplay
        {
            get => _chargeNowDisplay;
            private set => SetProperty(ref _chargeNowDisplay, value);
        }

        public string ChargeFinalDisplay
        {
            get => _chargeFinalDisplay;
            private set => SetProperty(ref _chargeFinalDisplay, value);
        }

        public string ChargeTooltip
        {
            get => _chargeTooltip;
            private set => SetProperty(ref _chargeTooltip, value);
        }

        public void UpdateDeviceInfo(DeviceInfo device)
        {
            DriveLetter = device.DriveLetter;
            Label = device.Label;
            FileSystem = device.FileSystem;
            BusHint = device.BusHint;
        }

        public void ApplySnapshot(TargetSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            Health = snapshot.Health switch
            {
                TransferHealth.Warn => HealthState.Warn,
                TransferHealth.Critical => HealthState.Critical,
                _ => HealthState.Ok
            };

            State = snapshot.State switch
            {
                TransferState.Reading => TransferStateLabel.Reading,
                TransferState.Writing => TransferStateLabel.Writing,
                TransferState.BufferWait => TransferStateLabel.BufferWait,
                TransferState.Paused => TransferStateLabel.Paused,
                TransferState.DoneError => TransferStateLabel.DoneErrors,
                TransferState.DoneOk => TransferStateLabel.DoneOk,
                _ => TransferStateLabel.Idle
            };

            ActiveWorkers = snapshot.ActiveWorkers;
            WorkerSlots = Math.Max(1, snapshot.WorkerSlots);
            QueueCount = snapshot.QueueCount;
            QueueWarning = snapshot.QueueWarning;

            CurrentMBps = snapshot.CurrentMBps;
            MaxMBps = snapshot.MaxMBps;
            AvgMBps = snapshot.AvgMBps;
            EtaText = snapshot.Eta.HasValue ? snapshot.Eta.Value.ToString(@"hh\:mm\:ss") : "--";

            ProgressValue = snapshot.Progress;
            DataText = BuildDataText(snapshot);
            CurrentSpeedText = FormatSpeed(snapshot.CurrentMBps);
            AverageSpeedText = snapshot.AvgMBps > 0 ? $"Prom: {snapshot.AvgMBps:0.#} MB/s" : "Prom: --";
            TooltipText = BuildTooltip(snapshot);
        }

        public void UpdatePricing(decimal expectedNow, decimal expectedFinal, decimal realGbNow, decimal billableGbNow, decimal realGbFinal, decimal billableGbFinal, string currency, string appliedTierNow, string appliedTierFinal)
        {
            ExpectedNow = expectedNow;
            ExpectedFinal = expectedFinal;
            RealGbNow = realGbNow;
            BillableGbNow = billableGbNow;
            RealGbFinal = realGbFinal;
            BillableGbFinal = billableGbFinal;
            Currency = currency;
            AppliedTierNow = appliedTierNow;
            AppliedTierFinal = appliedTierFinal;

            ChargeNowDisplay = $"Cobro ahora: {expectedNow:N0} {currency}";
            ChargeFinalDisplay = $"Cobro final: {expectedFinal:N0} {currency}";
            ChargeTooltip = $"GB reales -> GB cobrables\nAhora: {realGbNow:0.##} -> {billableGbNow:0.##}\nFinal: {realGbFinal:0.##} -> {billableGbFinal:0.##}\nTarifa aplicada: {appliedTierNow}\nTarifa final: {appliedTierFinal}";
        }

        private string BuildTooltip(TargetSnapshot snapshot)
        {
            var copiedGb = BytesToGb(snapshot.BytesOk);
            var plannedGb = BytesToGb(snapshot.BytesPlanned);
            var eta = snapshot.Eta.HasValue ? snapshot.Eta.Value.ToString(@"hh\:mm\:ss") : "n/a";
            var current = FormatSpeed(snapshot.CurrentMBps);
            var average = FormatSpeed(snapshot.AvgMBps);
            return $"Copiado {copiedGb:0.##}/{plannedGb:0.##} GB | ETA {eta} | {current} / {average}";
        }

        private static double BytesToGb(long bytes) => bytes / 1024d / 1024d / 1024d;

        private static string FormatSpeed(double mbps) => mbps <= 0 ? "--" : $"{mbps:0.#} MB/s";

        private static string BuildDataText(TargetSnapshot snapshot)
        {
            var copiedGb = BytesToGb(snapshot.BytesOk);
            var plannedGb = BytesToGb(snapshot.BytesPlanned);
            return $"{copiedGb:0.##} GB / {plannedGb:0.##} GB";
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value!;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
