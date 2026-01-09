namespace CopyOpsSuite.App.WinUI.ViewModels
{
    public sealed class DeviceChoice
    {
        public string DeviceId { get; }
        public string DisplayName { get; }

        public DeviceChoice(string deviceId, string displayName)
        {
            DeviceId = deviceId;
            DisplayName = displayName;
        }
    }
}
