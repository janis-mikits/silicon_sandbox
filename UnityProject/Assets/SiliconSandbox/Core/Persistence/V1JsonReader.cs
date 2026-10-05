using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SiliconSandbox.Persistence
{
    // Strict field/type helpers for the accepted V1 record shapes. This is a
    // data boundary: no runtime or Unity scene objects are deserialized here.
    internal static class V1JsonReader
    {
        public static JObject Root(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            try
            {
                var utf8 = new UTF8Encoding(false, true);
                var source = utf8.GetString(bytes);
                if (source.Length > 0 && source[0] == '\ufeff')
                    throw new InvalidDataException("V1 JSON must be UTF-8 without a BOM.");
                RejectJsonExtensions(source);
                using (var input = new StringReader(source))
                using (var reader = new JsonTextReader(input)
                {
                    DateParseHandling = DateParseHandling.None,
                    MaxDepth = 64,
                    SupportMultipleContent = false
                })
                {
                    if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
                        throw new InvalidDataException("V1 JSON root must be an object.");
                    var root = JObject.Load(reader, new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                        CommentHandling = CommentHandling.Load
                    });
                    while (reader.Read())
                        if (reader.TokenType != JsonToken.None)
                            throw new InvalidDataException("Extra JSON content.");
                    foreach (var token in root.DescendantsAndSelf())
                        if (token.Type == JTokenType.Comment)
                            throw new InvalidDataException("JSON comments are not V1 data.");
                    return root;
                }
            }
            catch (Exception error) when (error is DecoderFallbackException ||
                error is JsonException || error is ArgumentException ||
                error is OverflowException)
            {
                throw new InvalidDataException("Malformed version 1 JSON.", error);
            }
        }

        private static void RejectJsonExtensions(string source)
        {
            var quoted = false;
            var escaped = false;
            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                if (quoted)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') quoted = false;
                    continue;
                }
                if (c == '"') { quoted = true; continue; }
                if (c == '/')
                    throw new InvalidDataException("JSON comments are not V1 data.");
                if (c != ',') continue;
                var next = i + 1;
                while (next < source.Length && char.IsWhiteSpace(source[next]))
                    next++;
                if (next < source.Length &&
                    (source[next] == '}' || source[next] == ']'))
                    throw new InvalidDataException("Trailing JSON comma.");
            }
        }

        public static JObject Object(JToken token, params string[] fields)
        {
            if (!(token is JObject value))
                throw new InvalidDataException("Expected JSON object.");
            var allowed = new HashSet<string>(fields, StringComparer.Ordinal);
            if (value.Count != allowed.Count)
                throw new InvalidDataException("Missing or unknown V1 object field.");
            foreach (var field in value.Properties())
                if (!allowed.Contains(field.Name))
                    throw new InvalidDataException("Unknown V1 object field: " + field.Name);
            return value;
        }

        public static JArray Array(JToken token)
        {
            if (!(token is JArray value))
                throw new InvalidDataException("Expected JSON array.");
            return value;
        }

        public static string String(JToken token)
        {
            if (token == null || token.Type != JTokenType.String)
                throw new InvalidDataException("Expected JSON string.");
            return (string)token;
        }

        public static long Integer(JToken token)
        {
            if (token == null || token.Type != JTokenType.Integer)
                throw new InvalidDataException("Expected JSON integer.");
            try { return Convert.ToInt64(((JValue)token).Value,
                CultureInfo.InvariantCulture); }
            catch (Exception error) when (error is OverflowException ||
                error is InvalidCastException)
            { throw new InvalidDataException("JSON integer is out of range.", error); }
        }

        public static int Int32(JToken token)
        {
            var value = Integer(token);
            if (value < int.MinValue || value > int.MaxValue)
                throw new InvalidDataException("JSON integer is out of Int32 range.");
            return (int)value;
        }

        public static double Real(JToken token)
        {
            if (token == null || token.Type != JTokenType.Float &&
                token.Type != JTokenType.Integer)
                throw new InvalidDataException("Expected JSON number.");
            var value = Convert.ToDouble(((JValue)token).Value,
                CultureInfo.InvariantCulture);
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new InvalidDataException("JSON number must be finite.");
            return value;
        }

        public static bool Boolean(JToken token)
        {
            if (token == null || token.Type != JTokenType.Boolean)
                throw new InvalidDataException("Expected JSON boolean.");
            return (bool)token;
        }

        public static Guid Uuid(JToken token)
        {
            var value = String(token);
            if (value.Length != 36 || !Guid.TryParseExact(value, "D", out var id) ||
                id.ToString("D") != value || !WorldManifestIntegrity.IsVersionFour(id))
                throw new InvalidDataException("V1 identity must be lowercase UUIDv4.");
            return id;
        }

        public static string Sha256(JToken token)
        {
            var value = String(token);
            if (value.Length != 64) throw new InvalidDataException("Invalid SHA-256 digest.");
            foreach (var digit in value)
                if (!(digit >= '0' && digit <= '9' ||
                      digit >= 'a' && digit <= 'f'))
                    throw new InvalidDataException("Invalid lowercase SHA-256 digest.");
            return value;
        }
    }
}
