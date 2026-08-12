using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Odyssey.Unity.Client.Editor.Serialization
{
    public static class SerializationAotBuild
    {
        public static void Build()
        {
            BuildTargetGroup group = BuildTargetGroup.Standalone;
            BuildTarget target = BuildTarget.StandaloneWindows64;
            string outputDirectory = Path.GetFullPath("artifacts/serialization-aot-smoke");
            string outputPath = Path.Combine(outputDirectory, "serialization-aot-smoke.exe");
            Directory.CreateDirectory(outputDirectory);
            ScriptingImplementation originalBackend = PlayerSettings.GetScriptingBackend(group);
            string originalDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
            try
            {
                PlayerSettings.SetScriptingBackend(group, ScriptingImplementation.IL2CPP);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, AppendDefine(originalDefines, "ODYSSEY_SERIALIZATION_AOT_SMOKE"));
                BuildPlayerOptions options = new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Odyssey/Client/Scenes/Bootstrap.unity" },
                    locationPathName = outputPath,
                    target = target,
                    options = BuildOptions.Development
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException("serialization-aot-smoke build failed: " + report.summary.result);
                }

                UnityEngine.Debug.Log("serialization-aot-smoke build PASS " + outputPath);
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(group, originalBackend);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, originalDefines);
            }
        }

        private static string AppendDefine(string defines, string define)
        {
            if (string.IsNullOrWhiteSpace(defines)) return define;
            foreach (string existing in defines.Split(';'))
            {
                if (existing == define) return defines;
            }

            return defines + ";" + define;
        }
    }
}
