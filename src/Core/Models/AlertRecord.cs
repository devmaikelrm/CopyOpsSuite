using System;

namespace CopyOpsSuite.Core.Models
{
    public sealed record AlertRecord(
        Guid AlertId,
        string Message,
        EventSeverity Severity,
        Guid? JobId,
        string? DeviceId,
        DateTime RaisedAt,
        DateTime? ResolvedAt)
    {
        public bool IsResolved => ResolvedAt != null;
    }
}
