using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace CopyOpsSuite.Core.Models
{
    public enum PricingMode { PerGB, Fixed, Mixed }
    public enum RoundMode { CeilGB, Exact }

    public partial class CopyProfile : ObservableObject
    {
        [ObservableProperty]
        private string profileId = string.Empty;
        
        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private PricingMode pricingMode;

        [ObservableProperty]
        private decimal priceFixed;

        [ObservableProperty]
        private decimal pricePerGB;

        [ObservableProperty]
        private RoundMode roundMode = RoundMode.CeilGB;

        [ObservableProperty]
        private string currency = string.Empty;

        [ObservableProperty]
        private List<string> defaultExclusions = new();
    }
}
