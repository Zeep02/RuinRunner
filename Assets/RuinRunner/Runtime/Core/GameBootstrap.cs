using UnityEngine;

namespace RuinRunner
{
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            // The asset-pack playground is a separate scene, not a replacement run.
            if (Object.FindAnyObjectByType<DungeonGatheringTest>() != null) return;
#if UNITY_6000_0_OR_NEWER
            if (Object.FindAnyObjectByType<GameController>() != null) return;
#else
            if (Object.FindObjectOfType<GameController>() != null) return;
#endif

            Application.targetFrameRate = 120;
            Time.fixedDeltaTime = GameTick.Duration;
            Physics2D.gravity = Vector2.zero;
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--native-hero-atlas-validation") >= 0)
            {
                new GameObject("NativeHeroAtlasValidation").AddComponent<NativeHeroAtlasValidation>();
                return;
            }
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--boss-sprite-validation") >= 0)
            {
                new GameObject("BossSpriteValidation").AddComponent<BossSpriteValidation>();
                return;
            }
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--continuous-world-validation") >= 0)
            {
                new GameObject("ContinuousWorldValidation").AddComponent<ContinuousWorldValidation>();
                return;
            }
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--feedback-validation") >= 0)
            {
                new GameObject("FeedbackValidation").AddComponent<FeedbackValidation>();
                return;
            }
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--presentation-validation") >= 0)
            {
                new GameObject("PresentationValidation").AddComponent<PresentationValidation>();
                return;
            }
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--portal-validation") >= 0)
            {
                new GameObject("PortalValidation").AddComponent<PortalValidation>();
                return;
            }
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--alpha-validation") >= 0)
            {
                new GameObject("AlphaValidation").AddComponent<AlphaValidation>();
                return;
            }
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--room-traversal-validation") >= 0)
            {
                new GameObject("RoomTraversalValidation").AddComponent<RoomTraversalValidation>();
                return;
            }
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--motion-validation") >= 0)
            {
                new GameObject("MotionValidation").AddComponent<MotionValidation>();
                return;
            }
            var game = new GameObject("GameController").AddComponent<GameController>();
            var performanceArgs=System.Environment.GetCommandLineArgs();
            var performanceIndex=System.Array.IndexOf(performanceArgs,"--performance-dir");
            if(performanceIndex>=0 && performanceIndex+1<performanceArgs.Length)
                game.gameObject.AddComponent<PerformanceRecorder>().Initialize(game,performanceArgs[performanceIndex+1]);
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--save-relaunch-validation") >= 0)
                game.gameObject.AddComponent<SaveLifecycleValidation>().Initialize(game, true);
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--map-validation") >= 0)
                game.gameObject.AddComponent<MapValidation>();
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--ui-validation") >= 0)
                game.gameObject.AddComponent<UiValidation>();
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--memory-validation") >= 0)
                game.gameObject.AddComponent<SessionMemoryValidation>();
        }
    }
}
