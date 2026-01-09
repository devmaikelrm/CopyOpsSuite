using System;

namespace CopyOpsSuite.Core.Models
{
    public sealed class ValidationLog
    {
        public Guid JobId { get; set; }
        public string Rule { get; set; } = string.Empty;
        public string Result { get; set; } = "PASS";
        public string Details { get; set; } = string.Empty;
        public DateTime Ts { get; set; }
    }
}
