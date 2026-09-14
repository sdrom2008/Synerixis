using System.Security.Cryptography;
using System.Text;

namespace Synerixis.Infrastructure.Services
{
    /// <summary>
    /// 坐席密码：sha256:salt:hash。开发期仍接受明文存储以便迁移已有测号。
    /// </summary>
    public static class AgentPasswordHasher
    {
        private const string Prefix = "sha256:";

        public static string Hash(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password required", nameof(password));

            var saltBytes = RandomNumberGenerator.GetBytes(16);
            var salt = Convert.ToHexString(saltBytes).ToLowerInvariant();
            var hash = Compute(salt, password);
            return $"{Prefix}{salt}:{hash}";
        }

        public static bool Verify(string password, string stored)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(stored))
                return false;

            // 开发迁移：历史明文 PasswordHash
            if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
                return string.Equals(stored, password, StringComparison.Ordinal);

            var parts = stored.Split(':');
            if (parts.Length != 3)
                return false;

            var salt = parts[1];
            var expected = parts[2];
            var actual = Compute(salt, password);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(actual),
                Encoding.UTF8.GetBytes(expected));
        }

        private static string Compute(string salt, string password)
        {
            var bytes = Encoding.UTF8.GetBytes(salt + password);
            return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }
    }
}
