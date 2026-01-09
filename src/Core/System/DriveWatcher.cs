using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CopyOpsSuite.Core.Models;
using CopyOpsSuite.Storage;

namespace CopyOpsSuite.System
{
    public sealed class DriveWatcher : IDisposable
    {
        private readonly Repositories _repositories;
        private readonly SemaphoreSlim _sync = new(1, 1);
        private readonly global::System.Timers.Timer _timer;
        private IReadOnlyList<DeviceInfo> _lastSnapshot = Array.Empty<DeviceInfo>();
        private readonly UsbBusInspector _busInspector = new();

        public DriveWatcher(Repositories repositories)
        {
            _repositories = repositories;
            _timer = new global::System.Timers.Timer(2000);
            _timer.Elapsed += async (_, _) => await RefreshAsync().ConfigureAwait(false);
        }

        public event EventHandler<IReadOnlyList<DeviceInfo>>? DevicesChanged;

        public void Start()
        {
            _timer.Start();
            _ = RefreshAsync();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        public async Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync()
        {
            return await BuildSnapshotAsync().ConfigureAwait(false);
        }

        private async Task RefreshAsync()
        {
            if (!await _sync.WaitAsync(0).ConfigureAwait(false))
            {
                return;
            }

            try
            {
                var snapshot = await BuildSnapshotAsync().ConfigureAwait(false);
                if (IsSnapshotEqual(_lastSnapshot, snapshot))
                {
                    return;
                }

                _lastSnapshot = snapshot;
                DevicesChanged?.Invoke(this, snapshot);
            }
            finally
            {
                _sync.Release();
            }
        }

        private static bool IsSnapshotEqual(IReadOnlyList<DeviceInfo> left, IReadOnlyList<DeviceInfo> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (var i = 0; i < left.Count; i++)
            {
                if (!string.Equals(left[i].Id, right[i].Id, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (left[i].FreeBytes != right[i].FreeBytes || left[i].TotalBytes != right[i].TotalBytes)
                {
                    return false;
                }
            }

            return true;
        }

        private async Task<IReadOnlyList<DeviceInfo>> BuildSnapshotAsync()
        {
            var protectedIds = await _repositories.DeviceProtections.GetProtectedIdsAsync().ConfigureAwait(false);
            var devices = new List<DeviceInfo>();
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady)
                    {
                        continue;
                    }

                    var id = NormalizeDriveId(drive.RootDirectory.FullName);
                    var busInfo = _busInspector.Inspect(id);
                    devices.Add(new DeviceInfo
                    {
                        Id = id,
                        DriveLetter = drive.Name,
                        Label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "(sin etiqueta)" : drive.VolumeLabel,
                        FileSystem = drive.DriveFormat,
                        TotalBytes = drive.TotalSize,
                        FreeBytes = drive.TotalFreeSpace,
                        Type = MapDeviceType(drive),
                        BusHint = $"{busInfo.SpeedHint} ({busInfo.ConnectionType})",
                        IsProtectedFromFormat = protectedIds.Contains(id)
                    });
                }
                catch
                {
                    // Ignore drives that throw on query.
                }
            }

            devices.Sort((a, b) => string.Compare(a.DriveLetter, b.DriveLetter, StringComparison.OrdinalIgnoreCase));
            return devices.AsReadOnly();
        }

        private static DeviceType MapDeviceType(DriveInfo drive)
        {
            return drive.DriveType switch
            {
                DriveType.Fixed => DeviceType.FixedDisk,
                DriveType.Removable => DeviceType.RemovableUsb,
                DriveType.Network => DeviceType.Network,
                DriveType.Ram => DeviceType.FixedDisk,
                DriveType.CDRom => DeviceType.RemovableUsb,
                DriveType.NoRootDirectory => DeviceType.System,
                _ => DeviceType.System
            };
        }

        private static string NormalizeDriveId(string root)
        {
            return root.TrimEnd(Path.DirectorySeparatorChar);
        }

        public void Dispose()
        {
            _timer?.Stop();
            _timer?.Dispose();
            _sync?.Dispose();
        }
    }
}
