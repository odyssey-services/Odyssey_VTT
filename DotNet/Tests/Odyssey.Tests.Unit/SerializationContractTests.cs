using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Diagnostics;
using Odyssey.Application.Identity;
using Odyssey.Application.Serialization;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Tests.Unit
{
    public sealed class SerializationContractTests
    {
        private const string ExpectedPayloadHash = "297210561a33067f767e70b9529e758bba64d4994519a8de02f33d2de9d9308b";
        private const string ExpectedFingerprint = "fp_34cb57ecc14fe9985455ed66e42a75e641c7dda3131274d1e8b15a6a0d1ba347";
        private const string ExpectedDiagnosticHash = "95a9b6007c2add9f0faf00f55519dd1abff72d41b19ae7469d07131324111c52";
        private const string ExpectedManifestHash = "ab596e69df0d4a59e3940d36006edf04782c4998e8c51e66b4facd5f2d4cbf92";

        [Test]
        public void ContractTypeGrammarAndRegistryAreExact()
        {
            Assert.That(ContractType.TryParse("campaign.manifest", out _), Is.True);
            Assert.That(ContractType.TryParse("a.b", out _), Is.True);
            Assert.That(ContractType.TryParse("a_b.c", out _), Is.False);
            Assert.That(ContractType.TryParse("a-b.c", out _), Is.False);
            Assert.That(ContractType.TryParse("a.1b", out _), Is.False);
            Assert.That(ContractType.TryParse("a." + new string('b', 126), out _), Is.True);
            Assert.That(ContractType.TryParse("a." + new string('b', 127), out _), Is.False);

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
            Assert.That(CommandFingerprintMaterialV1Codec.Type.ToString(), Is.EqualTo("odyssey.command.fingerprint.material"));
            Assert.That(SyntheticEventRecordCodec.Type.ToString(), Is.EqualTo("odyssey.synthetic.event.record"));
            Assert.That(LogEventV1JsonCodec.Type.ToString(), Is.EqualTo("odyssey.diagnostics.log.event"));
        }

        [Test]
        public void JsonPayloadIsImmutableAtPublicBoundary()
        {
            byte[] source = CanonicalJson.ToUtf8Bytes("{\"name\":\"ready\",\"count\":42,\"mode\":\"ready\"}");
            JsonPayload payload = new JsonPayload(source);
            string hash = CanonicalJson.Sha256LowerHex(payload.Bytes);

            source[2] = (byte)'X';
            byte[] exposed = payload.Bytes;
            exposed[2] = (byte)'Y';

            Assert.That(payload.Utf8Text, Is.EqualTo(ReadFixtureText("Tests/Fixtures/Serialization/synthetic-payload-v2.json")));
            Assert.That(CanonicalJson.Sha256LowerHex(payload.Bytes), Is.EqualTo(hash));
            Assert.That(payload.BytesMemory.Span[2], Is.EqualTo((byte)'n'));
        }

        [Test]
        public void CanonicalPayloadHashesAndCommandFingerprintMaterialAreFrozen()
        {
            JsonPayload payload = SyntheticPayloadCodec.WriteV2(new SyntheticPayloadV2("ready", 42, SyntheticMode.Ready));
            Assert.That(payload.Utf8Text, Is.EqualTo(ReadFixtureText("Tests/Fixtures/Serialization/synthetic-payload-v2.json")));
            Assert.That(CanonicalJson.Sha256LowerHex(payload.Bytes), Is.EqualTo(ExpectedPayloadHash));

            CommandFingerprintMaterialV1 material = CreateFingerprintMaterial(payload);
            CommandFingerprintMaterialV1 reversed = CreateFingerprintMaterial(payload, reverseAggregateInput: true);
            CommandFingerprintMaterialV1Codec codec = new CommandFingerprintMaterialV1Codec();
            JsonPayload materialJson = codec.Write(material).Value;

            Assert.That(materialJson.Utf8Text, Is.EqualTo(ReadFixtureText("Tests/Fixtures/Serialization/command-fingerprint-material-v1.json")));
            Assert.That(materialJson.Utf8Text, Does.Contain("\"expectedCampaignRevision\":null"));
            Assert.That(materialJson.Utf8Text, Does.Contain("\"expectedSessionSequence\":null"));
            Assert.That(materialJson.Utf8Text, Does.Contain("\"expectedAggregateRevisions\":[{\"aggregateType\":\"synthetic.operation\",\"aggregateId\":\"alpha\",\"expectedRevision\":3},{\"aggregateType\":\"synthetic.operation\",\"aggregateId\":\"beta\",\"expectedRevision\":7}]"));
            Assert.That(materialJson.Utf8Text, Does.Contain("\"canonicalPayload\":{\"name\":\"ready\",\"count\":42,\"mode\":\"ready\"}"));
            Assert.That(materialJson.Utf8Text, Does.Not.Contain("commandId\":\"cmd_0123456789abcdef0123456789abcdef"));
            Assert.That(codec.ComputeFingerprint(material).Value.ToString(), Is.EqualTo(ExpectedFingerprint));
            Assert.That(codec.ComputeFingerprint(reversed).Value.ToString(), Is.EqualTo(ExpectedFingerprint));
        }

        [Test]
        public void SyntheticEventHashRoundTripAndFixtureBytesAreStable()
        {
            JsonPayload payload = SyntheticPayloadCodec.WriteV2(new SyntheticPayloadV2("ready", 42, SyntheticMode.Ready));
            SyntheticEventRecordCodec codec = new SyntheticEventRecordCodec();
            SyntheticEventRecord record = new SyntheticEventRecord(SyntheticEventRecordCodec.Type, ContractVersion.Create(1), payload, ExpectedPayloadHash);
            JsonPayload encoded = codec.Write(record).Value;
            SyntheticEventRecord decoded = codec.Read(encoded.Bytes).Value;

            Assert.That(decoded.PayloadJson.Utf8Text, Is.EqualTo(payload.Utf8Text));
            Assert.That(decoded.VerifyHash().IsSuccess, Is.True);
            Assert.That(codec.Read(CanonicalJson.ToUtf8Bytes(encoded.Utf8Text.Replace(ExpectedPayloadHash, new string('0', 64)))).IsFailure, Is.True);
            Assert.That(File.ReadAllText(FindRepositoryFile("Tests/Fixtures/Serialization/synthetic-payload-v1.json")), Is.EqualTo("{\"name\":\"ready\",\"count\":42}\n"));
        }

        [Test]
        public void PureUpcasterChainPassesAndMissingPathFailsBeforeMutation()
        {
            byte[] original = File.ReadAllBytes(FindRepositoryFile("Tests/Fixtures/Serialization/synthetic-payload-v1.json"));
            byte[] copy = original.ToArray();
            JsonPayloadUpcasterRegistry registry = new JsonPayloadUpcasterRegistry(new IJsonPayloadUpcaster[] { new SyntheticPayloadV1ToV2Upcaster() });

            JsonPayload upcasted = registry.Upcast(SyntheticEventRecordCodec.Type, ContractVersion.Create(1), ContractVersion.Create(2), original).Value;
            var missingPath = registry.Upcast(SyntheticEventRecordCodec.Type, ContractVersion.Create(1), ContractVersion.Create(3), original);

            Assert.That(original, Is.EqualTo(copy));
            Assert.That(upcasted.Utf8Text, Is.EqualTo(ReadFixtureText("Tests/Fixtures/Serialization/synthetic-payload-v2.json")));
            Assert.That(missingPath.IsFailure, Is.True);
        }

        [Test]
        public void StrictReaderEnforcesDepthBytesAndMalformedInputWithoutStringFalsePositive()
        {
            Assert.That(JsonObjectReader.ValidateJson(NestedObject(64), 4096, JsonPayloadLimits.MaxDepth).IsSuccess, Is.True);
            Assert.That(JsonObjectReader.ValidateJson(NestedObject(65), 4096, JsonPayloadLimits.MaxDepth).IsFailure, Is.True);
            Assert.That(JsonObjectReader.Read(CanonicalJson.ToUtf8Bytes("{\"name\":\"a\",\"name\":\"b\"}"), 256).IsFailure, Is.True);
            Assert.That(JsonObjectReader.Read(CanonicalJson.ToUtf8Bytes("{/*x*/\"name\":\"a\"}"), 256).IsFailure, Is.True);
            Assert.That(JsonObjectReader.Read(CanonicalJson.ToUtf8Bytes("{\"name\":\"a\",}"), 256).IsFailure, Is.True);
            Assert.That(JsonObjectReader.Read(CanonicalJson.ToUtf8Bytes("{\"name\":\"a\", }"), 256).IsFailure, Is.True);
            Assert.That(JsonObjectReader.Read(CanonicalJson.ToUtf8Bytes("{\"items\":[1, ]}"), 256).IsFailure, Is.True);
            Assert.That(JsonObjectReader.Read(CanonicalJson.ToUtf8Bytes("{\"name\":\",}\"}"), 256).IsSuccess, Is.True);
            Assert.That(JsonObjectReader.Read(new byte[] { 0xEF, 0xBB, 0xBF, 0x7B, 0x7D }, 256).IsFailure, Is.True);
            Assert.That(JsonObjectReader.Read(new byte[] { 0xFF }, 256).IsFailure, Is.True);
            Assert.That(JsonObjectReader.Read(CanonicalJson.ToUtf8Bytes("{\"name\":\"ready\"}"), 10).IsFailure, Is.True);
            Assert.That(SyntheticPayloadCodec.ReadV2(CanonicalJson.ToUtf8Bytes("{\"name\":\"ready\",\"count\":42,\"mode\":\"ready\",\"unexpected\":1}")).IsFailure, Is.True);
        }

        [Test]
        public void InvalidTypedIdTimestampEnumNumericAndContractVectorsFailSafely()
        {
            Assert.That(SyntheticPayloadCodec.ReadV2(CanonicalJson.ToUtf8Bytes("{\"name\":\"ready\",\"count\":42,\"mode\":\"future\"}")).IsFailure, Is.True);
            Assert.That(SyntheticPayloadCodec.ReadV2(CanonicalJson.ToUtf8Bytes("{\"name\":\"ready\",\"count\":42,\"mode\":\"ready\",\"nan\":NaN}")).IsFailure, Is.True);
            Assert.That(SyntheticPayloadCodec.ReadV2(CanonicalJson.ToUtf8Bytes("{\"name\":\"ready\",\"count\":42,\"mode\":\"ready\",\"infinity\":Infinity}")).IsFailure, Is.True);
            Assert.That(SyntheticPayloadCodec.ReadV2(CanonicalJson.ToUtf8Bytes("{\"name\":\"ready\",\"count\":42,\"mode\":\"ready\",\"infinity\":-Infinity}")).IsFailure, Is.True);
            Assert.That(SyntheticPayloadCodec.ReadV2(CanonicalJson.ToUtf8Bytes("{\"name\":\"ready\",\"count\":-0,\"mode\":\"ready\"}")).IsSuccess, Is.True);

            LogEventV1JsonCodec codec = new LogEventV1JsonCodec();
            string diagnostic = ReadFixtureText("Tests/Fixtures/Serialization/diagnostic-log-event-v1.json");
            Assert.That(codec.Read(CanonicalJson.ToUtf8Bytes(diagnostic.Replace("proc_0123456789abcdef0123456789abcdef", "proc_bad"))).IsFailure, Is.True);
            Assert.That(codec.Read(CanonicalJson.ToUtf8Bytes(diagnostic.Replace("2026-08-12T00:00:00.0000000Z", "2026-08-12T00:00:00"))).IsFailure, Is.True);
            Assert.That(codec.Read(CanonicalJson.ToUtf8Bytes(diagnostic.Replace("\"information\"", "\"info\""))).IsFailure, Is.True);
            Assert.That(codec.Read(CanonicalJson.ToUtf8Bytes(diagnostic.Replace("\"contractType\":\"odyssey.diagnostics.log.event\"", "\"contractType\":\"odyssey.unknown.event\""))).IsFailure, Is.True);
            Assert.That(codec.Read(CanonicalJson.ToUtf8Bytes(diagnostic.Replace("\"contractVersion\":1", "\"contractVersion\":99"))).IsFailure, Is.True);
            Assert.That(codec.Read(CanonicalJson.ToUtf8Bytes("{\"contractType\":\"odyssey.diagnostics.log.event\",\"contractVersion\":1}")).IsFailure, Is.True);
        }

        [Test]
        public void OdcampManifestUsesFrozenHashAndSafeRelativePathsOnly()
        {
            OdcampManifestV1Codec codec = new OdcampManifestV1Codec();
            JsonPayload json = codec.Write(new OdcampManifestV1("smoke", "Serialization Smoke", "assets/smoke")).Value;
            Assert.That(json.Utf8Text, Is.EqualTo(ReadFixtureText("Tests/Fixtures/Serialization/odcamp-manifest-v1.json")));
            Assert.That(CanonicalJson.Sha256LowerHex(json.Bytes), Is.EqualTo(ExpectedManifestHash));
            Assert.That(codec.Read(json.Bytes).Value.RelativeAssetPath, Is.EqualTo("assets/smoke"));

            Assert.That(codec.Read(CanonicalJson.ToUtf8Bytes("{\"contractType\":\"odyssey.odcamp.manifest\",\"contractVersion\":1,\"manifestId\":\"smoke\",\"displayName\":\"Smoke\",\"relativeAssetPath\":\"C:/Users/alexx/file\"}")).IsFailure, Is.True);
            Assert.That(codec.Read(CanonicalJson.ToUtf8Bytes("{\"contractType\":\"odyssey.odcamp.manifest\",\"contractVersion\":1,\"manifestId\":\"smoke\",\"displayName\":\"Smoke\",\"relativeAssetPath\":\"../secret\"}")).IsFailure, Is.True);
            Assert.That(codec.Read(CanonicalJson.ToUtf8Bytes("{\"contractType\":\"odyssey.odcamp.manifest\",\"contractVersion\":1,\"manifestId\":\"smoke\",\"displayName\":\"Smoke\",\"relativeAssetPath\":\"assets/smoke\",\"unexpected\":\"x\"}")).IsFailure, Is.True);
        }

        [Test]
        public void DiagnosticLogEventUsesStructuredJsonAndOmittedOptionals()
        {
            LogEventV1JsonCodec codec = new LogEventV1JsonCodec();
            JsonPayload json = codec.Write(CreateLogEvent()).Value;
            LogEventV1 decoded = codec.Read(json.Bytes).Value;

            Assert.That(json.Utf8Text, Is.EqualTo(ReadFixtureText("Tests/Fixtures/Serialization/diagnostic-log-event-v1.json")));
            Assert.That(CanonicalJson.Sha256LowerHex(json.Bytes), Is.EqualTo(ExpectedDiagnosticHash));
            Assert.That(json.Utf8Text, Does.Contain("\"safeProperties\":[{\"key\":\"probe\""));
            Assert.That(json.Utf8Text, Does.Not.Contain("\"diagnosticId\":null"));
            Assert.That(json.Utf8Text, Does.Not.Contain("\"commandId\":null"));
            Assert.That(json.Utf8Text, Does.Not.Contain("\"sessionReference\":null"));
            Assert.That(decoded.SafeProperties[0].Value.ValueKind, Is.EqualTo(SafeLogValueKind.Code));
            Assert.That(codec.Read(CanonicalJson.ToUtf8Bytes(json.Utf8Text.Replace("\"contractVersion\":1", "\"contractVersion\":2"))).IsFailure, Is.True);
            Assert.That(codec.Read(CanonicalJson.ToUtf8Bytes("{\"contractType\":\"odyssey.diagnostics.log.event\",\"contractType\":\"odyssey.diagnostics.log.event\",\"contractVersion\":1}")).IsFailure, Is.True);
        }

        [Test]
        public void DiagnosticSafePropertiesAndExceptionSummaryRoundTripWithoutDelimiterEscaping()
        {
            ExceptionSummary exception = ExceptionSummary.Rehydrate(ExceptionCategory.IoFailure, SubsystemName.Parse("diagnostics"), 2, true, DiagnosticId.Parse("diag_11111111111111111111111111111111"));
            string json = WriteAppStartupCompletedDiagnostic(exception);
            string incident = WriteIncidentDiagnostic();
            string crash = WriteCrashMarkerDiagnostic();

            Assert.That(json, Does.Contain("\"valueKind\":\"duration\""));
            Assert.That(incident, Does.Contain("\"valueKind\":\"technical_identifier\""));
            Assert.That(crash, Does.Contain("\"valueKind\":\"sanitized_path\""));
            Assert.That(json, Does.Contain("\"exceptionSummary\":{\"category\":\"io_failure\",\"subsystem\":\"diagnostics\",\"innerExceptionCount\":2,\"isTransient\":true,\"diagnosticId\":\"diag_11111111111111111111111111111111\"}"));
            Assert.That(json, Does.Not.Contain("%252C"));
            Assert.That(json, Does.Not.Contain("%257C"));
        }

        [Test]
        public void SerializationSmokeMatchesFrozenGoldenVectors()
        {
            SerializationSmokeResult result = SerializationSmoke.Run().Value;
            Assert.That(result.PayloadJson, Is.EqualTo(ReadFixtureText("Tests/Fixtures/Serialization/synthetic-payload-v2.json")));
            Assert.That(result.PayloadHash, Is.EqualTo(ExpectedPayloadHash));
            Assert.That(result.FingerprintMaterialJson, Is.EqualTo(ReadFixtureText("Tests/Fixtures/Serialization/command-fingerprint-material-v1.json")));
            Assert.That(result.Fingerprint, Is.EqualTo(ExpectedFingerprint));
            Assert.That(result.DiagnosticJson, Is.EqualTo(ReadFixtureText("Tests/Fixtures/Serialization/diagnostic-log-event-v1.json")));
            Assert.That(result.DiagnosticHash, Is.EqualTo(ExpectedDiagnosticHash));
            Assert.That(result.ManifestJson, Is.EqualTo(ReadFixtureText("Tests/Fixtures/Serialization/odcamp-manifest-v1.json")));
            Assert.That(result.ManifestHash, Is.EqualTo(ExpectedManifestHash));
            Assert.That(ReadFixtureText("Tests/Fixtures/Serialization/golden-vectors.json"), Does.Contain(ExpectedFingerprint));
        }

        private static string WriteAppStartupCompletedDiagnostic(ExceptionSummary exception)
        {
            LogEventV1 logEvent = new LogEventV1(
                UtcInstant.Parse("2026-08-12T00:00:00.0000000Z"),
                LogLevel.Information,
                OdysseyEventCodes.AppStartupCompleted,
                SubsystemName.Parse("app"),
                BuildIdAvailability.UnavailableNotYetComposed,
                ProcessInstanceId.Parse("proc_0123456789abcdef0123456789abcdef"),
                MessageTemplateKey.Parse("log.app.startup.completed"),
                new[] { new SafeLogProperty(SafePropertyKey.Parse("state"), SafeLogValue.Code("ready")), new SafeLogProperty(SafePropertyKey.Parse("duration_ms"), SafeLogValue.Duration(TimeSpan.FromMilliseconds(12.5))) },
                exceptionSummary: exception);
            LogEventV1JsonCodec codec = new LogEventV1JsonCodec();
            JsonPayload json = codec.Write(logEvent).Value;
            LogEventV1 decoded = codec.Read(json.Bytes).Value;
            Assert.That(decoded.SafeProperties.Select(p => p.Value.ValueKind), Is.EqualTo(new[] { SafeLogValueKind.Code, SafeLogValueKind.Duration }));
            Assert.That(decoded.ExceptionSummary!.Value.Category, Is.EqualTo(ExceptionCategory.IoFailure));
            Assert.That(decoded.ExceptionSummary!.Value.InnerExceptionCount, Is.EqualTo(2));
            return json.Utf8Text;
        }

        private static string WriteIncidentDiagnostic()
        {
            LogEventV1 logEvent = new LogEventV1(
                UtcInstant.Parse("2026-08-12T00:00:00.0000000Z"),
                LogLevel.Error,
                OdysseyEventCodes.DiagnosticsIncidentUnexpected,
                SubsystemName.Parse("diagnostics"),
                BuildIdAvailability.UnavailableNotYetComposed,
                ProcessInstanceId.Parse("proc_0123456789abcdef0123456789abcdef"),
                MessageTemplateKey.Parse("log.diagnostics.incident.unexpected"),
                new[]
                {
                    new SafeLogProperty(SafePropertyKey.Parse("diagnostic_id"), SafeLogValue.TechnicalIdentifier("diag_0123456789abcdef0123456789abcdef")),
                    new SafeLogProperty(SafePropertyKey.Parse("incident_category"), SafeLogValue.Code("io_failure")),
                    new SafeLogProperty(SafePropertyKey.Parse("repeat_count"), SafeLogValue.Count(1))
                });
            LogEventV1JsonCodec codec = new LogEventV1JsonCodec();
            JsonPayload json = codec.Write(logEvent).Value;
            LogEventV1 decoded = codec.Read(json.Bytes).Value;
            Assert.That(decoded.SafeProperties[0].Value.ValueKind, Is.EqualTo(SafeLogValueKind.TechnicalIdentifier));
            Assert.That(decoded.SafeProperties[2].Value.ValueKind, Is.EqualTo(SafeLogValueKind.Integer));
            return json.Utf8Text;
        }

        private static string WriteCrashMarkerDiagnostic()
        {
            LogEventV1 logEvent = new LogEventV1(
                UtcInstant.Parse("2026-08-12T00:00:00.0000000Z"),
                LogLevel.Warning,
                OdysseyEventCodes.DiagnosticsCrashPreviousUncleanDetected,
                SubsystemName.Parse("diagnostics"),
                BuildIdAvailability.UnavailableNotYetComposed,
                ProcessInstanceId.Parse("proc_0123456789abcdef0123456789abcdef"),
                MessageTemplateKey.Parse("log.diagnostics.crash.previous_unclean_detected"),
                new[] { new SafeLogProperty(SafePropertyKey.Parse("marker"), SafeLogValue.SanitizedPath("C:/Users/alexx/secret/process-started.json")) });
            LogEventV1JsonCodec codec = new LogEventV1JsonCodec();
            JsonPayload json = codec.Write(logEvent).Value;
            LogEventV1 decoded = codec.Read(json.Bytes).Value;
            Assert.That(decoded.SafeProperties[0].Value.ValueKind, Is.EqualTo(SafeLogValueKind.SanitizedPath));
            return json.Utf8Text;
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

        private static byte[] NestedObject(int depth)
        {
            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < depth; index++) builder.Append("{\"a\":");
            builder.Append("0");
            for (int index = 0; index < depth; index++) builder.Append("}");
            return CanonicalJson.ToUtf8Bytes(builder.ToString());
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
