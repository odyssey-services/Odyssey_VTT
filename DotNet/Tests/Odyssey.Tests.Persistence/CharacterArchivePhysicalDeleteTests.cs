using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Character;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence
{
    /// <summary>
    /// ODY-S04-110: real, non-stubbed tests against a real temp-directory
    /// campaign and a real SQLite database, mirroring
    /// <see cref="CharacterResourceAnatomyTests"/>'s exact fixture
    /// convention. Covers <c>ArchiveCharacter</c> (legal transitions, actor
    /// gate per section 1.3) and <c>DeleteCharacterPermanently</c>
    /// (MainGM-only, backup reuse per section 1.2, extensible dependency
    /// check per section 1.1, historical-identity survival per ADR-022
    /// section 7-8/ADR-025 section 5.3).
    /// </summary>
    public sealed class CharacterArchivePhysicalDeleteTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

        private string _campaignDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaignRepository = null!;
        private SqliteCharacterRepository _characterRepository = null!;
        private SqliteBackupRepository _backupRepository = null!;

        [SetUp]
        public void SetUp()
        {
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s04-110-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_campaignDir, "Archive Delete Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
            Result<CampaignHandle> created = _campaignRepository.Create(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
            _backupRepository = new SqliteBackupRepository(Clock);
            _characterRepository = new SqliteCharacterRepository(Clock);
        }

        [TearDown]
        public void TearDown()
        {
            try { _campaignRepository.Close(_campaign, TestCorrelationId); }
            catch (IOException) { }

            try { if (Directory.Exists(_campaignDir)) Directory.Delete(_campaignDir, recursive: true); }
            catch (IOException) { }
        }

        private CharacterRecord CreateCharacter(string name = "Archive Delete Character")
        {
            var request = new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, name);
            Result<CharacterRecord> created = _characterRepository.CreateCharacter(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        private static long ReadLong(SqliteConnection connection, string sql, params string[] parameters)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            for (int index = 0; index < parameters.Length; index++)
            {
                command.Parameters.AddWithValue("$c" + index, parameters[index]);
            }

            object? result = command.ExecuteScalar();
            return result == null || result is DBNull ? 0L : Convert.ToInt64(result);
        }

        private SqliteConnection OpenReadOnly() => new SqliteConnection("Data Source=" + Path.Combine(_campaignDir, "campaign.db") + ";Mode=ReadOnly");

        // ---- ArchiveCharacter -------------------------------------------------

        [Test]
        public void ArchiveCharacter_FromDraft_TransitionsToArchived()
        {
            CharacterRecord character = CreateCharacter();

            Result<CharacterRecord> archived = _characterRepository.ArchiveCharacter(_campaign, character.CharacterId, NewUserId(), actorIsMainGm: true, character.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);

            Assert.That(archived.IsSuccess, Is.True);
            Assert.That(archived.Value.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Archived));
        }

        [Test]
        public void ArchiveCharacter_FromActive_TransitionsToArchived()
        {
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> approved = _characterRepository.ApproveCharacterDraft(_campaign, character.CharacterId, actorIsMainGm: true, character.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);
            Assert.That(approved.IsSuccess, Is.True);
            Assert.That(approved.Value.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Active));

            Result<CharacterRecord> archived = _characterRepository.ArchiveCharacter(_campaign, character.CharacterId, NewUserId(), actorIsMainGm: true, approved.Value.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);

            Assert.That(archived.IsSuccess, Is.True);
            Assert.That(archived.Value.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Archived));
        }

        [Test]
        public void ArchiveCharacter_Twice_SecondCallIsRejected()
        {
            CharacterRecord character = CreateCharacter();
            Result<CharacterRecord> archived = _characterRepository.ArchiveCharacter(_campaign, character.CharacterId, NewUserId(), actorIsMainGm: true, character.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);
            Assert.That(archived.IsSuccess, Is.True);

            Result<CharacterRecord> secondArchive = _characterRepository.ArchiveCharacter(_campaign, character.CharacterId, NewUserId(), actorIsMainGm: true, archived.Value.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);

            Assert.That(secondArchive.IsFailure, Is.True);
            Assert.That(secondArchive.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterLifecycleTransitionInvalid));
        }

        [Test]
        public void ArchiveCharacter_ByAssignedUser_Succeeds()
        {
            // Section 1.3: MainGM-or-assigned, not MainGM-only.
            CharacterRecord character = CreateCharacter();
            UserId owner = NewUserId();
            Result<CharacterRecord> assigned = _characterRepository.AssignPrimaryOwner(_campaign, character.CharacterId, owner, "initial owner", actorIsMainGm: true, character.Revisions.OwnershipRevision, NewCommandId(), TestCorrelationId);
            Assert.That(assigned.IsSuccess, Is.True);

            Result<CharacterRecord> archived = _characterRepository.ArchiveCharacter(_campaign, character.CharacterId, owner, actorIsMainGm: false, assigned.Value.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);

            Assert.That(archived.IsSuccess, Is.True);
            Assert.That(archived.Value.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Archived));
        }

        [Test]
        public void ArchiveCharacter_ByUnrelatedUser_IsRejected_NoStateChange()
        {
            CharacterRecord character = CreateCharacter();

            Result<CharacterRecord> archived = _characterRepository.ArchiveCharacter(_campaign, character.CharacterId, NewUserId(), actorIsMainGm: false, character.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);

            Assert.That(archived.IsFailure, Is.True);
            Assert.That(archived.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterArchiveDenied));

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.Value.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Draft));
        }

        [Test]
        public void ArchiveCharacter_DuplicateCommandId_DoesNotDoubleTransition()
        {
            CharacterRecord character = CreateCharacter();
            CommandId commandId = NewCommandId();

            Result<CharacterRecord> first = _characterRepository.ArchiveCharacter(_campaign, character.CharacterId, NewUserId(), actorIsMainGm: true, character.Revisions.LifecycleRevision, commandId, TestCorrelationId);
            Assert.That(first.IsSuccess, Is.True);

            Result<CharacterRecord> replay = _characterRepository.ArchiveCharacter(_campaign, character.CharacterId, NewUserId(), actorIsMainGm: true, character.Revisions.LifecycleRevision, commandId, TestCorrelationId);
            Assert.That(replay.IsSuccess, Is.True);

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.Value.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Archived));
            Assert.That(reRead.Value.Revisions.LifecycleRevision, Is.EqualTo(character.Revisions.LifecycleRevision + 1), "a replayed duplicate CommandId must not transition twice");
        }

        // ---- DeleteCharacterPermanently -----------------------------------------

        [Test]
        public void DeleteCharacterPermanently_ByNonMainGm_IsRejected_NoStateChange()
        {
            CharacterRecord character = CreateCharacter();

            Result deleted = _characterRepository.DeleteCharacterPermanently(_campaign, character.CharacterId, "test", NewUserId(), actorIsMainGm: false, character.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);

            Assert.That(deleted.IsFailure, Is.True);
            Assert.That(deleted.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterDeletionDenied));

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.IsSuccess, Is.True);
        }

        [Test]
        public void DeleteCharacterPermanently_WithoutReasonCode_IsRejected()
        {
            CharacterRecord character = CreateCharacter();

            Result deleted = _characterRepository.DeleteCharacterPermanently(_campaign, character.CharacterId, "", NewUserId(), actorIsMainGm: true, character.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);

            Assert.That(deleted.IsFailure, Is.True);
            Assert.That(deleted.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterDeletionReasonRequired));
        }

        [Test]
        public void DeleteCharacterPermanently_WithEmptyCheckerList_Succeeds()
        {
            CharacterRecord character = CreateCharacter();

            Result deleted = _characterRepository.DeleteCharacterPermanently(_campaign, character.CharacterId, "test cleanup", NewUserId(), actorIsMainGm: true, character.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);

            Assert.That(deleted.IsSuccess, Is.True);
        }

        [Test]
        public void DeleteCharacterPermanently_CreatesBackupBeforeDeleting()
        {
            CharacterRecord character = CreateCharacter();

            Result<IReadOnlyList<Odyssey.Application.Persistence.BackupRecord>> backupsBefore = _backupRepository.ListBackups(_campaignDir, TestCorrelationId);
            Assert.That(backupsBefore.IsSuccess, Is.True);
            int countBefore = backupsBefore.Value.Count;

            Result deleted = _characterRepository.DeleteCharacterPermanently(_campaign, character.CharacterId, "test cleanup", NewUserId(), actorIsMainGm: true, character.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);
            Assert.That(deleted.IsSuccess, Is.True);

            Result<IReadOnlyList<Odyssey.Application.Persistence.BackupRecord>> backupsAfter = _backupRepository.ListBackups(_campaignDir, TestCorrelationId);
            Assert.That(backupsAfter.IsSuccess, Is.True);
            Assert.That(backupsAfter.Value.Count, Is.EqualTo(countBefore + 1));
            Assert.That(backupsAfter.Value.Any(b => b.Reason.Contains(character.CharacterId.ToString())), Is.True, "the new backup's Reason must reference the deleted CharacterId");
        }

        [Test]
        public void DeleteCharacterPermanently_ThenGetCharacter_ReturnsNotFound_ButHistorySurvives()
        {
            CharacterRecord character = CreateCharacter();
            string originalDisplayName = character.DisplayName;
            string originalRulesetVersion = character.RulesetVersion;

            Result deleted = _characterRepository.DeleteCharacterPermanently(_campaign, character.CharacterId, "test cleanup", NewUserId(), actorIsMainGm: true, character.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);
            Assert.That(deleted.IsSuccess, Is.True);

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.IsFailure, Is.True);
            Assert.That(reRead.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterNotFound));

            Result<IReadOnlyList<CharacterHistoryEntry>> history = _characterRepository.GetCharacterHistory(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(history.IsSuccess, Is.True);
            Assert.That(history.Value, Is.Not.Empty);
            Assert.That(history.Value.Any(e => e.EventType == "odyssey.persistence.character_deleted"), Is.True);
            Assert.That(history.Value.All(e => e.DisplayNameSnapshot == originalDisplayName), Is.True, "every historical entry's own DisplayNameSnapshot must still be correct");

            using SqliteConnection connection = OpenReadOnly();
            connection.Open();
            string deletedPayload = ReadStringHelper(connection, "SELECT PayloadJson FROM DomainEvents WHERE EventType = 'odyssey.persistence.character_deleted'");
            Assert.That(deletedPayload, Does.Contain(originalRulesetVersion));
        }

        private static string ReadStringHelper(SqliteConnection connection, string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            object? result = command.ExecuteScalar();
            return result == null || result is DBNull ? string.Empty : (string)result;
        }

        [Test]
        public void DeleteCharacterPermanently_DoesNotDeleteDomainEventsRows()
        {
            CharacterRecord character = CreateCharacter();

            long eventCountBefore;
            using (SqliteConnection connection = OpenReadOnly())
            {
                connection.Open();
                eventCountBefore = ReadLong(connection, "SELECT COUNT(*) FROM DomainEvents");
            }

            Result deleted = _characterRepository.DeleteCharacterPermanently(_campaign, character.CharacterId, "test cleanup", NewUserId(), actorIsMainGm: true, character.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);
            Assert.That(deleted.IsSuccess, Is.True);

            using (SqliteConnection connection = OpenReadOnly())
            {
                connection.Open();
                long eventCountAfter = ReadLong(connection, "SELECT COUNT(*) FROM DomainEvents");
                Assert.That(eventCountAfter, Is.EqualTo(eventCountBefore + 1), "only the new CharacterDeleted event is appended -- no prior DomainEvents row is removed");
            }
        }

        [Test]
        public void DeleteCharacterPermanently_DuplicateCommandId_DoesNotDuplicateEffect()
        {
            CharacterRecord character = CreateCharacter();
            CommandId commandId = NewCommandId();

            Result first = _characterRepository.DeleteCharacterPermanently(_campaign, character.CharacterId, "test cleanup", NewUserId(), actorIsMainGm: true, character.Revisions.LifecycleRevision, commandId, TestCorrelationId);
            Assert.That(first.IsSuccess, Is.True);

            Result replay = _characterRepository.DeleteCharacterPermanently(_campaign, character.CharacterId, "test cleanup", NewUserId(), actorIsMainGm: true, character.Revisions.LifecycleRevision, commandId, TestCorrelationId);
            Assert.That(replay.IsSuccess, Is.True);

            using SqliteConnection connection = OpenReadOnly();
            connection.Open();
            long deletedEventCount = ReadLong(connection, "SELECT COUNT(*) FROM DomainEvents WHERE EventType = 'odyssey.persistence.character_deleted'");
            Assert.That(deletedEventCount, Is.EqualTo(1), "a replayed duplicate CommandId must not append a second CharacterDeleted event");
        }

        [Test]
        public void DeleteCharacterPermanently_WithBlockingDependencyChecker_IsRejected_NoStateChange()
        {
            // Section 1.1's own extensibility proof: a test checker that
            // always reports a dependency must actually influence the
            // decision, not merely exist as an unused parameter.
            var blockingChecker = new AlwaysBlockingDependencyChecker();
            var repositoryWithChecker = new SqliteCharacterRepository(Clock, deletionDependencyCheckers: new ICharacterDeletionDependencyChecker[] { blockingChecker });

            CharacterRecord character = CreateCharacter();

            Result deleted = repositoryWithChecker.DeleteCharacterPermanently(_campaign, character.CharacterId, "test cleanup", NewUserId(), actorIsMainGm: true, character.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);

            Assert.That(deleted.IsFailure, Is.True);
            Assert.That(deleted.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterDeletionHasDependent));

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.IsSuccess, Is.True, "the Character must still exist -- the blocking dependency must prevent the delete, not merely be consulted");
        }

        private sealed class AlwaysBlockingDependencyChecker : ICharacterDeletionDependencyChecker
        {
            public string? CheckBlockingDependency(CampaignHandle campaign, CharacterId characterId) => "test-fixture: always blocks";
        }
    }
}
