using System;

namespace CopyOpsSuite.Core.Models
{
    public sealed class ErrorLog
    {
        public Guid JobId { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Ts { get; set; }
    }
}
