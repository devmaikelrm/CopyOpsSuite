using System;

namespace CopyOpsSuite.Core.Models
{
    public enum SaleStatus
    {
        Pending,
        Paid,
        Partial,
        Canceled
    }

    public sealed class Sale
    {
        public Guid SaleId { get; set; }
        public Guid JobId { get; set; }
        public DateTime Ts { get; set; }
        public string OperatorName { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Currency { get; set; } = "CUP";
        public decimal AmountExpected { get; set; }
        public decimal AmountPaid { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public SaleStatus Status { get; set; } = SaleStatus.Pending;
        public string Notes { get; set; } = string.Empty;
        public decimal RealGb { get; set; }
        public decimal BillableGb { get; set; }
        public RoundMode RoundMode { get; set; } = RoundMode.CeilGB;
    }
}
