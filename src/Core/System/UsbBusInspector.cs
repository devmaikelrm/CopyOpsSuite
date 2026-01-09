using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;

namespace CopyOpsSuite.System
{
    public sealed record BusInfo(string SpeedHint, string ConnectionType, string FriendlyName);

    public sealed class UsbBusInspector
    {
        private readonly ManagementScope _scope = new(@"\\.\root\cimv2");
        private readonly Dictionary<string, BusInfo> _cache = new(StringComparer.OrdinalIgnoreCase);

        public BusInfo Inspect(string driveLetter)
        {
            if (string.IsNullOrWhiteSpace(driveLetter))
            {
                return new BusInfo("Unknown", "Unknown", string.Empty);
            }

            var normalized = driveLetter.TrimEnd('\\').ToUpperInvariant();
            if (_cache.TryGetValue(normalized, out var cached))
            {
                return cached;
            }

            var info = QueryBusInfo(normalized);
            _cache[normalized] = info;
            return info;
        }

        private BusInfo QueryBusInfo(string driveLetter)
        {
            try
            {
                var assocPartition = new ManagementObjectSearcher(_scope, new ObjectQuery(
                    $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{driveLetter}'}} WHERE AssocClass=Win32_LogicalDiskToPartition"));
                foreach (ManagementObject partition in assocPartition.Get())
                {
                    var diskAssoc = new ManagementObjectSearcher(_scope, new ObjectQuery(
                        $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition"));
                    foreach (ManagementObject disk in diskAssoc.Get())
                    {
                        var interfaceType = disk["InterfaceType"]?.ToString() ?? "Unknown";
                        var caption = disk["Caption"]?.ToString() ?? string.Empty;
                        var pnpId = disk["PNPDeviceID"]?.ToString() ?? string.Empty;

                        var connection = MapConnection(interfaceType, pnpId, caption);
                        var speed = MapSpeedHint(connection, pnpId, caption);
                        return new BusInfo(speed, connection, caption);
                    }
                }
            }
            catch
            {
                // WMI might not be available; fallback to unknown.
            }

            return new BusInfo("Unknown", "Unknown", string.Empty);
        }

        private static string MapConnection(string interfaceType, string pnpId, string caption)
        {
            if (interfaceType.Contains("USB", StringComparison.OrdinalIgnoreCase) || pnpId.Contains("USBSTOR", StringComparison.OrdinalIgnoreCase))
            {
                return "USB";
            }

            if (interfaceType.Contains("SCSI", StringComparison.OrdinalIgnoreCase) || caption.Contains("NVMe", StringComparison.OrdinalIgnoreCase))
            {
                return "SATA/NVMe";
            }

            return interfaceType switch
            {
                string s when s.Contains("IDE", StringComparison.OrdinalIgnoreCase) => "IDE",
                _ => interfaceType
            };
        }

        private static string MapSpeedHint(string connection, string pnpId, string caption)
        {
            if (!connection.Equals("USB", StringComparison.OrdinalIgnoreCase))
            {
                return connection;
            }

            if (caption.Contains("USB 3.0", StringComparison.OrdinalIgnoreCase) || caption.Contains("USB 3", StringComparison.OrdinalIgnoreCase))
            {
                return "USB 3.0";
            }

            if (pnpId.Contains("USB3", StringComparison.OrdinalIgnoreCase))
            {
                return "USB 3.0";
            }

            if (pnpId.Contains("USB", StringComparison.OrdinalIgnoreCase))
            {
                return "USB 2.0";
            }

            return "USB (Unknown speed)";
        }
    }
}
