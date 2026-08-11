using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Odyssey.Application.Commands;
using Odyssey.Application.Identity;
using Odyssey.Application.Results;
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

    public readonly struct CampaignId : IEquatable<CampaignId>
    {
        private const string Prefix = "camp_";
        private const int HexLength = 32;
        private readonly string _value;

        private CampaignId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static bool TryParse(string? value, out CampaignId id) => RngText.TryParsePrefixedHex(value, Prefix, HexLength, out id, static v => new CampaignId(v));
        public static CampaignId Parse(string value) => TryParse(value, out CampaignId id) ? id : throw new FormatException("CampaignId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(CampaignId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is CampaignId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(CampaignId left, CampaignId right) => left.Equals(right);
        public static bool operator !=(CampaignId left, CampaignId right) => !left.Equals(right);
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

    public readonly struct RngHash : IEquatable<RngHash>
    {
        private readonly string _hex;

        private RngHash(string hex) => _hex = hex;
        public bool IsValid => _hex != null;
        public static RngHash FromBytes(byte[] bytes) => bytes == null ? throw new ArgumentNullException(nameof(bytes)) : new RngHash(RngText.ToLowerHex(bytes));
        public override string ToString() => _hex ?? string.Empty;
        public bool Equals(RngHash other) => string.Equals(_hex, other._hex, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is RngHash other && Equals(other);
        public override int GetHashCode() => _hex == null ? 0 : StringComparer.Ordinal.GetHashCode(_hex);
    }

    public sealed class RandomDecisionContext
    {
        private RandomDecisionContext(CampaignId campaignId, CommandId rootCommandId, int decisionOrdinal, RngPurpose purpose, RulesetVersion rulesetVersion, RngKeyEpochId keyEpochId, CorrelationId correlationId)
        {
            CampaignId = campaignId;
            RootCommandId = rootCommandId;
            DecisionOrdinal = decisionOrdinal;
            Purpose = purpose;
            RulesetVersion = rulesetVersion;
            RngKeyEpochId = keyEpochId;
            CorrelationId = correlationId;
        }

        public const int RngDerivationVersion = 1;
        public const int RngAlgorithmVersion = 1;
        public const int BoundedMappingVersion = 1;
        public const string RngAlgorithmId = "odyssey.xoshiro256starstar.v1";

        public CampaignId CampaignId { get; }
        public CommandId RootCommandId { get; }
        public int DecisionOrdinal { get; }
        public RngPurpose Purpose { get; }
        public RulesetVersion RulesetVersion { get; }
        public RngKeyEpochId RngKeyEpochId { get; }
        public CorrelationId CorrelationId { get; }

        public static RandomDecisionContext Create(CampaignId campaignId, CommandId rootCommandId, int decisionOrdinal, RngPurpose purpose, RulesetVersion rulesetVersion, RngKeyEpochId keyEpochId, CorrelationId correlationId)
        {
            if (!campaignId.IsValid) throw new ArgumentException("Campaign id is required.", nameof(campaignId));
            if (!rootCommandId.IsValid) throw new ArgumentException("Root command id is required.", nameof(rootCommandId));
            if (decisionOrdinal < 0) throw new ArgumentOutOfRangeException(nameof(decisionOrdinal));
            if (!purpose.IsValid) throw new ArgumentException("RNG purpose is required.", nameof(purpose));
            if (!rulesetVersion.IsValid) throw new ArgumentException("Ruleset version is required.", nameof(rulesetVersion));
            if (!keyEpochId.IsValid) throw new ArgumentException("RNG key epoch id is required.", nameof(keyEpochId));
            if (!correlationId.IsValid) throw new ArgumentException("Correlation id is required.", nameof(correlationId));
            return new RandomDecisionContext(campaignId, rootCommandId, decisionOrdinal, purpose, rulesetVersion, keyEpochId, correlationId);
        }
    }

    public readonly struct RandomStreamIdentity
    {
        public RandomStreamIdentity(RandomDecisionContext context, RngHash streamId, RngHash seedCommitment)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            StreamId = streamId.IsValid ? streamId : throw new ArgumentException("Stream id is required.", nameof(streamId));
            SeedCommitment = seedCommitment.IsValid ? seedCommitment : throw new ArgumentException("Seed commitment is required.", nameof(seedCommitment));
        }

        public RandomDecisionContext Context { get; }
        public RngHash StreamId { get; }
        public RngHash SeedCommitment { get; }
    }

    public readonly struct RngProofData
    {
        public RngProofData(RandomDecisionContext context, RngHash streamId, RngHash seedCommitment, int drawIndex, int requestedMin, int requestedMax, int rawStepCount, int result)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (!streamId.IsValid) throw new ArgumentException("Stream id is required.", nameof(streamId));
            if (!seedCommitment.IsValid) throw new ArgumentException("Seed commitment is required.", nameof(seedCommitment));
            if (drawIndex < 0) throw new ArgumentOutOfRangeException(nameof(drawIndex));
            if (rawStepCount <= 0) throw new ArgumentOutOfRangeException(nameof(rawStepCount));
            RngAlgorithmId = RandomDecisionContext.RngAlgorithmId;
            RngAlgorithmVersion = RandomDecisionContext.RngAlgorithmVersion;
            RngDerivationVersion = RandomDecisionContext.RngDerivationVersion;
            BoundedMappingVersion = RandomDecisionContext.BoundedMappingVersion;
            RngKeyEpochId = context.RngKeyEpochId;
            SeedCommitment = seedCommitment;
            StreamId = streamId;
            DecisionOrdinal = context.DecisionOrdinal;
            DrawIndex = drawIndex;
            RequestedMin = requestedMin;
            RequestedMax = requestedMax;
            RawStepCount = rawStepCount;
            RejectionCount = rawStepCount - 1;
            Result = result;
        }

        public string RngAlgorithmId { get; }
        public int RngAlgorithmVersion { get; }
        public int RngDerivationVersion { get; }
        public int BoundedMappingVersion { get; }
        public RngKeyEpochId RngKeyEpochId { get; }
        public RngHash SeedCommitment { get; }
        public RngHash StreamId { get; }
        public int DecisionOrdinal { get; }
        public int DrawIndex { get; }
        public int RequestedMin { get; }
        public int RequestedMax { get; }
        public int RawStepCount { get; }
        public int RejectionCount { get; }
        public int Result { get; }
    }

    public readonly struct RandomSample
    {
        public RandomSample(int value, RngProofData proofData)
        {
            Value = value;
            ProofData = proofData;
        }

        public int Value { get; }
        public RngProofData ProofData { get; }
    }

    public interface IAuthoritativeRandomStreamFactory
    {
        Result<IAuthoritativeRandomStream> Create(RandomDecisionContext context);
    }

    public interface IAuthoritativeRandomStream
    {
        RandomStreamIdentity Identity { get; }
        Result<RandomSample> NextInclusive(int minInclusive, int maxInclusive, int drawIndex);
    }

    public sealed class DeterministicRandomStreamFactory : IAuthoritativeRandomStreamFactory
    {
        private readonly CampaignRngKey _key;

        public DeterministicRandomStreamFactory(CampaignRngKey key)
        {
            if (!key.IsValid) throw new ArgumentException("Campaign RNG key is required.", nameof(key));
            _key = key;
        }

        public Result<IAuthoritativeRandomStream> Create(RandomDecisionContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return Result<IAuthoritativeRandomStream>.Success(HmacSha256StreamDeriverV1.Create(_key, context));
        }
    }

    public static class HmacSha256StreamDeriverV1
    {
        public static byte[] CreateCanonicalMessage(RandomDecisionContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            List<byte> bytes = new List<byte>();
            AppendString(bytes, "odyssey-rng-stream-v1");
            AppendString(bytes, context.CampaignId.ToString());
            AppendString(bytes, context.RootCommandId.ToString());
            AppendUInt32(bytes, checked((uint)context.DecisionOrdinal));
            AppendString(bytes, context.Purpose.ToString());
            AppendString(bytes, context.RulesetVersion.ToString());
            AppendUInt32(bytes, RandomDecisionContext.RngAlgorithmVersion);
            AppendUInt32(bytes, RandomDecisionContext.BoundedMappingVersion);
            AppendString(bytes, context.RngKeyEpochId.ToString());
            return bytes.ToArray();
        }

        public static IAuthoritativeRandomStream Create(CampaignRngKey key, RandomDecisionContext context)
        {
            byte[] message = CreateCanonicalMessage(context);
            byte[] keyBytes = key.CopyBytes();
            byte[] digest = ComputeHmac(keyBytes, message);
            ulong s0 = ReadUInt64LittleEndian(digest, 0);
            ulong s1 = ReadUInt64LittleEndian(digest, 8);
            ulong s2 = ReadUInt64LittleEndian(digest, 16);
            ulong s3 = ReadUInt64LittleEndian(digest, 24);
            if ((s0 | s1 | s2 | s3) == 0UL)
            {
                byte[] fallback = new byte[message.Length + 1];
                Array.Copy(message, fallback, message.Length);
                fallback[fallback.Length - 1] = 0x01;
                digest = ComputeHmac(keyBytes, fallback);
                s0 = ReadUInt64LittleEndian(digest, 0);
                s1 = ReadUInt64LittleEndian(digest, 8);
                s2 = ReadUInt64LittleEndian(digest, 16);
                s3 = ReadUInt64LittleEndian(digest, 24);
            }

            return new AuthoritativeRandomStream(
                new Xoshiro256StarStarV1(s0, s1, s2, s3),
                new RandomStreamIdentity(context, RngHash.FromBytes(Sha256(message)), RngHash.FromBytes(CreateSeedCommitment(context.RngKeyEpochId, keyBytes))));
        }

        public static byte[] CreateSeedCommitment(RngKeyEpochId keyEpochId, byte[] secretKeyMaterial)
        {
            if (!keyEpochId.IsValid) throw new ArgumentException("RNG key epoch id is required.", nameof(keyEpochId));
            if (secretKeyMaterial == null) throw new ArgumentNullException(nameof(secretKeyMaterial));
            byte[] prefix = Encoding.UTF8.GetBytes("odyssey-rng-key-commitment-v1");
            byte[] epoch = Encoding.UTF8.GetBytes(keyEpochId.ToString());
            byte[] input = new byte[prefix.Length + epoch.Length + secretKeyMaterial.Length];
            Buffer.BlockCopy(prefix, 0, input, 0, prefix.Length);
            Buffer.BlockCopy(epoch, 0, input, prefix.Length, epoch.Length);
            Buffer.BlockCopy(secretKeyMaterial, 0, input, prefix.Length + epoch.Length, secretKeyMaterial.Length);
            return Sha256(input);
        }

        internal static byte[] ComputeHmacForTest(byte[] key, byte[] message) => ComputeHmac(key, message);

        private static byte[] ComputeHmac(byte[] key, byte[] message)
        {
            using (HMACSHA256 hmac = new HMACSHA256(key))
            {
                return hmac.ComputeHash(message);
            }
        }

        private static byte[] Sha256(byte[] message)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return sha.ComputeHash(message);
            }
        }

        private static void AppendString(List<byte> target, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            AppendUInt32(target, checked((uint)bytes.Length));
            target.AddRange(bytes);
        }

        private static void AppendUInt32(List<byte> target, int value) => AppendUInt32(target, checked((uint)value));

        private static void AppendUInt32(List<byte> target, uint value)
        {
            target.Add((byte)(value >> 24));
            target.Add((byte)(value >> 16));
            target.Add((byte)(value >> 8));
            target.Add((byte)value);
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

    internal sealed class AuthoritativeRandomStream : IAuthoritativeRandomStream
    {
        private Xoshiro256StarStarV1 _stream;
        private int _nextDrawIndex;

        public AuthoritativeRandomStream(Xoshiro256StarStarV1 stream, RandomStreamIdentity identity)
        {
            _stream = stream;
            Identity = identity;
        }

        public RandomStreamIdentity Identity { get; }

        public Result<RandomSample> NextInclusive(int minInclusive, int maxInclusive, int drawIndex)
        {
            if (minInclusive > maxInclusive)
            {
                return Result<RandomSample>.Failure(CreateRngError(ErrorCodes.RandomInvalidRange, Identity.Context.CorrelationId));
            }

            if (drawIndex != _nextDrawIndex)
            {
                return Result<RandomSample>.Failure(CreateRngError(ErrorCodes.RandomDrawIndexMismatch, Identity.Context.CorrelationId));
            }

            ulong range = checked((ulong)((long)maxInclusive - minInclusive) + 1UL);
            ulong threshold = (0UL - range) % range;
            int rawStepCount = 0;

            while (true)
            {
                ulong sample = _stream.NextUInt64();
                rawStepCount++;
                if (sample >= threshold)
                {
                    int value = checked((int)((long)minInclusive + (long)(sample % range)));
                    _nextDrawIndex++;
                    return Result<RandomSample>.Success(new RandomSample(value, new RngProofData(Identity.Context, Identity.StreamId, Identity.SeedCommitment, drawIndex, minInclusive, maxInclusive, rawStepCount, value)));
                }
            }
        }

        private static Error CreateRngError(ErrorCode code, CorrelationId correlationId)
        {
            return Error.Create(
                code,
                ErrorCategory.Validation,
                SafeReasonCode.InvalidRequest,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                RetryDirective.DoNotRetry,
                correlationId);
        }
    }

    internal struct Xoshiro256StarStarV1
    {
        private ulong _s0;
        private ulong _s1;
        private ulong _s2;
        private ulong _s3;

        public Xoshiro256StarStarV1(ulong s0, ulong s1, ulong s2, ulong s3)
        {
            if ((s0 | s1 | s2 | s3) == 0UL) throw new ArgumentException("xoshiro256** state cannot be all zero.");
            _s0 = s0;
            _s1 = s1;
            _s2 = s2;
            _s3 = s3;
        }

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
        internal static bool TryParsePrefixedHex<T>(string? value, string prefix, int hexLength, out T result, Func<string, T> factory)
        {
            if (value == null || value.Length != prefix.Length + hexLength || !value.StartsWith(prefix, StringComparison.Ordinal))
            {
                result = default!;
                return false;
            }

            for (int index = prefix.Length; index < value.Length; index++)
            {
                char c = value[index];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                {
                    result = default!;
                    return false;
                }
            }

            result = factory(value);
            return true;
        }

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

        internal static string ToLowerHex(byte[] bytes)
        {
            char[] chars = new char[bytes.Length * 2];
            const string hex = "0123456789abcdef";
            for (int index = 0; index < bytes.Length; index++)
            {
                chars[index * 2] = hex[bytes[index] >> 4];
                chars[index * 2 + 1] = hex[bytes[index] & 0x0F];
            }

            return new string(chars);
        }
    }
}
