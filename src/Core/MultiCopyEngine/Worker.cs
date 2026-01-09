using CopyOpsSuite.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace CopyOpsSuite.MultiCopyEngine
{
    public class Worker
    {
        public Task RunAsync(TransferTarget target, CancellationToken token)
        {
            return Task.CompletedTask;
        }
    }
}
