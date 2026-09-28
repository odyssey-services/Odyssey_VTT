using System;
using System.IO;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Effects;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Character;
using Odyssey.Domain.Content;
using Odyssey.Domain.Effects;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence
{
    /// <summary>
    /// ODY-S05-506: real tests for `ADR-028` §9/§10 rules 2-3's own two
    /// MainGM-only permission gates over `ActiveEffect` -- the explicit
    /// `RemoveActiveEffect` command (`Status → Removed`, CAS-guarded, routed
    /// through `SqliteSavingPipeline`) and direct (non-item) creation
    /// (`ActiveEffectDirectCommandService`, sourced `ForGMDirect()`, built on
    /// the unmodified `CreateActiveEffect`). Neither `ODY-S05-505`'s own
    /// deliberately-ungated `ItemEffectLifecycleService` nor `ODY-S05-503`'s
    /// own permission-free `ActiveEffectStackingRules` is touched or
    /// exercised here.
    /// </summary>
    public sealed class ActiveEffectGmCommandTests
    {
        private static readonly CorrelationId Corr = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));
        private static readonly UserId MainGm = UserId.Parse("user_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        private static readonly UserId Player = UserId.Parse("user_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

        private string _campaignDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaigns = null!;
        private SqliteActiveEffectRepository _effects = null!;

        [SetUp]
        public void SetUp()
        {
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s05-506-" + Guid.NewGuid().ToString("N"));
            _campaigns = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> created = _campaigns.Create(new CreateCampaignRequest(_campaignDir, "GM Command Test Campaign", "ruleset.core", "1.0.0", "0.1.0", global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost()), NewCommandId(), Corr);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
            _effects = new SqliteActiveEffectRepository(Clock, _campaigns);
        }

        [TearDown]
        public void TearDown()
        {
            try { _campaigns.Close(_campaign, Corr); } catch (IOException) { }
            try { if (Directory.Exists(_campaignDir)) Directory.Delete(_campaignDir, true); } catch (IOException) { }
        }

        // ---- helpers ----

        private ContentDefinitionRef NewEffectRef() => new ContentDefinitionRef(ContentDefinitionId.NewId(Clock.GetUtcNow()), 1);

        private ActiveEffectRecord PersistEffect(ActiveEffectStatus status = ActiveEffectStatus.Active)
        {
            ContentDefinitionRef effectRef = NewEffectRef();
            var snapshot = new EffectMechanicsSnapshot(effectRef, 1, ContentDefinitionType.Effect, "{\"duration\":\"permanent\"}");
            var target = ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow()));
            var effect = new ActiveEffect(ActiveEffectId.NewId(Clock.GetUtcNow()), effectRef, snapshot, ActiveEffectSourceRef.ForGMDirect(), target, status, 1, MainGm, Clock.GetUtcNow(), null, 1);
            var record = new ActiveEffectRecord(_campaign.CampaignId, effect);
            Result<ActiveEffectRecord> created = _effects.CreateActiveEffect(_campaign, record, NewCommandId(), Corr);
            Assert.That(created.IsSuccess, Is.True, created.IsFailure ? created.Error.Code.ToString() : string.Empty);
            return created.Value;
        }

        // ---- RemoveActiveEffect ----

        [Test] // TC-ACTIVEEFFECT-069
        public void RemoveActiveEffect_ByMainGm_TransitionsToRemoved_NoPhysicalDelete()
        {
            ActiveEffectRecord record = PersistEffect();

            Result<ActiveEffectRecord> removed = _effects.RemoveActiveEffect(_campaign, _campaign.CampaignId, record.Effect.ActiveEffectId, global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost(), record.Effect.Revision, NewCommandId(), Corr);

            Assert.That(removed.IsSuccess, Is.True, removed.IsFailure ? removed.Error.Code.ToString() : string.Empty);
            Assert.That(removed.Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Removed));
            Assert.That(removed.Value.Effect.Revision, Is.EqualTo(record.Effect.Revision + 1));

            Result<ActiveEffectRecord> fetched = _effects.GetActiveEffect(_campaign, record.Effect.ActiveEffectId, Corr);
            Assert.That(fetched.IsSuccess, Is.True, "the row survives as Status=Removed history, never a physical delete");
            Assert.That(fetched.Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Removed));
        }

        [Test] // TC-ACTIVEEFFECT-070
        public void RemoveActiveEffect_ByNonMainGm_IsRejected_NoMutation()
        {
            ActiveEffectRecord record = PersistEffect();

            Result<ActiveEffectRecord> removed = _effects.RemoveActiveEffect(_campaign, _campaign.CampaignId, record.Effect.ActiveEffectId, Player, record.Effect.Revision, NewCommandId(), Corr);

            Assert.That(removed.IsFailure, Is.True);
            Assert.That(removed.Error.Code, Is.EqualTo(ErrorCodes.PersistenceActiveEffectOperationDenied));

            Result<ActiveEffectRecord> fetched = _effects.GetActiveEffect(_campaign, record.Effect.ActiveEffectId, Corr);
            Assert.That(fetched.IsSuccess, Is.True);
            Assert.That(fetched.Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Active), "a denied removal must not mutate the row at all");
            Assert.That(fetched.Value.Effect.Revision, Is.EqualTo(record.Effect.Revision));
        }

        [Test] // TC-ACTIVEEFFECT-071
        public void RemoveActiveEffect_WithAStaleExpectedRevision_IsRejected_NoMutation()
        {
            ActiveEffectRecord record = PersistEffect();
            long staleRevision = record.Effect.Revision + 5;

            Result<ActiveEffectRecord> removed = _effects.RemoveActiveEffect(_campaign, _campaign.CampaignId, record.Effect.ActiveEffectId, global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost(), staleRevision, NewCommandId(), Corr);

            Assert.That(removed.IsFailure, Is.True);
            Assert.That(removed.Error.Code, Is.EqualTo(ErrorCodes.PersistenceActiveEffectRevisionConflict));

            Result<ActiveEffectRecord> fetched = _effects.GetActiveEffect(_campaign, record.Effect.ActiveEffectId, Corr);
            Assert.That(fetched.Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Active));
            Assert.That(fetched.Value.Effect.Revision, Is.EqualTo(record.Effect.Revision));
        }

        [Test] // TC-ACTIVEEFFECT-072
        public void RemoveActiveEffect_ReplayWithSameCommandId_ReturnsTheSameResultWithoutDoubleTransitioning()
        {
            ActiveEffectRecord record = PersistEffect();
            CommandId commandId = NewCommandId();

            Result<ActiveEffectRecord> first = _effects.RemoveActiveEffect(_campaign, _campaign.CampaignId, record.Effect.ActiveEffectId, global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost(), record.Effect.Revision, commandId, Corr);
            Result<ActiveEffectRecord> replay = _effects.RemoveActiveEffect(_campaign, _campaign.CampaignId, record.Effect.ActiveEffectId, global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost(), record.Effect.Revision, commandId, Corr);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(replay.IsSuccess, Is.True);
            Assert.That(replay.Value.Effect.Revision, Is.EqualTo(first.Value.Effect.Revision), "a replay must not increment Revision a second time");
        }

        [TestCase(ActiveEffectStatus.Expired)] // TC-ACTIVEEFFECT-073
        [TestCase(ActiveEffectStatus.Removed)]
        public void RemoveActiveEffect_OnATerminalStatus_IsRejected(ActiveEffectStatus terminalStatus)
        {
            ActiveEffectRecord record = PersistEffect(terminalStatus);

            Result<ActiveEffectRecord> removed = _effects.RemoveActiveEffect(_campaign, _campaign.CampaignId, record.Effect.ActiveEffectId, global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost(), record.Effect.Revision, NewCommandId(), Corr);

            Assert.That(removed.IsFailure, Is.True);
            Assert.That(removed.Error.Code, Is.EqualTo(ErrorCodes.PersistenceActiveEffectRevisionConflict));

            Result<ActiveEffectRecord> fetched = _effects.GetActiveEffect(_campaign, record.Effect.ActiveEffectId, Corr);
            Assert.That(fetched.Value.Effect.Status, Is.EqualTo(terminalStatus), "an already-terminal effect is left exactly as it was");
        }

        [Test] // TC-ACTIVEEFFECT-074
        public void RemoveActiveEffect_OnASuspendedEffect_Succeeds()
        {
            ActiveEffectRecord record = PersistEffect(ActiveEffectStatus.Suspended);

            Result<ActiveEffectRecord> removed = _effects.RemoveActiveEffect(_campaign, _campaign.CampaignId, record.Effect.ActiveEffectId, global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost(), record.Effect.Revision, NewCommandId(), Corr);

            Assert.That(removed.IsSuccess, Is.True);
            Assert.That(removed.Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Removed), "removal can end a Suspended WhileItemEquipped effect early too, not only an Active one");
        }

        [Test] // TC-ACTIVEEFFECT-075
        public void RemoveActiveEffect_WithMismatchedCampaignId_IsRejected()
        {
            ActiveEffectRecord record = PersistEffect();

            Result<ActiveEffectRecord> removed = _effects.RemoveActiveEffect(_campaign, CampaignId.NewId(Clock.GetUtcNow()), record.Effect.ActiveEffectId, global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost(), record.Effect.Revision, NewCommandId(), Corr);

            Assert.That(removed.IsFailure, Is.True);
            Assert.That(removed.Error.Code, Is.EqualTo(ErrorCodes.PersistenceActiveEffectCampaignMismatch));
        }

        // ---- ActiveEffectDirectCommandService.CreateDirectActiveEffect ----

        [Test] // TC-ACTIVEEFFECT-076
        public void CreateDirectActiveEffect_ByMainGm_Succeeds_SourcedGMDirect()
        {
            ContentDefinitionRef effectRef = NewEffectRef();
            var snapshot = new EffectMechanicsSnapshot(effectRef, 1, ContentDefinitionType.Effect, "{\"duration\":\"permanent\"}");
            var target = ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow()));

            Result<ActiveEffectRecord> result = ActiveEffectDirectCommandService.CreateDirectActiveEffect(
                _effects, _campaigns, _campaign, _campaign.CampaignId, effectRef, snapshot, target, global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost(), Clock.GetUtcNow(), null, NewCommandId(), Corr);

            Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error.Code.ToString() : string.Empty);
            Assert.That(result.Value.Effect.SourceRef.Kind, Is.EqualTo(ActiveEffectSourceKind.GMDirect));
            Assert.That(result.Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Active));

            Result<ActiveEffectRecord> fetched = _effects.GetActiveEffect(_campaign, result.Value.Effect.ActiveEffectId, Corr);
            Assert.That(fetched.IsSuccess, Is.True);
            Assert.That(fetched.Value.Effect.SourceRef.Kind, Is.EqualTo(ActiveEffectSourceKind.GMDirect));
        }

        [Test] // TC-ACTIVEEFFECT-077
        public void CreateDirectActiveEffect_ByNonMainGm_IsRejected_NoRowCreated()
        {
            ContentDefinitionRef effectRef = NewEffectRef();
            var snapshot = new EffectMechanicsSnapshot(effectRef, 1, ContentDefinitionType.Effect, "{\"duration\":\"permanent\"}");
            var target = ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow()));

            Result<ActiveEffectRecord> result = ActiveEffectDirectCommandService.CreateDirectActiveEffect(
                _effects, _campaigns, _campaign, _campaign.CampaignId, effectRef, snapshot, target, Player, Clock.GetUtcNow(), null, NewCommandId(), Corr);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceActiveEffectOperationDenied));

            Result<System.Collections.Generic.IReadOnlyList<ActiveEffectRecord>> listed = _effects.ListActiveEffectsByTarget(_campaign, _campaign.CampaignId, target, Corr);
            Assert.That(listed.IsSuccess, Is.True);
            Assert.That(listed.Value.Count, Is.EqualTo(0), "a denied direct creation must create no row at all");
        }

        [Test] // TC-ACTIVEEFFECT-078
        public void CreateDirectActiveEffect_ReplayWithSameCommandId_ReturnsTheSameRecordWithoutDuplicating()
        {
            ContentDefinitionRef effectRef = NewEffectRef();
            var snapshot = new EffectMechanicsSnapshot(effectRef, 1, ContentDefinitionType.Effect, "{\"duration\":\"permanent\"}");
            var target = ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow()));
            CommandId commandId = NewCommandId();
            UtcInstant appliedAt = Clock.GetUtcNow();

            Result<ActiveEffectRecord> first = ActiveEffectDirectCommandService.CreateDirectActiveEffect(
                _effects, _campaigns, _campaign, _campaign.CampaignId, effectRef, snapshot, target, global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost(), appliedAt, null, commandId, Corr);
            Result<ActiveEffectRecord> replay = ActiveEffectDirectCommandService.CreateDirectActiveEffect(
                _effects, _campaigns, _campaign, _campaign.CampaignId, effectRef, snapshot, target, global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost(), appliedAt, null, commandId, Corr);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(replay.IsSuccess, Is.True);
            Assert.That(replay.Value.Effect.ActiveEffectId, Is.EqualTo(first.Value.Effect.ActiveEffectId));

            Result<System.Collections.Generic.IReadOnlyList<ActiveEffectRecord>> listed = _effects.ListActiveEffectsByTarget(_campaign, _campaign.CampaignId, target, Corr);
            Assert.That(listed.Value.Count, Is.EqualTo(1), "a replay must not create a second row");
        }

        // ---- ODY-S10-103: MainGM is the stored membership -------------------------------------------

        private UserId AddMember(CampaignMembershipRole role)
        {
            UserId user = NewUserId();
            Assert.That(_campaigns.AddMember(_campaign, user, role, NewCommandId(), Corr).IsSuccess, Is.True);
            return user;
        }

        [Test] // TC-PERSIST-052
        public void CreateDirectActiveEffect_AndRemoveActiveEffect_AreMainGmOnly_ByTheStoredMembership()
        {
            UserId host = global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost();
            UserId stranger = NewUserId();
            UserId player = AddMember(CampaignMembershipRole.Player);
            UserId observer = AddMember(CampaignMembershipRole.Observer);
            UserId secondGm = AddMember(CampaignMembershipRole.MainGm);
            ContentDefinitionRef effectRef = NewEffectRef();
            var snapshot = new EffectMechanicsSnapshot(effectRef, 1, ContentDefinitionType.Effect, "{\"duration\":\"permanent\"}");
            var target = ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow()));

            foreach (UserId actor in new[] { stranger, player, observer })
            {
                Result<ActiveEffectRecord> created = ActiveEffectDirectCommandService.CreateDirectActiveEffect(_effects, _campaigns, _campaign, _campaign.CampaignId, effectRef, snapshot, target, actor, Clock.GetUtcNow(), null, NewCommandId(), Corr);
                Assert.That(created.IsFailure, Is.True, "CreateDirectActiveEffect must deny a user who is not a stored MainGm");
                Assert.That(created.Error.Code, Is.EqualTo(ErrorCodes.PersistenceActiveEffectOperationDenied));
            }

            foreach (UserId actor in new[] { host, secondGm })
            {
                Assert.That(ActiveEffectDirectCommandService.CreateDirectActiveEffect(_effects, _campaigns, _campaign, _campaign.CampaignId, effectRef, snapshot, target, actor, Clock.GetUtcNow(), null, NewCommandId(), Corr).IsSuccess, Is.True);
            }

            ActiveEffectRecord record = PersistEffect();
            foreach (UserId actor in new[] { stranger, player, observer })
            {
                Result<ActiveEffectRecord> removed = _effects.RemoveActiveEffect(_campaign, _campaign.CampaignId, record.Effect.ActiveEffectId, actor, record.Effect.Revision, NewCommandId(), Corr);
                Assert.That(removed.IsFailure, Is.True, "RemoveActiveEffect must deny a user who is not a stored MainGm");
                Assert.That(removed.Error.Code, Is.EqualTo(ErrorCodes.PersistenceActiveEffectOperationDenied));
            }

            Assert.That(_effects.GetActiveEffect(_campaign, record.Effect.ActiveEffectId, Corr).Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Active), "nothing was removed");
            Assert.That(_effects.RemoveActiveEffect(_campaign, _campaign.CampaignId, record.Effect.ActiveEffectId, secondGm, record.Effect.Revision, NewCommandId(), Corr).IsSuccess, Is.True);
        }

        [Test] // TC-PERSIST-056
        public void CreateDirectActiveEffect_AndRemoveActiveEffect_FailClosed_WhenTheMembershipLookupFails()
        {
            ActiveEffectRecord record = PersistEffect();
            var poisoned = PoisonedMembershipCampaignRepository.FailsOnLookup();
            var effects = new SqliteActiveEffectRepository(Clock, poisoned);
            UserId host = global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost();

            Result<ActiveEffectRecord> removed = effects.RemoveActiveEffect(_campaign, _campaign.CampaignId, record.Effect.ActiveEffectId, host, record.Effect.Revision, NewCommandId(), Corr);
            Assert.That(removed.IsFailure, Is.True);
            Assert.That(removed.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCampaignIoFailed), "Persistence layer: the lookup's own failure, not a pass and not a fake denial");
            Assert.That(_effects.GetActiveEffect(_campaign, record.Effect.ActiveEffectId, Corr).Value.Effect.Status, Is.EqualTo(ActiveEffectStatus.Active));

            ContentDefinitionRef effectRef = NewEffectRef();
            var snapshot = new EffectMechanicsSnapshot(effectRef, 1, ContentDefinitionType.Effect, "{\"duration\":\"permanent\"}");
            Result<ActiveEffectRecord> created = ActiveEffectDirectCommandService.CreateDirectActiveEffect(effects, poisoned, _campaign, _campaign.CampaignId, effectRef, snapshot, ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow())), host, Clock.GetUtcNow(), null, NewCommandId(), Corr);
            Assert.That(created.IsFailure, Is.True);
            Assert.That(created.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCampaignIoFailed), "Application layer");

            // The system rollback needs no membership at all: it works even though the lookup is unreadable.
            Assert.That(poisoned.LookupCalls, Is.EqualTo(2));
            Result<ActiveEffectRecord> rolledBack = effects.RemoveActiveEffectAsSystemRollback(_campaign, _campaign.CampaignId, record.Effect.ActiveEffectId, NewUserId(), record.Effect.Revision, NewCommandId(), Corr);
            Assert.That(rolledBack.IsSuccess, Is.True, "the system rollback is not a MainGM operation");
            Assert.That(poisoned.LookupCalls, Is.EqualTo(2), "and performs no membership lookup");
        }
    }
}
