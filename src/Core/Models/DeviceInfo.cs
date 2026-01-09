using CommunityToolkit.Mvvm.ComponentModel;

namespace CopyOpsSuite.Core.Models
{
    public enum DeviceType
    {
        System,
        FixedDisk,
        RemovableUsb,
        Network,
        PhoneLike
    }

    public partial class DeviceInfo : ObservableObject
    {
        [ObservableProperty]
        private string id = string.Empty;

        [ObservableProperty]
        private string driveLetter = string.Empty;

        [ObservableProperty]
        private string label = string.Empty;

        [ObservableProperty]
        private string fileSystem = string.Empty;

        [ObservableProperty]
        private long totalBytes;

        [ObservableProperty]
        private long freeBytes;

        [ObservableProperty]
        private DeviceType type;

        [ObservableProperty]
        private string busHint = string.Empty;

        [ObservableProperty]
        private bool isSelected;

        [ObservableProperty]
        private bool isProtectedFromFormat;
    }
}
