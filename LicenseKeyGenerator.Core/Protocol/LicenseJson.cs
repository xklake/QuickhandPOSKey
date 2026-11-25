using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Quickhand.Licensing
{
    public static class LicenseJson
    {
        public const int MaximumBytes = 65536;
        public static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public static JObject Read(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || Utf8.GetByteCount(json) > MaximumBytes)
                throw new InvalidDataException("The license document is empty or too large.");
            try
            {
                // Do not enable TypeNameHandling or deserialize arbitrary object types.
                using (var reader = new JsonTextReader(new StringReader(json)) { MaxDepth = 12, DateParseHandling = DateParseHandling.None })
                {
                    var value = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new InvalidDataException("The license contains trailing data.");
                    return value;
                }
            }
            catch (JsonException ex) { throw new InvalidDataException("The license document has invalid or duplicate fields.", ex); }
        }

        public static void Fields(JObject value, params string[] names)
        {
            if (value == null || value.Properties().Any(p => !names.Contains(p.Name, StringComparer.Ordinal)) || names.Any(n => value.Property(n) == null))
                throw new InvalidDataException("The license document contains missing or unsupported fields.");
        }

        public static string Text(JObject value, string name, int maximum = 160, bool allowEmpty = false)
        {
            var token = value[name];
            if (token == null || token.Type != JTokenType.String) throw new InvalidDataException("Invalid license text: " + name);
            string text = (string)token;
            if (text.Length > maximum || (!allowEmpty && string.IsNullOrWhiteSpace(text)) || text.Any(char.IsControl))
                throw new InvalidDataException("Invalid license text: " + name);
            return text;
        }

        public static int Number(JObject value, string name, int minimum, int maximum)
        {
            var token = value[name];
            long number;
            if (token == null || token.Type != JTokenType.Integer || !long.TryParse(token.ToString(), out number) || number < minimum || number > maximum)
                throw new InvalidDataException("Invalid license number: " + name);
            return (int)number;
        }

        public static bool Flag(JObject value, string name)
        {
            if (value[name]?.Type != JTokenType.Boolean) throw new InvalidDataException("Invalid license flag: " + name);
            return (bool)value[name];
        }

        public static DateTime Time(JObject value, string name)
        {
            DateTime time;
            if (!DateTime.TryParseExact(Text(value, name, 20), "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out time))
                throw new InvalidDataException("The license date must be a valid UTC date: " + name);
            return time;
        }

        public static string TimeText(DateTime utc) { return utc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture); }
        public static string Compact(JObject value) { return value.ToString(Formatting.None); }
        public static bool IsHash(string value) { return value != null && value.Length == 64 && value.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')); }
    }
}
