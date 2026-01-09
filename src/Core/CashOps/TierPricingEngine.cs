using System;
using System.Collections.Generic;
using System.Linq;
using CopyOpsSuite.Core.Models;

namespace CopyOpsSuite.CashOps
{
    public sealed record TierPricingResult(int TotalCUP, string AppliedTierText, int BillableGB, bool IsCapApplied);

    public static class TierPricingEngine
    {
        public static TierPricingResult Calculate(long bytes, IReadOnlyList<PricingTier> tiers)
        {
            if (bytes <= 0)
            {
                return new TierPricingResult(0, "Sin tarifa", 0, false);
            }

            var billableGb = (int)Math.Ceiling(bytes / 1024d / 1024d / 1024d);
            if (billableGb < 1)
            {
                billableGb = 1;
            }

            var ordered = (tiers ?? Array.Empty<PricingTier>())
                .Where(t => t.IsActive)
                .OrderBy(t => t.Order)
                .ThenBy(t => t.MinGB)
                .ToList();

            var capTier = ordered.FirstOrDefault(t => t.MinGB >= 1024 && t.MaxGB == 0);
            if (capTier != null && billableGb >= 1024)
            {
                var capText = $"{capTier.MinGB}-{(capTier.MaxGB == 0 ? "INF" : capTier.MaxGB.ToString())} CAP";
                return new TierPricingResult(capTier.PriceCUP, capText, billableGb, true);
            }

            var tier = ordered.FirstOrDefault(t => MatchesTier(billableGb, t));
            if (tier == null)
            {
                return new TierPricingResult(0, "Sin tarifa", billableGb, false);
            }

            var isCap = tier.MinGB >= 1024 && tier.MaxGB == 0;
            var tierText = $"{tier.MinGB}-{(tier.MaxGB == 0 ? "INF" : tier.MaxGB.ToString())}";
            if (isCap)
            {
                tierText += " CAP";
            }

            return new TierPricingResult(tier.PriceCUP, tierText, billableGb, isCap);
        }

        private static bool MatchesTier(int billableGb, PricingTier tier)
        {
            if (tier.MaxGB == 0)
            {
                return tier.MinGB <= billableGb;
            }

            return tier.MinGB <= billableGb && billableGb <= tier.MaxGB;
        }
    }
}
