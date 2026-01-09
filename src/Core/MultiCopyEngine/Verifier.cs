using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace CopyOpsSuite.MultiCopyEngine
{
    public class Verifier
    {
        public async Task<bool> VerifyFileAsync(string sourcePath, string destinationPath)
        {
            if (!File.Exists(sourcePath) || !File.Exists(destinationPath))
            {
                return false;
            }

            var sourceHashTask = ComputeHashAsync(sourcePath);
            var destinationHashTask = ComputeHashAsync(destinationPath);

            await Task.WhenAll(sourceHashTask, destinationHashTask).ConfigureAwait(false);

            return sourceHashTask.Result.SequenceEqual(destinationHashTask.Result);
        }

        private async Task<byte[]> ComputeHashAsync(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            return await sha256.ComputeHashAsync(stream).ConfigureAwait(false);
        }
    }
}

