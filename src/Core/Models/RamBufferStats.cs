using System;

namespace CopyOpsSuite.Core.Models
{
    public sealed class RamBufferStats
    {
        public Guid JobId { get; set; }
        public long BytesBuffered { get; set; }
        public int QueueDepth { get; set; }
        public double ThroughputMBps { get; set; }
        public DateTime Ts { get; set; }
    }
}
