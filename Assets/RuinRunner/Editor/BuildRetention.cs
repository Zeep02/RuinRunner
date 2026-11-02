#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace RuinRunner.EditorTools
{
    // Covers the editor menu, Build Profiles and builds invoked by Unity CLI.
    public sealed class BuildRetention : IPostprocessBuildWithReport
    {
        public int callbackOrder => int.MaxValue;

        public void OnPostprocessBuild(BuildReport report)
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var builds = Path.Combine(root, "Builds");
            var output = Path.GetDirectoryName(Path.GetFullPath(report.summary.outputPath));
            // Staging is pruned by Build-Clean only after its player smoke test passes.
            if (!string.Equals(Path.GetDirectoryName(output), builds, StringComparison.OrdinalIgnoreCase)) return;

            // The report is final only after all postprocessors return. Delay pruning
            // until then; quitting also handles successful -batchmode -quit builds.
            EditorApplication.CallbackFunction finish = null;
            Action onQuit = null;
            finish = () =>
            {
                if (BuildPipeline.isBuildingPlayer)
                {
                    EditorApplication.delayCall += finish;
                    return;
                }
                EditorApplication.delayCall -= finish;
                EditorApplication.quitting -= onQuit;
                if (report != null && report.summary.result == BuildResult.Succeeded)
                    Prune(root, output);
            };
            onQuit = () => finish();
            EditorApplication.delayCall += finish;
            EditorApplication.quitting += onQuit;
        }

        private static void Prune(string root, string output)
        {
            try
            {
                var script = Path.Combine(root, "Tools", "Prune-Builds.ps1");
                var powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
                var start = new ProcessStartInfo(powershell)
                {
                    Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + script
                        + "\" -ProjectRoot \"" + root + "\" -Kind Builds -CompletedPath \"" + output + "\" -Apply",
                    WorkingDirectory = root,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (var process = Process.Start(start))
                {
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                        Debug.LogWarning("[Build retention] Cleanup failed; see Tools/Prune-Builds.ps1. The new build was preserved.");
                    else
                        Debug.Log("[Build retention] Retained the latest build and two previous builds.");
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning("[Build retention] Cleanup skipped: " + error.Message);
            }
        }
    }
}
#endif
