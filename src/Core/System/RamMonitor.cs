using System;
using System.Runtime.InteropServices;
namespace CopyOpsSuite.System
{
    public sealed class RamMonitor : IDisposable
    {
        private readonly global::System.Timers.Timer _timer;

        public RamMonitor()
        {
            _timer = new global::System.Timers.Timer(1000);
            _timer.Elapsed += (_, _) => OnTick();
        }

        public event EventHandler<string>? RamUpdated;

        public void Start()
        {
            _timer.Start();
            OnTick();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        private void OnTick()
        {
            var status = GetFormattedStatus();
            RamUpdated?.Invoke(this, status);
        }

        private static string GetFormattedStatus()
        {
            var memory = GetMemoryStatus();
            var used = memory.ullTotalPhys - memory.ullAvailPhys;
            var total = memory.ullTotalPhys;
            var percent = total == 0 ? 0 : (int)Math.Round(used * 100.0 / total);
            return $"RAM {BytesToReadable(used)}/{BytesToReadable(total)} ({percent}%)";
        }

        private static MEMORYSTATUSEX GetMemoryStatus()
        {
            var status = new MEMORYSTATUSEX();
            if (!GlobalMemoryStatusEx(ref status))
            {
                throw new InvalidOperationException("No se pudo leer la memoria del sistema.");
            }

            return status;
        }

        private static string BytesToReadable(ulong value)
        {
            const double scale = 1024;
            var units = new[] { "B", "KB", "MB", "GB", "TB" };
            var size = (double)value;
            var order = 0;
            while (size >= scale && order < units.Length - 1)
            {
                order++;
                size /= scale;
            }

            return $"{size:0.#} {units[order]}";
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                dwMemoryLoad = 0;
                ullTotalPhys = 0;
                ullAvailPhys = 0;
                ullTotalPageFile = 0;
                ullAvailPageFile = 0;
                ullTotalVirtual = 0;
                ullAvailVirtual = 0;
                ullAvailExtendedVirtual = 0;
            }
        }

        public void Dispose()
        {
            _timer?.Stop();
            _timer?.Dispose();
        }
    }
}
