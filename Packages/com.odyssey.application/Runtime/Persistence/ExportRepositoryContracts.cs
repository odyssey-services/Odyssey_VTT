using System;
using Odyssey.Application.Results;
using Odyssey.Domain.Identity;

namespace Odyssey.Application.Persistence
{
    /// <summary>
    /// ADR-011 section 3.2 / 05_Persistence section 27 `.odcamp` container.
    /// ODY-S01-012: export a campaign into a single portable archive file, and
    /// import a `.odcamp` into a brand-new local campaign copy. Explicitly does
    /// not implement ADR-014 section 11.3's owner-key-aware reopening behavior
    /// (still `[OPEN]`, not decided by this task) and does not attempt to
    /// migrate an imported campaign on a schema-version mismatch -- it returns
    /// a typed error instead (the full ADR-013 migration runner still does not
    /// exist after ODY-S01-010's narrow migration-registry-baseline scope).
    /// </summary>
    public interface IExportRepository
    {
        /// <summary>
        /// Exports <paramref name="campaign"/> into a new `.odcamp` file at
        /// <paramref name="destinationOdcampPath"/>. Returns the final path on
        /// success. The destination file must not already exist.
        /// </summary>
        Result<string> ExportCampaign(CampaignHandle campaign, string destinationOdcampPath, CorrelationId correlationId);

        /// <summary>
        /// Imports the given `.odcamp` file into a brand-new campaign directory
        /// under <paramref name="destinationParentDirectory"/>. Never writes into
        /// or merges with an existing campaign directory (roadmap section 10.3's
        /// "отсутствие автоматического merge") -- a non-empty destination is a
        /// typed error, not an attempted merge.
        /// </summary>
        Result<string> ImportCampaign(string odcampPath, string destinationParentDirectory, CorrelationId correlationId);
    }
}
