using System;

namespace CopyOpsSuite.Core.Models
{
    public sealed class TransferTarget
    {
        public Guid JobId { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string TargetRootPath { get; set; } = string.Empty;
        public JobStatus Status { get; set; } = JobStatus.Pending;
        public long BytesOk { get; set; }
        public long BytesFailed { get; set; }

        public double CurrentMBps { get; set; }
        public double MaxMBps { get; set; }
        public double AvgMBps { get; set; }

        public int QueueCount { get; set; }
        public TimeSpan Elapsed { get; set; }
        public TimeSpan? ETA { get; set; }

        public bool MiniSimpleMode { get; set; }
        public int MiniOpacityPercent { get; set; } = 90;
        public string MiniDockSide { get; set; } = "Left";
        public int MiniTopMargin { get; set; } = 80;
    }
}
