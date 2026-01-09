using System;

namespace CopyOpsSuite.Core.Models
{
    public enum EventSeverity
    {
        Info,
        Warn,
        Critical
    }

    public sealed record AppEvent(
        Guid EventId,
        DateTime Ts,
        string Type,
        EventSeverity Severity,
        string Message,
        Guid? JobId = null,
        string? DeviceId = null,
        Guid? SaleId = null);
}
