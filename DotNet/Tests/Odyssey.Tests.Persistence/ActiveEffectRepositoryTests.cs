using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
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
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence
{
    /// <summary>
    /// ODY-S05-502: real SQLite tests for the `ActiveEffect` persistence
    /// foundation only -- `ADR-028` §6's own standalone
    /// `IActiveEffectRepository`/`SqliteActiveEffectRepository`, structurally
    /// parallel to `SqliteSceneRepository`, never an extension of
    /// `SqliteInventoryRepository`/`SqliteCharacterRepository`. No stacking
    /// logic (`ODY-S05-503`), no expiry mechanism (`ODY-S05-504`/`505`), and
    /// no removal command (`ODY-S05-506`) are introduced or exercised here.
    /// </summary>
    public sealed class ActiveEffectRepositoryTests
    {
        private static readonly CorrelationId Corr = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

        private string _campaignDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaigns = null!;
        private SqliteActiveEffectRepository _activeEffects = null!;

        [SetUp]
        public void SetUp()
        {
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s05-502-" + Guid.NewGuid().ToString("N"));
            _campaigns = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> created = _campaigns.Create(new CreateCampaignRequest(_campaignDir, "ActiveEffect Persistence Test Campaign", "ruleset.core", "1.0.0", "0.1.0"), NewCommandId(), Corr);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
            _activeEffects = new SqliteActiveEffectRepository(Clock);
        }

        [TearDown]
        public void TearDown()
        {
            try { _campaigns.Close(_campaign, Corr); } catch (IOException) { }
            try { if (Directory.Exists(_campaignDir)) Directory.Delete(_campaignDir, true); } catch (IOException) { }
        }

        [Test] // TC-ACTIVEEFFECT-001
        public void CreateActiveEffect_ThenGetActiveEffect_RoundTrips_ItemSourceCharacterTarget()
        {
            ActiveEffectSourceRef sourceRef = ActiveEffectSourceRef.ForItem(InventoryItemRef.ForInstance(ItemInstanceId.NewId(Clock.GetUtcNow())));
            ActiveEffectTargetRef targetRef = ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow()));
            ActiveEffectRecord record = NewRecord(sourceRef, targetRef);

            Result<ActiveEffectRecord> created = _activeEffects.CreateActiveEffect(_campaign, record, NewCommandId(), Corr);
            Result<ActiveEffectRecord> fetched = _activeEffects.GetActiveEffect(_campaign, record.Effect.ActiveEffectId, Corr);

            Assert.That(created.IsSuccess, Is.True, created.IsFailure ? created.Error.Code.ToString() : string.Empty);
            Assert.That(fetched.IsSuccess, Is.True);
            AssertRecordEquals(record, fetched.Value);
        }

        [Test] // TC-ACTIVEEFFECT-002
        public void CreateActiveEffect_ThenGetActiveEffect_RoundTrips_EquippedItemSourceItemInstanceTarget()
        {
            ActiveEffectSourceRef sourceRef = ActiveEffectSourceRef.ForEquippedItem(InventoryItemRef.ForStack(ItemStackId.NewId(Clock.GetUtcNow())));
            ActiveEffectTargetRef targetRef = ActiveEffectTargetRef.ForItemInstance(ItemInstanceId.NewId(Clock.GetUtcNow()));
            ActiveEffectRecord record = NewRecord(sourceRef, targetRef);

            _activeEffects.CreateActiveEffect(_campaign, record, NewCommandId(), Corr);
            Result<ActiveEffectRecord> fetched = _activeEffects.GetActiveEffect(_campaign, record.Effect.ActiveEffectId, Corr);

            Assert.That(fetched.IsSuccess, Is.True);
            AssertRecordEquals(record, fetched.Value);
        }

        [Test] // TC-ACTIVEEFFECT-003
        public void CreateActiveEffect_ThenGetActiveEffect_RoundTrips_ActionSource_WithNoItemReferencePersisted()
        {
            ActiveEffectRecord record = NewRecord(ActiveEffectSourceRef.ForAction(), ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow())));

            _activeEffects.CreateActiveEffect(_campaign, record, NewCommandId(), Corr);
            Result<ActiveEffectRecord> fetched = _activeEffects.GetActiveEffect(_campaign, record.Effect.ActiveEffectId, Corr);

            Assert.That(fetched.IsSuccess, Is.True);
            Assert.That(fetched.Value.Effect.SourceRef.Kind, Is.EqualTo(ActiveEffectSourceKind.Action));
            Assert.That(fetched.Value.Effect.SourceRef.ItemRef.IsValid, Is.False);
        }

        [Test] // TC-ACTIVEEFFECT-004
        public void CreateActiveEffect_ThenGetActiveEffect_RoundTrips_GMDirectSource()
        {
            ActiveEffectRecord record = NewRecord(ActiveEffectSourceRef.ForGMDirect(), ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow())));

            _activeEffects.CreateActiveEffect(_campaign, record, NewCommandId(), Corr);
            Result<ActiveEffectRecord> fetched = _activeEffects.GetActiveEffect(_campaign, record.Effect.ActiveEffectId, Corr);

            Assert.That(fetched.IsSuccess, Is.True);
            Assert.That(fetched.Value.Effect.SourceRef.Kind, Is.EqualTo(ActiveEffectSourceKind.GMDirect));
        }

        [Test] // TC-ACTIVEEFFECT-005
        public void GetActiveEffect_WithMissingId_ReturnsNotFound()
        {
            ActiveEffectId missingId = ActiveEffectId.NewId(Clock.GetUtcNow());

            Result<ActiveEffectRecord> fetched = _activeEffects.GetActiveEffect(_campaign, missingId, Corr);

            Assert.That(fetched.IsFailure, Is.True);
            Assert.That(fetched.Error.Code, Is.EqualTo(ErrorCodes.PersistenceActiveEffectNotFound));
        }

        [Test] // TC-ACTIVEEFFECT-006
        public void GetActiveEffect_BelongingToAnotherCampaign_ReturnsNotFound_NotACrossCampaignLeak()
        {
            ActiveEffectRecord record = NewRecord(ActiveEffectSourceRef.ForGMDirect(), ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow())));
            _activeEffects.CreateActiveEffect(_campaign, record, NewCommandId(), Corr);

            string otherCampaignDir = Path.Combine(Path.GetTempPath(), "ody-s05-502-other-" + Guid.NewGuid().ToString("N"));
            try
            {
                Result<CampaignHandle> otherCreated = _campaigns.Create(new CreateCampaignRequest(otherCampaignDir, "Other Campaign", "ruleset.core", "1.0.0", "0.1.0"), NewCommandId(), Corr);
                Assert.That(otherCreated.IsSuccess, Is.True);

                Result<ActiveEffectRecord> fetched = _activeEffects.GetActiveEffect(otherCreated.Value, record.Effect.ActiveEffectId, Corr);

                Assert.That(fetched.IsFailure, Is.True);
                Assert.That(fetched.Error.Code, Is.EqualTo(ErrorCodes.PersistenceActiveEffectNotFound), "each campaign owns its own physical database file; an id from one campaign is simply absent from another's own ActiveEffect table, never returned across the boundary");
                _campaigns.Close(otherCreated.Value, Corr);
            }
            finally
            {
                try { if (Directory.Exists(otherCampaignDir)) Directory.Delete(otherCampaignDir, true); } catch (IOException) { }
            }
        }

        [Test] // TC-ACTIVEEFFECT-007
        public void ListActiveEffectsByTarget_ReturnsOnlyRecordsMatchingThatExactTarget()
        {
            CharacterId targetCharacterId = CharacterId.NewId(Clock.GetUtcNow());
            ActiveEffectTargetRef matchingTarget = ActiveEffectTargetRef.ForCharacter(targetCharacterId);
            ActiveEffectTargetRef otherTarget = ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow()));

            ActiveEffectRecord matchingA = NewRecord(ActiveEffectSourceRef.ForGMDirect(), matchingTarget);
            ActiveEffectRecord matchingB = NewRecord(ActiveEffectSourceRef.ForGMDirect(), matchingTarget);
            ActiveEffectRecord other = NewRecord(ActiveEffectSourceRef.ForGMDirect(), otherTarget);
            _activeEffects.CreateActiveEffect(_campaign, matchingA, NewCommandId(), Corr);
            _activeEffects.CreateActiveEffect(_campaign, matchingB, NewCommandId(), Corr);
            _activeEffects.CreateActiveEffect(_campaign, other, NewCommandId(), Corr);

            Result<System.Collections.Generic.IReadOnlyList<ActiveEffectRecord>> listed = _activeEffects.ListActiveEffectsByTarget(_campaign, _campaign.CampaignId, matchingTarget, Corr);

            Assert.That(listed.IsSuccess, Is.True);
            Assert.That(listed.Value.Select(r => r.Effect.ActiveEffectId), Is.EquivalentTo(new[] { matchingA.Effect.ActiveEffectId, matchingB.Effect.ActiveEffectId }));
        }

        [Test] // TC-ACTIVEEFFECT-008
        public void ListActiveEffectsBySource_ReturnsOnlyRecordsMatchingThatExactSource()
        {
            InventoryItemRef sourceItemRef = InventoryItemRef.ForInstance(ItemInstanceId.NewId(Clock.GetUtcNow()));
            ActiveEffectSourceRef matchingSource = ActiveEffectSourceRef.ForItem(sourceItemRef);
            ActiveEffectSourceRef otherSource = ActiveEffectSourceRef.ForItem(InventoryItemRef.ForInstance(ItemInstanceId.NewId(Clock.GetUtcNow())));

            ActiveEffectRecord matching = NewRecord(matchingSource, ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow())));
            ActiveEffectRecord gmDirect = NewRecord(ActiveEffectSourceRef.ForGMDirect(), ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow())));
            ActiveEffectRecord otherItem = NewRecord(otherSource, ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow())));
            _activeEffects.CreateActiveEffect(_campaign, matching, NewCommandId(), Corr);
            _activeEffects.CreateActiveEffect(_campaign, gmDirect, NewCommandId(), Corr);
            _activeEffects.CreateActiveEffect(_campaign, otherItem, NewCommandId(), Corr);

            Result<System.Collections.Generic.IReadOnlyList<ActiveEffectRecord>> listed = _activeEffects.ListActiveEffectsBySource(_campaign, _campaign.CampaignId, matchingSource, Corr);

            Assert.That(listed.IsSuccess, Is.True);
            Assert.That(listed.Value.Select(r => r.Effect.ActiveEffectId), Is.EquivalentTo(new[] { matching.Effect.ActiveEffectId }));
        }

        [Test] // TC-ACTIVEEFFECT-009
        public void CreateActiveEffect_WithMismatchedCampaignId_IsRejected()
        {
            UtcInstant now = Clock.GetUtcNow();
            var effectRef = new ContentDefinitionRef(ContentDefinitionId.NewId(now), 1);
            var snapshot = new EffectMechanicsSnapshot(effectRef, 1, ContentDefinitionType.Effect, "{}");
            var effect = new ActiveEffect(ActiveEffectId.NewId(now), effectRef, snapshot, ActiveEffectSourceRef.ForGMDirect(),
                ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(now)), ActiveEffectStatus.Active, 1, NewUserId(), now, null, 1);
            var mismatched = new ActiveEffectRecord(CampaignId.NewId(now), effect);

            Result<ActiveEffectRecord> created = _activeEffects.CreateActiveEffect(_campaign, mismatched, NewCommandId(), Corr);

            Assert.That(created.IsFailure, Is.True);
            Assert.That(created.Error.Code, Is.EqualTo(ErrorCodes.PersistenceActiveEffectCampaignMismatch));
            Assert.That(CountRows(effect.ActiveEffectId), Is.EqualTo(0), "a rejected create must not write any row");
        }

        [Test] // TC-ACTIVEEFFECT-010
        public void CreateActiveEffect_ReplayWithSameCommandId_ReturnsTheSameRecordWithoutDuplicating()
        {
            ActiveEffectRecord record = NewRecord(ActiveEffectSourceRef.ForGMDirect(), ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Clock.GetUtcNow())));
            CommandId commandId = NewCommandId();

            Result<ActiveEffectRecord> first = _activeEffects.CreateActiveEffect(_campaign, record, commandId, Corr);
            Result<ActiveEffectRecord> replay = _activeEffects.CreateActiveEffect(_campaign, record, commandId, Corr);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(replay.IsSuccess, Is.True);
            Assert.That(replay.Value.Effect.ActiveEffectId, Is.EqualTo(first.Value.Effect.ActiveEffectId));
            Assert.That(CountRows(record.Effect.ActiveEffectId), Is.EqualTo(1), "a replay with the same CommandId must not create a second row");
        }

        // ---- helpers (private methods, no new production type) ----

        private ActiveEffectRecord NewRecord(ActiveEffectSourceRef sourceRef, ActiveEffectTargetRef targetRef)
        {
            UtcInstant now = Clock.GetUtcNow();
            var effectRef = new ContentDefinitionRef(ContentDefinitionId.NewId(now), 2);
            var snapshot = new EffectMechanicsSnapshot(effectRef, 2, ContentDefinitionType.Effect, "{\"duration\":\"instant\"}");
            var effect = new ActiveEffect(ActiveEffectId.NewId(now), effectRef, snapshot, sourceRef, targetRef, ActiveEffectStatus.Active, 1, NewUserId(), now, null, 1);
            return new ActiveEffectRecord(_campaign.CampaignId, effect);
        }

        private static void AssertRecordEquals(ActiveEffectRecord expected, ActiveEffectRecord actual)
        {
            Assert.That(actual.CampaignId, Is.EqualTo(expected.CampaignId));
            Assert.That(actual.Effect.ActiveEffectId, Is.EqualTo(expected.Effect.ActiveEffectId));
            Assert.That(actual.Effect.EffectDefinitionRef, Is.EqualTo(expected.Effect.EffectDefinitionRef));
            Assert.That(actual.Effect.EffectMechanicsSnapshot, Is.EqualTo(expected.Effect.EffectMechanicsSnapshot));
            Assert.That(actual.Effect.SourceRef, Is.EqualTo(expected.Effect.SourceRef));
            Assert.That(actual.Effect.TargetRef, Is.EqualTo(expected.Effect.TargetRef));
            Assert.That(actual.Effect.Status, Is.EqualTo(expected.Effect.Status));
            Assert.That(actual.Effect.StackCount, Is.EqualTo(expected.Effect.StackCount));
            Assert.That(actual.Effect.AppliedByUserId, Is.EqualTo(expected.Effect.AppliedByUserId));
            Assert.That(actual.Effect.AppliedAt, Is.EqualTo(expected.Effect.AppliedAt));
            Assert.That(actual.Effect.ExpiresAt, Is.EqualTo(expected.Effect.ExpiresAt));
            Assert.That(actual.Effect.Revision, Is.EqualTo(expected.Effect.Revision));
        }

        private long CountRows(ActiveEffectId activeEffectId)
        {
            using var connection = new SqliteConnection("Data Source=" + Path.Combine(_campaignDir, "campaign.db"));
            connection.Open();
            using var tableCheck = connection.CreateCommand();
            tableCheck.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'ActiveEffect';";
            if (tableCheck.ExecuteScalar() == null)
            {
                // A rejected create (e.g. a campaign-boundary mismatch) never opens the
                // table-creation path, so the ActiveEffect table may not exist yet -- that
                // is itself proof no row was written.
                return 0;
            }

            using var count = connection.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM ActiveEffect WHERE ActiveEffectId = $id;";
            count.Parameters.AddWithValue("$id", activeEffectId.ToString());
            return (long)count.ExecuteScalar()!;
        }
    }
}
