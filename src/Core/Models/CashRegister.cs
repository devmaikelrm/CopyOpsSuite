using System;
using System.Collections.Generic;

namespace CopyOpsSuite.Core.Models
{
    public sealed class CashRegister
    {
        public DateOnly Day { get; set; }
        public decimal OpeningAmount { get; set; }
        public decimal ClosingAmount { get; set; }
        public decimal CountedAmount { get; set; }
        public decimal ExpectedAmount { get; set; }
        public string Notes { get; set; } = string.Empty;
        public List<Sale> Sales { get; set; } = new();
    }
}
