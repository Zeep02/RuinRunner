#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuinRunner.EditorTools
{
    public static class BuildMenu
    {
        private const string DemoScene = "Assets/RuinRunner/Scenes/Demo.unity";
        private const string PlayerAnimationAsset = "Assets/RuinRunner/Resources/Animation/PlayerAnimationLibrary.asset";


        [MenuItem("RuinRunner/Open Demo Scene", priority = 1)]
        public static void OpenDemoScene()
        {
            EditorSceneManager.OpenScene(DemoScene);
        }

        [MenuItem("RuinRunner/Run Project Check", priority = 2)]
        public static void RunProjectCheck()
        {
            ValidateProject(true);
        }

        [MenuItem("RuinRunner/Animation/Create or Select Player Animation Library", priority = 5)]
        public static void CreatePlayerAnimationLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<PlayerAnimationLibrary>(PlayerAnimationAsset);
            if (library == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PlayerAnimationAsset));
                AssetDatabase.Refresh();
                library = ScriptableObject.CreateInstance<PlayerAnimationLibrary>();
                AssetDatabase.CreateAsset(library, PlayerAnimationAsset);
                AssetDatabase.SaveAssets();
                Debug.Log("[Animation Setup] Created: " + PlayerAnimationAsset);
            }

            Selection.activeObject = library;
            EditorGUIUtility.PingObject(library);
        }

        [MenuItem("RuinRunner/Animation/Create or Select Enemy Animation Library", priority = 5)]
        public static void CreateEnemyAnimationLibrary()
        {
            const string path = "Assets/RuinRunner/Resources/Animation/EnemyAnimationLibrary.asset";
            var library = AssetDatabase.LoadAssetAtPath<EnemyAnimationLibrary>(path);
            if (library == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                AssetDatabase.Refresh();
                library = ScriptableObject.CreateInstance<EnemyAnimationLibrary>();
                AssetDatabase.CreateAsset(library, path);
                AssetDatabase.SaveAssets();
            }
            Selection.activeObject = library;
            EditorGUIUtility.PingObject(library);
        }

        [MenuItem("RuinRunner/Rooms/Generate Starter Room Templates", priority = 6)]
        public static void GenerateStarterRoomTemplates()
        {
            RoomTemplateEditorWindow.GenerateStarterTemplates();
        }

        [MenuItem("RuinRunner/Build Windows Demo", priority = 10)]
        public static void BuildWindowsDemo()
        {
            PixelArtImportProcessor.EnsurePixelImports();
            if (!ValidateProject(true))
            {
                if (Application.isBatchMode)
                    throw new System.InvalidOperationException("Project validation failed before the Windows build.");

                return;
            }

            PlayerSettings.productName = "Ruin Runner Prototype";
            PlayerSettings.companyName = "Graduation Project";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;

            var buildFolder = ResolveBuildFolder();
            Directory.CreateDirectory(buildFolder);
            var executablePath = Path.Combine(buildFolder, "RuinRunnerPrototype.exe");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { DemoScene },
                locationPathName = executablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CleanBuildCache
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log("[Build] Success: " + executablePath);
                if (!Application.isBatchMode) EditorUtility.RevealInFinder(executablePath);
            }
            else
            {
                Debug.LogError("[Build] Failed. Open the Console and fix red errors first.");
                if (Application.isBatchMode)
                    throw new System.InvalidOperationException("Windows player build failed: " + report.summary.result);
            }
        }

        private static string ResolveBuildFolder()
        {
            var requestedOutput = GetCommandLineValue("-buildOutput");
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var buildsRoot = Path.GetFullPath(Path.Combine(projectRoot, "Builds"));
            var buildFolder = string.IsNullOrWhiteSpace(requestedOutput)
                ? Path.Combine(buildsRoot, "Windows-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"))
                : Path.GetFullPath(requestedOutput);

            var buildsBoundary = buildsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                 + Path.DirectorySeparatorChar;
            if (!buildFolder.StartsWith(buildsBoundary, System.StringComparison.OrdinalIgnoreCase))
                throw new System.InvalidOperationException("Build output must stay inside the project's Builds folder: " + buildFolder);

            return buildFolder;
        }

        private static string GetCommandLineValue(string argumentName)
        {
            var arguments = System.Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], argumentName, System.StringComparison.OrdinalIgnoreCase))
                    return arguments[index + 1];
            }

            return null;
        }

        private static bool ValidateProject(bool logResult)
        {
            var errors = 0;
            if (!File.Exists(DemoScene))
            {
                Debug.LogError("[Project Check] Demo scene is missing: " + DemoScene);
                errors++;
            }

            var roomTemplates = AssetDatabase.FindAssets("t:RoomTemplate", new[] { "Assets/RuinRunner/Resources/Rooms" });
            if (roomTemplates.Length < 4)
            {
                Debug.LogError("[Project Check] At least four RoomTemplate assets are required. Run RuinRunner > Rooms > Generate Starter Room Templates.");
                errors++;
            }

            if (logResult)
            {
                if (errors == 0)
                    Debug.Log("[Project Check] OK — demo scene and room templates are present.");
                else
                    Debug.LogError("[Project Check] Failed with " + errors + " issue(s). Check the Console.");
            }

            return errors == 0;
        }
    }
}
#endif
