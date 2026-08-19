using System;
using System.Collections.Generic;
using System.IO;
using Odyssey.Application.Versions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Odyssey.Unity.Client.Editor
{
    public static class OdysseyDevelopmentBuild
    {
        private const string ExpectedUnityVersion = "6000.4.0f1";

        public static void Build()
        {
            Dictionary<string, string> args = ParseCommandLine();
            string outputPath = Required(args, "-odysseyBuildOutput");
            if (!string.Equals(UnityEngine.Application.unityVersion, ExpectedUnityVersion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Unity Editor version must be " + ExpectedUnityVersion + ".");
            }

            outputPath = ResolveValidatedBuildOutputPath(outputPath);
            string outputDirectory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(outputDirectory)) throw new InvalidOperationException("Build output directory is required.");
            Directory.CreateDirectory(outputDirectory);

            BuildTarget originalTarget = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup originalGroup = BuildPipeline.GetBuildTargetGroup(originalTarget);
            ScriptingImplementation originalBackend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone);
            string originalBundleVersion = PlayerSettings.bundleVersion;
            try
            {
                if (originalTarget != BuildTarget.StandaloneWindows64)
                {
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
                }

                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
                PlayerSettings.bundleVersion = "0.1.0";
                BuildPlayerOptions options = new BuildPlayerOptions
                {
                    scenes = new[]
                    {
                        "Assets/Odyssey/Client/Scenes/Bootstrap.unity",
                        "Assets/Odyssey/Client/Scenes/AppShell.unity"
                    },
                    locationPathName = outputPath,
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.Development | BuildOptions.AllowDebugging
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException("Unity build failed with result " + report.summary.result + ".");
                }

                if (!File.Exists(outputPath))
                {
                    throw new InvalidOperationException("Unity build succeeded but Odyssey.exe was not created.");
                }
            }
            finally
            {
                PlayerSettings.bundleVersion = originalBundleVersion;
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, originalBackend);
                if (originalTarget != BuildTarget.StandaloneWindows64)
                {
                    EditorUserBuildSettings.SwitchActiveBuildTarget(originalGroup, originalTarget);
                }
            }
        }

        private static Dictionary<string, string> ParseCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < args.Length; index++)
            {
                if (!args[index].StartsWith("-odyssey", StringComparison.Ordinal)) continue;
                if (index + 1 >= args.Length) throw new InvalidOperationException("Missing value for " + args[index] + ".");
                result[args[index]] = args[++index];
            }

            return result;
        }

        private static string Required(Dictionary<string, string> args, string name)
        {
            if (!args.TryGetValue(name, out string value) || string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException("Missing required argument " + name + ".");
            }

            return value;
        }

        public static string ValidateBuildOutputPathForTest(string repositoryRoot, string outputPath)
        {
            return ResolveValidatedBuildOutputPath(repositoryRoot, outputPath);
        }

        private static string ResolveValidatedBuildOutputPath(string outputPath)
        {
            string repositoryRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            return ResolveValidatedBuildOutputPath(repositoryRoot, outputPath);
        }

        private static string ResolveValidatedBuildOutputPath(string repositoryRoot, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(repositoryRoot)) throw new InvalidOperationException("Repository root is required.");
            if (string.IsNullOrWhiteSpace(outputPath)) throw new InvalidOperationException("Build output path is required.");

            string root = CanonicalDirectory(repositoryRoot);
            string identityPath = Path.Combine(root, "Assets", "StreamingAssets", "Odyssey", "build-identity.json");
            if (!File.Exists(identityPath)) throw new InvalidOperationException("Generated BuildIdentity is required before Unity build.");

            BuildIdentity identity = BuildIdentityCodec.ReadBuildIdentity(File.ReadAllBytes(identityPath)).Value;
            if (!BuildIdentityText.IsBuildId(identity.BuildId)) throw new InvalidOperationException("Generated BuildId is not canonical.");

            string expected = Path.GetFullPath(Path.Combine(root, "artifacts", "builds", identity.BuildId, "Windows-x64", "Odyssey.exe"));
            string actual = Path.GetFullPath(outputPath);
            if (!IsSamePath(expected, actual)) throw new InvalidOperationException("Build output path must match the canonical Odyssey artifact executable.");
            if (!IsChildOf(root, expected)) throw new InvalidOperationException("Build output path must stay inside the repository artifacts directory.");

            RejectReparsePointAncestors(expected, root);
            return expected;
        }

        private static string CanonicalDirectory(string path)
        {
            string full = Path.GetFullPath(path);
            if (!Directory.Exists(full)) throw new InvalidOperationException("Repository root does not exist.");
            return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static bool IsSamePath(string left, string right)
        {
            return string.Equals(left.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), right.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsChildOf(string root, string child)
        {
            string normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return child.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static void RejectReparsePointAncestors(string path, string repositoryRoot)
        {
            DirectoryInfo? current = Directory.GetParent(path);
            while (current != null)
            {
                if (Directory.Exists(current.FullName) && (current.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                {
                    throw new InvalidOperationException("Build output path must not traverse reparse-point directories.");
                }

                if (IsSamePath(current.FullName, repositoryRoot)) return;
                current = current.Parent;
            }

            throw new InvalidOperationException("Build output path must stay inside the repository.");
        }
    }
}
