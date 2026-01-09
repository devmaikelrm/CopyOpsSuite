using System;

namespace CopyOpsSuite.Core.Models
{
    public sealed class TransferItemLog
    {
        public Guid JobId { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string Action { get; set; } = "COPY";
        public string Source { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string Extension { get; set; } = string.Empty;
        public string Status { get; set; } = "OK";
        public DateTime Ts { get; set; }
    }
}
