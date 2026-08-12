using Odyssey.Application.Commands;
using Odyssey.Application.Diagnostics;
using Odyssey.Application.Results;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Serialization
{
    public sealed class SerializationSmokeResult
    {
        public SerializationSmokeResult(string fingerprint, string payloadHash, string diagnosticHash, string manifestHash)
        {
            Fingerprint = fingerprint;
            PayloadHash = payloadHash;
            DiagnosticHash = diagnosticHash;
            ManifestHash = manifestHash;
        }

        public string Fingerprint { get; }
        public string PayloadHash { get; }
        public string DiagnosticHash { get; }
        public string ManifestHash { get; }
    }

    public static class SerializationSmoke
    {
        public static Result<SerializationSmokeResult> Run()
        {
            JsonPayload payload = SyntheticPayloadCodec.WriteV2(new SyntheticPayloadV2("ready", 42, SyntheticMode.Ready));
            string payloadHash = CanonicalJson.Sha256LowerHex(payload.Bytes);

            CommandFingerprintMaterialV1 material = new CommandFingerprintMaterialV1(
                CommandType.Parse("odyssey.synthetic.command"),
                CommandVersion.Create(1),
                CampaignId.Parse("camp_0123456789abcdef0123456789abcdef"),
                CommandIssuerKind.User,
                CommandId.Parse("cmd_0123456789abcdef0123456789abcdef"),
                CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef"),
                SyntheticEventRecordCodec.Type,
                ContractVersion.Create(1),
                payload,
                SessionId.Parse("sess_0123456789abcdef0123456789abcdef"),
                UserId.Parse("user_0123456789abcdef0123456789abcdef"),
                CharacterId.Parse("char_0123456789abcdef0123456789abcdef"),
                expectedAggregateRevisions: new[]
                {
                    new ExpectedAggregateRevisionMaterial(AggregateType.Parse("synthetic.operation"), AggregateId.Parse("beta"), 7),
                    new ExpectedAggregateRevisionMaterial(AggregateType.Parse("synthetic.operation"), AggregateId.Parse("alpha"), 3)
                });
            CommandFingerprintMaterialV1Codec commandCodec = new CommandFingerprintMaterialV1Codec();
            Result<CommandFingerprint> fingerprint = commandCodec.ComputeFingerprint(material);
            if (fingerprint.IsFailure) return Result<SerializationSmokeResult>.Failure(fingerprint.Error);

            SyntheticEventRecord record = new SyntheticEventRecord(SyntheticEventRecordCodec.Type, ContractVersion.Create(1), payload, payloadHash);
            Result<JsonPayload> eventJson = new SyntheticEventRecordCodec().Write(record);
            if (eventJson.IsFailure) return Result<SerializationSmokeResult>.Failure(eventJson.Error);
            Result<SyntheticEventRecord> eventRoundTrip = new SyntheticEventRecordCodec().Read(eventJson.Value.Bytes);
            if (eventRoundTrip.IsFailure) return Result<SerializationSmokeResult>.Failure(eventRoundTrip.Error);

            LogEventV1 logEvent = new LogEventV1(
                UtcInstant.Parse("2026-08-12T00:00:00.0000000Z"),
                LogLevel.Information,
                OdysseyEventCodes.DiagnosticsProbeEmitted,
                SubsystemName.Parse("diagnostics"),
                BuildIdAvailability.UnavailableNotYetComposed,
                ProcessInstanceId.Parse("proc_0123456789abcdef0123456789abcdef"),
                MessageTemplateKey.Parse("log.diagnostics.probe.emitted"),
                new[] { new SafeLogProperty(SafePropertyKey.Parse("probe"), SafeLogValue.Code("serialization")) },
                CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef"));
            Result<JsonPayload> diagnosticJson = new LogEventV1JsonCodec().Write(logEvent);
            if (diagnosticJson.IsFailure) return Result<SerializationSmokeResult>.Failure(diagnosticJson.Error);
            Result<LogEventV1> diagnosticRoundTrip = new LogEventV1JsonCodec().Read(diagnosticJson.Value.Bytes);
            if (diagnosticRoundTrip.IsFailure) return Result<SerializationSmokeResult>.Failure(diagnosticRoundTrip.Error);

            OdcampManifestV1 manifest = new OdcampManifestV1("smoke", "Serialization Smoke", "assets/smoke");
            Result<JsonPayload> manifestJson = new OdcampManifestV1Codec().Write(manifest);
            if (manifestJson.IsFailure) return Result<SerializationSmokeResult>.Failure(manifestJson.Error);
            Result<OdcampManifestV1> manifestRoundTrip = new OdcampManifestV1Codec().Read(manifestJson.Value.Bytes);
            if (manifestRoundTrip.IsFailure) return Result<SerializationSmokeResult>.Failure(manifestRoundTrip.Error);

            return Result<SerializationSmokeResult>.Success(new SerializationSmokeResult(
                fingerprint.Value.ToString(),
                payloadHash,
                CanonicalJson.Sha256LowerHex(diagnosticJson.Value.Bytes),
                CanonicalJson.Sha256LowerHex(manifestJson.Value.Bytes)));
        }
    }
}
