#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuinRunner.EditorTools
{
    public static class PrototypeBuildMenu
    {
        private const string DemoScene = "Assets/RuinRunner/Scenes/Demo.unity";
        private const string PlayerAnimationAsset = "Assets/RuinRunner/Resources/Animation/PlayerAnimationLibrary.asset";

        private static readonly string[] RequiredFiles =
        {
            "Assets/RuinRunner/Runtime/Core/GameBootstrap.cs",
            "Assets/RuinRunner/Runtime/Core/GameTick.cs",
            "Assets/RuinRunner/Runtime/Core/GameBalance.cs",
            "Assets/RuinRunner/Runtime/Core/GameController.cs",
            "Assets/RuinRunner/Runtime/Core/RuntimeSceneObjects.cs",
            "Assets/RuinRunner/Runtime/Diagnostics/RuntimeSmokeTest.cs",
            "Assets/RuinRunner/Runtime/Player/PlayerController.cs",
            "Assets/RuinRunner/Runtime/Player/PlayerAnimationController.cs",
            "Assets/RuinRunner/Runtime/Player/HeldWeaponView.cs",
            "Assets/RuinRunner/Runtime/Enemies/EnemyController.cs",
            "Assets/RuinRunner/Runtime/Enemies/EnemyPixelAnimator.cs",
            "Assets/RuinRunner/Runtime/Combat/EnemyProjectile.cs",
            "Assets/RuinRunner/Runtime/Combat/PlayerProjectile.cs",
            "Assets/RuinRunner/Runtime/Items/RewardChest.cs",
            "Assets/RuinRunner/Runtime/Items/ExperienceOrb.cs",
            "Assets/RuinRunner/Runtime/Items/FieldLootPickup.cs",
            "Assets/RuinRunner/Runtime/Items/InventorySystem.cs",
            "Assets/RuinRunner/Runtime/Items/WeaponSlotState.cs",
            "Assets/RuinRunner/Runtime/Progression/PlayerProgression.cs",
            "Assets/RuinRunner/Runtime/Progression/PlayerStats.cs",
            "Assets/RuinRunner/Runtime/Progression/MetaProgressionService.cs",
            "Assets/RuinRunner/Runtime/Save/SaveSlotData.cs",
            "Assets/RuinRunner/Runtime/Save/GameSettingsData.cs",
            "Assets/RuinRunner/Runtime/Save/SaveSlotService.cs",
            "Assets/RuinRunner/Runtime/Save/GameSettingsService.cs",
            "Assets/RuinRunner/Runtime/Data/AccessoryDefinition.cs",
            "Assets/RuinRunner/Runtime/Data/AccessoryLootTable.cs",
            "Assets/RuinRunner/Runtime/Data/ItemSetDefinition.cs",
            "Assets/RuinRunner/Runtime/Data/MetaUpgradeDefinition.cs",
            "Assets/RuinRunner/Runtime/Data/GameBalanceConfig.cs",
            "Assets/RuinRunner/Runtime/Data/EnemyAnimationLibrary.cs",
            "Assets/RuinRunner/Runtime/Data/MeleeComboDefinition.cs",
            "Assets/RuinRunner/Runtime/Data/RoomTemplate.cs",
            "Assets/RuinRunner/Runtime/Data/PassiveItemDefinition.cs",
            "Assets/RuinRunner/Runtime/Data/PassiveItemLootTable.cs",
            "Assets/RuinRunner/Runtime/Data/UpgradeDefinition.cs",
            "Assets/RuinRunner/Runtime/Data/WeaponDefinition.cs",
            "Assets/RuinRunner/Runtime/Data/WeaponLootTable.cs",
            "Assets/RuinRunner/Runtime/World/ArenaBuilder.cs",
            "Assets/RuinRunner/Runtime/World/DungeonMap.cs",
            "Assets/RuinRunner/Runtime/World/RoomExit.cs",
            "Assets/RuinRunner/Runtime/World/WaveDirector.cs",
            "Assets/RuinRunner/Runtime/World/EnemySpawnTelegraph.cs",
            "Assets/RuinRunner/Runtime/UI/PrototypeHud.cs",
            "Assets/RuinRunner/Runtime/UI/InventoryIconFactory.cs",
            "Assets/RuinRunner/Runtime/UI/PixelUiSkin.cs",
            "Assets/RuinRunner/Runtime/UI/PixelHudSkin.cs",
            "Assets/RuinRunner/Runtime/UI/PlayerResourceDisplay.cs",
            "Assets/RuinRunner/Runtime/Feedback/PrototypeAudioController.cs",
            "Assets/RuinRunner/Runtime/Feedback/PixelArtAtlas.cs",
            "Assets/RuinRunner/Runtime/Feedback/PixelWorldArt.cs",
            "Assets/RuinRunner/Runtime/Feedback/SignatureSlashEffect.cs",
            "Assets/RuinRunner/Editor/BalanceTuningWindow.cs",
            "Assets/RuinRunner/Resources/Art/ruin_background.png",
            "Assets/RuinRunner/Resources/Art/Pixel/TopDown/topdown_nature_room_v1.png",
            "Assets/RuinRunner/Resources/Art/Pixel/Separated/manifest.json"
        };

        [MenuItem("Prototype/Open Demo Scene", priority = 1)]
        public static void OpenDemoScene()
        {
            EditorSceneManager.OpenScene(DemoScene);
        }

        [MenuItem("Prototype/Run Project Check", priority = 2)]
        public static void RunProjectCheck()
        {
            ValidateProject(true);
        }

        [MenuItem("Prototype/Animation/Create or Select Player Animation Library", priority = 5)]
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

        [MenuItem("Prototype/Animation/Create or Select Enemy Animation Library", priority = 5)]
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

        [MenuItem("Prototype/Rooms/Generate Starter Room Templates", priority = 6)]
        public static void GenerateStarterRoomTemplates()
        {
            RoomTemplateEditorWindow.GenerateStarterTemplates();
        }

        [MenuItem("Prototype/Build Windows Demo", priority = 10)]
        public static void BuildWindowsDemo()
        {
            PixelArtImportProcessor.EnsurePixelImports();
            ItemIconMigration.ExportMissing();
            IndividualArtMigration.ExportMissing();
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
                Debug.Log("[Prototype Build] Success: " + executablePath);
                if (!Application.isBatchMode) EditorUtility.RevealInFinder(executablePath);
            }
            else
            {
                Debug.LogError("[Prototype Build] Failed. Open the Console and fix red errors first.");
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
                Debug.LogError("[Prototype Check] Demo scene is missing: " + DemoScene);
                errors++;
            }

            foreach (var requiredFile in RequiredFiles)
            {
                if (File.Exists(requiredFile)) continue;
                Debug.LogError("[Prototype Check] Required file is missing: " + requiredFile);
                errors++;
            }

            var roomTemplates = AssetDatabase.FindAssets("t:RoomTemplate", new[] { "Assets/RuinRunner/Resources/Rooms" });
            if (roomTemplates.Length < 4)
            {
                Debug.LogError("[Prototype Check] At least four RoomTemplate assets are required. Run Prototype > Rooms > Generate Starter Room Templates.");
                errors++;
            }

            if (logResult)
            {
                if (errors == 0)
                    Debug.Log("[Prototype Check] OK — demo scene and all required files are present.");
                else
                    Debug.LogError("[Prototype Check] Failed with " + errors + " issue(s). Check the Console.");
            }

            return errors == 0;
        }
    }
}
#endif
