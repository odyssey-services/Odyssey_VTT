using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Odyssey.Application.Results;

namespace Odyssey.Application.Serialization
{
    public static class JsonPayloadLimits
    {
        public const int CommandPayloadBytes = 256 * 1024;
        public const int EventPayloadBytes = 1024 * 1024;
        public const int ManifestBytes = 4 * 1024 * 1024;
        public const int DiagnosticRecordBytes = 1024 * 1024;
        public const int MaxDepth = 64;
    }

    public sealed class JsonObjectReader
    {
        private readonly Dictionary<string, string?> _values;

        private JsonObjectReader(Dictionary<string, string?> values)
        {
            _values = values;
        }

        public static Result<JsonObjectReader> Read(byte[] utf8Json, int maxBytes, int maxDepth = JsonPayloadLimits.MaxDepth)
        {
            if (utf8Json == null) throw new ArgumentNullException(nameof(utf8Json));
            if (utf8Json.Length == 0 || utf8Json.Length > maxBytes) return Result<JsonObjectReader>.Failure(SerializationFailures.InvalidPayload());
            if (HasUtf8Bom(utf8Json)) return Result<JsonObjectReader>.Failure(SerializationFailures.InvalidPayload());

            try
            {
                string json = new UTF8Encoding(false, true).GetString(utf8Json);
                if (HasStructuralTrailingComma(json)) return Result<JsonObjectReader>.Failure(SerializationFailures.InvalidPayload());
                JsonTextReader reader = new JsonTextReader(new StringReader(json))
                {
                    DateParseHandling = DateParseHandling.None,
                    FloatParseHandling = FloatParseHandling.Decimal,
                    MaxDepth = maxDepth
                };
                Dictionary<string, string?> values = new Dictionary<string, string?>(StringComparer.Ordinal);
                if (!reader.Read() || reader.TokenType != JsonToken.StartObject) return Result<JsonObjectReader>.Failure(SerializationFailures.InvalidPayload());
                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.EndObject)
                    {
                        if (reader.Read()) return Result<JsonObjectReader>.Failure(SerializationFailures.InvalidPayload());
                        return Result<JsonObjectReader>.Success(new JsonObjectReader(values));
                    }

                    if (reader.TokenType == JsonToken.Comment || reader.TokenType != JsonToken.PropertyName) return Result<JsonObjectReader>.Failure(SerializationFailures.InvalidPayload());
                    string name = (string)reader.Value!;
                    if (!SerializationText.IsLowerCamelProperty(name) || values.ContainsKey(name)) return Result<JsonObjectReader>.Failure(SerializationFailures.InvalidPayload());
                    if (!reader.Read()) return Result<JsonObjectReader>.Failure(SerializationFailures.InvalidPayload());
                    switch (reader.TokenType)
                    {
                        case JsonToken.String:
                        case JsonToken.Integer:
                        case JsonToken.Boolean:
                            values.Add(name, Convert.ToString(reader.Value, CultureInfo.InvariantCulture));
                            break;
                        case JsonToken.Null:
                            values.Add(name, null);
                            break;
                        default:
                            return Result<JsonObjectReader>.Failure(SerializationFailures.InvalidPayload());
                    }
                }
            }
            catch (JsonException)
            {
                return Result<JsonObjectReader>.Failure(SerializationFailures.InvalidPayload());
            }
            catch (DecoderFallbackException)
            {
                return Result<JsonObjectReader>.Failure(SerializationFailures.InvalidPayload());
            }

            return Result<JsonObjectReader>.Failure(SerializationFailures.InvalidPayload());
        }

        public bool TryGetString(string name, out string? value) => _values.TryGetValue(name, out value);

        public Result EnsureOnly(params string[] allowedNames)
        {
            HashSet<string> allowed = new HashSet<string>(allowedNames, StringComparer.Ordinal);
            foreach (string name in _values.Keys)
            {
                if (!allowed.Contains(name)) return Result.Failure(SerializationFailures.InvalidPayload());
            }

            return Result.Success();
        }

        public Result<string> RequiredString(string name)
        {
            if (!_values.TryGetValue(name, out string? value) || value == null) return Result<string>.Failure(SerializationFailures.InvalidPayload());
            return Result<string>.Success(value);
        }

        public Result<int> RequiredInt32(string name)
        {
            Result<string> value = RequiredString(name);
            if (value.IsFailure) return Result<int>.Failure(value.Error);
            if (!int.TryParse(value.Value, NumberStyles.None, CultureInfo.InvariantCulture, out int result)) return Result<int>.Failure(SerializationFailures.InvalidPayload());
            return Result<int>.Success(result);
        }

        public Result<long> RequiredInt64(string name)
        {
            Result<string> value = RequiredString(name);
            if (value.IsFailure) return Result<long>.Failure(value.Error);
            if (!long.TryParse(value.Value, NumberStyles.None, CultureInfo.InvariantCulture, out long result)) return Result<long>.Failure(SerializationFailures.InvalidPayload());
            return Result<long>.Success(result);
        }

        public Result<bool> RequiredBoolean(string name)
        {
            Result<string> value = RequiredString(name);
            if (value.IsFailure) return Result<bool>.Failure(value.Error);
            if (value.Value == "True") return Result<bool>.Success(true);
            if (value.Value == "False") return Result<bool>.Success(false);
            return Result<bool>.Failure(SerializationFailures.InvalidPayload());
        }

        public static Result ValidateJson(byte[] utf8Json, int maxBytes, int maxDepth = JsonPayloadLimits.MaxDepth)
        {
            if (utf8Json == null) throw new ArgumentNullException(nameof(utf8Json));
            if (utf8Json.Length == 0 || utf8Json.Length > maxBytes) return Result.Failure(SerializationFailures.InvalidPayload());
            if (HasUtf8Bom(utf8Json)) return Result.Failure(SerializationFailures.InvalidPayload());
            try
            {
                string json = new UTF8Encoding(false, true).GetString(utf8Json);
                if (HasStructuralTrailingComma(json)) return Result.Failure(SerializationFailures.InvalidPayload());
                JsonTextReader reader = new JsonTextReader(new StringReader(json))
                {
                    DateParseHandling = DateParseHandling.None,
                    FloatParseHandling = FloatParseHandling.Decimal,
                    MaxDepth = maxDepth
                };
                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.Comment) return Result.Failure(SerializationFailures.InvalidPayload());
                }

                return Result.Success();
            }
            catch (JsonException)
            {
                return Result.Failure(SerializationFailures.InvalidPayload());
            }
            catch (DecoderFallbackException)
            {
                return Result.Failure(SerializationFailures.InvalidPayload());
            }
        }

        private static bool HasUtf8Bom(byte[] value) => value.Length >= 3 && value[0] == 0xEF && value[1] == 0xBB && value[2] == 0xBF;

        private static bool HasStructuralTrailingComma(string json)
        {
            bool inString = false;
            bool escaping = false;
            for (int index = 0; index < json.Length; index++)
            {
                char c = json[index];
                if (inString)
                {
                    if (escaping)
                    {
                        escaping = false;
                    }
                    else if (c == '\\')
                    {
                        escaping = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c != ',') continue;
                int next = index + 1;
                while (next < json.Length && char.IsWhiteSpace(json[next])) next++;
                if (next < json.Length && (json[next] == '}' || json[next] == ']')) return true;
            }

            return false;
        }
    }

    public sealed class CanonicalJsonWriter
    {
        private readonly StringWriter _stringWriter = new StringWriter(CultureInfo.InvariantCulture);
        private readonly JsonTextWriter _writer;
        private bool _closed;

        public CanonicalJsonWriter()
        {
            _writer = new JsonTextWriter(_stringWriter)
            {
                Formatting = Formatting.None,
                Culture = CultureInfo.InvariantCulture,
                FloatFormatHandling = FloatFormatHandling.Symbol,
                StringEscapeHandling = StringEscapeHandling.Default
            };
        }

        public CanonicalJsonWriter StartObject()
        {
            _writer.WriteStartObject();
            return this;
        }

        public CanonicalJsonWriter EndObject()
        {
            _writer.WriteEndObject();
            _closed = true;
            return this;
        }

        public CanonicalJsonWriter String(string name, string value)
        {
            WriteName(name);
            _writer.WriteValue(value);
            return this;
        }

        public CanonicalJsonWriter NullableString(string name, string? value)
        {
            WriteName(name);
            if (value == null) _writer.WriteNull();
            else _writer.WriteValue(value);
            return this;
        }

        public CanonicalJsonWriter Int32(string name, int value)
        {
            WriteName(name);
            _writer.WriteValue(value);
            return this;
        }

        public CanonicalJsonWriter Int64(string name, long value)
        {
            WriteName(name);
            _writer.WriteValue(value);
            return this;
        }

        public CanonicalJsonWriter NullableInt64(string name, long? value)
        {
            WriteName(name);
            if (value.HasValue) _writer.WriteValue(value.Value);
            else _writer.WriteNull();
            return this;
        }

        public CanonicalJsonWriter Boolean(string name, bool value)
        {
            WriteName(name);
            _writer.WriteValue(value);
            return this;
        }

        public CanonicalJsonWriter Null(string name)
        {
            WriteName(name);
            _writer.WriteNull();
            return this;
        }

        public CanonicalJsonWriter RawJson(string name, string json)
        {
            WriteName(name);
            _writer.WriteRawValue(json);
            return this;
        }

        public CanonicalJsonWriter StartArray(string name)
        {
            WriteName(name);
            _writer.WriteStartArray();
            return this;
        }

        public CanonicalJsonWriter EndArray()
        {
            _writer.WriteEndArray();
            return this;
        }

        public CanonicalJsonWriter StartArrayObject()
        {
            _writer.WriteStartObject();
            return this;
        }

        public CanonicalJsonWriter EndArrayObject()
        {
            _writer.WriteEndObject();
            return this;
        }

        public JsonPayload ToPayload()
        {
            if (!_closed) throw new InvalidOperationException("Canonical JSON object must be closed.");
            return new JsonPayload(CanonicalJson.ToUtf8Bytes(_stringWriter.ToString()));
        }

        private void WriteName(string name)
        {
            if (!SerializationText.IsLowerCamelProperty(name)) throw new ArgumentException("JSON property is not lowerCamelCase.", nameof(name));
            _writer.WritePropertyName(name);
        }
    }

    public static class CanonicalJson
    {
        public static byte[] ToUtf8Bytes(string json) => new UTF8Encoding(false).GetBytes(json);
        public static string ToUtf8Text(byte[] bytes) => new UTF8Encoding(false, true).GetString(bytes);

        public static string Sha256LowerHex(byte[] bytes)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(bytes);
            char[] chars = new char[hash.Length * 2];
            for (int index = 0; index < hash.Length; index++)
            {
                byte b = hash[index];
                chars[index * 2] = ToHex(b >> 4);
                chars[index * 2 + 1] = ToHex(b & 0xF);
            }

            return new string(chars);
        }

        private static char ToHex(int value) => (char)(value < 10 ? '0' + value : 'a' + value - 10);
    }

    internal static partial class SerializationText
    {
        internal static bool IsLowerCamelProperty(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length > 64 || value.Trim() != value) return false;
            char first = value[0];
            if (first < 'a' || first > 'z') return false;
            for (int index = 1; index < value.Length; index++)
            {
                char c = value[index];
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))) return false;
            }

            return true;
        }
    }
}
