using CopyOpsSuite.Core.Models;
using System.Collections.Generic;

namespace CopyOpsSuite.CashOps
{
    public class BillingEngine
    {
        public decimal CalculateCharge(IEnumerable<Sale> sales)
        {
            decimal total = 0;
            foreach (var sale in sales)
            {
                total += sale.AmountPaid;
            }

            return total;
        }
    }
}
