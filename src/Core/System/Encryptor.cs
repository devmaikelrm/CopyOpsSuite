using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CopyOpsSuite.System
{
    public sealed class Encryptor
    {
        private const int KeySize = 256;
        private const int BlockSize = 128;
        private const int Iterations = 10000;

        public async Task EncryptStreamAsync(Stream source, Stream destination, string pin)
        {
            if (string.IsNullOrWhiteSpace(pin)) throw new ArgumentException("El PIN no puede estar vacío para el cifrado.", nameof(pin));

            var salt = GenerateRandomSalt();
            await destination.WriteAsync(salt, 0, salt.Length).ConfigureAwait(false); // Write salt to the beginning of the stream

            using var aes = Aes.Create();
            aes.KeySize = KeySize;
            aes.BlockSize = BlockSize;

            var keyAndIv = new Rfc2898DeriveBytes(pin, salt, Iterations, HashAlgorithmName.SHA256);
            aes.Key = keyAndIv.GetBytes(KeySize / 8);
            aes.IV = keyAndIv.GetBytes(BlockSize / 8);

            using var cryptoStream = new CryptoStream(destination, aes.CreateEncryptor(), CryptoStreamMode.Write);
            await source.CopyToAsync(cryptoStream).ConfigureAwait(false);
        }

        public async Task DecryptStreamAsync(Stream source, Stream destination, string pin)
        {
            if (string.IsNullOrWhiteSpace(pin)) throw new ArgumentException("El PIN no puede estar vacío para el descifrado.", nameof(pin));

            var salt = new byte[16];
            await source.ReadAsync(salt, 0, salt.Length).ConfigureAwait(false); // Read salt from the beginning

            using var aes = Aes.Create();
            aes.KeySize = KeySize;
            aes.BlockSize = BlockSize;

            var keyAndIv = new Rfc2898DeriveBytes(pin, salt, Iterations, HashAlgorithmName.SHA256);
            aes.Key = keyAndIv.GetBytes(KeySize / 8);
            aes.IV = keyAndIv.GetBytes(BlockSize / 8);

            using var cryptoStream = new CryptoStream(source, aes.CreateDecryptor(), CryptoStreamMode.Read);
            await cryptoStream.CopyToAsync(destination).ConfigureAwait(false);
        }

        private static byte[] GenerateRandomSalt()
        {
            var salt = new byte[16];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(salt);
            return salt;
        }
    }
}
