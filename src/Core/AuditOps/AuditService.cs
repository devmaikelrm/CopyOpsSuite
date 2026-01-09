using System;
using CopyOpsSuite.Core.Models;

namespace CopyOpsSuite.AuditOps
{
    public class AuditService
    {
        private readonly EventBus _bus;

        public AuditService(EventBus bus)
        {
            _bus = bus;
        }

        public void RecordEvent(
            string type,
            string message,
            EventSeverity severity = EventSeverity.Info,
            Guid? jobId = null,
            string? deviceId = null,
            Guid? saleId = null)
        {
            var appEvent = new AppEvent(Guid.NewGuid(), DateTime.UtcNow, type, severity, message, jobId, deviceId, saleId);
            _bus.Publish(appEvent);
        }
    }
}
