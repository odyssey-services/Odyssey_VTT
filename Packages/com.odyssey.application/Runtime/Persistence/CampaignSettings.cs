using System;

namespace Odyssey.Application.Persistence
{
    /// <summary>
    /// Minimal campaign settings sufficient for the roadmap section 10.5 vertical
    /// slice scenario. Not a full settings UI/model — extended by later tasks as
    /// features that need settings fields are actually implemented.
    /// </summary>
    public sealed class CampaignSettings
    {
        public const int CurrentSettingsSchemaVersion = 1;

        public CampaignSettings(int settingsSchemaVersion = CurrentSettingsSchemaVersion)
        {
            if (settingsSchemaVersion < 1) throw new ArgumentOutOfRangeException(nameof(settingsSchemaVersion));
            SettingsSchemaVersion = settingsSchemaVersion;
        }

        public int SettingsSchemaVersion { get; }
    }
}
