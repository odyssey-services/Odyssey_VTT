using System;
using System.Collections;
using System.IO;
using System.Text;
using Odyssey.Application.Versions;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    internal sealed class PlayerSmokeMode
    {
        private const string SmokeArg = "--odyssey-player-smoke";
        private const string EvidenceArg = "--odyssey-smoke-evidence";
        private const string RunNumberArg = "--odyssey-smoke-run";

        private readonly PlayerSmokeEvidence _evidence;
        private readonly string _evidencePath;

        private PlayerSmokeMode(string evidencePath, int runNumber)
        {
            _evidencePath = evidencePath;
            _evidence = new PlayerSmokeEvidence
            {
                SchemaVersion = 1,
                RunNumber = runNumber,
                StartedUtc = UtcNow(),
                Result = "running"
            };
        }

        internal static bool TryCreateFromCommandLine(out PlayerSmokeMode? smokeMode)
        {
            smokeMode = null;
            string[] args = Environment.GetCommandLineArgs();
            bool enabled = false;
            string? evidenceName = null;
            int runNumber = 0;
            for (int index = 0; index < args.Length; index++)
            {
                if (args[index] == SmokeArg)
                {
                    enabled = true;
                    continue;
                }

                if (args[index] == EvidenceArg && index + 1 < args.Length)
                {
                    evidenceName = args[++index];
                    continue;
                }

                if (args[index] == RunNumberArg && index + 1 < args.Length)
                {
                    int.TryParse(args[++index], out runNumber);
                }
            }

            if (!enabled) return false;
            if (!IsSafeEvidenceFileName(evidenceName) || runNumber <= 0) return false;
            string path = Path.Combine(Directory.GetCurrentDirectory(), evidenceName!);
            smokeMode = new PlayerSmokeMode(path, runNumber);
            smokeMode.WriteEvidence();
            return true;
        }

        internal IEnumerator Run(AppRuntime runtime, AppShellEntryPoint entryPoint)
        {
            yield return null;

            BuildIdentity? identity = runtime.BuildIdentity;
            _evidence.BuildId = identity?.BuildId ?? string.Empty;
            _evidence.GitCommitSha = identity?.GitCommitSha ?? string.Empty;
            _evidence.BootstrapReady = runtime.State == OdysseyRuntimeState.Ready;
            _evidence.AppShellLoaded = SceneManager.GetSceneByName("AppShell").isLoaded;
            _evidence.HdrpActive = IsHdrpActive();
            _evidence.UiToolkitRootDisplayed = entryPoint.HasDisplayedUiRoot;
            _evidence.BuildIdentityLoaded = identity != null && identity.Channel == BuildChannel.Development;
            _evidence.BootstrapReadyUtc = UtcNow();
            WriteEvidence();

            PlayerSmokeInputResult inputResult = entryPoint.RunPlayerSmokeInputProbe();
            _evidence.SubmitPerformed = inputResult.SubmitPerformed;
            _evidence.CancelPerformed = inputResult.CancelPerformed;
            _evidence.ShutdownRequestedUtc = UtcNow();
            _evidence.CompletedUtc = _evidence.ShutdownRequestedUtc;
            _evidence.Result = IsPass() ? "pass" : "fail";
            WriteEvidence();

            UnityEngine.Application.Quit(IsPass() ? 0 : 1);
        }

        private bool IsPass()
        {
            return _evidence.BootstrapReady
                && _evidence.AppShellLoaded
                && _evidence.HdrpActive
                && _evidence.UiToolkitRootDisplayed
                && _evidence.SubmitPerformed
                && _evidence.CancelPerformed
                && _evidence.BuildIdentityLoaded
                && !string.IsNullOrWhiteSpace(_evidence.BuildId)
                && !string.IsNullOrWhiteSpace(_evidence.GitCommitSha);
        }

        private static bool IsHdrpActive()
        {
            RenderPipelineAsset? pipeline = GraphicsSettings.currentRenderPipeline;
            string typeName = pipeline?.GetType().FullName ?? string.Empty;
            return typeName.IndexOf("HighDefinition", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void WriteEvidence()
        {
            string json = _evidence.ToJson();
            string tempPath = _evidencePath + ".tmp";
            File.WriteAllText(tempPath, json, new UTF8Encoding(false));
            if (File.Exists(_evidencePath))
            {
                File.Delete(_evidencePath);
            }

            File.Move(tempPath, _evidencePath);
        }

        private static string UtcNow()
        {
            return DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static bool IsSafeEvidenceFileName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (value != Path.GetFileName(value)) return false;
            if (!value.EndsWith(".json", StringComparison.Ordinal)) return false;
            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '.')) return false;
            }

            return true;
        }

        private sealed class PlayerSmokeEvidence
        {
            internal int SchemaVersion;
            internal string BuildId = string.Empty;
            internal string GitCommitSha = string.Empty;
            internal int RunNumber;
            internal string StartedUtc = string.Empty;
            internal string BootstrapReadyUtc = string.Empty;
            internal string ShutdownRequestedUtc = string.Empty;
            internal string CompletedUtc = string.Empty;
            internal bool BootstrapReady;
            internal bool AppShellLoaded;
            internal bool HdrpActive;
            internal bool UiToolkitRootDisplayed;
            internal bool SubmitPerformed;
            internal bool CancelPerformed;
            internal bool BuildIdentityLoaded;
            internal string Result = string.Empty;

            internal string ToJson()
            {
                StringBuilder builder = new StringBuilder();
                builder.Append('{')
                    .Append("\"schemaVersion\":").Append(SchemaVersion).Append(',')
                    .Append("\"buildId\":\"").Append(BuildId).Append("\",")
                    .Append("\"gitCommitSha\":\"").Append(GitCommitSha).Append("\",")
                    .Append("\"runNumber\":").Append(RunNumber).Append(',')
                    .Append("\"startedUtc\":\"").Append(StartedUtc).Append("\",")
                    .Append("\"bootstrapReadyUtc\":\"").Append(BootstrapReadyUtc).Append("\",")
                    .Append("\"shutdownRequestedUtc\":\"").Append(ShutdownRequestedUtc).Append("\",")
                    .Append("\"completedUtc\":\"").Append(CompletedUtc).Append("\",")
                    .Append("\"bootstrapReady\":").Append(Bool(BootstrapReady)).Append(',')
                    .Append("\"appShellLoaded\":").Append(Bool(AppShellLoaded)).Append(',')
                    .Append("\"hdrpActive\":").Append(Bool(HdrpActive)).Append(',')
                    .Append("\"uiToolkitRootDisplayed\":").Append(Bool(UiToolkitRootDisplayed)).Append(',')
                    .Append("\"submitPerformed\":").Append(Bool(SubmitPerformed)).Append(',')
                    .Append("\"cancelPerformed\":").Append(Bool(CancelPerformed)).Append(',')
                    .Append("\"buildIdentityLoaded\":").Append(Bool(BuildIdentityLoaded)).Append(',')
                    .Append("\"result\":\"").Append(Result).Append("\"")
                    .Append('}');
                return builder.ToString();
            }

            private static string Bool(bool value) => value ? "true" : "false";
        }
    }
}
