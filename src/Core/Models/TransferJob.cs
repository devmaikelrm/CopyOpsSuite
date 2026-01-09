using System;
using System.Collections.Generic;

namespace CopyOpsSuite.Core.Models
{
    public enum JobStatus
    {
        Pending,
        Running,
        Paused,
        Completed,
        Canceled,
        Error
    }

    public sealed class TransferJob
    {
        public Guid JobId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public string OperatorName { get; set; }
        public string SourcePath { get; set; }
        public string ProfileId { get; set; }
        public JobStatus Status { get; set; }
        public long BytesPlanned { get; set; }
        public long BytesOk { get; set; }
        public long BytesFailed { get; set; }
        public List<TransferTarget> Targets { get; set; } = new();
    }
}
