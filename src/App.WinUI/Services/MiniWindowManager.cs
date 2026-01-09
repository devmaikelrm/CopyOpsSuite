using System;
using System.Collections.Generic;
using System.Linq;
using CopyOpsSuite.App.WinUI.Views;
using CopyOpsSuite.App.WinUI.ViewModels;
using CopyOpsSuite.Core.Models;
using CopyOpsSuite.System;
using Microsoft.UI.Windowing;
using WinRT.Interop;
using Windows.Graphics;

namespace CopyOpsSuite.App.WinUI.Services
{
    internal sealed class MiniWindowManager
    {
        private readonly AppServices _services;
        private readonly List<MiniWindowHolder> _holders = new();

        public MiniWindowManager(AppServices services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public void OpenForDevices(IEnumerable<DeviceRowViewModel> rows, Guid jobId)
        {
            var settings = _services.SettingsService.MiniWindowSettings;
            if (!settings.MiniAutoOpen)
            {
                return;
            }

            foreach (var row in rows)
            {
                if (_holders.Any(h => string.Equals(h.DeviceId, row.DeviceId, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var window = new MiniTransferWindow();
                var holder = new MiniWindowHolder(row.DeviceId, window, _services, jobId);
                holder.ApplySettings(settings);
                holder.Update(row);
                _holders.Add(holder);
                window.Activate();
            }

            ArrangeWindows(settings);
        }

        public void UpdateFromRows(IEnumerable<DeviceRowViewModel> rows)
        {
            var lookup = rows.ToDictionary(r => r.DeviceId, StringComparer.OrdinalIgnoreCase);
            foreach (var holder in _holders)
            {
                if (lookup.TryGetValue(holder.DeviceId, out var row))
                {
                    holder.Update(row);
                }
            }
        }

        public void HandleJobCompletion(TransferJob job)
        {
            var settings = _services.SettingsService.MiniWindowSettings;
            var success = job.Status == JobStatus.Completed && job.BytesFailed == 0;

            if (success && settings.MiniAutoCleanNoErrors)
            {
                foreach (var holder in _holders)
                {
                    holder.Reset();
                }
            }

            if (success && settings.MiniAutoCloseNoErrors)
            {
                CloseAll();
                return;
            }

            if (!success)
            {
                foreach (var holder in _holders)
                {
                    holder.ViewModel.StatusMessage = "Finalizado con errores";
                }
            }
        }

        private void CloseAll()
        {
            foreach (var holder in _holders)
            {
                holder.Close();
            }

            _holders.Clear();
        }

        private void ArrangeWindows(MiniWindowSettings settings)
        {
            if (!_holders.Any())
            {
                return;
            }

            var area = GetWorkingArea();
            const int windowWidth = 360;
            const int windowHeight = 260;
            const int spacing = 12;
            var dockRight = settings.MiniDockSide.Equals("Right", StringComparison.OrdinalIgnoreCase);
            var startX = dockRight
                ? area.Right - windowWidth - spacing
                : area.Left + spacing;
            var xStep = (dockRight ? -1 : 1) * (windowWidth + spacing);
            var y = area.Top + settings.MiniTopMarginPx;
            var column = 0;

            foreach (var holder in _holders)
            {
                var x = startX + column * xStep;
                var pos = new PointInt32(ClampHorizontal(x, area, windowWidth), ClampVertical(y, area, windowHeight));
                holder.MoveTo(pos);
                y += windowHeight + spacing;

                if (y + windowHeight > area.Bottom - spacing)
                {
                    column++;
                    y = area.Top + settings.MiniTopMarginPx;
                }
            }
        }

        private static int ClampHorizontal(double x, Rect area, int width)
        {
            var min = area.Left;
            var max = area.Right - width;
            return (int)Math.Clamp(x, min, Math.Max(min, max));
        }

        private static int ClampVertical(double y, Rect area, int height)
        {
            var min = area.Top;
            var max = area.Bottom - height;
            return (int)Math.Clamp(y, min, Math.Max(min, max));
        }

        private static Rect GetWorkingArea()
        {
            if (App.MainWindowInstance == null)
            {
                return new Rect(0, 0, 1280, 720);
            }

            var hwnd = WindowNative.GetWindowHandle(App.MainWindowInstance);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var area = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
            return area.WorkArea;
        }
    }

    internal sealed class MiniWindowHolder
    {
        private readonly AppServices _services;
        private readonly Guid _jobId;
        private readonly AppWindow _appWindow;
        public string DeviceId { get; }
        public MiniTransferWindow Window { get; }

        public MiniTransferViewModel ViewModel => Window.ViewModel;

        public MiniWindowHolder(string deviceId, MiniTransferWindow window, AppServices services, Guid jobId)
        {
            DeviceId = deviceId;
            Window = window;
            _services = services;
            _jobId = jobId;
            var handle = WindowNative.GetWindowHandle(window);
            var windowId = Win32Interop.GetWindowIdFromWindow(handle);
            _appWindow = AppWindow.GetFromWindowId(windowId);
            ViewModel.JobId = jobId;
            ViewModel.DetailsRequested += ViewModel_DetailsRequested;
        }

        private void ViewModel_DetailsRequested(object? sender, EventArgs e)
        {
            _ = _services.ShowJobDetailAsync(_jobId);
        }

        public void ApplySettings(MiniWindowSettings settings)
        {
            var opacity = Math.Clamp(settings.MiniOpacityPercent / 100.0, 0.2, 1.0);
            Window.Opacity = opacity;
            SetTopMost(settings.TopMostPin);
        }

        public void Update(DeviceRowViewModel row)
        {
            ViewModel.JobId = _jobId;
            ViewModel.UpdateFromRow(row);
        }

        public void Reset()
        {
            ViewModel.Reset();
        }

        public void MoveTo(PointInt32 position)
        {
            _appWindow.Move(position);
        }

        public void SetTopMost(bool topMost)
        {
            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsAlwaysOnTop = topMost;
            }
        }

        public void Close()
        {
            ViewModel.DetailsRequested -= ViewModel_DetailsRequested;
            Window.Close();
        }
    }
}
