using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence
{
    /// <summary>
    /// ODY-S01-009: real tests for the ADR-012 section 5 single-transaction
    /// journal-projection commit pipeline (SqliteSavingPipeline), the ADR-012
    /// section 7 command idempotency it provides, and the 05_Persistence
    /// section 22/23 open-time integrity check and unclean-shutdown recovery.
    /// </summary>
    public sealed class SqliteSavingPipelineTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private string _workDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaignRepository = null!;

        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));

        [SetUp]
        public void SetUp()
        {
            _workDir = Path.Combine(Path.GetTempPath(), "ody-s01-009-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_workDir, "Pipeline Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
            Result<CampaignHandle> created = _campaignRepository.Create(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
        }

        [TearDown]
        public void TearDown()
        {
            try { _campaignRepository.Close(_campaign, TestCorrelationId); } catch (IOException) { }
            try { if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true); } catch (IOException) { }
        }

        [Test]
        public void CreateScene_CommitsProjectionEventAndAppliedCommand_AsOneConsistentGroup()
        {
            var sceneRepository = new SqliteSceneRepository(Clock);
            CommandId commandId = NewCommandId();

            Result<SceneRecord> result = sceneRepository.CreateScene(_campaign, "Consistency Test", commandId, TestCorrelationId);
            Assert.That(result.IsSuccess, Is.True);
            SceneId sceneId = result.Value.SceneId;

            using var connection = new SqliteConnection("Data Source=" + Path.Combine(_workDir, "campaign.db") + ";Mode=ReadOnly");
            connection.Open();

            long appliedFrom = ReadLong(connection, "SELECT ResultEventSequenceFrom FROM AppliedCommands WHERE CommandId = $c", commandId.ToString());
            long appliedTo = ReadLong(connection, "SELECT ResultEventSequenceTo FROM AppliedCommands WHERE CommandId = $c", commandId.ToString());
            long eventCount = ReadLong(connection, "SELECT COUNT(*) FROM DomainEvents WHERE CommandId = $c AND EventType = 'odyssey.persistence.scene_created'", commandId.ToString());
            long eventSequence = ReadLong(connection, "SELECT EventSequence FROM DomainEvents WHERE CommandId = $c", commandId.ToString());
            long aggregateRevision = ReadLong(connection, "SELECT Revision FROM AggregateRevisions WHERE AggregateType = 'scene' AND AggregateId = $c", sceneId.ToString());
            long sceneRowCount = ReadLong(connection, "SELECT COUNT(*) FROM Scene WHERE SceneId = $c AND LastCommandId = $c2", sceneId.ToString(), commandId.ToString());

            Assert.That(eventCount, Is.EqualTo(1), "exactly one DomainEvent must exist for the committed command");
            Assert.That(appliedFrom, Is.EqualTo(eventSequence), "AppliedCommands must reference the exact event it produced");
            Assert.That(appliedTo, Is.EqualTo(eventSequence));
            Assert.That(aggregateRevision, Is.EqualTo(1), "AggregateRevisions must be updated in the same transaction");
            Assert.That(sceneRowCount, Is.EqualTo(1), "the Scene projection row must exist, tagged with the committing CommandId");
        }

        [Test]
        public void CreateToken_OnRejectedCommand_LeavesNoEventOrAppliedCommandRow()
        {
            var sceneRepository = new SqliteSceneRepository(Clock);
            SceneId phantomScene = SceneId.NewId(Clock.GetUtcNow());
            CommandId commandId = NewCommandId();

            Result<TokenRecord> result = sceneRepository.CreateToken(_campaign, phantomScene, new TokenPosition(1, 1), commandId, TestCorrelationId);
            Assert.That(result.IsFailure, Is.True);

            using var connection = new SqliteConnection("Data Source=" + Path.Combine(_workDir, "campaign.db") + ";Mode=ReadOnly");
            connection.Open();

            // Section 5: a rejected command must never leave a partial group behind
            // -- since CreateToken rejects before the pipeline even runs (scene
            // does not exist), nothing in AppliedCommands/DomainEvents/Token can
            // exist for this CommandId.
            Assert.That(ReadLong(connection, "SELECT COUNT(*) FROM AppliedCommands WHERE CommandId = $c", commandId.ToString()), Is.EqualTo(0));
            Assert.That(ReadLong(connection, "SELECT COUNT(*) FROM DomainEvents WHERE CommandId = $c", commandId.ToString()), Is.EqualTo(0));
            Assert.That(ReadLong(connection, "SELECT COUNT(*) FROM Token WHERE LastCommandId = $c", commandId.ToString()), Is.EqualTo(0));
        }

        [Test]
        public void CreateScene_RedeliveredWithSameCommandId_ReplaysStoredOutcome_DoesNotDuplicateEffect()
        {
            var sceneRepository = new SqliteSceneRepository(Clock);
            CommandId commandId = NewCommandId();

            Result<SceneRecord> first = sceneRepository.CreateScene(_campaign, "Idempotent Scene", commandId, TestCorrelationId);
            Result<SceneRecord> second = sceneRepository.CreateScene(_campaign, "Idempotent Scene", commandId, TestCorrelationId);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(second.IsSuccess, Is.True);
            Assert.That(second.Value.SceneId, Is.EqualTo(first.Value.SceneId), "redelivery must return the same SceneId, not create a second one");

            using var connection = new SqliteConnection("Data Source=" + Path.Combine(_workDir, "campaign.db") + ";Mode=ReadOnly");
            connection.Open();
            Assert.That(ReadLong(connection, "SELECT COUNT(*) FROM Scene WHERE CampaignId = $c", _campaign.CampaignId.ToString()), Is.EqualTo(1), "only one Scene row must exist after redelivery");
            Assert.That(ReadLong(connection, "SELECT COUNT(*) FROM DomainEvents WHERE CommandId = $c", commandId.ToString()), Is.EqualTo(1), "redelivery must not append a second DomainEvent");
            Assert.That(ReadLong(connection, "SELECT COUNT(*) FROM AppliedCommands WHERE CommandId = $c", commandId.ToString()), Is.EqualTo(1));
        }

        [Test]
        public void MoveToken_RedeliveredWithSameCommandId_ReplaysStoredPosition_DoesNotMoveTwice()
        {
            var sceneRepository = new SqliteSceneRepository(Clock);
            SceneId sceneId = sceneRepository.CreateScene(_campaign, "Move Idempotency", NewCommandId(), TestCorrelationId).Value.SceneId;
            TokenId tokenId = sceneRepository.CreateToken(_campaign, sceneId, new TokenPosition(0, 0), NewCommandId(), TestCorrelationId).Value.TokenId;

            CommandId moveCommandId = NewCommandId();
            Result<TokenRecord> firstMove = sceneRepository.MoveToken(_campaign, tokenId, new TokenPosition(9, 9), moveCommandId, TestCorrelationId);
            Result<TokenRecord> redeliveredMove = sceneRepository.MoveToken(_campaign, tokenId, new TokenPosition(9, 9), moveCommandId, TestCorrelationId);

            Assert.That(firstMove.IsSuccess, Is.True);
            Assert.That(redeliveredMove.IsSuccess, Is.True);
            Assert.That(firstMove.Value.Revision, Is.EqualTo(2));
            Assert.That(redeliveredMove.Value.Revision, Is.EqualTo(2), "redelivery must not advance the revision a second time");

            using var connection = new SqliteConnection("Data Source=" + Path.Combine(_workDir, "campaign.db") + ";Mode=ReadOnly");
            connection.Open();
            Assert.That(ReadLong(connection, "SELECT Revision FROM Token WHERE TokenId = $c", tokenId.ToString()), Is.EqualTo(2));
            Assert.That(ReadLong(connection, "SELECT COUNT(*) FROM DomainEvents WHERE CommandId = $c AND EventType = 'odyssey.persistence.token_moved'", moveCommandId.ToString()), Is.EqualTo(1));
        }

        [Test]
        public void Close_TruncatesWalFile_SafeCloseCheckpoint()
        {
            var sceneRepository = new SqliteSceneRepository(Clock);
            sceneRepository.CreateScene(_campaign, "Checkpoint Scene", NewCommandId(), TestCorrelationId);

            string walPath = Path.Combine(_workDir, "campaign.db-wal");
            Assert.That(File.Exists(walPath), Is.True, "WAL file must exist while the campaign is open with pending writes");

            Result closeResult = _campaignRepository.Close(_campaign, TestCorrelationId);
            Assert.That(closeResult.IsSuccess, Is.True);

            // ADR-011 section 7.4: PRAGMA wal_checkpoint(TRUNCATE) on clean close
            // truncates the -wal file to zero bytes (checkpointed pages move into
            // campaign.db itself); observing a 0-byte or removed -wal file after
            // Close is independent, file-level evidence the checkpoint actually ran.
            long walSize = File.Exists(walPath) ? new FileInfo(walPath).Length : 0L;
            Assert.That(walSize, Is.EqualTo(0L));
        }

        [Test]
        public void Open_AfterHardKillMidTransaction_RecoversCleanly_KilledWriteNeverAppears()
        {
            // Establish one committed baseline scene before the kill, so the test
            // can distinguish "recovery discarded everything" (a real bug) from
            // "recovery discarded only the uncommitted transaction" (the correct,
            // required behavior).
            var sceneRepository = new SqliteSceneRepository(Clock);
            Result<SceneRecord> baseline = sceneRepository.CreateScene(_campaign, "Baseline Before Kill", NewCommandId(), TestCorrelationId);
            Assert.That(baseline.IsSuccess, Is.True);
            Assert.That(_campaignRepository.Close(_campaign, TestCorrelationId).IsSuccess, Is.True);

            string dbPath = Path.Combine(_workDir, "campaign.db");
            string killedSceneId = "scn_" + Guid.NewGuid().ToString("N");
            string killedCommandId = "cmd_" + Guid.NewGuid().ToString("N");

            string harnessDll = LocateHarnessDll();
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{harnessDll}\" \"{dbPath}\" {killedSceneId} {killedCommandId} 4000",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (Process harness = Process.Start(startInfo)!)
            {
                string? stagedLine = harness.StandardOutput.ReadLine();
                Assert.That(stagedLine, Is.EqualTo("STAGED"), "harness must confirm it staged the write before the kill timer starts");

                // The transaction is now open with the insert done but not
                // committed (harness sleeps 4s before Commit()). Kill hard, well
                // inside that window.
                System.Threading.Thread.Sleep(500);
                Assert.That(harness.HasExited, Is.False, "harness must still be mid-transaction, not already committed, when killed");
                harness.Kill(entireProcessTree: true);
                harness.WaitForExit(10000);
            }

            var reopenRepository = new SqliteCampaignRepository(Clock);
            Result<CampaignHandle> reopened = reopenRepository.Open(_workDir, TestCorrelationId);
            Assert.That(reopened.IsSuccess, Is.True, "the ADR-011/05_Persistence section 22.1 quick_check must pass after an unclean shutdown -- WAL recovery must leave the database structurally sound");

            using var connection = new SqliteConnection("Data Source=" + dbPath + ";Mode=ReadOnly");
            connection.Open();
            Assert.That(ReadLong(connection, "SELECT COUNT(*) FROM Scene WHERE SceneId = $c", killedSceneId), Is.EqualTo(0), "the killed, never-committed scene must not exist after recovery");
            Assert.That(ReadLong(connection, "SELECT COUNT(*) FROM DomainEvents WHERE CommandId = $c", killedCommandId), Is.EqualTo(0), "the killed, never-committed event must not exist after recovery");
            Assert.That(ReadLong(connection, "SELECT COUNT(*) FROM AppliedCommands WHERE CommandId = $c", killedCommandId), Is.EqualTo(0));
            Assert.That(ReadLong(connection, "SELECT COUNT(*) FROM Scene WHERE Name = 'Baseline Before Kill'"), Is.EqualTo(1), "the scene committed before the kill must survive recovery intact");

            reopenRepository.Close(reopened.Value, TestCorrelationId);
        }

        private static string LocateHarnessDll()
        {
            string testBinDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string configFolder = new DirectoryInfo(testBinDir).Name;
            string artifactsBinDir = Directory.GetParent(testBinDir)!.Parent!.FullName;
            string dllPath = Path.Combine(artifactsBinDir, "Odyssey.Tests.Persistence.RecoveryHarness", configFolder, "Odyssey.Tests.Persistence.RecoveryHarness.dll");
            if (!File.Exists(dllPath))
            {
                throw new FileNotFoundException("Recovery harness dll not found -- build Odyssey.Tests.Persistence.RecoveryHarness.csproj first.", dllPath);
            }

            return dllPath;
        }

        private static long ReadLong(SqliteConnection connection, string sql, params string[] parameters)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            for (int index = 0; index < parameters.Length; index++)
            {
                string name = index == 0 ? "$c" : "$c" + (index + 1);
                command.Parameters.AddWithValue(name, parameters[index]);
            }

            object? result = command.ExecuteScalar();
            return result == null || result is DBNull ? 0L : Convert.ToInt64(result);
        }
    }
}
