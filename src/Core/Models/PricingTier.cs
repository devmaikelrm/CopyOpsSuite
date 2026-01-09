namespace CopyOpsSuite.Core.Models
{
    public sealed class PricingTier
    {
        public string TierId { get; set; } = string.Empty;
        public int MinGB { get; set; }
        public int MaxGB { get; set; }
        public int PriceCUP { get; set; }
        public bool IsActive { get; set; }
        public int Order { get; set; }
    }
}
