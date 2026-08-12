using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Odyssey.Application.Commands;
using Odyssey.Application.Results;
using Odyssey.Domain.Identity;

namespace Odyssey.Application.Serialization
{
    public enum SyntheticMode
    {
        Ready = 1,
        Archived = 2
    }

    public readonly struct ExpectedAggregateRevisionMaterial
    {
        public ExpectedAggregateRevisionMaterial(AggregateType aggregateType, AggregateId aggregateId, long expectedRevision)
        {
            if (!aggregateType.IsValid) throw new ArgumentException("AggregateType is required.", nameof(aggregateType));
            if (!aggregateId.IsValid) throw new ArgumentException("AggregateId is required.", nameof(aggregateId));
            if (expectedRevision < 0) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            AggregateType = aggregateType;
            AggregateId = aggregateId;
            ExpectedRevision = expectedRevision;
        }

        public AggregateType AggregateType { get; }
        public AggregateId AggregateId { get; }
        public long ExpectedRevision { get; }
    }

    public sealed class CommandFingerprintMaterialV1
    {
        private readonly ReadOnlyCollection<ExpectedAggregateRevisionMaterial> _expectedAggregateRevisions;

        public CommandFingerprintMaterialV1(
            CommandType commandType,
            CommandVersion commandVersion,
            CampaignId campaignId,
            CommandIssuerKind issuerKind,
            CommandId rootCommandId,
            CorrelationId correlationId,
            ContractType payloadContractType,
            ContractVersion payloadContractVersion,
            JsonPayload canonicalPayload,
            SessionId? sessionId = null,
            UserId? actorUserId = null,
            CharacterId? actorCharacterId = null,
            CommandId? parentCommandId = null,
            long? expectedCampaignRevision = null,
            long? expectedSessionSequence = null,
            IReadOnlyList<ExpectedAggregateRevisionMaterial>? expectedAggregateRevisions = null)
        {
            if (!commandType.IsValid) throw new ArgumentException("CommandType is required.", nameof(commandType));
            if (!commandVersion.IsValid) throw new ArgumentException("CommandVersion is required.", nameof(commandVersion));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (!Enum.IsDefined(typeof(CommandIssuerKind), issuerKind)) throw new ArgumentOutOfRangeException(nameof(issuerKind));
            if (!rootCommandId.IsValid) throw new ArgumentException("RootCommandId is required.", nameof(rootCommandId));
            if (!correlationId.IsValid) throw new ArgumentException("CorrelationId is required.", nameof(correlationId));
            if (!payloadContractType.IsValid) throw new ArgumentException("Payload ContractType is required.", nameof(payloadContractType));
            if (!payloadContractVersion.IsValid) throw new ArgumentException("Payload ContractVersion is required.", nameof(payloadContractVersion));
            CanonicalPayload = canonicalPayload ?? throw new ArgumentNullException(nameof(canonicalPayload));
            CommandType = commandType;
            CommandVersion = commandVersion;
            CampaignId = campaignId;
            SessionId = sessionId;
            IssuerKind = issuerKind;
            ActorUserId = actorUserId;
            ActorCharacterId = actorCharacterId;
            RootCommandId = rootCommandId;
            ParentCommandId = parentCommandId;
            CorrelationId = correlationId;
            ExpectedCampaignRevision = expectedCampaignRevision;
            ExpectedSessionSequence = expectedSessionSequence;
            PayloadContractType = payloadContractType;
            PayloadContractVersion = payloadContractVersion;
            _expectedAggregateRevisions = Array.AsReadOnly(CopyAndSort(expectedAggregateRevisions ?? Array.Empty<ExpectedAggregateRevisionMaterial>()));
        }

        public int FingerprintVersion => 1;
        public CommandType CommandType { get; }
        public CommandVersion CommandVersion { get; }
        public CampaignId CampaignId { get; }
        public SessionId? SessionId { get; }
        public CommandIssuerKind IssuerKind { get; }
        public UserId? ActorUserId { get; }
        public CharacterId? ActorCharacterId { get; }
        public CommandId RootCommandId { get; }
        public CommandId? ParentCommandId { get; }
        public CorrelationId CorrelationId { get; }
        public long? ExpectedCampaignRevision { get; }
        public long? ExpectedSessionSequence { get; }
        public IReadOnlyList<ExpectedAggregateRevisionMaterial> ExpectedAggregateRevisions => _expectedAggregateRevisions;
        public ContractType PayloadContractType { get; }
        public ContractVersion PayloadContractVersion { get; }
        public JsonPayload CanonicalPayload { get; }

        private static ExpectedAggregateRevisionMaterial[] CopyAndSort(IReadOnlyList<ExpectedAggregateRevisionMaterial> source)
        {
            ExpectedAggregateRevisionMaterial[] copy = new ExpectedAggregateRevisionMaterial[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                copy[index] = source[index];
            }

            return copy.OrderBy(v => v.AggregateType.ToString(), StringComparer.Ordinal)
                .ThenBy(v => v.AggregateId.ToString(), StringComparer.Ordinal)
                .ToArray();
        }
    }

    public sealed class CommandFingerprintMaterialV1Codec : IJsonContractCodec<CommandFingerprintMaterialV1>
    {
        public static readonly ContractType Type = ContractType.Parse("odyssey.command.fingerprint.material");
        public JsonContractKey Key { get; } = new JsonContractKey(SerializationProfile.AuthoritativePayloadJson, Type, ContractVersion.Create(1));

        public Result<JsonPayload> Write(CommandFingerprintMaterialV1 value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            CanonicalJsonWriter writer = new CanonicalJsonWriter().StartObject()
                .Int32("fingerprintVersion", value.FingerprintVersion)
                .String("commandType", value.CommandType.ToString())
                .Int32("commandVersion", value.CommandVersion.Value)
                .String("campaignId", value.CampaignId.ToString())
                .NullableString("sessionId", value.SessionId?.ToString())
                .String("issuerKind", ToIssuerToken(value.IssuerKind))
                .NullableString("actorUserId", value.ActorUserId?.ToString())
                .NullableString("actorCharacterId", value.ActorCharacterId?.ToString())
                .String("rootCommandId", value.RootCommandId.ToString())
                .NullableString("parentCommandId", value.ParentCommandId?.ToString())
                .String("correlationId", value.CorrelationId.ToString())
                .NullableInt64("expectedCampaignRevision", value.ExpectedCampaignRevision)
                .NullableInt64("expectedSessionSequence", value.ExpectedSessionSequence)
                .StartArray("expectedAggregateRevisions");
            for (int index = 0; index < value.ExpectedAggregateRevisions.Count; index++)
            {
                ExpectedAggregateRevisionMaterial revision = value.ExpectedAggregateRevisions[index];
                writer.StartArrayObject()
                    .String("aggregateType", revision.AggregateType.ToString())
                    .String("aggregateId", revision.AggregateId.ToString())
                    .Int64("expectedRevision", revision.ExpectedRevision)
                    .EndArrayObject();
            }

            writer.EndArray()
                .String("payloadContractType", value.PayloadContractType.ToString())
                .Int32("payloadContractVersion", value.PayloadContractVersion.Value)
                .RawJson("canonicalPayload", value.CanonicalPayload.Utf8Text)
                .EndObject();
            return Result<JsonPayload>.Success(writer.ToPayload());
        }

        public Result<CommandFingerprintMaterialV1> Read(byte[] utf8Json)
        {
            Result<JsonObjectReader> reader = JsonObjectReader.Read(utf8Json, JsonPayloadLimits.CommandPayloadBytes);
            if (reader.IsFailure) return Result<CommandFingerprintMaterialV1>.Failure(reader.Error);
            Result<string> commandType = reader.Value.RequiredString("commandType");
            return commandType.IsFailure ? Result<CommandFingerprintMaterialV1>.Failure(commandType.Error) : Result<CommandFingerprintMaterialV1>.Failure(SerializationFailures.UnsupportedContract());
        }

        public Result<CommandFingerprint> ComputeFingerprint(CommandFingerprintMaterialV1 value)
        {
            Result<JsonPayload> payload = Write(value);
            if (payload.IsFailure) return Result<CommandFingerprint>.Failure(payload.Error);
            return Result<CommandFingerprint>.Success(CommandFingerprint.Parse("fp_" + CanonicalJson.Sha256LowerHex(payload.Value.Bytes)));
        }

        private static string ToIssuerToken(CommandIssuerKind kind)
        {
            switch (kind)
            {
                case CommandIssuerKind.User: return "user";
                case CommandIssuerKind.HostSystem: return "host_system";
                case CommandIssuerKind.Migration: return "migration";
                case CommandIssuerKind.Recovery: return "recovery";
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

    }

    public sealed class SyntheticEventRecord
    {
        public SyntheticEventRecord(ContractType contractType, ContractVersion contractVersion, JsonPayload payloadJson, string payloadHash)
        {
            if (!contractType.IsValid) throw new ArgumentException("ContractType is required.", nameof(contractType));
            if (!contractVersion.IsValid) throw new ArgumentException("ContractVersion is required.", nameof(contractVersion));
            PayloadJson = payloadJson ?? throw new ArgumentNullException(nameof(payloadJson));
            if (!IsSha256Hex(payloadHash)) throw new ArgumentException("PayloadHash is not canonical.", nameof(payloadHash));
            ContractType = contractType;
            ContractVersion = contractVersion;
            PayloadHash = payloadHash;
        }

        public ContractType ContractType { get; }
        public ContractVersion ContractVersion { get; }
        public JsonPayload PayloadJson { get; }
        public string PayloadHash { get; }
        public Result VerifyHash() => PayloadHash == CanonicalJson.Sha256LowerHex(PayloadJson.Bytes) ? Result.Success() : Result.Failure(SerializationFailures.IntegrityMismatch());

        private static bool IsSha256Hex(string value)
        {
            if (value == null || value.Length != 64) return false;
            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            }

            return true;
        }
    }

    public sealed class SyntheticEventRecordCodec : IJsonContractCodec<SyntheticEventRecord>
    {
        public static readonly ContractType Type = ContractType.Parse("odyssey.synthetic.event.record");
        public JsonContractKey Key { get; } = new JsonContractKey(SerializationProfile.PersistenceJson, Type, ContractVersion.Create(1));

        public Result<JsonPayload> Write(SyntheticEventRecord value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            JsonPayload payload = new CanonicalJsonWriter().StartObject()
                .String("contractType", value.ContractType.ToString())
                .Int32("contractVersion", value.ContractVersion.Value)
                .String("payloadJson", value.PayloadJson.Utf8Text)
                .String("payloadHash", value.PayloadHash)
                .EndObject()
                .ToPayload();
            return Result<JsonPayload>.Success(payload);
        }

        public Result<SyntheticEventRecord> Read(byte[] utf8Json)
        {
            Result<JsonObjectReader> reader = JsonObjectReader.Read(utf8Json, JsonPayloadLimits.EventPayloadBytes);
            if (reader.IsFailure) return Result<SyntheticEventRecord>.Failure(reader.Error);
            Result schema = reader.Value.EnsureOnly("contractType", "contractVersion", "payloadJson", "payloadHash");
            if (schema.IsFailure) return Result<SyntheticEventRecord>.Failure(schema.Error);
            Result<string> type = reader.Value.RequiredString("contractType");
            Result<int> version = reader.Value.RequiredInt32("contractVersion");
            Result<string> payloadJson = reader.Value.RequiredString("payloadJson");
            Result<string> hash = reader.Value.RequiredString("payloadHash");
            if (type.IsFailure) return Result<SyntheticEventRecord>.Failure(type.Error);
            if (version.IsFailure) return Result<SyntheticEventRecord>.Failure(version.Error);
            if (payloadJson.IsFailure) return Result<SyntheticEventRecord>.Failure(payloadJson.Error);
            if (hash.IsFailure) return Result<SyntheticEventRecord>.Failure(hash.Error);
            if (!ContractType.TryParse(type.Value, out ContractType contractType) || contractType != Type || version.Value != 1) return Result<SyntheticEventRecord>.Failure(SerializationFailures.UnsupportedContract());
            SyntheticEventRecord record = new SyntheticEventRecord(contractType, ContractVersion.Create(version.Value), new JsonPayload(CanonicalJson.ToUtf8Bytes(payloadJson.Value)), hash.Value);
            Result verify = record.VerifyHash();
            return verify.IsFailure ? Result<SyntheticEventRecord>.Failure(verify.Error) : Result<SyntheticEventRecord>.Success(record);
        }
    }

    public sealed class SyntheticPayloadV2
    {
        public SyntheticPayloadV2(string name, int count, SyntheticMode mode)
        {
            if (!SerializationText.IsLowerToken(name, 64)) throw new ArgumentException("Name is not canonical.", nameof(name));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (!Enum.IsDefined(typeof(SyntheticMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            Name = name;
            Count = count;
            Mode = mode;
        }

        public string Name { get; }
        public int Count { get; }
        public SyntheticMode Mode { get; }
    }

    public static class SyntheticPayloadCodec
    {
        public static JsonPayload WriteV2(SyntheticPayloadV2 value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return new CanonicalJsonWriter().StartObject()
                .String("name", value.Name)
                .Int32("count", value.Count)
                .String("mode", ToModeToken(value.Mode))
                .EndObject()
                .ToPayload();
        }

        public static Result<SyntheticPayloadV2> ReadV2(byte[] json)
        {
            Result<JsonObjectReader> reader = JsonObjectReader.Read(json, JsonPayloadLimits.EventPayloadBytes);
            if (reader.IsFailure) return Result<SyntheticPayloadV2>.Failure(reader.Error);
            Result schema = reader.Value.EnsureOnly("name", "count", "mode");
            if (schema.IsFailure) return Result<SyntheticPayloadV2>.Failure(schema.Error);
            Result<string> name = reader.Value.RequiredString("name");
            Result<int> count = reader.Value.RequiredInt32("count");
            Result<string> mode = reader.Value.RequiredString("mode");
            if (name.IsFailure) return Result<SyntheticPayloadV2>.Failure(name.Error);
            if (count.IsFailure) return Result<SyntheticPayloadV2>.Failure(count.Error);
            if (mode.IsFailure) return Result<SyntheticPayloadV2>.Failure(mode.Error);
            if (!TryParseMode(mode.Value, out SyntheticMode parsedMode)) return Result<SyntheticPayloadV2>.Failure(SerializationFailures.InvalidPayload());
            return Result<SyntheticPayloadV2>.Success(new SyntheticPayloadV2(name.Value, count.Value, parsedMode));
        }

        public static Result<JsonPayload> UpcastV1ToV2(byte[] v1Json)
        {
            Result<JsonObjectReader> reader = JsonObjectReader.Read(v1Json, JsonPayloadLimits.EventPayloadBytes);
            if (reader.IsFailure) return Result<JsonPayload>.Failure(reader.Error);
            Result schema = reader.Value.EnsureOnly("name", "count");
            if (schema.IsFailure) return Result<JsonPayload>.Failure(schema.Error);
            Result<string> name = reader.Value.RequiredString("name");
            Result<int> count = reader.Value.RequiredInt32("count");
            if (name.IsFailure) return Result<JsonPayload>.Failure(name.Error);
            if (count.IsFailure) return Result<JsonPayload>.Failure(count.Error);
            return Result<JsonPayload>.Success(WriteV2(new SyntheticPayloadV2(name.Value, count.Value, SyntheticMode.Ready)));
        }

        private static string ToModeToken(SyntheticMode mode)
        {
            switch (mode)
            {
                case SyntheticMode.Ready: return "ready";
                case SyntheticMode.Archived: return "archived";
                default: throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }

        private static bool TryParseMode(string value, out SyntheticMode mode)
        {
            if (value == "ready")
            {
                mode = SyntheticMode.Ready;
                return true;
            }

            if (value == "archived")
            {
                mode = SyntheticMode.Archived;
                return true;
            }

            mode = default;
            return false;
        }
    }

    public interface IJsonPayloadUpcaster
    {
        ContractType ContractType { get; }
        ContractVersion FromVersion { get; }
        ContractVersion ToVersion { get; }
        Result<JsonPayload> Upcast(byte[] sourceUtf8Json);
    }

    public sealed class SyntheticPayloadV1ToV2Upcaster : IJsonPayloadUpcaster
    {
        public ContractType ContractType => SyntheticEventRecordCodec.Type;
        public ContractVersion FromVersion => ContractVersion.Create(1);
        public ContractVersion ToVersion => ContractVersion.Create(2);
        public Result<JsonPayload> Upcast(byte[] sourceUtf8Json) => SyntheticPayloadCodec.UpcastV1ToV2(sourceUtf8Json);
    }

    public sealed class JsonPayloadUpcasterRegistry
    {
        private readonly IReadOnlyList<IJsonPayloadUpcaster> _upcasters;

        public JsonPayloadUpcasterRegistry(IReadOnlyList<IJsonPayloadUpcaster> upcasters)
        {
            if (upcasters == null) throw new ArgumentNullException(nameof(upcasters));
            IJsonPayloadUpcaster[] copy = new IJsonPayloadUpcaster[upcasters.Count];
            for (int index = 0; index < upcasters.Count; index++)
            {
                copy[index] = upcasters[index] ?? throw new ArgumentException("Upcaster is required.", nameof(upcasters));
            }

            _upcasters = Array.AsReadOnly(copy);
        }

        public Result<JsonPayload> Upcast(ContractType contractType, ContractVersion fromVersion, ContractVersion toVersion, byte[] sourceUtf8Json)
        {
            if (!contractType.IsValid) throw new ArgumentException("ContractType is required.", nameof(contractType));
            if (!fromVersion.IsValid) throw new ArgumentException("FromVersion is required.", nameof(fromVersion));
            if (!toVersion.IsValid) throw new ArgumentException("ToVersion is required.", nameof(toVersion));
            if (sourceUtf8Json == null) throw new ArgumentNullException(nameof(sourceUtf8Json));
            ContractVersion current = fromVersion;
            byte[] currentBytes = Copy(sourceUtf8Json);
            while (current.CompareTo(toVersion) < 0)
            {
                IJsonPayloadUpcaster? next = null;
                for (int index = 0; index < _upcasters.Count; index++)
                {
                    IJsonPayloadUpcaster candidate = _upcasters[index];
                    if (candidate.ContractType == contractType && candidate.FromVersion.Equals(current))
                    {
                        next = candidate;
                        break;
                    }
                }

                if (next == null) return Result<JsonPayload>.Failure(SerializationFailures.UnsupportedContract());
                Result<JsonPayload> upcasted = next.Upcast(currentBytes);
                if (upcasted.IsFailure) return upcasted;
                currentBytes = upcasted.Value.Bytes;
                current = next.ToVersion;
            }

            return current.Equals(toVersion) ? Result<JsonPayload>.Success(new JsonPayload(currentBytes)) : Result<JsonPayload>.Failure(SerializationFailures.UnsupportedContract());
        }

        private static byte[] Copy(byte[] source)
        {
            byte[] copy = new byte[source.Length];
            Buffer.BlockCopy(source, 0, copy, 0, source.Length);
            return copy;
        }
    }
}
