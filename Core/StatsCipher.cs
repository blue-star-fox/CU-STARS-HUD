using System;
using System.Security.Cryptography;
using System.Text;

namespace CuStatsHud.Core
{
    /// <summary>
    /// 统计数据文件的加密。
    ///
    /// AES-256-CBC + PKCS7，密钥由插件标识与固定盐经 PBKDF2 派生。
    /// 目的是**让玩家没法随手查看或直接改存档数据**（数据是二进制密文 + Base64）。
    ///
    /// 如实说明：密钥随插件一起分发，所以这属于「防随手篡改」级别，不是强安全边界——
    /// 能反编译插件的人依然能解出来。对本地统计这个量级的需求来说这是合适的取舍。
    /// </summary>
    public static class StatsCipher
    {
        /// <summary>文件头标记：用于识别格式与版本，便于以后更换算法。</summary>
        public const string Prefix = "CUSH1:";

        private static readonly byte[] PassBytes =
            Encoding.UTF8.GetBytes("com.fox.custatshud/stats/v1");

        private static readonly byte[] Salt =
        {
            0x43, 0x55, 0x53, 0x74, 0x61, 0x74, 0x73, 0x48,
            0x75, 0x64, 0x2F, 0x76, 0x31, 0x2F, 0x73, 0x61
        };

        private const int Iterations = 50000;
        private const int KeySize = 32;   // AES-256
        private const int IvSize = 16;

        /// <summary>派生密钥只算一次：PBKDF2 迭代 5 万次，每次存档都算会明显卡顿。</summary>
        private static byte[] _key;

        private static byte[] Key()
        {
            if (_key != null) return _key;

            // 显式指定哈希算法：不带 HashAlgorithmName 的重载在新版 .NET 上已标记过时
            using (Rfc2898DeriveBytes derive =
                new Rfc2898DeriveBytes(PassBytes, Salt, Iterations, HashAlgorithmName.SHA256))
            {
                _key = derive.GetBytes(KeySize);
            }
            return _key;
        }

        /// <summary>把明文加密成可安全写入文本文件的一行字符串。</summary>
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

                // 文件格式：IV ‖ 密文
                byte[] packed = new byte[iv.Length + cipher.Length];
                Buffer.BlockCopy(iv, 0, packed, 0, iv.Length);
                Buffer.BlockCopy(cipher, 0, packed, iv.Length, cipher.Length);

                return Prefix + Convert.ToBase64String(packed);
            }
        }

        /// <summary>
        /// 解密。数据损坏、被截断、格式不符或密钥不匹配时返回 null，
        /// 由调用方按「没有历史数据」处理（首次运行就是这个分支）。
        /// </summary>
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
                        // PKCS7 填充校验能挡住绝大多数随手改动
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
