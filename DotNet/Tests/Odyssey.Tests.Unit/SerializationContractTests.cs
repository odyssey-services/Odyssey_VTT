using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Diagnostics;
using Odyssey.Application.Serialization;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Tests.Unit
{
    public sealed class SerializationContractTests
    {
        [Test]
        public void CanonicalPayloadAndCommandFingerprintAreStableAndSemantic()
        {
            JsonPayload payload = SyntheticPayloadCodec.WriteV2(new SyntheticPayloadV2("ready", 42, SyntheticMode.Ready));
            Assert.That(payload.Utf8Text, Is.EqualTo("{\"name\":\"ready\",\"count\":42,\"mode\":\"ready\"}"));
            Assert.That(payload.Bytes[0], Is.Not.EqualTo(0xEF));

            CommandFingerprintMaterialV1 material = CreateFingerprintMaterial(payload);
            CommandFingerprintMaterialV1 reversed = CreateFingerprintMaterial(payload, reverseAggregateInput: true);
            CommandFingerprintMaterialV1Codec codec = new CommandFingerprintMaterialV1Codec();
            string fingerprint = codec.ComputeFingerprint(material).Value.ToString();
            string reversedFingerprint = codec.ComputeFingerprint(reversed).Value.ToString();

            Assert.That(fingerprint, Is.EqualTo(reversedFingerprint));
            Assert.That(fingerprint, Does.StartWith("fp_"));
            Assert.That(fingerprint.Length, Is.EqualTo(67));
            Assert.That(codec.Write(material).Value.Utf8Text, Does.Not.Contain("commandId"));
            Assert.That(codec.Write(material).Value.Utf8Text, Does.Not.Contain("transport"));
        }

        [Test]
        public void SyntheticEventPayloadRoundTripsAndHashMismatchFailsSafely()
        {
            JsonPayload payload = SyntheticPayloadCodec.WriteV2(new SyntheticPayloadV2("ready", 42, SyntheticMode.Ready));
            string hash = CanonicalJson.Sha256LowerHex(payload.Bytes);
            SyntheticEventRecord record = new SyntheticEventRecord(SyntheticEventRecordCodec.Type, ContractVersion.Create(1), payload, hash);
            SyntheticEventRecordCodec codec = new SyntheticEventRecordCodec();

            JsonPayload encoded = codec.Write(record).Value;
            SyntheticEventRecord decoded = codec.Read(encoded.Bytes).Value;
            Assert.That(decoded.PayloadJson.Utf8Text, Is.EqualTo(payload.Utf8Text));
            Assert.That(decoded.VerifyHash().IsSuccess, Is.True);

            string corrupted = encoded.Utf8Text.Replace(hash, new string('0', 64));
            Assert.That(codec.Read(CanonicalJson.ToUtf8Bytes(corrupted)).IsFailure, Is.True);
        }

        [Test]
        public void UpcasterIsPureAndOriginalBytesRemainUnchanged()
        {
            byte[] original = File.ReadAllBytes(FindRepositoryFile("Tests/Fixtures/Serialization/synthetic-payload-v1.json"));
            byte[] copy = original.ToArray();
            JsonPayload upcasted = SyntheticPayloadCodec.UpcastV1ToV2(original).Value;

            Assert.That(original, Is.EqualTo(copy));
            Assert.That(upcasted.Utf8Text, Is.EqualTo(ReadFixtureText("Tests/Fixtures/Serialization/synthetic-payload-v2.json")));
            Assert.That(SyntheticPayloadCodec.ReadV2(upcasted.Bytes).IsSuccess, Is.True);
            Assert.That(SyntheticPayloadCodec.ReadV2(CanonicalJson.ToUtf8Bytes("{\"name\":\"ready\",\"count\":42,\"mode\":\"future\"}")).IsFailure, Is.True);
        }

        [Test]
        public void StrictReaderRejectsDuplicateCommentsTrailingCommaBadUtf8DepthAndOversize()
        {
            Assert.That(JsonObjectReader.Read(CanonicalJson.ToUtf8Bytes("{\"name\":\"a\",\"name\":\"b\"}"), 256).IsFailure, Is.True);
            Assert.That(JsonObjectReader.Read(CanonicalJson.ToUtf8Bytes("{/*x*/\"name\":\"a\"}"), 256).IsFailure, Is.True);
            Assert.That(JsonObjectReader.Read(CanonicalJson.ToUtf8Bytes("{\"name\":\"a\",}"), 256).IsFailure, Is.True);
            Assert.That(JsonObjectReader.Read(new byte[] { 0xEF, 0xBB, 0xBF, 0x7B, 0x7D }, 256).IsFailure, Is.True);
            Assert.That(JsonObjectReader.Read(new byte[] { 0xFF }, 256).IsFailure, Is.True);
            Assert.That(JsonObjectReader.Read(CanonicalJson.ToUtf8Bytes("{\"name\":\"" + new string('a', 300) + "\"}"), 32).IsFailure, Is.True);
            Assert.That(SyntheticPayloadCodec.ReadV2(CanonicalJson.ToUtf8Bytes("{\"name\":\"ready\",\"count\":42,\"mode\":\"ready\",\"nan\":NaN}")).IsFailure, Is.True);
        }

        [Test]
        public void OdcampManifestUsesSafeRelativePathsOnly()
        {
            OdcampManifestV1Codec codec = new OdcampManifestV1Codec();
            JsonPayload json = codec.Write(new OdcampManifestV1("smoke", "Serialization Smoke", "assets/smoke")).Value;
            Assert.That(json.Utf8Text, Is.EqualTo(ReadFixtureText("Tests/Fixtures/Serialization/odcamp-manifest-v1.json")));
            Assert.That(codec.Read(json.Bytes).Value.RelativeAssetPath, Is.EqualTo("assets/smoke"));

            Assert.That(codec.Read(CanonicalJson.ToUtf8Bytes("{\"contractType\":\"odyssey.odcamp.manifest\",\"contractVersion\":1,\"manifestId\":\"smoke\",\"displayName\":\"Smoke\",\"relativeAssetPath\":\"C:/Users/alexx/file\"}")).IsFailure, Is.True);
            Assert.That(codec.Read(CanonicalJson.ToUtf8Bytes("{\"contractType\":\"odyssey.odcamp.manifest\",\"contractVersion\":1,\"manifestId\":\"smoke\",\"displayName\":\"Smoke\",\"relativeAssetPath\":\"../secret\"}")).IsFailure, Is.True);
            Assert.That(codec.Read(CanonicalJson.ToUtf8Bytes("{\"contractType\":\"odyssey.odcamp.manifest\",\"contractVersion\":99,\"manifestId\":\"smoke\",\"displayName\":\"Smoke\",\"relativeAssetPath\":\"assets/smoke\"}")).IsFailure, Is.True);
        }

        [Test]
        public void DiagnosticLogEventSerializesAsLogEventV1AndRejectsFutureSchemaAndDuplicates()
        {
            LogEventV1 logEvent = CreateLogEvent();
            LogEventV1JsonCodec codec = new LogEventV1JsonCodec();
            JsonPayload json = codec.Write(logEvent).Value;

            Assert.That(json.Utf8Text, Does.Contain("\"contractType\":\"odyssey.diagnostics.log_event\""));
            Assert.That(json.Utf8Text, Does.Contain("\"contractVersion\":1"));
            Assert.That(json.Utf8Text, Does.Not.Contain("C:\\"));
            Assert.That(codec.Read(json.Bytes).Value.EventCode, Is.EqualTo(OdysseyEventCodes.DiagnosticsProbeEmitted));
            Assert.That(codec.Read(CanonicalJson.ToUtf8Bytes(json.Utf8Text.Replace("\"contractVersion\":1", "\"contractVersion\":2"))).IsFailure, Is.True);
            Assert.That(codec.Read(CanonicalJson.ToUtf8Bytes("{\"contractType\":\"odyssey.diagnostics.log_event\",\"contractType\":\"odyssey.diagnostics.log_event\",\"contractVersion\":1}")).IsFailure, Is.True);
        }

        [Test]
        public void SerializationSmokeProducesCrossRuntimeVectorShape()
        {
            SerializationSmokeResult result = SerializationSmoke.Run().Value;
            Assert.That(result.Fingerprint, Does.StartWith("fp_"));
            Assert.That(result.PayloadHash, Has.Length.EqualTo(64));
            Assert.That(result.DiagnosticHash, Has.Length.EqualTo(64));
            Assert.That(result.ManifestHash, Has.Length.EqualTo(64));
        }

        [Test]
        public void ExplicitCodecRegistryUsesCompileTimeContractKeys()
        {
            JsonContractRegistry registry = new JsonContractRegistry(new object[]
            {
                new CommandFingerprintMaterialV1Codec(),
                new SyntheticEventRecordCodec(),
                new OdcampManifestV1Codec(),
                new LogEventV1JsonCodec()
            });

            Assert.That(registry.TryGet(new JsonContractKey(SerializationProfile.AuthoritativePayloadJson, CommandFingerprintMaterialV1Codec.Type, ContractVersion.Create(1)), out IJsonContractCodec<CommandFingerprintMaterialV1> commandCodec), Is.True);
            Assert.That(commandCodec, Is.Not.Null);
            Assert.That(registry.TryGet(new JsonContractKey(SerializationProfile.DiagnosticJson, LogEventV1JsonCodec.Type, ContractVersion.Create(1)), out IJsonContractCodec<LogEventV1> diagnosticCodec), Is.True);
            Assert.That(diagnosticCodec, Is.Not.Null);
        }

        private static CommandFingerprintMaterialV1 CreateFingerprintMaterial(JsonPayload payload, bool reverseAggregateInput = false)
        {
            ExpectedAggregateRevisionMaterial[] revisions =
            {
                new ExpectedAggregateRevisionMaterial(AggregateType.Parse("synthetic.operation"), AggregateId.Parse("alpha"), 3),
                new ExpectedAggregateRevisionMaterial(AggregateType.Parse("synthetic.operation"), AggregateId.Parse("beta"), 7)
            };
            if (reverseAggregateInput) Array.Reverse(revisions);

            return new CommandFingerprintMaterialV1(
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
                expectedAggregateRevisions: revisions);
        }

        private static LogEventV1 CreateLogEvent()
        {
            return new LogEventV1(
                UtcInstant.Parse("2026-08-12T00:00:00.0000000Z"),
                LogLevel.Information,
                OdysseyEventCodes.DiagnosticsProbeEmitted,
                SubsystemName.Parse("diagnostics"),
                BuildIdAvailability.UnavailableNotYetComposed,
                ProcessInstanceId.Parse("proc_0123456789abcdef0123456789abcdef"),
                MessageTemplateKey.Parse("log.diagnostics.probe.emitted"),
                new[] { new SafeLogProperty(SafePropertyKey.Parse("probe"), SafeLogValue.Code("serialization")) },
                CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef"));
        }

        private static string FindRepositoryFile(string relativePath)
        {
            DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null)
            {
                string candidate = Path.Combine(current.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate)) return candidate;
                current = current.Parent;
            }

            throw new FileNotFoundException(relativePath);
        }

        private static string ReadFixtureText(string relativePath)
        {
            return File.ReadAllText(FindRepositoryFile(relativePath)).TrimEnd('\r', '\n');
        }
    }
}
