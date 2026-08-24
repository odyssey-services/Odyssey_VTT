using System;
using System.Threading;
using Microsoft.Data.Sqlite;

namespace Odyssey.Tests.Persistence.RecoveryHarness
{
    /// <summary>
    /// ODY-S01-009 recovery test support only -- not production code, not
    /// referenced by any Packages/com.odyssey.* module. Opens the given
    /// campaign.db under the ADR-011 section 7.1 PRAGMA profile, begins a
    /// transaction that inserts one Scene row plus its matching DomainEvent and
    /// AppliedCommands rows (the same three-table group SqliteSavingPipeline
    /// commits atomically), sleeps for the requested window with the transaction
    /// still open, then commits. The parent test process kills this process
    /// during the sleep window to prove the ADR-012 section 5 transaction group
    /// never lands partially after a hard kill (SQLite WAL recovery discards the
    /// whole uncommitted transaction on next open -- SP-02 section 2.2 already
    /// proved this for the general case; this harness proves it for this
    /// project's actual DomainEvents/AppliedCommands/Scene table shapes).
    ///
    /// Args: [0] campaign.db full path, [1] SceneId marker, [2] CommandId marker,
    /// [3] sleep milliseconds before commit.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 4)
            {
                Console.Error.WriteLine("usage: RecoveryHarness <dbPath> <sceneId> <commandId> <sleepMs>");
                return 2;
            }

            string dbPath = args[0];
            string sceneId = args[1];
            string commandId = args[2];
            int sleepMs = int.Parse(args[3]);
            string now = DateTimeOffset.UtcNow.ToString("O");

            using var connection = new SqliteConnection("Data Source=" + dbPath);
            connection.Open();
            using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText =
                    "PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON; " +
                    "PRAGMA synchronous = FULL; PRAGMA busy_timeout = 5000;";
                pragma.ExecuteNonQuery();
            }

            using SqliteTransaction transaction = connection.BeginTransaction();

            using (var insertScene = connection.CreateCommand())
            {
                insertScene.Transaction = transaction;
                insertScene.CommandText =
                    "INSERT INTO Scene (SceneId, CampaignId, Name, Status, Revision, CreatedAt, UpdatedAt, LastCommandId) " +
                    "SELECT $sceneId, CampaignId, 'Kill Test Scene', 'Draft', 1, $now, $now, $commandId FROM Campaign LIMIT 1;";
                insertScene.Parameters.AddWithValue("$sceneId", sceneId);
                insertScene.Parameters.AddWithValue("$now", now);
                insertScene.Parameters.AddWithValue("$commandId", commandId);
                insertScene.ExecuteNonQuery();
            }

            long eventSequence;
            using (var insertEvent = connection.CreateCommand())
            {
                insertEvent.Transaction = transaction;
                insertEvent.CommandText =
                    "INSERT INTO DomainEvents (CampaignId, EventType, CommandId, PayloadJson, PayloadHash, CreatedAtHost) " +
                    "SELECT CampaignId, 'odyssey.persistence.scene_created', $commandId, '{}', 'harness', $now FROM Campaign LIMIT 1; " +
                    "SELECT last_insert_rowid();";
                insertEvent.Parameters.AddWithValue("$commandId", commandId);
                insertEvent.Parameters.AddWithValue("$now", now);
                eventSequence = Convert.ToInt64(insertEvent.ExecuteScalar());
            }

            using (var insertApplied = connection.CreateCommand())
            {
                insertApplied.Transaction = transaction;
                insertApplied.CommandText =
                    "INSERT INTO AppliedCommands (CommandId, Status, ResultEventSequenceFrom, ResultEventSequenceTo, ResultSummary, FailureCode, CreatedAt, CompletedAt) " +
                    "VALUES ($commandId, 'Completed', $seq, $seq, $sceneId, NULL, $now, $now);";
                insertApplied.Parameters.AddWithValue("$commandId", commandId);
                insertApplied.Parameters.AddWithValue("$seq", eventSequence);
                insertApplied.Parameters.AddWithValue("$sceneId", sceneId);
                insertApplied.Parameters.AddWithValue("$now", now);
                insertApplied.ExecuteNonQuery();
            }

            // Signal the parent that the write is staged and the sleep window has
            // started, so the parent's kill timer starts counting from a known
            // point rather than guessing how long process startup + inserts took.
            Console.WriteLine("STAGED");
            Console.Out.Flush();

            Thread.Sleep(sleepMs);
            transaction.Commit();
            Console.WriteLine("COMMITTED");
            return 0;
        }
    }
}
