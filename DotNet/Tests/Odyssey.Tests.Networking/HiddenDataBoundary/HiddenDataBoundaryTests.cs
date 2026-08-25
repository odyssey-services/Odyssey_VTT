using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Odyssey.Application.Diagnostics;
using Odyssey.Application.Identity;
using Odyssey.Application.Networking;
using Odyssey.Application.Results;
using Odyssey.Application.Serialization;
using Odyssey.Application.Time;
using Odyssey.Application.Versions;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Networking.InProcess;
using Odyssey.Tests.Networking;
using Odyssey.Tests.Networking.HiddenDataBoundary.Harness;

namespace Odyssey.Tests.Networking.HiddenDataBoundary
{
    /// <summary>
    /// ODY-S02-007 (SP-04): real, functional proof of the ADR-017/ADR-019 hidden
    /// data boundary contract (roadmap 17_Roadmap section 11.5), delivered over
    /// the real, already-accepted InProcessSessionTransport (ADR-015) -- not
    /// reasoning on paper. See the harness README for why this lives here
    /// (permanent CI-wired regression coverage for a security-relevant contract)
    /// rather than a throwaway Tools/Spikes/ harness like SP-02/SP-03.
    /// </summary>
    public sealed class HiddenDataBoundaryTests
    {
        private static readonly IWallClock Clock = new SystemWallClock();
        private const string VisibleEntityId = "obj_visible_torch_001";
        private const string HiddenEntityId = "obj_hidden_trapdoor_002";
        private const string HiddenEntityDisplayName = "trapdoor lever GM secret";
        private const string InteractCapability = "Scene.Interact.TrapdoorLever";

        private static (HostWorldState World, ActorPermissionState PlayerPermissions, ActorPermissionState MainGmPermissions) BuildWorld()
        {
            var world = new HostWorldState();
            world.AddEntity(new GameEntity(VisibleEntityId, "torch", DataClassification.Public));
            world.AddEntity(new GameEntity(HiddenEntityId, HiddenEntityDisplayName, DataClassification.HiddenGameplay));

            var player = new ActorPermissionState(BaselineRole.Player);
            var mainGm = new ActorPermissionState(BaselineRole.MainGM);
            return (world, player, mainGm);
        }

        private static async Task<(ISessionTransport Host, ISessionTransport Client, ConnectionHandle HostHandle, ConnectionHandle ClientHandle)> ConnectPairAsync()
        {
            var range = new ProtocolVersionRange(ProtocolVersion.Create(1), ProtocolVersion.Create(1), ProtocolVersion.Create(1));
            (ISessionTransport host, ISessionTransport client) = InProcessSessionTransport.CreatePair(range, range, Clock);
            Result<ConnectionHandle> clientConnected = await client.ConnectAsync(new SessionEndpoint("host-1"), range, CancellationToken.None);
            Result<ConnectionHandle> hostConnected = await host.ConnectAsync(new SessionEndpoint("client-1"), range, CancellationToken.None);
            Assert.That(clientConnected.IsSuccess, Is.True);
            Assert.That(hostConnected.IsSuccess, Is.True);
            return (host, client, hostConnected.Value, clientConnected.Value);
        }

        private static NetworkEnvelope MakeEnvelope(ConnectionHandle handle, string payloadType, byte[] payload) =>
            new(
                MessageId.NewId(Clock.GetUtcNow()),
                handle.SessionId,
                senderUserId: null,
                senderClientInstanceId: null,
                NetworkMessageKind.ApplicationPayload,
                handle.NegotiatedProtocolVersion,
                correlationId: null,
                causationId: null,
                sentAtHostTime: Clock.GetUtcNow(),
                payloadType,
                payloadVersion: 1,
                payload: payload);

        [Test]
        public async Task Snapshot_ForPlayerWithoutGrant_ExcludesHiddenEntity_BothInWireBytesAndDecodedPayload()
        {
            (HostWorldState world, ActorPermissionState player, _) = BuildWorld();
            ProjectionSnapshot snapshot = ProjectionBuilder.BuildSnapshot(world, "user_player", player, sequence: 1);

            byte[] wireBytes = WireCodec.EncodeSnapshot(snapshot);

            Assert.That(WireCodec.WireBytesContain(wireBytes, HiddenEntityId), Is.False, "the hidden entity's id must never appear in the wire bytes sent to Player");
            Assert.That(WireCodec.WireBytesContain(wireBytes, HiddenEntityDisplayName), Is.False, "the hidden entity's content must never appear in the wire bytes sent to Player");

            ProjectionSnapshot decoded = WireCodec.DecodeSnapshot(wireBytes);
            Assert.That(decoded.Entities.Select(e => e.Id), Does.Not.Contain(HiddenEntityId));
            Assert.That(decoded.Entities.Select(e => e.Id), Does.Contain(VisibleEntityId));
        }

        [Test]
        public void Snapshot_ForMainGM_IncludesHiddenEntity_ControlCase()
        {
            (HostWorldState world, _, ActorPermissionState mainGm) = BuildWorld();
            ProjectionSnapshot snapshot = ProjectionBuilder.BuildSnapshot(world, "user_host", mainGm, sequence: 1);

            Assert.That(snapshot.Entities.Select(e => e.Id), Does.Contain(HiddenEntityId), "MainGM must see the hidden entity by default (ADR-019 section 5.1) -- proves the harness isn't just omitting everything");
        }

        [Test]
        public void UnrelatedChangeDelta_ForPlayerWithoutGrant_NeverMentionsHiddenEntity()
        {
            (HostWorldState world, ActorPermissionState player, _) = BuildWorld();
            GameEntity changedVisibleEntity = world.Entities[VisibleEntityId];
            ProjectionDeltaBatch delta = ProjectionBuilder.BuildUnrelatedChangeDelta(world, "user_player", player, changedVisibleEntity, sequenceFrom: 2, sequenceTo: 2);

            byte[] wireBytes = WireCodec.EncodeDelta(delta);
            Assert.That(WireCodec.WireBytesContain(wireBytes, HiddenEntityId), Is.False);
            Assert.That(delta.Operations.Any(op => op.TargetId == HiddenEntityId), Is.False);
        }

        [Test]
        public async Task ClientRuntimeStateAndLocalCache_ForPlayerWithoutGrant_NeverContainHiddenEntity_AfterRealTransportDelivery()
        {
            (HostWorldState world, ActorPermissionState player, _) = BuildWorld();
            ProjectionSnapshot snapshot = ProjectionBuilder.BuildSnapshot(world, "user_player", player, sequence: 1);

            (ISessionTransport host, ISessionTransport client, ConnectionHandle hostHandle, ConnectionHandle clientHandle) = await ConnectPairAsync();
            byte[] wireBytes = WireCodec.EncodeSnapshot(snapshot);
            Result sendResult = await host.SendReliableAsync(hostHandle, MakeEnvelope(hostHandle, "hidden-data-boundary.snapshot", wireBytes), CancellationToken.None);
            Assert.That(sendResult.IsSuccess, Is.True);

            Result<System.Collections.Generic.IReadOnlyList<NetworkEnvelope>> drained = client.DrainReliable(clientHandle);
            Assert.That(drained.IsSuccess, Is.True);
            Assert.That(drained.Value.Count, Is.EqualTo(1));

            var clientState = new HiddenDataBoundaryClient();
            clientState.ApplySnapshot(WireCodec.DecodeSnapshot(drained.Value[0].Payload));

            Assert.That(clientState.Runtime.Entities.ContainsKey(HiddenEntityId), Is.False, "runtime-state surface");
            Assert.That(clientState.Cache.CachedEntities.ContainsKey(HiddenEntityId), Is.False, "local-cache surface");
            Assert.That(clientState.Runtime.Entities.ContainsKey(VisibleEntityId), Is.True);
        }

        [Test]
        public async Task DiagnosticExport_FromPlayerRuntimeState_NeverContainsHiddenEntity_AndPlannerRejectsAForcedLeak()
        {
            (HostWorldState world, ActorPermissionState player, _) = BuildWorld();
            ProjectionSnapshot snapshot = ProjectionBuilder.BuildSnapshot(world, "user_player", player, sequence: 1);
            var clientState = new HiddenDataBoundaryClient();
            clientState.ApplySnapshot(snapshot);

            // Real diagnostic export path (ADR-010/ADR-010's DiagnosticBundlePlanner),
            // built only from what the client actually knows -- never from host state.
            var knownIds = clientState.KnownEntityIdsForDiagnostics();
            Assert.That(knownIds, Does.Not.Contain(HiddenEntityId));

            // OdysseyEventCodes.DiagnosticsProbeEmitted's registry entry
            // (DiagnosticsContracts.cs) fixes subsystem="diagnostics" and exactly
            // one "probe" property -- reused verbatim, not a custom schema.
            LogEventV1 logEvent = new LogEventV1(
                UtcInstant.Parse("2026-08-25T00:00:00.0000000Z"),
                LogLevel.Information,
                OdysseyEventCodes.DiagnosticsProbeEmitted,
                SubsystemName.Parse("diagnostics"),
                BuildIdAvailability.UnavailableNotYetComposed,
                ProcessInstanceId.Parse("proc_0123456789abcdef0123456789abcdef"),
                MessageTemplateKey.Parse("log.diagnostics.probe.emitted"),
                new[] { new SafeLogProperty(SafePropertyKey.Parse("probe"), SafeLogValue.Code("known_entity_count_" + knownIds.Count)) },
                CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef"));
            byte[] logBytes = new LogEventV1JsonCodec().Write(logEvent).Value.Bytes;

            DiagnosticId diagnosticId = DiagnosticId.Parse("diag_00000000000000000000000000000002");
            DiagnosticBundleManifest manifest = DiagnosticBundlePlanner.CreateManifest(diagnosticId, "odyssey-0.0.0-test", new[]
            {
                (DiagnosticBundleCategory.DiagnosticLogs, "logs/hidden-data-boundary.jsonl", logBytes)
            });
            Assert.That(manifest.Entries.Single().Status, Is.EqualTo(DiagnosticBundleEntryStatus.Included), "the real client-only-derived log line is legitimately export-safe");

            // Defense-in-depth: if the hidden entity's own flagged content had
            // somehow leaked into a log line, the existing safety scan rejects it
            // outright (Odyssey.Application/Diagnostics/DiagnosticBundleContracts.cs
            // PassesFinalExportSafetyScan already denylists "hidden", "secret",
            // "private", etc. as raw substrings).
            LogEventV1 leakedEvent = new LogEventV1(
                UtcInstant.Parse("2026-08-25T00:00:00.0000000Z"),
                LogLevel.Information,
                OdysseyEventCodes.DiagnosticsProbeEmitted,
                SubsystemName.Parse("diagnostics"),
                BuildIdAvailability.UnavailableNotYetComposed,
                ProcessInstanceId.Parse("proc_0123456789abcdef0123456789abcdef"),
                MessageTemplateKey.Parse("log.diagnostics.probe.emitted"),
                new[] { new SafeLogProperty(SafePropertyKey.Parse("probe"), SafeLogValue.Code(HiddenEntityId)) },
                CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef"));
            byte[] leakedBytes = new LogEventV1JsonCodec().Write(leakedEvent).Value.Bytes;
            Action leakAttempt = () => DiagnosticBundlePlanner.CreateManifest(diagnosticId, "odyssey-0.0.0-test", new[]
            {
                (DiagnosticBundleCategory.DiagnosticLogs, "logs/forced-leak.jsonl", leakedBytes)
            });
            Assert.Throws<ArgumentException>(leakAttempt, "a log line naming the hidden entity ('hidden' substring) must be rejected by the existing export safety scan, not silently bundled");
        }

        [Test]
        public async Task GrantingVisibility_DeliversAddEntityDelta_ClientRuntimeAndCacheNowContainHiddenEntity()
        {
            (HostWorldState world, ActorPermissionState player, _) = BuildWorld();
            var initialSnapshot = ProjectionBuilder.BuildSnapshot(world, "user_player", player, sequence: 1);
            var clientState = new HiddenDataBoundaryClient();
            clientState.ApplySnapshot(initialSnapshot);
            var previouslyVisible = new System.Collections.Generic.HashSet<string>(initialSnapshot.Entities.Select(e => e.Id));
            var previousCapabilities = player.SnapshotCapabilities();

            player.GrantVisibility(HiddenEntityId);
            ProjectionDeltaBatch grantDelta = ProjectionBuilder.BuildPermissionChangeDelta(world, "user_player", previouslyVisible, player, previousCapabilities, sequenceFrom: 2, sequenceTo: 2);

            (ISessionTransport host, ISessionTransport client, ConnectionHandle hostHandle, ConnectionHandle clientHandle) = await ConnectPairAsync();
            byte[] wireBytes = WireCodec.EncodeDelta(grantDelta);
            await host.SendReliableAsync(hostHandle, MakeEnvelope(hostHandle, "hidden-data-boundary.delta", wireBytes), CancellationToken.None);
            Result<System.Collections.Generic.IReadOnlyList<NetworkEnvelope>> drained = client.DrainReliable(clientHandle);
            Assert.That(drained.Value.Count, Is.EqualTo(1));

            clientState.ApplyDelta(WireCodec.DecodeDelta(drained.Value[0].Payload));

            Assert.That(clientState.Runtime.Entities.ContainsKey(HiddenEntityId), Is.True, "granting visibility must deliver the entity via AddEntity");
            Assert.That(clientState.Cache.CachedEntities.ContainsKey(HiddenEntityId), Is.True);
        }

        [Test]
        public async Task RevokingVisibility_DeliversRemoveFromProjectionDelta_ClientRuntimeAndCacheNoLongerContainHiddenEntity()
        {
            (HostWorldState world, ActorPermissionState player, _) = BuildWorld();
            player.GrantVisibility(HiddenEntityId);
            var grantedSnapshot = ProjectionBuilder.BuildSnapshot(world, "user_player", player, sequence: 1);
            var clientState = new HiddenDataBoundaryClient();
            clientState.ApplySnapshot(grantedSnapshot);
            Assert.That(clientState.Runtime.Entities.ContainsKey(HiddenEntityId), Is.True, "precondition: client currently has the entity");

            var previouslyVisible = new System.Collections.Generic.HashSet<string>(grantedSnapshot.Entities.Select(e => e.Id));
            var previousCapabilities = player.SnapshotCapabilities();
            player.RevokeVisibility(HiddenEntityId);
            ProjectionDeltaBatch revokeDelta = ProjectionBuilder.BuildPermissionChangeDelta(world, "user_player", previouslyVisible, player, previousCapabilities, sequenceFrom: 2, sequenceTo: 2);
            Assert.That(revokeDelta.Operations.Single(op => op.TargetId == HiddenEntityId).Kind, Is.EqualTo(ProjectionOperationKind.RemoveFromProjection));

            (ISessionTransport host, ISessionTransport client, ConnectionHandle hostHandle, ConnectionHandle clientHandle) = await ConnectPairAsync();
            byte[] wireBytes = WireCodec.EncodeDelta(revokeDelta);
            await host.SendReliableAsync(hostHandle, MakeEnvelope(hostHandle, "hidden-data-boundary.delta", wireBytes), CancellationToken.None);
            Result<System.Collections.Generic.IReadOnlyList<NetworkEnvelope>> drained = client.DrainReliable(clientHandle);

            clientState.ApplyDelta(WireCodec.DecodeDelta(drained.Value[0].Payload));

            Assert.That(clientState.Runtime.Entities.ContainsKey(HiddenEntityId), Is.False, "runtime-state must drop the entity on revocation");
            Assert.That(clientState.Cache.CachedEntities.ContainsKey(HiddenEntityId), Is.False, "local-cache must also drop the entity, not remain stale");
        }

        [Test]
        public void RevokingCapability_ProducesRemoveCapabilityOperation_ClientLosesAllowedCommand()
        {
            (HostWorldState world, ActorPermissionState player, _) = BuildWorld();
            player.GrantVisibility(HiddenEntityId);
            player.GrantCapability(InteractCapability);
            var grantedSnapshot = ProjectionBuilder.BuildSnapshot(world, "user_player", player, sequence: 1);
            var clientState = new HiddenDataBoundaryClient();
            clientState.ApplySnapshot(grantedSnapshot);
            Assert.That(clientState.Runtime.AllowedCommands.Contains(InteractCapability), Is.True);

            var previouslyVisible = new System.Collections.Generic.HashSet<string>(grantedSnapshot.Entities.Select(e => e.Id));
            var previousCapabilities = player.SnapshotCapabilities();
            player.RevokeCapability(InteractCapability);
            ProjectionDeltaBatch delta = ProjectionBuilder.BuildPermissionChangeDelta(world, "user_player", previouslyVisible, player, previousCapabilities, sequenceFrom: 2, sequenceTo: 2);

            Assert.That(delta.Operations.Any(op => op.Kind == ProjectionOperationKind.RemoveCapability && op.TargetId == InteractCapability), Is.True);

            clientState.ApplyDelta(delta);
            Assert.That(clientState.Runtime.AllowedCommands.Contains(InteractCapability), Is.False);
        }
    }
}
