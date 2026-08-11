using System;
using System.Security.Cryptography;
using System.Text;
using Odyssey.Application.Commands;
using Odyssey.Rules.Versions;

namespace Odyssey.Application.Random
{
    public readonly struct CampaignRngKey
    {
        public const int ByteLength = 32;
        private readonly byte[]? _bytes;

        private CampaignRngKey(byte[] bytes) => _bytes = bytes;
        public bool IsValid => _bytes != null && _bytes.Length == ByteLength;

        public static CampaignRngKey FromBytes(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length != ByteLength) throw new ArgumentException("Campaign RNG key must be 32 bytes.", nameof(bytes));
            byte[] copy = new byte[ByteLength];
            Array.Copy(bytes, copy, ByteLength);
            return new CampaignRngKey(copy);
        }

        internal byte[] CopyBytes()
        {
            if (!IsValid) throw new InvalidOperationException("Campaign RNG key is invalid.");
            byte[] copy = new byte[ByteLength];
            Array.Copy(_bytes!, copy, ByteLength);
            return copy;
        }
    }

    public readonly struct RngKeyEpochId : IEquatable<RngKeyEpochId>
    {
        public const int MaxLength = 48;
        private readonly string _value;

        private RngKeyEpochId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static bool TryParse(string? value, out RngKeyEpochId id)
        {
            if (RngText.IsLowerToken(value, MaxLength))
            {
                id = new RngKeyEpochId(value!);
                return true;
            }

            id = default;
            return false;
        }

        public static RngKeyEpochId Parse(string value) => TryParse(value, out RngKeyEpochId id) ? id : throw new FormatException("RngKeyEpochId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(RngKeyEpochId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is RngKeyEpochId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
    }

    public readonly struct RngPurpose : IEquatable<RngPurpose>
    {
        public const int MaxLength = 64;
        private readonly string _value;

        private RngPurpose(string value) => _value = value;
        public bool IsValid => _value != null;
        public static bool TryParse(string? value, out RngPurpose purpose)
        {
            if (RngText.IsDottedLowerIdentifier(value, MaxLength, 2))
            {
                purpose = new RngPurpose(value!);
                return true;
            }

            purpose = default;
            return false;
        }

        public static RngPurpose Parse(string value) => TryParse(value, out RngPurpose purpose) ? purpose : throw new FormatException("RngPurpose is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(RngPurpose other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is RngPurpose other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
    }

    public sealed class RngStreamContext
    {
        private RngStreamContext(CommandId commandId, int decisionOrdinal, RngPurpose purpose, RulesetVersion rulesetVersion)
        {
            CommandId = commandId;
            DecisionOrdinal = decisionOrdinal;
            Purpose = purpose;
            RulesetVersion = rulesetVersion;
        }

        public CommandId CommandId { get; }
        public int DecisionOrdinal { get; }
        public RngPurpose Purpose { get; }
        public RulesetVersion RulesetVersion { get; }

        public static RngStreamContext Create(CommandId commandId, int decisionOrdinal, RngPurpose purpose, RulesetVersion rulesetVersion)
        {
            if (!commandId.IsValid) throw new ArgumentException("Command id is required.", nameof(commandId));
            if (decisionOrdinal < 0) throw new ArgumentOutOfRangeException(nameof(decisionOrdinal));
            if (!purpose.IsValid) throw new ArgumentException("RNG purpose is required.", nameof(purpose));
            if (!rulesetVersion.IsValid) throw new ArgumentException("Ruleset version is required.", nameof(rulesetVersion));
            return new RngStreamContext(commandId, decisionOrdinal, purpose, rulesetVersion);
        }

        internal string ToCanonicalMessage()
        {
            return "odyssey.rng.v1\n" +
                "command=" + CommandId + "\n" +
                "ordinal=" + DecisionOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n" +
                "purpose=" + Purpose + "\n" +
                "ruleset=" + RulesetVersion;
        }
    }

    public readonly struct RngProofData
    {
        public const string DerivationAlgorithmId = "odyssey.hmac-sha256.v1";
        public const string RngAlgorithmId = "odyssey.xoshiro256starstar.v1";
        public const string BoundedMappingId = "odyssey.rejection-u64.v1";

        public RngProofData(RngKeyEpochId keyEpochId, RngPurpose purpose, int decisionOrdinal, int rejectionCount)
        {
            if (!keyEpochId.IsValid) throw new ArgumentException("RNG key epoch id is required.", nameof(keyEpochId));
            if (!purpose.IsValid) throw new ArgumentException("RNG purpose is required.", nameof(purpose));
            if (decisionOrdinal < 0) throw new ArgumentOutOfRangeException(nameof(decisionOrdinal));
            if (rejectionCount < 0) throw new ArgumentOutOfRangeException(nameof(rejectionCount));
            KeyEpochId = keyEpochId;
            Purpose = purpose;
            DecisionOrdinal = decisionOrdinal;
            RejectionCount = rejectionCount;
        }

        public RngKeyEpochId KeyEpochId { get; }
        public RngPurpose Purpose { get; }
        public int DecisionOrdinal { get; }
        public int RejectionCount { get; }
    }

    public readonly struct RngIntegerResult
    {
        public RngIntegerResult(int value, RngProofData proofData)
        {
            Value = value;
            ProofData = proofData;
        }

        public int Value { get; }
        public RngProofData ProofData { get; }
    }

    public static class DeterministicRng
    {
        public static Xoshiro256StarStar CreateStream(CampaignRngKey key, RngStreamContext context)
        {
            if (!key.IsValid) throw new ArgumentException("Campaign RNG key is required.", nameof(key));
            if (context == null) throw new ArgumentNullException(nameof(context));
            byte[] digest;
            using (HMACSHA256 hmac = new HMACSHA256(key.CopyBytes()))
            {
                digest = hmac.ComputeHash(Encoding.UTF8.GetBytes(context.ToCanonicalMessage()));
            }

            ulong s0 = ReadUInt64LittleEndian(digest, 0);
            ulong s1 = ReadUInt64LittleEndian(digest, 8);
            ulong s2 = ReadUInt64LittleEndian(digest, 16);
            ulong s3 = ReadUInt64LittleEndian(digest, 24);
            if ((s0 | s1 | s2 | s3) == 0UL)
            {
                digest[31] = 1;
                s0 = ReadUInt64LittleEndian(digest, 0);
                s1 = ReadUInt64LittleEndian(digest, 8);
                s2 = ReadUInt64LittleEndian(digest, 16);
                s3 = ReadUInt64LittleEndian(digest, 24);
            }

            return new Xoshiro256StarStar(s0, s1, s2, s3);
        }

        public static RngIntegerResult NextIntInclusive(ref Xoshiro256StarStar stream, int minInclusive, int maxInclusive, RngKeyEpochId keyEpochId, RngPurpose purpose, int decisionOrdinal)
        {
            if (minInclusive > maxInclusive) throw new ArgumentOutOfRangeException(nameof(minInclusive));
            ulong range = (ulong)((long)maxInclusive - minInclusive) + 1UL;
            ulong threshold = (0UL - range) % range;
            int rejections = 0;

            while (true)
            {
                ulong sample = stream.NextUInt64();
                if (sample >= threshold)
                {
                    int value = minInclusive + (int)(sample % range);
                    return new RngIntegerResult(value, new RngProofData(keyEpochId, purpose, decisionOrdinal, rejections));
                }

                rejections++;
            }
        }

        private static ulong ReadUInt64LittleEndian(byte[] bytes, int offset)
        {
            return ((ulong)bytes[offset]) |
                ((ulong)bytes[offset + 1] << 8) |
                ((ulong)bytes[offset + 2] << 16) |
                ((ulong)bytes[offset + 3] << 24) |
                ((ulong)bytes[offset + 4] << 32) |
                ((ulong)bytes[offset + 5] << 40) |
                ((ulong)bytes[offset + 6] << 48) |
                ((ulong)bytes[offset + 7] << 56);
        }
    }

    public struct Xoshiro256StarStar
    {
        private ulong _s0;
        private ulong _s1;
        private ulong _s2;
        private ulong _s3;

        public Xoshiro256StarStar(ulong s0, ulong s1, ulong s2, ulong s3)
        {
            if ((s0 | s1 | s2 | s3) == 0UL) throw new ArgumentException("xoshiro256** state cannot be all zero.");
            _s0 = s0;
            _s1 = s1;
            _s2 = s2;
            _s3 = s3;
        }

        public ulong State0 => _s0;
        public ulong State1 => _s1;
        public ulong State2 => _s2;
        public ulong State3 => _s3;

        public ulong NextUInt64()
        {
            ulong result = RotateLeft(_s1 * 5UL, 7) * 9UL;
            ulong t = _s1 << 17;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = RotateLeft(_s3, 45);
            return result;
        }

        private static ulong RotateLeft(ulong value, int offset)
        {
            return (value << offset) | (value >> (64 - offset));
        }
    }

    internal static class RngText
    {
        internal static bool IsLowerToken(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length > maxLength || value.Trim() != value)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-'))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool IsDottedLowerIdentifier(string? value, int maxLength, int minSegments)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length > maxLength || value.Trim() != value)
            {
                return false;
            }

            int segments = 1;
            bool segmentHasCharacter = false;
            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (c == '.')
                {
                    if (!segmentHasCharacter) return false;
                    segments++;
                    segmentHasCharacter = false;
                    continue;
                }

                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_'))
                {
                    return false;
                }

                segmentHasCharacter = true;
            }

            return segmentHasCharacter && segments >= minSegments;
        }
    }
}
