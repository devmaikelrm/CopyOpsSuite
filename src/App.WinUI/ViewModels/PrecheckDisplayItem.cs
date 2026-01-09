namespace CopyOpsSuite.App.WinUI.ViewModels
{
    public enum PrecheckResultLevel
    {
        Pass,
        Warn,
        Fail
    }

    public sealed class PrecheckDisplayItem
    {
        public string Device { get; }
        public string Message { get; }
        public PrecheckResultLevel Level { get; }
        public string LevelText => Level switch
        {
            PrecheckResultLevel.Fail => "FAIL",
            PrecheckResultLevel.Warn => "WARN",
            _ => "PASS"
        };

        public PrecheckDisplayItem(string device, string message, PrecheckResultLevel level)
        {
            Device = device;
            Message = message;
            Level = level;
        }
    }
}
