using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Odyssey.Application.Results;
using Odyssey.Application.Serialization;
using Odyssey.Application.Versions;

namespace Odyssey.Tools.BuildIdentity
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                Dictionary<string, string> options = ParseArgs(args);
                string root = FullPath(Required(options, "root"));
                BuildChannel channel = BuildIdentityCodec.ParseChannelToken(Required(options, "channel"));
                if (channel != BuildChannel.Local && channel != BuildChannel.PullRequest && channel != BuildChannel.Development) throw new InvalidOperationException("Only Local, PullRequest and Development channels are supported.");

                string versionPath = Path.Combine(root, "version.json");
                string compatibilityPath = Path.Combine(root, "config", "compatibility.json");
                Result<VersionSource> version = BuildIdentityCodec.ReadVersionSource(File.ReadAllBytes(versionPath));
                Result<CompatibilityConfig> compatibility = BuildIdentityCodec.ReadCompatibilityConfig(File.ReadAllBytes(compatibilityPath));
                if (version.IsFailure) throw new InvalidOperationException("version.json is invalid.");
                if (compatibility.IsFailure) throw new InvalidOperationException("config/compatibility.json is invalid.");
                if (version.Value.ApplicationVersion.ToString() != "0.1.0") throw new InvalidOperationException("ApplicationVersion must remain 0.1.0.");

                string fullSha = RunGit(root, "rev-parse HEAD").Trim();
                string status = RunGit(root, "status --porcelain");
                WorkingTreeState state = string.IsNullOrWhiteSpace(status) ? WorkingTreeState.Clean : WorkingTreeState.Dirty;
                string gitRef = Required(options, "git-ref");
                string timestamp = Required(options, "timestamp-utc");
                long buildNumber = long.Parse(Required(options, "build-number"), CultureInfo.InvariantCulture);
                int runAttempt = int.Parse(Required(options, "run-attempt"), CultureInfo.InvariantCulture);
                long? pullRequestNumber = null;
                if (options.TryGetValue("pull-request-number", out string? prValue) && !string.IsNullOrWhiteSpace(prValue))
                {
                    pullRequestNumber = long.Parse(prValue, CultureInfo.InvariantCulture);
                }

                string projectVersion = File.ReadAllText(Path.Combine(root, "ProjectSettings", "ProjectVersion.txt"));
                string unityVersion = Match(projectVersion, @"m_EditorVersion:\s*(6000\.4\.0f1)");
                string unityChangeset = Match(projectVersion, @"m_EditorVersionWithRevision:\s*6000\.4\.0f1\s+\(([0-9a-f]{12})\)");
                string dotnetSdk = RunProcess(root, "dotnet", "--version").Trim();

                Odyssey.Application.Versions.BuildIdentity identity = BuildIdentityCodec.Create(
                    version.Value,
                    compatibility.Value,
                    channel,
                    buildNumber,
                    runAttempt,
                    fullSha,
                    gitRef,
                    state,
                    timestamp,
                    unityVersion,
                    unityChangeset,
                    dotnetSdk,
                    Required(options, "configuration"),
                    Required(options, "platform"),
                    Required(options, "architecture"),
                    Required(options, "scripting-backend"),
                    Required(options, "api-compatibility"),
                    pullRequestNumber);

                Result<JsonPayload> payload = BuildIdentityCodec.WriteBuildIdentity(identity);
                if (payload.IsFailure) throw new InvalidOperationException("BuildIdentity serialization failed.");

                string identityDirectory = Path.Combine(root, "artifacts", "build-identity", identity.BuildId);
                Directory.CreateDirectory(identityDirectory);
                string jsonPath = Path.Combine(identityDirectory, "build-identity.json");
                File.WriteAllBytes(jsonPath, payload.Value.Bytes);

                string checksumPath = Path.Combine(identityDirectory, "checksums.sha256");
                string digest = CanonicalJson.Sha256LowerHex(payload.Value.Bytes);
                File.WriteAllText(checksumPath, digest + "  build-identity.json" + Environment.NewLine, new UTF8Encoding(false));

                string unityGeneratedDirectory = Path.Combine(root, "Assets", "Odyssey", "Generated");
                string streamingDirectory = Path.Combine(root, "Assets", "StreamingAssets", "Odyssey");
                Directory.CreateDirectory(unityGeneratedDirectory);
                Directory.CreateDirectory(streamingDirectory);
                File.WriteAllText(Path.Combine(unityGeneratedDirectory, "BuildIdentity.g.cs"), GenerateCSharp(identity), new UTF8Encoding(false));
                File.WriteAllBytes(Path.Combine(streamingDirectory, "build-identity.json"), payload.Value.Bytes);

                Console.WriteLine("BuildId=" + identity.BuildId);
                Console.WriteLine("BuildIdentityJson=" + jsonPath);
                Console.WriteLine("Checksums=" + checksumPath);
                Console.WriteLine("CompatibilityConfigDigest=" + identity.CompatibilityConfigDigest);
                Console.WriteLine("ContractRegistryDigest=" + identity.ContractRegistryDigest);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("BuildIdentity generation failed: " + ex.Message);
                return 1;
            }
        }

        private static Dictionary<string, string> ParseArgs(string[] args)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < args.Length; index++)
            {
                string key = args[index];
                if (!key.StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length) throw new ArgumentException("Arguments must be --key value pairs.");
                result[key.Substring(2)] = args[++index];
            }

            return result;
        }

        private static string Required(Dictionary<string, string> options, string name)
        {
            if (!options.TryGetValue(name, out string? value) || string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Missing required option --" + name + ".");
            return value;
        }

        private static string FullPath(string value)
        {
            return Path.GetFullPath(value);
        }

        private static string RunGit(string root, string arguments)
        {
            return RunProcess(root, "git", arguments);
        }

        private static string RunProcess(string root, string fileName, string arguments)
        {
            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = root,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (Process process = Process.Start(start) ?? throw new InvalidOperationException("Process failed to start."))
            {
                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0) throw new InvalidOperationException(fileName + " " + arguments + " failed: " + stderr.Trim());
                return stdout;
            }
        }

        private static string Match(string text, string pattern)
        {
            System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(text, pattern);
            if (!match.Success) throw new InvalidOperationException("Required Unity project version metadata was not found.");
            return match.Groups[1].Value;
        }

        private static string GenerateCSharp(Odyssey.Application.Versions.BuildIdentity identity)
        {
            return
@"// <auto-generated />
using Odyssey.Application.Versions;

namespace Odyssey.Unity.Client
{
    internal static partial class OdysseyGeneratedBuildIdentity
    {
        static partial void GetGenerated(ref BuildIdentity? identity)
        {
            identity = new BuildIdentity(
                ""Odyssey VTT"",
                ApplicationVersion.Parse(""" + identity.ApplicationVersion + @"""),
                """ + identity.DisplayVersion + @""",
                """ + identity.BuildId + @""",
                BuildChannel." + identity.Channel + @",
                " + identity.BuildNumber.ToString(CultureInfo.InvariantCulture) + @"L,
                " + identity.RunAttempt.ToString(CultureInfo.InvariantCulture) + @",
                """ + identity.GitCommitSha + @""",
                """ + identity.GitShortSha + @""",
                """ + identity.GitRef + @""",
                null,
                WorkingTreeState." + identity.WorkingTreeState + @",
                """ + identity.BuildTimestampUtc + @""",
                """ + identity.UnityVersion + @""",
                """ + identity.UnityChangeset + @""",
                """ + identity.DotNetSdkVersion + @""",
                """ + identity.Configuration + @""",
                """ + identity.Platform + @""",
                """ + identity.Architecture + @""",
                """ + identity.ScriptingBackend + @""",
                """ + identity.ApiCompatibilityLevel + @""",
                new CompatibilityConfig(
                    " + GenerateRange(identity.Compatibility.DatabaseSchemaVersion) + @",
                    " + GenerateRange(identity.Compatibility.CampaignFormatVersion) + @",
                    " + GenerateRange(identity.Compatibility.ManifestSchemaVersion) + @",
                    " + GenerateRange(identity.Compatibility.AssetManifestVersion) + @",
                    " + GenerateRange(identity.Compatibility.CommandContractVersion) + @",
                    " + GenerateRange(identity.Compatibility.EventContractVersion) + @",
                    " + GenerateRange(identity.Compatibility.FingerprintVersion) + @",
                    " + GenerateProtocolRange(identity.Compatibility.NetworkProtocolVersion) + @"),
                """ + identity.CompatibilityConfigDigest + @""",
                """ + identity.ContractRegistryDigest + @""",
                false);
        }
    }
}
";
        }

        private static string GenerateRange(CompatibilityRange value)
        {
            return "new CompatibilityRange(" + value.Minimum.ToString(CultureInfo.InvariantCulture) + ", " + value.Current.ToString(CultureInfo.InvariantCulture) + ")";
        }

        private static string GenerateProtocolRange(ProtocolCompatibilityRange value)
        {
            return "new ProtocolCompatibilityRange(" + value.Minimum.ToString(CultureInfo.InvariantCulture) + ", " + value.Preferred.ToString(CultureInfo.InvariantCulture) + ", " + value.Maximum.ToString(CultureInfo.InvariantCulture) + ")";
        }
    }
}
