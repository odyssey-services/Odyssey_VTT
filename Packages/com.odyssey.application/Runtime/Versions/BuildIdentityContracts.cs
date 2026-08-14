using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Odyssey.Application.Results;
using Odyssey.Application.Serialization;

namespace Odyssey.Application.Versions
{
    public enum BuildChannel
    {
        Local = 1,
        PullRequest = 2,
        Development = 3
    }

    public enum WorkingTreeState
    {
        Clean = 1,
        Dirty = 2
    }

    public readonly struct CompatibilityRange
    {
        public CompatibilityRange(int minimum, int current)
        {
            if (minimum <= 0) throw new ArgumentOutOfRangeException(nameof(minimum));
            if (current <= 0) throw new ArgumentOutOfRangeException(nameof(current));
            if (minimum > current) throw new ArgumentException("Minimum cannot exceed current.", nameof(minimum));
            Minimum = minimum;
            Current = current;
        }

        public int Minimum { get; }
        public int Current { get; }
    }

    public readonly struct ProtocolCompatibilityRange
    {
        public ProtocolCompatibilityRange(int minimum, int preferred, int maximum)
        {
            if (minimum <= 0) throw new ArgumentOutOfRangeException(nameof(minimum));
            if (preferred <= 0) throw new ArgumentOutOfRangeException(nameof(preferred));
            if (maximum <= 0) throw new ArgumentOutOfRangeException(nameof(maximum));
            if (minimum > preferred || preferred > maximum) throw new ArgumentException("Protocol range must be minimum <= preferred <= maximum.");
            Minimum = minimum;
            Preferred = preferred;
            Maximum = maximum;
        }

        public int Minimum { get; }
        public int Preferred { get; }
        public int Maximum { get; }
    }

    public sealed class VersionSource
    {
        public VersionSource(ApplicationVersion applicationVersion)
        {
            if (!applicationVersion.IsValid) throw new ArgumentException("ApplicationVersion is required.", nameof(applicationVersion));
            SchemaVersion = 1;
            ApplicationVersion = applicationVersion;
        }

        public int SchemaVersion { get; }
        public ApplicationVersion ApplicationVersion { get; }
    }

    public sealed class CompatibilityConfig
    {
        public CompatibilityConfig(CompatibilityRange databaseSchemaVersion, CompatibilityRange campaignFormatVersion, CompatibilityRange manifestSchemaVersion, CompatibilityRange assetManifestVersion, CompatibilityRange commandContractVersion, CompatibilityRange eventContractVersion, CompatibilityRange fingerprintVersion, ProtocolCompatibilityRange networkProtocolVersion)
        {
            SchemaVersion = 1;
            DatabaseSchemaVersion = databaseSchemaVersion;
            CampaignFormatVersion = campaignFormatVersion;
            ManifestSchemaVersion = manifestSchemaVersion;
            AssetManifestVersion = assetManifestVersion;
            CommandContractVersion = commandContractVersion;
            EventContractVersion = eventContractVersion;
            FingerprintVersion = fingerprintVersion;
            NetworkProtocolVersion = networkProtocolVersion;
        }

        public int SchemaVersion { get; }
        public CompatibilityRange DatabaseSchemaVersion { get; }
        public CompatibilityRange CampaignFormatVersion { get; }
        public CompatibilityRange ManifestSchemaVersion { get; }
        public CompatibilityRange AssetManifestVersion { get; }
        public CompatibilityRange CommandContractVersion { get; }
        public CompatibilityRange EventContractVersion { get; }
        public CompatibilityRange FingerprintVersion { get; }
        public ProtocolCompatibilityRange NetworkProtocolVersion { get; }
    }

    public sealed class BuildIdentity
    {
        public BuildIdentity(
            string productName,
            ApplicationVersion applicationVersion,
            string displayVersion,
            string buildId,
            BuildChannel channel,
            long buildNumber,
            int runAttempt,
            string gitCommitSha,
            string gitShortSha,
            string gitRef,
            string? gitTag,
            WorkingTreeState workingTreeState,
            string buildTimestampUtc,
            string unityVersion,
            string unityChangeset,
            string dotNetSdkVersion,
            string configuration,
            string platform,
            string architecture,
            string scriptingBackend,
            string apiCompatibilityLevel,
            CompatibilityConfig compatibility,
            string compatibilityConfigDigest,
            string contractRegistryDigest,
            bool release)
        {
            if (productName != "Odyssey VTT") throw new ArgumentException("ProductName must be Odyssey VTT.", nameof(productName));
            if (!applicationVersion.IsValid) throw new ArgumentException("ApplicationVersion is required.", nameof(applicationVersion));
            if (!BuildIdentityText.IsDisplayVersion(displayVersion)) throw new ArgumentException("DisplayVersion is invalid.", nameof(displayVersion));
            if (!BuildIdentityText.IsBuildId(buildId)) throw new ArgumentException("BuildId is invalid.", nameof(buildId));
            if (!Enum.IsDefined(typeof(BuildChannel), channel)) throw new ArgumentOutOfRangeException(nameof(channel));
            if (buildNumber <= 0) throw new ArgumentOutOfRangeException(nameof(buildNumber));
            if (runAttempt <= 0) throw new ArgumentOutOfRangeException(nameof(runAttempt));
            if (!BuildIdentityText.IsFullSha(gitCommitSha)) throw new ArgumentException("GitCommitSha is invalid.", nameof(gitCommitSha));
            if (!BuildIdentityText.IsShortSha(gitShortSha) || !gitCommitSha.StartsWith(gitShortSha, StringComparison.Ordinal)) throw new ArgumentException("GitShortSha is invalid.", nameof(gitShortSha));
            if (!BuildIdentityText.IsSafeRef(gitRef)) throw new ArgumentException("GitRef is invalid.", nameof(gitRef));
            if (gitTag != null && !BuildIdentityText.IsSafeRef(gitTag)) throw new ArgumentException("GitTag is invalid.", nameof(gitTag));
            if (!Enum.IsDefined(typeof(WorkingTreeState), workingTreeState)) throw new ArgumentOutOfRangeException(nameof(workingTreeState));
            if (!BuildIdentityText.IsUtcTimestamp(buildTimestampUtc)) throw new ArgumentException("Build timestamp is invalid.", nameof(buildTimestampUtc));
            if (!BuildIdentityText.IsSafeToken(unityVersion, 32)) throw new ArgumentException("UnityVersion is invalid.", nameof(unityVersion));
            if (!BuildIdentityText.IsShortSha(unityChangeset, 12)) throw new ArgumentException("UnityChangeset is invalid.", nameof(unityChangeset));
            if (!BuildIdentityText.IsSafeToken(dotNetSdkVersion, 32)) throw new ArgumentException("DotNetSdkVersion is invalid.", nameof(dotNetSdkVersion));
            if (!BuildIdentityText.IsSafeToken(configuration, 64) || !BuildIdentityText.IsSafeToken(platform, 64) || !BuildIdentityText.IsSafeToken(architecture, 32) || !BuildIdentityText.IsSafeToken(scriptingBackend, 32) || !BuildIdentityText.IsSafeToken(apiCompatibilityLevel, 64)) throw new ArgumentException("Toolchain target metadata is invalid.");
            if (compatibility == null) throw new ArgumentNullException(nameof(compatibility));
            if (!BuildIdentityText.IsSha256(compatibilityConfigDigest)) throw new ArgumentException("CompatibilityConfigDigest is invalid.", nameof(compatibilityConfigDigest));
            if (!BuildIdentityText.IsSha256(contractRegistryDigest)) throw new ArgumentException("ContractRegistryDigest is invalid.", nameof(contractRegistryDigest));
            if (release) throw new ArgumentException("Release identity is out of scope for ODY-S00-008.", nameof(release));

            ProductName = productName;
            ApplicationVersion = applicationVersion;
            DisplayVersion = displayVersion;
            BuildId = buildId;
            Channel = channel;
            BuildNumber = buildNumber;
            RunAttempt = runAttempt;
            GitCommitSha = gitCommitSha;
            GitShortSha = gitShortSha;
            GitRef = gitRef;
            GitTag = gitTag;
            WorkingTreeState = workingTreeState;
            BuildTimestampUtc = buildTimestampUtc;
            UnityVersion = unityVersion;
            UnityChangeset = unityChangeset;
            DotNetSdkVersion = dotNetSdkVersion;
            Configuration = configuration;
            Platform = platform;
            Architecture = architecture;
            ScriptingBackend = scriptingBackend;
            ApiCompatibilityLevel = apiCompatibilityLevel;
            Compatibility = compatibility;
            CompatibilityConfigDigest = compatibilityConfigDigest;
            ContractRegistryDigest = contractRegistryDigest;
            Release = release;
        }

        public int SchemaVersion => 1;
        public string ProductName { get; }
        public ApplicationVersion ApplicationVersion { get; }
        public string DisplayVersion { get; }
        public string BuildId { get; }
        public BuildChannel Channel { get; }
        public long BuildNumber { get; }
        public int RunAttempt { get; }
        public string GitCommitSha { get; }
        public string GitShortSha { get; }
        public string GitRef { get; }
        public string? GitTag { get; }
        public WorkingTreeState WorkingTreeState { get; }
        public string BuildTimestampUtc { get; }
        public string UnityVersion { get; }
        public string UnityChangeset { get; }
        public string DotNetSdkVersion { get; }
        public string Configuration { get; }
        public string Platform { get; }
        public string Architecture { get; }
        public string ScriptingBackend { get; }
        public string ApiCompatibilityLevel { get; }
        public CompatibilityConfig Compatibility { get; }
        public string CompatibilityConfigDigest { get; }
        public string ContractRegistryDigest { get; }
        public bool Release { get; }
    }

    public static class BuildIdentityCodec
    {
        public const int VersionSourceMaxBytes = 4096;
        public const int CompatibilityConfigMaxBytes = 16384;
        public const int BuildIdentityMaxBytes = 65536;

        public static Result<VersionSource> ReadVersionSource(byte[] utf8Json)
        {
            Result<JsonObjectReader> reader = JsonObjectReader.Read(utf8Json, VersionSourceMaxBytes, 8);
            if (reader.IsFailure) return Result<VersionSource>.Failure(VersioningFailures.InvalidSource());
            if (reader.Value.EnsureOnly("schemaVersion", "applicationVersion").IsFailure) return Result<VersionSource>.Failure(VersioningFailures.InvalidSource());
            Result<int> schema = reader.Value.RequiredInt32("schemaVersion");
            Result<string> app = reader.Value.RequiredString("applicationVersion");
            if (schema.IsFailure || app.IsFailure || schema.Value != 1 || !ApplicationVersion.TryParse(app.Value, out ApplicationVersion version))
            {
                return Result<VersionSource>.Failure(VersioningFailures.InvalidSource());
            }

            return Result<VersionSource>.Success(new VersionSource(version));
        }

        public static Result<CompatibilityConfig> ReadCompatibilityConfig(byte[] utf8Json)
        {
            if (utf8Json == null) throw new ArgumentNullException(nameof(utf8Json));
            if (utf8Json.Length == 0 || utf8Json.Length > CompatibilityConfigMaxBytes) return Result<CompatibilityConfig>.Failure(VersioningFailures.InvalidSource());
            if (HasUtf8Bom(utf8Json)) return Result<CompatibilityConfig>.Failure(VersioningFailures.InvalidSource());

            try
            {
                string json = new UTF8Encoding(false, true).GetString(utf8Json);
                JsonTextReader reader = new JsonTextReader(new StringReader(json))
                {
                    DateParseHandling = DateParseHandling.None,
                    FloatParseHandling = FloatParseHandling.Decimal,
                    MaxDepth = 8
                };
                Dictionary<string, object?> values = ReadRootObject(reader);
                EnsureOnly(values, "schemaVersion", "databaseSchemaVersion", "campaignFormatVersion", "manifestSchemaVersion", "assetManifestVersion", "commandContractVersion", "eventContractVersion", "fingerprintVersion", "networkProtocolVersion");
                if (ReadInt(values, "schemaVersion") != 1) return Result<CompatibilityConfig>.Failure(VersioningFailures.InvalidSource());
                return Result<CompatibilityConfig>.Success(new CompatibilityConfig(
                    ReadRange(values, "databaseSchemaVersion"),
                    ReadRange(values, "campaignFormatVersion"),
                    ReadRange(values, "manifestSchemaVersion"),
                    ReadRange(values, "assetManifestVersion"),
                    ReadRange(values, "commandContractVersion"),
                    ReadRange(values, "eventContractVersion"),
                    ReadRange(values, "fingerprintVersion"),
                    ReadProtocolRange(values, "networkProtocolVersion")));
            }
            catch (JsonException)
            {
                return Result<CompatibilityConfig>.Failure(VersioningFailures.InvalidSource());
            }
            catch (DecoderFallbackException)
            {
                return Result<CompatibilityConfig>.Failure(VersioningFailures.InvalidSource());
            }
            catch (ArgumentException)
            {
                return Result<CompatibilityConfig>.Failure(VersioningFailures.InvalidSource());
            }
            catch (InvalidOperationException)
            {
                return Result<CompatibilityConfig>.Failure(VersioningFailures.InvalidSource());
            }
        }

        public static JsonPayload WriteCompatibilityConfig(CompatibilityConfig value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            CanonicalJsonWriter writer = new CanonicalJsonWriter();
            writer.StartObject()
                .Int32("schemaVersion", 1)
                .RawJson("databaseSchemaVersion", WriteRange(value.DatabaseSchemaVersion))
                .RawJson("campaignFormatVersion", WriteRange(value.CampaignFormatVersion))
                .RawJson("manifestSchemaVersion", WriteRange(value.ManifestSchemaVersion))
                .RawJson("assetManifestVersion", WriteRange(value.AssetManifestVersion))
                .RawJson("commandContractVersion", WriteRange(value.CommandContractVersion))
                .RawJson("eventContractVersion", WriteRange(value.EventContractVersion))
                .RawJson("fingerprintVersion", WriteRange(value.FingerprintVersion))
                .RawJson("networkProtocolVersion", WriteProtocolRange(value.NetworkProtocolVersion))
                .EndObject();
            return writer.ToPayload();
        }

        public static string ComputeCompatibilityDigest(CompatibilityConfig value)
        {
            return CanonicalJson.Sha256LowerHex(WriteCompatibilityConfig(value).Bytes);
        }

        public static string ComputeContractRegistryDigest()
        {
            string canonicalContracts = "odyssey.build-identity:1\nodyssey.compatibility-config:1\nodyssey.diagnostics.bundle-manifest:1\nodyssey.version-source:1\n";
            return CanonicalJson.Sha256LowerHex(CanonicalJson.ToUtf8Bytes(canonicalContracts));
        }

        public static Result<JsonPayload> WriteBuildIdentity(BuildIdentity value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            CanonicalJsonWriter writer = new CanonicalJsonWriter();
            writer.StartObject()
                .Int32("schemaVersion", value.SchemaVersion)
                .String("productName", value.ProductName)
                .String("applicationVersion", value.ApplicationVersion.ToString())
                .String("displayVersion", value.DisplayVersion)
                .String("buildId", value.BuildId)
                .String("channel", ToChannelToken(value.Channel))
                .Int64("buildNumber", value.BuildNumber)
                .Int32("runAttempt", value.RunAttempt)
                .String("gitCommitSha", value.GitCommitSha)
                .String("gitShortSha", value.GitShortSha)
                .String("gitRef", value.GitRef)
                .NullableString("gitTag", value.GitTag)
                .String("workingTreeState", ToWorkingTreeStateToken(value.WorkingTreeState))
                .String("buildTimestampUtc", value.BuildTimestampUtc)
                .String("unityVersion", value.UnityVersion)
                .String("unityChangeset", value.UnityChangeset)
                .String("dotNetSdkVersion", value.DotNetSdkVersion)
                .String("configuration", value.Configuration)
                .String("platform", value.Platform)
                .String("architecture", value.Architecture)
                .String("scriptingBackend", value.ScriptingBackend)
                .String("apiCompatibilityLevel", value.ApiCompatibilityLevel)
                .RawJson("compatibility", WriteCompatibilityConfig(value.Compatibility).Utf8Text)
                .String("compatibilityConfigDigest", value.CompatibilityConfigDigest)
                .String("contractRegistryDigest", value.ContractRegistryDigest)
                .Boolean("release", value.Release)
                .EndObject();
            return Result<JsonPayload>.Success(writer.ToPayload());
        }

        public static Result<BuildIdentity> ReadBuildIdentity(byte[] utf8Json)
        {
            if (utf8Json == null) throw new ArgumentNullException(nameof(utf8Json));
            if (utf8Json.Length == 0 || utf8Json.Length > BuildIdentityMaxBytes) return Result<BuildIdentity>.Failure(VersioningFailures.InvalidSource());
            if (HasUtf8Bom(utf8Json)) return Result<BuildIdentity>.Failure(VersioningFailures.InvalidSource());

            try
            {
                string json = new UTF8Encoding(false, true).GetString(utf8Json);
                JsonTextReader reader = new JsonTextReader(new StringReader(json))
                {
                    DateParseHandling = DateParseHandling.None,
                    FloatParseHandling = FloatParseHandling.Decimal,
                    MaxDepth = 16
                };
                Dictionary<string, object?> values = ReadRootObject(reader);
                EnsureOnly(values, "schemaVersion", "productName", "applicationVersion", "displayVersion", "buildId", "channel", "buildNumber", "runAttempt", "gitCommitSha", "gitShortSha", "gitRef", "gitTag", "workingTreeState", "buildTimestampUtc", "unityVersion", "unityChangeset", "dotNetSdkVersion", "configuration", "platform", "architecture", "scriptingBackend", "apiCompatibilityLevel", "compatibility", "compatibilityConfigDigest", "contractRegistryDigest", "release");
                if (ReadInt(values, "schemaVersion") != 1) return Result<BuildIdentity>.Failure(VersioningFailures.InvalidSource());
                ApplicationVersion app = ApplicationVersion.Parse(ReadString(values, "applicationVersion"));
                BuildChannel channel = ParseChannelToken(ReadString(values, "channel"));
                WorkingTreeState state = ParseWorkingTreeStateToken(ReadString(values, "workingTreeState"));
                CompatibilityConfig compatibility = ReadCompatibility(values, "compatibility");

                return Result<BuildIdentity>.Success(new BuildIdentity(
                    ReadString(values, "productName"),
                    app,
                    ReadString(values, "displayVersion"),
                    ReadString(values, "buildId"),
                    channel,
                    ReadLong(values, "buildNumber"),
                    ReadInt(values, "runAttempt"),
                    ReadString(values, "gitCommitSha"),
                    ReadString(values, "gitShortSha"),
                    ReadString(values, "gitRef"),
                    ReadNullableString(values, "gitTag"),
                    state,
                    ReadString(values, "buildTimestampUtc"),
                    ReadString(values, "unityVersion"),
                    ReadString(values, "unityChangeset"),
                    ReadString(values, "dotNetSdkVersion"),
                    ReadString(values, "configuration"),
                    ReadString(values, "platform"),
                    ReadString(values, "architecture"),
                    ReadString(values, "scriptingBackend"),
                    ReadString(values, "apiCompatibilityLevel"),
                    compatibility,
                    ReadString(values, "compatibilityConfigDigest"),
                    ReadString(values, "contractRegistryDigest"),
                    ReadBool(values, "release")));
            }
            catch (JsonException)
            {
                return Result<BuildIdentity>.Failure(VersioningFailures.InvalidSource());
            }
            catch (DecoderFallbackException)
            {
                return Result<BuildIdentity>.Failure(VersioningFailures.InvalidSource());
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is FormatException)
            {
                return Result<BuildIdentity>.Failure(VersioningFailures.InvalidSource());
            }
        }

        public static BuildIdentity Create(
            VersionSource version,
            CompatibilityConfig compatibility,
            BuildChannel channel,
            long buildNumber,
            int runAttempt,
            string gitCommitSha,
            string gitRef,
            WorkingTreeState workingTreeState,
            string buildTimestampUtc,
            string unityVersion,
            string unityChangeset,
            string dotNetSdkVersion,
            string configuration,
            string platform,
            string architecture,
            string scriptingBackend,
            string apiCompatibilityLevel,
            long? pullRequestNumber = null)
        {
            if (version == null) throw new ArgumentNullException(nameof(version));
            if (compatibility == null) throw new ArgumentNullException(nameof(compatibility));
            string shortSha = gitCommitSha.Substring(0, 12);
            string channelText = ToChannelToken(channel);
            string display;
            string buildId;
            switch (channel)
            {
                case BuildChannel.Local:
                    string dirtySuffix = workingTreeState == WorkingTreeState.Dirty ? ".dirty" : string.Empty;
                    string dirtyBuildSuffix = workingTreeState == WorkingTreeState.Dirty ? "-dirty" : string.Empty;
                    display = version.ApplicationVersion + "-local." + buildTimestampUtc + "+g" + shortSha + dirtySuffix;
                    buildId = "odyssey-local-" + buildTimestampUtc.ToLowerInvariant() + "-g" + shortSha + dirtyBuildSuffix;
                    break;
                case BuildChannel.PullRequest:
                    long displayNumber = pullRequestNumber.GetValueOrDefault(buildNumber);
                    if (displayNumber <= 0) throw new ArgumentOutOfRangeException(nameof(pullRequestNumber));
                    display = version.ApplicationVersion + "-pr." + displayNumber.ToString(CultureInfo.InvariantCulture) + "." + runAttempt.ToString(CultureInfo.InvariantCulture) + "+g" + shortSha;
                    buildId = "odyssey-pr-" + buildNumber.ToString(CultureInfo.InvariantCulture) + "." + runAttempt.ToString(CultureInfo.InvariantCulture) + "-g" + shortSha;
                    break;
                case BuildChannel.Development:
                    display = version.ApplicationVersion + "-dev." + buildNumber.ToString(CultureInfo.InvariantCulture) + "." + runAttempt.ToString(CultureInfo.InvariantCulture) + "+g" + shortSha;
                    buildId = "odyssey-development-" + buildNumber.ToString(CultureInfo.InvariantCulture) + "." + runAttempt.ToString(CultureInfo.InvariantCulture) + "-g" + shortSha;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(channel));
            }

            return new BuildIdentity("Odyssey VTT", version.ApplicationVersion, display, buildId, channel, buildNumber, runAttempt, gitCommitSha, shortSha, gitRef, null, workingTreeState, buildTimestampUtc, unityVersion, unityChangeset, dotNetSdkVersion, configuration, platform, architecture, scriptingBackend, apiCompatibilityLevel, compatibility, ComputeCompatibilityDigest(compatibility), ComputeContractRegistryDigest(), false);
        }

        private static Dictionary<string, object?> ReadRootObject(JsonTextReader reader)
        {
            if (!reader.Read() || reader.TokenType != JsonToken.StartObject) throw new InvalidOperationException("Expected root object.");
            Dictionary<string, object?> result = ReadObject(reader);
            if (reader.Read()) throw new InvalidOperationException("Trailing content.");
            return result;
        }

        private static Dictionary<string, object?> ReadObject(JsonTextReader reader)
        {
            Dictionary<string, object?> values = new Dictionary<string, object?>(StringComparer.Ordinal);
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                {
                    return values;
                }

                if (reader.TokenType == JsonToken.Comment || reader.TokenType != JsonToken.PropertyName) throw new InvalidOperationException("Expected property.");
                string name = (string)reader.Value!;
                if (!SerializationText.IsLowerCamelProperty(name) || values.ContainsKey(name)) throw new InvalidOperationException("Invalid property.");
                if (!reader.Read()) throw new InvalidOperationException("Missing property value.");
                if (reader.TokenType == JsonToken.StartObject) values.Add(name, ReadObject(reader));
                else if (reader.TokenType == JsonToken.String) values.Add(name, (string)reader.Value!);
                else if (reader.TokenType == JsonToken.Integer) values.Add(name, Convert.ToInt64(reader.Value, CultureInfo.InvariantCulture));
                else if (reader.TokenType == JsonToken.Boolean) values.Add(name, (bool)reader.Value!);
                else if (reader.TokenType == JsonToken.Null) values.Add(name, null);
                else throw new InvalidOperationException("Unsupported value.");
            }

            throw new InvalidOperationException("Unclosed object.");
        }

        private static void EnsureOnly(Dictionary<string, object?> values, params string[] allowedNames)
        {
            HashSet<string> allowed = new HashSet<string>(allowedNames, StringComparer.Ordinal);
            foreach (string key in values.Keys)
            {
                if (!allowed.Contains(key)) throw new InvalidOperationException("Unknown property.");
            }
        }

        private static CompatibilityConfig ReadCompatibility(Dictionary<string, object?> values, string name)
        {
            Dictionary<string, object?> compatibility = ReadNested(values, name);
            EnsureOnly(compatibility, "schemaVersion", "databaseSchemaVersion", "campaignFormatVersion", "manifestSchemaVersion", "assetManifestVersion", "commandContractVersion", "eventContractVersion", "fingerprintVersion", "networkProtocolVersion");
            if (ReadInt(compatibility, "schemaVersion") != 1) throw new InvalidOperationException("Unknown compatibility schema.");
            return new CompatibilityConfig(
                ReadRange(compatibility, "databaseSchemaVersion"),
                ReadRange(compatibility, "campaignFormatVersion"),
                ReadRange(compatibility, "manifestSchemaVersion"),
                ReadRange(compatibility, "assetManifestVersion"),
                ReadRange(compatibility, "commandContractVersion"),
                ReadRange(compatibility, "eventContractVersion"),
                ReadRange(compatibility, "fingerprintVersion"),
                ReadProtocolRange(compatibility, "networkProtocolVersion"));
        }

        private static CompatibilityRange ReadRange(Dictionary<string, object?> values, string name)
        {
            Dictionary<string, object?> range = ReadNested(values, name);
            EnsureOnly(range, "minimum", "current");
            return new CompatibilityRange(ReadInt(range, "minimum"), ReadInt(range, "current"));
        }

        private static ProtocolCompatibilityRange ReadProtocolRange(Dictionary<string, object?> values, string name)
        {
            Dictionary<string, object?> range = ReadNested(values, name);
            EnsureOnly(range, "minimum", "preferred", "maximum");
            return new ProtocolCompatibilityRange(ReadInt(range, "minimum"), ReadInt(range, "preferred"), ReadInt(range, "maximum"));
        }

        private static Dictionary<string, object?> ReadNested(Dictionary<string, object?> values, string name)
        {
            if (!values.TryGetValue(name, out object? value) || !(value is Dictionary<string, object?> nested)) throw new InvalidOperationException("Missing object.");
            return nested;
        }

        private static int ReadInt(Dictionary<string, object?> values, string name)
        {
            long result = ReadLong(values, name);
            if (result < int.MinValue || result > int.MaxValue) throw new InvalidOperationException("Integer out of range.");
            return (int)result;
        }

        private static long ReadLong(Dictionary<string, object?> values, string name)
        {
            if (!values.TryGetValue(name, out object? value) || !(value is long result)) throw new InvalidOperationException("Missing integer.");
            return result;
        }

        private static bool ReadBool(Dictionary<string, object?> values, string name)
        {
            if (!values.TryGetValue(name, out object? value) || !(value is bool result)) throw new InvalidOperationException("Missing boolean.");
            return result;
        }

        private static string ReadString(Dictionary<string, object?> values, string name)
        {
            if (!values.TryGetValue(name, out object? value) || !(value is string result)) throw new InvalidOperationException("Missing string.");
            return result;
        }

        private static string? ReadNullableString(Dictionary<string, object?> values, string name)
        {
            if (!values.ContainsKey(name)) throw new InvalidOperationException("Missing nullable string.");
            object? value = values[name];
            if (value == null) return null;
            if (value is string result) return result;
            throw new InvalidOperationException("Invalid nullable string.");
        }

        private static string WriteRange(CompatibilityRange value)
        {
            return "{\"minimum\":" + value.Minimum.ToString(CultureInfo.InvariantCulture) + ",\"current\":" + value.Current.ToString(CultureInfo.InvariantCulture) + "}";
        }

        private static string WriteProtocolRange(ProtocolCompatibilityRange value)
        {
            return "{\"minimum\":" + value.Minimum.ToString(CultureInfo.InvariantCulture) + ",\"preferred\":" + value.Preferred.ToString(CultureInfo.InvariantCulture) + ",\"maximum\":" + value.Maximum.ToString(CultureInfo.InvariantCulture) + "}";
        }

        public static string ToChannelToken(BuildChannel channel)
        {
            switch (channel)
            {
                case BuildChannel.Local: return "local";
                case BuildChannel.PullRequest: return "pull_request";
                case BuildChannel.Development: return "development";
                default: throw new ArgumentOutOfRangeException(nameof(channel));
            }
        }

        public static BuildChannel ParseChannelToken(string value)
        {
            switch (value)
            {
                case "local": return BuildChannel.Local;
                case "pull_request": return BuildChannel.PullRequest;
                case "development": return BuildChannel.Development;
                default: throw new FormatException("Unknown build channel.");
            }
        }

        public static string ToWorkingTreeStateToken(WorkingTreeState state)
        {
            switch (state)
            {
                case WorkingTreeState.Clean: return "clean";
                case WorkingTreeState.Dirty: return "dirty";
                default: throw new ArgumentOutOfRangeException(nameof(state));
            }
        }

        public static WorkingTreeState ParseWorkingTreeStateToken(string value)
        {
            switch (value)
            {
                case "clean": return WorkingTreeState.Clean;
                case "dirty": return WorkingTreeState.Dirty;
                default: throw new FormatException("Unknown working tree state.");
            }
        }

        private static bool HasUtf8Bom(byte[] value)
        {
            return value.Length >= 3 && value[0] == 0xEF && value[1] == 0xBB && value[2] == 0xBF;
        }
    }

    public static class BuildIdentityText
    {
        public static bool IsUtcTimestamp(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length != 16 || !value.EndsWith("Z", StringComparison.Ordinal)) return false;
            return value[8] == 'T' && IsDigits(value, 0, 8) && IsDigits(value, 9, 6);
        }

        public static bool IsFullSha(string? value)
        {
            return IsHex(value, 40);
        }

        public static bool IsShortSha(string? value, int minLength = 12)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length < minLength || value.Length > 40) return false;
            return IsHex(value, value.Length);
        }

        public static bool IsSha256(string? value)
        {
            return IsHex(value, 64);
        }

        public static bool IsSafeToken(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length > maxLength || value.Trim() != value) return false;
            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-')) return false;
            }

            return true;
        }

        public static bool IsSafeRef(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length > 160 || value.Trim() != value) return false;
            if (value.Contains("..") || value.Contains("\\") || value.Contains(" ") || value.StartsWith("/", StringComparison.Ordinal) || value.EndsWith("/", StringComparison.Ordinal)) return false;
            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-' || c == '/')) return false;
            }

            return true;
        }

        public static bool IsBuildId(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length > 160 || value.Trim() != value) return false;
            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '-')) return false;
            }

            return value.StartsWith("odyssey-", StringComparison.Ordinal);
        }

        public static bool IsDisplayVersion(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length > 160 || value.Trim() != value || value.Contains(" ")) return false;
            return value.StartsWith("0.1.0-", StringComparison.Ordinal) && value.Contains("+g");
        }

        private static bool IsDigits(string value, int start, int count)
        {
            for (int index = start; index < start + count; index++)
            {
                if (value[index] < '0' || value[index] > '9') return false;
            }

            return true;
        }

        private static bool IsHex(string? value, int length)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length != length) return false;
            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            }

            return true;
        }
    }

    internal static class VersioningFailures
    {
        private static readonly Odyssey.Domain.Identity.CorrelationId CorrelationId = Odyssey.Domain.Identity.CorrelationId.Parse("corr_00000000000000000000000000000000");

        internal static Error InvalidSource()
        {
            return Error.Create(
                ErrorCodes.VersioningInvalidSource,
                ErrorCategory.Validation,
                SafeReasonCode.InvalidRequest,
                UserMessageKey.Parse("errors.versioning.invalid_source"),
                RetryDirective.UserActionRequired,
                CorrelationId);
        }
    }
}
