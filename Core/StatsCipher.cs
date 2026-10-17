using System;
using System.Security.Cryptography;
using System.Text;

namespace CuStatsHud.Core
{
    
    public static class StatsCipher
    {
        
        public const string Prefix = "CUSH1:";

        private static readonly byte[] PassBytes =
            Encoding.UTF8.GetBytes("com.fox.custatshud/stats/v1");

        private static readonly byte[] Salt =
        {
            0x43, 0x55, 0x53, 0x74, 0x61, 0x74, 0x73, 0x48,
            0x75, 0x64, 0x2F, 0x76, 0x31, 0x2F, 0x73, 0x61
        };

        private const int Iterations = 50000;
        private const int KeySize = 32;   
        private const int IvSize = 16;

        private static byte[] _key;

        private static byte[] Key()
        {
            if (_key != null) return _key;

            using (Rfc2898DeriveBytes derive =
                new Rfc2898DeriveBytes(PassBytes, Salt, Iterations, HashAlgorithmName.SHA256))
            {
                _key = derive.GetBytes(KeySize);
            }
            return _key;
        }

        public static string Encrypt(string plain)
        {
            if (plain == null) plain = string.Empty;

            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Key = Key();
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.GenerateIV();

                byte[] iv = aes.IV;
                byte[] data = Encoding.UTF8.GetBytes(plain);

                byte[] cipher;
                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                {
                    cipher = encryptor.TransformFinalBlock(data, 0, data.Length);
                }

                byte[] packed = new byte[iv.Length + cipher.Length];
                Buffer.BlockCopy(iv, 0, packed, 0, iv.Length);
                Buffer.BlockCopy(cipher, 0, packed, iv.Length, cipher.Length);

                return Prefix + Convert.ToBase64String(packed);
            }
        }

        public static string Decrypt(string stored)
        {
            if (string.IsNullOrEmpty(stored)) return null;
            if (!stored.StartsWith(Prefix, StringComparison.Ordinal)) return null;

            try
            {
                byte[] packed = Convert.FromBase64String(stored.Substring(Prefix.Length));
                if (packed.Length <= IvSize) return null;

                using (Aes aes = Aes.Create())
                {
                    aes.KeySize = 256;
                    aes.Key = Key();
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    byte[] iv = new byte[IvSize];
                    Buffer.BlockCopy(packed, 0, iv, 0, IvSize);
                    aes.IV = iv;

                    using (ICryptoTransform decryptor = aes.CreateDecryptor())
                    {
                        
                        byte[] plain = decryptor.TransformFinalBlock(packed, IvSize, packed.Length - IvSize);
                        return Encoding.UTF8.GetString(plain);
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
