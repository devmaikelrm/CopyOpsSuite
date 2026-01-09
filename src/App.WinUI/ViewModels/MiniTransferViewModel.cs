using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CopyOpsSuite.App.WinUI.ViewModels
{
    public partial class MiniTransferViewModel : ObservableObject
    {
        [ObservableProperty]
        private string deviceId = string.Empty;

        [ObservableProperty]
        private Guid jobId;

        [ObservableProperty]
        private string fileSystem = string.Empty;

        [ObservableProperty]
        private string busHint = string.Empty;

        [ObservableProperty]
        private string workersText = "0/1";

        [ObservableProperty]
        private string queueText = "Cola: 0";

        [ObservableProperty]
        private string stateText = "En espera";

        [ObservableProperty]
        private double progressValue;

        [ObservableProperty]
        private string currentSpeed = "--";

        [ObservableProperty]
        private string averageSpeed = "--";

        [ObservableProperty]
        private string currentFile = "--";

        [ObservableProperty]
        private string elapsed = "--";

        [ObservableProperty]
        private bool isPaused;

        [ObservableProperty]
        private string statusMessage = "Listo";

        [ObservableProperty]
        private string chargeNowDisplay = "Cobro ahora: 0 CUP+";

        [ObservableProperty]
        private string chargeFinalDisplay = "Cobro final: 0 CUP+";

        [ObservableProperty]
        private string chargeTooltip = string.Empty;

        public string PauseResumeText => IsPaused ? "Reanudar" : "Pausar";

        public event EventHandler? DetailsRequested;

        public void UpdateFromRow(DeviceRowViewModel row)
        {
            if (row == null) return;
            DeviceId = row.DeviceId;
            FileSystem = row.FileSystem;
            BusHint = row.BusHint;
            WorkersText = row.WorkersText;
            QueueText = row.QueueText;
            StateText = row.StateText;
            ProgressValue = row.ProgressValue;
            CurrentSpeed = row.CurrentSpeedText;
            AverageSpeed = row.AverageSpeedText;
            CurrentFile = row.TooltipText;
            Elapsed = row.EtaText;
            ChargeNowDisplay = $"Cobro ahora: {row.ExpectedNow:N0} {row.Currency}";
            ChargeFinalDisplay = $"Cobro final: {row.ExpectedFinal:N0} {row.Currency}";
            ChargeTooltip = row.ChargeTooltip;
        }

        public void Reset()
        {
            ProgressValue = 0;
            CurrentSpeed = "--";
            AverageSpeed = "--";
            CurrentFile = "--";
            Elapsed = "--";
            WorkersText = "0/1";
            QueueText = "Cola: 0";
            StateText = "En espera";
            StatusMessage = "Listo";
            ChargeNowDisplay = "Cobro ahora: 0 CUP+";
            ChargeFinalDisplay = "Cobro final: 0 CUP+";
            ChargeTooltip = string.Empty;
        }

        partial void OnIsPausedChanged(bool value)
        {
            StatusMessage = value ? "En pausa" : "Activo";
            OnPropertyChanged(nameof(PauseResumeText));
        }

        [RelayCommand]
        private void TogglePause()
        {
            IsPaused = !IsPaused;
        }

        [RelayCommand]
        private void Stop()
        {
            StatusMessage = "Detenido";
        }

        [RelayCommand]
        private void OpenDetails()
        {
            DetailsRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
