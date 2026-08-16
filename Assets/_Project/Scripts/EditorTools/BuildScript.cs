using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RPGArena.EditorTools
{
    // Headless build entry points, so a player can be produced from the command line without
    // opening the Editor:
    //
    //   Unity.exe -batchmode -quit -projectPath <proj> -executeMethod RPGArena.EditorTools.BuildScript.BuildWindows
    //
    // Scenes come from EditorBuildSettings rather than a hardcoded list, so the build can never
    // silently disagree with what the Build Settings window shows — the exact drift the submission
    // checklist warns about ("all required scenes are included in Build Settings").
    public static class BuildScript
    {
        private const string OutDir = "Builds/RPGArena";
        private const string ExeName = "RPGArena.exe";

        [MenuItem("RPGArena/Build/Windows Player (Release)")]
        public static void BuildWindows() => Run(BuildOptions.None, "release");

        [MenuItem("RPGArena/Build/Windows Player (Development + Cheats)")]
        public static void BuildWindowsDev() => Run(BuildOptions.Development, "development");

        private static void Run(BuildOptions options, string label)
        {
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Fail("No ENABLED scenes in Build Settings — the player would boot to nothing.");
                return;
            }

            // The first enabled scene is what the player actually starts on. Boot is the intended
            // entry point (it creates the persistent services and hands off to the menu), so a
            // reordering here would ship a game that opens mid-battle with no audio or run state.
            if (!scenes[0].EndsWith("Boot.unity", StringComparison.OrdinalIgnoreCase))
                Debug.LogWarning($"[Build] First scene is '{scenes[0]}', expected Boot.unity.");

            string dir = Path.GetFullPath(OutDir);
            Directory.CreateDirectory(dir);

            Debug.Log($"[Build] {label} · {scenes.Length} scene(s):\n  " + string.Join("\n  ", scenes));

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(dir, ExeName),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = options,
            });

            var s = report.summary;
            Debug.Log($"[Build] result={s.result} size={s.totalSize / (1024 * 1024)}MB " +
                      $"errors={s.totalErrors} warnings={s.totalWarnings} time={s.totalTime}");

            if (s.result != BuildResult.Succeeded)
            {
                Fail($"Build FAILED: {s.result} ({s.totalErrors} errors)");
                return;
            }

            Debug.Log($"[Build] OK -> {Path.Combine(dir, ExeName)}");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void Fail(string message)
        {
            Debug.LogError("[Build] " + message);
            // A non-zero exit code is what makes a CLI build fail loudly instead of "succeeding"
            // with no player on disk.
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
