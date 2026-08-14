using System;
using System.Collections.Generic;
using System.IO;
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
    }
}
