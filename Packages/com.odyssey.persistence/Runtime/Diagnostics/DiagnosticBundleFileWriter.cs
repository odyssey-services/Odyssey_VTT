using System;
using System.IO;
using System.Text;
using Odyssey.Application.Diagnostics;

namespace Odyssey.Persistence.Diagnostics
{
    public sealed class DiagnosticBundleFileWriter
    {
        public string WriteManifest(string directory, DiagnosticBundleManifest manifest)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Directory is required.", nameof(directory));
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "diagnostic-bundle-manifest.txt");
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("schemaVersion=1");
            builder.AppendLine("diagnosticId=" + manifest.DiagnosticId);
            builder.AppendLine("buildId=" + manifest.BuildId);
            builder.AppendLine("totalStoredBytes=" + manifest.TotalStoredBytes.ToString(System.Globalization.CultureInfo.InvariantCulture));
            for (int index = 0; index < manifest.Entries.Count; index++)
            {
                DiagnosticBundleEntry entry = manifest.Entries[index];
                builder.AppendLine(entry.RelativePath + " " + entry.Status + " " + entry.StoredBytes.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + entry.Sha256);
            }

            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
            return path;
        }
    }
}
