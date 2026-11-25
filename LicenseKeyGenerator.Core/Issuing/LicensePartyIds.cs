using System;
using System.Security.Cryptography;
using System.Text;

namespace LicenseGenerator
{
    /// <summary>Readable, offline identifiers. All 128 random bits are retained.</summary>
    public sealed class LicensePartyIds
    {
        // Crockford Base32 omits I, L and O (and U), avoiding confusion with 1/0.
        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        public string CustomerId { get; private set; }
        public string StoreId { get; private set; }

        public LicensePartyIds(string customerId, string storeId)
        { CustomerId = customerId; StoreId = storeId; }

        public static LicensePartyIds Create()
        { return new LicensePartyIds(CreateId("CUS"), CreateId("STR")); }

        private static string CreateId(string prefix)
        {
            var bytes = new byte[16];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            var encoded = new StringBuilder(26);
            int buffer = 0, bits = 0;
            foreach (byte value in bytes)
            {
                buffer = (buffer << 8) | value; bits += 8;
                while (bits >= 5)
                {
                    bits -= 5;
                    encoded.Append(Alphabet[(buffer >> bits) & 31]);
                }
                buffer &= (1 << bits) - 1;
            }
            if (bits > 0) encoded.Append(Alphabet[(buffer << (5 - bits)) & 31]);
            var result = new StringBuilder(prefix);
            for (int i = 0; i < encoded.Length; i++)
            {
                if (i % 4 == 0) result.Append('-');
                result.Append(encoded[i]);
            }
            // No machine ID, counter, clock, shared settings or truncated GUID.
            // Collisions are negligible, not an absolute offline uniqueness guarantee.
            return result.ToString();
        }
    }
}
