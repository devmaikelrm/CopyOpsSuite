using System;
using System.Collections.Concurrent;
using CopyOpsSuite.Core.Models;

namespace CopyOpsSuite.AuditOps
{
    public class EventBus
    {
        private readonly BlockingCollection<AppEvent> _events = new();

        public void Publish(AppEvent appEvent)
        {
            _events.Add(appEvent);
        }

        public BlockingCollection<AppEvent> Events => _events;
    }
}
