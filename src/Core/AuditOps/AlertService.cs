using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CopyOpsSuite.Core.Models;
using CopyOpsSuite.Storage;

namespace CopyOpsSuite.AuditOps
{
    public class AlertService
    {
        private readonly AlertsRepository _repository;
        private readonly EventBus _bus;
        private readonly ConcurrentDictionary<Guid, AlertRecord> _active = new();

        public AlertService(AlertsRepository repository, EventBus bus)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        public IReadOnlyCollection<AlertRecord> ActiveAlerts => _active.Values.ToList();

        public async Task<AlertRecord> RaiseAlertAsync(string message, EventSeverity severity = EventSeverity.Warn, Guid? jobId = null, string? deviceId = null)
        {
            var alert = new AlertRecord(Guid.NewGuid(), message, severity, jobId, deviceId, DateTime.UtcNow, null);
            _active[alert.AlertId] = alert;
            _bus.Publish(new AppEvent(Guid.NewGuid(), DateTime.UtcNow, "ALERT", severity, message, jobId, deviceId));
            return alert;
        }

        public bool TryGetAlert(Guid alertId, out AlertRecord? alert) => _active.TryGetValue(alertId, out alert);

        public Task<IEnumerable<AlertRecord>> GetResolvedAsync() => _repository.GetResolvedAsync();

        public async Task MarkResolvedAsync(Guid alertId)
        {
            if (_active.TryRemove(alertId, out var alert))
            {
                var resolved = alert with { ResolvedAt = DateTime.UtcNow };
                await _repository.SaveResolvedAsync(resolved).ConfigureAwait(false);
                _bus.Publish(new AppEvent(Guid.NewGuid(), DateTime.UtcNow, "ALERT_RESOLVED", EventSeverity.Info, $"Resolución: {resolved.Message}", resolved.JobId, resolved.DeviceId));
            }
        }
    }
}
