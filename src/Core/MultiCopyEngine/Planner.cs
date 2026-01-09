using CopyOpsSuite.Core.Models;
using System.Collections.Generic;

namespace CopyOpsSuite.MultiCopyEngine
{
    public class Planner
    {
        public IEnumerable<TransferJob> CreatePlan(IEnumerable<TransferTarget> targets)
        {
            return new List<TransferJob>();
        }
    }
}
