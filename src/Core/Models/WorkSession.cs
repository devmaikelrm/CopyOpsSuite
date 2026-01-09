using System;
using System.Collections.Generic;

namespace CopyOpsSuite.Core.Models
{
    public sealed class WorkSession
    {
        public Guid SessionId { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime? End { get; set; }
        public string OperatorName { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public List<AppEvent> Events { get; set; } = new();
    }
}
