using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Odyssey.Application.Results;
using Odyssey.Domain.Identity;

namespace Odyssey.Application.Serialization
{
    public enum SerializationProfile
    {
        AuthoritativePayloadJson = 1,
        PersistenceJson = 2,
        InterchangeJson = 3,
        ConfigurationJson = 4,
        DiagnosticJson = 5,
        FixtureJson = 6
    }

    public readonly struct ContractType : IEquatable<ContractType>
    {
        public const int MaxLength = 128;
        private readonly string _value;

        private ContractType(string value) => _value = value;
        public bool IsValid => _value != null;
        public static bool TryParse(string? value, out ContractType type)
        {
            if (SerializationText.IsContractType(value))
            {
                type = new ContractType(value!);
                return true;
            }

            type = default;
            return false;
        }

        public static ContractType Parse(string value) => TryParse(value, out ContractType type) ? type : throw new FormatException("ContractType is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(ContractType other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is ContractType other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(ContractType left, ContractType right) => left.Equals(right);
        public static bool operator !=(ContractType left, ContractType right) => !left.Equals(right);
    }

    public readonly struct ContractVersion : IEquatable<ContractVersion>, IComparable<ContractVersion>
    {
        private readonly int _value;

        private ContractVersion(int value) => _value = value;
        public bool IsValid => _value > 0;
        public int Value => IsValid ? _value : throw new InvalidOperationException("ContractVersion is invalid.");
        public static ContractVersion Create(int value) => value > 0 ? new ContractVersion(value) : throw new ArgumentOutOfRangeException(nameof(value));
        public int CompareTo(ContractVersion other) => _value.CompareTo(other._value);
        public bool Equals(ContractVersion other) => _value == other._value;
        public override bool Equals(object? obj) => obj is ContractVersion other && Equals(other);
        public override int GetHashCode() => _value;
        public override string ToString() => IsValid ? _value.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
    }

    public readonly struct JsonContractKey : IEquatable<JsonContractKey>
    {
        public JsonContractKey(SerializationProfile profile, ContractType contractType, ContractVersion contractVersion)
        {
            if (!Enum.IsDefined(typeof(SerializationProfile), profile)) throw new ArgumentOutOfRangeException(nameof(profile));
            if (!contractType.IsValid) throw new ArgumentException("ContractType is required.", nameof(contractType));
            if (!contractVersion.IsValid) throw new ArgumentException("ContractVersion is required.", nameof(contractVersion));
            Profile = profile;
            ContractType = contractType;
            ContractVersion = contractVersion;
        }

        public SerializationProfile Profile { get; }
        public ContractType ContractType { get; }
        public ContractVersion ContractVersion { get; }
        public bool Equals(JsonContractKey other) => Profile == other.Profile && ContractType == other.ContractType && ContractVersion.Equals(other.ContractVersion);
        public override bool Equals(object? obj) => obj is JsonContractKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Profile, ContractType, ContractVersion);
    }

    public sealed class JsonPayload
    {
        private readonly byte[] _bytes;

        public JsonPayload(byte[] bytes)
        {
            _bytes = bytes == null ? throw new ArgumentNullException(nameof(bytes)) : Copy(bytes);
        }

        public ReadOnlyMemory<byte> BytesMemory => _bytes;
        public byte[] Bytes => Copy(_bytes);
        public string Utf8Text => CanonicalJson.ToUtf8Text(_bytes);
        private static byte[] Copy(byte[] source)
        {
            byte[] copy = new byte[source.Length];
            Buffer.BlockCopy(source, 0, copy, 0, source.Length);
            return copy;
        }
    }

    public interface IJsonContractCodec<T>
        where T : notnull
    {
        JsonContractKey Key { get; }
        Result<JsonPayload> Write(T value);
        Result<T> Read(byte[] utf8Json);
    }

    public sealed class JsonContractRegistry
    {
        private readonly ReadOnlyDictionary<JsonContractKey, object> _codecs;

        public JsonContractRegistry(IReadOnlyList<object> codecs)
        {
            if (codecs == null) throw new ArgumentNullException(nameof(codecs));
            Dictionary<JsonContractKey, object> copy = new Dictionary<JsonContractKey, object>();
            for (int index = 0; index < codecs.Count; index++)
            {
                object codec = codecs[index] ?? throw new ArgumentException("Codec is required.", nameof(codecs));
                JsonContractKey key = ExtractKey(codec);
                if (copy.ContainsKey(key)) throw new ArgumentException("Duplicate contract codec.", nameof(codecs));
                copy.Add(key, codec);
            }

            _codecs = new ReadOnlyDictionary<JsonContractKey, object>(copy);
        }

        public bool TryGet<T>(JsonContractKey key, out IJsonContractCodec<T> codec)
            where T : notnull
        {
            if (_codecs.TryGetValue(key, out object value) && value is IJsonContractCodec<T> typed)
            {
                codec = typed;
                return true;
            }

            codec = null!;
            return false;
        }

        private static JsonContractKey ExtractKey(object codec)
        {
            if (codec is IJsonContractCodec<CommandFingerprintMaterialV1> command) return command.Key;
            if (codec is IJsonContractCodec<SyntheticEventRecord> eventRecord) return eventRecord.Key;
            if (codec is IJsonContractCodec<OdcampManifestV1> manifest) return manifest.Key;
            if (codec is IJsonContractCodec<Diagnostics.LogEventV1> log) return log.Key;
            throw new ArgumentException("Unsupported codec type.", nameof(codec));
        }
    }

    internal static class SerializationFailures
    {
        private static readonly CorrelationId CorrelationId = CorrelationId.Parse("corr_00000000000000000000000000000000");

        internal static Error InvalidPayload() => Error.Create(
            ErrorCodes.SerializationInvalidPayload,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.application.serialization_invalid_payload"),
            RetryDirective.DoNotRetry,
            CorrelationId);

        internal static Error UnsupportedContract() => Error.Create(
            ErrorCodes.SerializationUnsupportedContract,
            ErrorCategory.Compatibility,
            SafeReasonCode.VersionUnsupported,
            UserMessageKey.Parse("errors.application.serialization_unsupported_contract"),
            RetryDirective.DoNotRetry,
            CorrelationId);

        internal static Error IntegrityMismatch() => Error.Create(
            ErrorCodes.SerializationIntegrityMismatch,
            ErrorCategory.Integrity,
            SafeReasonCode.DataCorrupted,
            UserMessageKey.Parse("errors.application.serialization_integrity_mismatch"),
            RetryDirective.ManualRecoveryRequired,
            CorrelationId);
    }

    internal static partial class SerializationText
    {
        internal static bool IsLowerToken(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length > maxLength || value.Trim() != value) return false;
            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-')) return false;
            }

            return true;
        }

        internal static bool IsDottedLowerIdentifier(string? value, int maxLength, int minSegments)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length > maxLength || value.Trim() != value) return false;
            string[] segments = value.Split('.');
            if (segments.Length < minSegments) return false;
            for (int index = 0; index < segments.Length; index++)
            {
                if (!IsLowerToken(segments[index], maxLength)) return false;
            }

            return true;
        }

        internal static bool IsContractType(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length > ContractType.MaxLength || value.Trim() != value) return false;
            string[] segments = value.Split('.');
            if (segments.Length < 2) return false;
            for (int segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
            {
                string segment = segments[segmentIndex];
                if (segment.Length == 0) return false;
                char first = segment[0];
                if (first < 'a' || first > 'z') return false;
                for (int index = 1; index < segment.Length; index++)
                {
                    char c = segment[index];
                    if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))) return false;
                }
            }

            return true;
        }
    }
}
