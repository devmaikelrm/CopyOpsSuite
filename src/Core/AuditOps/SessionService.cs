using System;
using CopyOpsSuite.Core.Models;

namespace CopyOpsSuite.AuditOps
{
    public sealed class SessionService
    {
        public WorkSession CreateSession(string name, string operatorName)
        {
            return new WorkSession
            {
                SessionId = Guid.NewGuid(),
                Name = string.IsNullOrWhiteSpace(name) ? "Sesión" : name,
                Start = DateTime.UtcNow,
                OperatorName = operatorName ?? string.Empty
            };
        }
    }
}
