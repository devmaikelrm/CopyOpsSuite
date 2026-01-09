using System;

namespace CopyOpsSuite.App.WinUI.ViewModels
{
    public sealed class LogLineViewModel
    {
        public DateTime Timestamp { get; }
        public string Type { get; }
        public string Severity { get; }
        public string Message { get; }

        public LogLineViewModel(DateTime timestamp, string type, string severity, string message)
        {
            Timestamp = timestamp;
            Type = type;
            Severity = severity;
            Message = message;
        }
    }
}
