using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Persistence.Sqlite
{
    /// <summary>
    /// ODY-S01-012: ADR-011 section 3.2 / 05_Persistence section 27 `.odcamp`
    /// container export/import. Physical format: a standard ZIP archive
    /// (System.IO.Compression, no new dependency) containing exactly the five
    /// entries 05_Persistence section 27.1 names --
    /// <c>manifest.json</c>, <c>campaign.db</c>, <c>Assets/</c>,
    /// <c>checksums.json</c>, <c>export-manifest.json</c> -- built via section
    /// 27.2's 9-step export flow and unpacked via section 27.3's 9-step import
    /// flow, within this task's narrowed scope (no owner-key-aware reopening,
    /// no migration-on-import).
    ///
    /// Export reuses <see cref="SqliteSnapshotCopy"/> (the same ADR-012 section
    /// 8.4 SQLite-Backup-API-copy-and-validate helper ODY-S01-011's
    /// SqliteBackupRepository uses) for the database-copy core -- it does not
    /// reimplement a second parallel database-copy path.
    /// </summary>
    public sealed class SqliteExportRepository : IExportRepository
    {
        private const string ManifestFileName = "manifest.json";
        private const string DatabaseFileName = "campaign.db";
        private const string ChecksumsFileName = "checksums.json";
        private const string ExportManifestFileName = "export-manifest.json";
        private const string AssetsEntryDirectory = "Assets";

        private static readonly string[] DirectoryTree =
        {
            "Assets/Objects", "Assets/Staging", "Assets/Trash", "Assets/Quarantine",
            "Backups/Fast", "Backups/Daily", "Backups/Weekly", "Backups/Full", "Backups/Emergency",
            "Logs/Archive", "Logs/Diagnostics", "Logs/Migration",
            "Temp",
        };

        private readonly IWallClock _clock;
        private readonly ExportManifestV1Codec _exportManifestCodec = new ExportManifestV1Codec();
        private readonly CampaignManifestV1Codec _campaignManifestCodec = new CampaignManifestV1Codec();

        public SqliteExportRepository(IWallClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public Result<string> ExportCampaign(CampaignHandle campaign, string destinationOdcampPath, CorrelationId correlationId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (string.IsNullOrWhiteSpace(destinationOdcampPath)) throw new ArgumentException("Destination path is required.", nameof(destinationOdcampPath));

            string finalPath = Path.GetFullPath(destinationOdcampPath);
            if (File.Exists(finalPath))
            {
                // Explicit-absence-of-automatic-merge principle applies here too:
                // export never silently overwrites an existing .odcamp file.
                return Result<string>.Failure(PersistenceFailures.ExportCreateFailed(correlationId));
            }

            string stagingDir = Path.Combine(campaign.RootPath, "Temp", ".export-staging-" + Guid.NewGuid().ToString("N"));
            string tempOdcampPath = finalPath + ".tmp";

            try
            {
                Directory.CreateDirectory(stagingDir);
                string stagingDbPath = Path.Combine(stagingDir, DatabaseFileName);
                string sourceDbPath = Path.Combine(campaign.RootPath, DatabaseFileName);

                // Steps 1-5: full integrity check, SQLite Backup API copy,
                // validate, compute hash/size -- via the shared snapshot helper.
                SqliteSnapshotCopy.SnapshotInfo snapshot = SqliteSnapshotCopy.CreateValidated(sourceDbPath, stagingDbPath);

                File.Copy(Path.Combine(campaign.RootPath, ManifestFileName), Path.Combine(stagingDir, ManifestFileName));

                // Steps 3-4: freeze the asset manifest at the snapshot's own
                // revision (read from the just-validated copy, not the live db)
                // and copy only the assets it actually references.
                string assetsStagingDir = Path.Combine(stagingDir, AssetsEntryDirectory);
                List<(string RelativePath, string Hash)> assetEntries = CopyReferencedAssets(campaign.RootPath, stagingDbPath, assetsStagingDir);

                // Step 5/6: checksums.json + export-manifest.json.
                var checksums = new Dictionary<string, string>(StringComparer.Ordinal) { [DatabaseFileName] = snapshot.DatabaseHash };
                foreach ((string relativePath, string hash) in assetEntries)
                {
                    checksums[relativePath] = hash;
                }

                WriteChecksumsFile(Path.Combine(stagingDir, ChecksumsFileName), checksums);

                var exportManifest = new ExportManifest(
                    campaign.CampaignId, snapshot.CampaignRevision, snapshot.EventSequence,
                    campaign.Manifest.DatabaseSchemaVersion, campaign.Manifest.CampaignFormatVersion,
                    campaign.Manifest.RulesetId + "@" + campaign.Manifest.RulesetVersion,
                    _clock.GetUtcNow(), campaign.Manifest.ApplicationVersionLastOpened, snapshot.DatabaseHash, snapshot.SizeBytes);
                Result<Application.Serialization.JsonPayload> manifestWrite = _exportManifestCodec.Write(exportManifest);
                if (manifestWrite.IsFailure)
                {
                    return Result<string>.Failure(manifestWrite.Error);
                }

                File.WriteAllBytes(Path.Combine(stagingDir, ExportManifestFileName), manifestWrite.Value.Bytes);

                // Step 7: create archive to a temporary filename.
                if (File.Exists(tempOdcampPath)) File.Delete(tempOdcampPath);
                ZipFile.CreateFromDirectory(stagingDir, tempOdcampPath, CompressionLevel.Optimal, includeBaseDirectory: false);

                // Step 8: verify the archive before it is ever visible under its
                // final name -- re-read campaign.db back out of the zip and
                // confirm its hash still matches what step 5 computed.
                if (!VerifyArchiveDatabaseHash(tempOdcampPath, snapshot.DatabaseHash))
                {
                    SafeDeleteFile(tempOdcampPath);
                    return Result<string>.Failure(PersistenceFailures.ExportCreateFailed(correlationId));
                }

                // Step 9: atomic rename to *.odcamp.
                File.Move(tempOdcampPath, finalPath);
                return Result<string>.Success(finalPath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException || ex is SqliteSnapshotCopy.SnapshotIntegrityException || ex is InvalidDataException)
            {
                SafeDeleteFile(tempOdcampPath);
                return Result<string>.Failure(PersistenceFailures.ExportCreateFailed(correlationId));
            }
            finally
            {
                SafeDeleteDirectory(stagingDir);
            }
        }

        public Result<string> ImportCampaign(string odcampPath, string destinationParentDirectory, CorrelationId correlationId)
        {
            if (string.IsNullOrWhiteSpace(odcampPath)) throw new ArgumentException("Archive path is required.", nameof(odcampPath));
            if (string.IsNullOrWhiteSpace(destinationParentDirectory)) throw new ArgumentException("Destination parent directory is required.", nameof(destinationParentDirectory));

            if (!File.Exists(odcampPath))
            {
                return Result<string>.Failure(PersistenceFailures.ExportImportFailed(correlationId));
            }

            string extractDir = Path.Combine(Path.GetTempPath(), "odcamp-import-" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(extractDir);

                // Steps 1-2: extract to a temp folder, guarding against path
                // traversal / absolute-path / archive-escape entries (05_Persistence
                // section 27.5) before anything is written to disk.
                Result extractResult = SafeExtractArchive(odcampPath, extractDir, correlationId);
                if (extractResult.IsFailure)
                {
                    return Result<string>.Failure(extractResult.Error);
                }

                // Step 3: validate manifest.json -- same codec ODY-S01-007 already
                // uses to validate the working-folder manifest.
                string manifestPath = Path.Combine(extractDir, ManifestFileName);
                if (!File.Exists(manifestPath))
                {
                    return Result<string>.Failure(PersistenceFailures.ExportImportFailed(correlationId));
                }

                Result<CampaignManifest> manifestResult = _campaignManifestCodec.Read(File.ReadAllBytes(manifestPath));
                if (manifestResult.IsFailure)
                {
                    return Result<string>.Failure(manifestResult.Error);
                }

                CampaignManifest manifest = manifestResult.Value;

                // Step 4: validate version compatibility. A mismatched
                // DatabaseSchemaVersion returns a typed error -- it is never an
                // attempt to migrate the imported campaign (no runner exists,
                // and migrating a just-imported campaign is out of this task's
                // scope even once one does).
                if (!string.Equals(manifest.DatabaseSchemaVersion, SqliteCampaignRepository.DatabaseSchemaVersion, StringComparison.Ordinal))
                {
                    return Result<string>.Failure(PersistenceFailures.ExportImportFailed(correlationId));
                }

                // Step 5: validate campaign.db.
                string dbPath = Path.Combine(extractDir, DatabaseFileName);
                if (!File.Exists(dbPath) || !QuickCheckPasses(dbPath))
                {
                    return Result<string>.Failure(PersistenceFailures.ExportImportFailed(correlationId));
                }

                // Step 6: validate checksums against the export manifest's
                // recorded database hash (the strongest available authoritative
                // reference -- checksums.json is supplementary, corroborating detail).
                string exportManifestPath = Path.Combine(extractDir, ExportManifestFileName);
                if (File.Exists(exportManifestPath))
                {
                    Result<ExportManifest> exportManifestResult = _exportManifestCodec.Read(File.ReadAllBytes(exportManifestPath));
                    if (exportManifestResult.IsSuccess)
                    {
                        string actualHash = SqliteSnapshotCopy.ComputeSha256Hex(File.ReadAllBytes(dbPath));
                        if (!string.Equals(actualHash, exportManifestResult.Value.DatabaseHash, StringComparison.Ordinal))
                        {
                            return Result<string>.Failure(PersistenceFailures.ExportImportFailed(correlationId));
                        }
                    }
                }

                // Step 7-8: choose the target folder (a brand-new directory
                // derived from the imported CampaignId -- never the caller's
                // choice of an existing directory) and copy into it. A
                // pre-existing, non-empty target is a typed error, never an
                // attempted merge (roadmap section 10.3).
                string newRootPath = Path.Combine(Path.GetFullPath(destinationParentDirectory), "imported-" + manifest.CampaignId);
                if (Directory.Exists(newRootPath) && Directory.GetFileSystemEntries(newRootPath).Length > 0)
                {
                    return Result<string>.Failure(PersistenceFailures.ExportImportFailed(correlationId));
                }

                Directory.CreateDirectory(newRootPath);
                foreach (string relative in DirectoryTree)
                {
                    Directory.CreateDirectory(Path.Combine(newRootPath, relative.Replace('/', Path.DirectorySeparatorChar)));
                }

                File.Copy(dbPath, Path.Combine(newRootPath, DatabaseFileName), overwrite: false);
                File.Copy(manifestPath, Path.Combine(newRootPath, ManifestFileName), overwrite: false);

                string extractedAssetsDir = Path.Combine(extractDir, AssetsEntryDirectory);
                if (Directory.Exists(extractedAssetsDir))
                {
                    CopyDirectoryRecursive(extractedAssetsDir, Path.Combine(newRootPath, AssetsEntryDirectory, "Objects"));
                }

                // Step 9: 05_Persistence calls for lock creation + open here, but
                // no campaign.lock mechanism exists yet in this codebase
                // (ODY-S01-009 already documented this absence for Open()) -- so,
                // consistent with ODY-S01-011's RestoreBackup, this returns the
                // new root path without creating a lock or auto-opening; the
                // caller opens it explicitly via ICampaignRepository.Open.
                return Result<string>.Success(newRootPath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException || ex is InvalidDataException)
            {
                return Result<string>.Failure(PersistenceFailures.ExportImportFailed(correlationId));
            }
            finally
            {
                SafeDeleteDirectory(extractDir);
            }
        }

        private static List<(string RelativePath, string Hash)> CopyReferencedAssets(string campaignRootPath, string snapshotDbPath, string assetsStagingDir)
        {
            var results = new List<(string, string)>();
            using var connection = new SqliteConnection("Data Source=" + snapshotDbPath + ";Mode=ReadOnly;Pooling=False");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT RelativePath FROM AssetManifestEntries;";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string relativePath = reader.GetString(0);
                string sourcePath = Path.Combine(campaignRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(sourcePath))
                {
                    // 05_Persistence section 27.4: a missing optional asset does
                    // not block export/import here (only a missing/corrupt
                    // campaign.db does, section 27.4's own rule) -- it is simply
                    // omitted from the archive.
                    continue;
                }

                string destinationPath = Path.Combine(assetsStagingDir, relativePath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                File.Copy(sourcePath, destinationPath, overwrite: false);
                string hash = SqliteSnapshotCopy.ComputeSha256Hex(File.ReadAllBytes(destinationPath));
                results.Add((relativePath, hash));
            }

            return results;
        }

        private static void CopyDirectoryRecursive(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                File.Copy(file, Path.Combine(destinationDir, Path.GetFileName(file)), overwrite: false);
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                CopyDirectoryRecursive(subDir, Path.Combine(destinationDir, Path.GetFileName(subDir)));
            }
        }

        private static void WriteChecksumsFile(string path, IReadOnlyDictionary<string, string> checksums)
        {
            // Supplementary per-file integrity listing (05_Persistence section
            // 27.1), not an authoritative domain/event/command payload -- so this
            // deliberately stays a small hand-written flat JSON object rather than
            // a full ADR-003 versioned contract type/codec (export-manifest.json,
            // the actual authoritative record, already has one).
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write("{");
            bool first = true;
            foreach (KeyValuePair<string, string> entry in checksums)
            {
                if (!first) writer.Write(",");
                first = false;
                writer.Write("\"" + entry.Key.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\":\"" + entry.Value + "\"");
            }

            writer.Write("}");
        }

        private static bool VerifyArchiveDatabaseHash(string odcampPath, string expectedHash)
        {
            using var archive = ZipFile.OpenRead(odcampPath);
            ZipArchiveEntry? entry = archive.GetEntry(DatabaseFileName);
            if (entry == null) return false;

            using var entryStream = entry.Open();
            using var memory = new MemoryStream();
            entryStream.CopyTo(memory);
            string actualHash = SqliteSnapshotCopy.ComputeSha256Hex(memory.ToArray());
            return string.Equals(actualHash, expectedHash, StringComparison.Ordinal);
        }

        private static bool QuickCheckPasses(string dbPath)
        {
            using var connection = new SqliteConnection("Data Source=" + dbPath + ";Mode=ReadOnly;Pooling=False");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA quick_check;";
            object? result = command.ExecuteScalar();
            return result is string status && string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase);
        }

        private static Result SafeExtractArchive(string odcampPath, string extractDir, CorrelationId correlationId)
        {
            string normalizedExtractDir = Path.GetFullPath(extractDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            using (var archive = ZipFile.OpenRead(odcampPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    // 05_Persistence section 27.5: reject path traversal, absolute
                    // paths, and any entry that would resolve outside extractDir --
                    // checked before a single byte is extracted.
                    if (string.IsNullOrEmpty(entry.FullName)) continue;
                    if (Path.IsPathRooted(entry.FullName) || entry.FullName.Contains(".."))
                    {
                        return Result.Failure(PersistenceFailures.ExportImportFailed(correlationId));
                    }

                    string destinationPath = Path.GetFullPath(Path.Combine(normalizedExtractDir, entry.FullName));
                    if (!destinationPath.StartsWith(normalizedExtractDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        return Result.Failure(PersistenceFailures.ExportImportFailed(correlationId));
                    }
                }
            }

            ZipFile.ExtractToDirectory(odcampPath, extractDir);
            return Result.Success();
        }

        private static void SafeDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static void SafeDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
