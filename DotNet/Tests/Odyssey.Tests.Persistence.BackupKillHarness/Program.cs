using System;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence.BackupKillHarness
{
    /// <summary>
    /// ODY-S01-011 recovery test support only -- not production code. Opens the
    /// given campaign and calls the real, production SqliteBackupRepository.
    /// CreateBackup (not a re-implementation), so the parent test can measure a
    /// baseline duration and then kill this process mid-copy to prove the
    /// temp -> validate -> atomic-rename flow never promotes a partial backup.
    ///
    /// Args: [0] campaign folder path, [1] backup reason string.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine("usage: BackupKillHarness <campaignFolderPath> <reason>");
                return 2;
            }

            string campaignFolderPath = args[0];
            string reason = args[1];
            var correlationId = CorrelationId.Parse("corr_00000000000000000000000000000000");
            IWallClock clock = new SystemWallClock();

            var campaignRepository = new SqliteCampaignRepository(clock);
            Result<CampaignHandle> opened = campaignRepository.Open(campaignFolderPath, correlationId);
            if (opened.IsFailure)
            {
                Console.Error.WriteLine("open-failed: " + opened.Error.Code);
                return 3;
            }

            var backupRepository = new SqliteBackupRepository(clock);
            Result<BackupRecord> backup = backupRepository.CreateBackup(opened.Value, reason, correlationId);
            campaignRepository.Close(opened.Value, correlationId);

            if (backup.IsFailure)
            {
                Console.Error.WriteLine("backup-failed: " + backup.Error.Code);
                return 4;
            }

            Console.WriteLine("BACKED_UP " + backup.Value.BackupId);
            return 0;
        }
    }

    internal sealed class SystemWallClock : IWallClock
    {
        public UtcInstant GetUtcNow() => UtcInstant.FromDateTimeOffset(DateTimeOffset.UtcNow);
    }
}
