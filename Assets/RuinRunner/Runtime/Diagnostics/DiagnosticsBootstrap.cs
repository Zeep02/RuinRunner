using System;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>
    /// Entry point of the validation assembly. Registers <see cref="DiagnosticsHooks"/> before the
    /// first scene loads; every validation, tour and recorder is selected by a command-line flag.
    /// </summary>
    public static class DiagnosticsBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            DiagnosticsHooks.StartStandalone = StartStandalone;
            DiagnosticsHooks.AttachToSession = AttachToSession;
            DiagnosticsHooks.AttachToGame = AttachToGame;
            DiagnosticsHooks.EnemyDamaged = BossCombatObservation.RecordDamage;
        }

        private static bool Has(string argument) => Array.IndexOf(Environment.GetCommandLineArgs(), argument) >= 0;

        private static string ValueOf(string argument)
        {
            var arguments = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(arguments, argument);
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
        }

        /// <summary>Validations that build their own scene instead of a normal game.</summary>
        private static bool StartStandalone()
        {
            if (Has("--native-hero-atlas-validation"))
                return Start<NativeHeroAtlasValidation>();
            if (Has(StageArtTour.Argument))
                return Start<StageArtTour>();
            if (Has(AudioLevelTour.Argument))
                return Start<AudioLevelTour>();
            if (Has("--continuous-world-validation"))
                return Start<ContinuousWorldValidation>();
            if (Has("--feedback-validation"))
                return Start<FeedbackValidation>();
            if (Has("--presentation-validation"))
                return Start<PresentationValidation>();
            if (Has("--portal-validation"))
                return Start<PortalValidation>();
            if (Has("--alpha-validation"))
                return Start<AlphaValidation>();
            if (Has("--room-traversal-validation"))
                return Start<RoomTraversalValidation>();
            if (Has("--motion-validation"))
                return Start<MotionValidation>();
            return false;
        }

        private static bool Start<T>() where T : Component
        {
            new GameObject(typeof(T).Name).AddComponent<T>();
            return true;
        }

        /// <summary>Recorders and checks that run alongside a normal game.</summary>
        private static void AttachToGame(GameController game)
        {
            var performanceFolder = ValueOf("--performance-dir");
            if (performanceFolder != null)
                game.gameObject.AddComponent<PerformanceRecorder>().Initialize(game, performanceFolder);
            if (Has("--save-relaunch-validation"))
                game.gameObject.AddComponent<SaveLifecycleValidation>().Initialize(game, true);
            if (Has("--map-validation"))
                game.gameObject.AddComponent<MapValidation>();
            if (Has("--ui-validation"))
                game.gameObject.AddComponent<UiValidation>();
            if (Has("--memory-validation"))
                game.gameObject.AddComponent<SessionMemoryValidation>();
        }

        /// <summary>Automated play, the combat sandbox and the player smoke test.</summary>
        private static void AttachToSession(GameController game)
        {
            var slots = game.SaveSlots;
            var automatedRun = Has(StageValidationInput.CommandLineArgument) &&
                               (!Application.isBatchMode || Has("--headless-campaign-validation"));
            if (automatedRun)
            {
                StageValidationInput.ConfigureRandomSeed();
                var recoveryRequested = Has("--validation-recovery-ticket");
                var isolatedAllowed = recoveryRequested
                    ? AutomatedStageValidation.AuthorizeRecoveryProfile(slots)
                    : slots.IsValidationProfile && !slots.GetSummary(1).Exists &&
                      !System.IO.Directory.Exists(System.IO.Path.Combine(slots.SaveDirectory, "BackendRecovery"));
                if (!isolatedAllowed || recoveryRequested && Has("--legacy-combat-fixture"))
                {
                    Debug.LogError("[Stage E2E] A new isolated profile or verified recovery ticket is required; no slot was overwritten.");
                    Application.Quit(3);
                    return;
                }
                if (Has("--legacy-combat-fixture"))
                {
                    slots.OpenSlot(1);
                    game.BeginRunForActiveSlot();
                }
                game.gameObject.AddComponent<AutomatedStageValidation>().Initialize(game);
                return;
            }

            if (Has(DiagnosticsHooks.CombatSandboxArgument))
            {
                slots.OpenTransientForTests();
                game.BeginRunForActiveSlot();
                game.gameObject.AddComponent<CombatSandbox>().Initialize(game);
                return;
            }

#if UNITY_STANDALONE && !UNITY_EDITOR
            if (Application.isBatchMode || Has("--smoke-test"))
            {
                slots.OpenTransientForTests();
                game.BeginRunForActiveSlot();
                game.gameObject.AddComponent<RuntimeSmokeTest>().Initialize(game);
            }
#endif
        }
    }
}
