using UnityEngine;

namespace RuinRunner
{
    /// <summary>Creates the game controller after the first scene loads. The scene itself holds no game objects.</summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Object.FindAnyObjectByType<GameController>() != null)
                return;

            Application.targetFrameRate = 120;
            Time.fixedDeltaTime = GameTick.Duration;
            Physics2D.gravity = Vector2.zero;

            if (DiagnosticsHooks.StartStandalone != null && DiagnosticsHooks.StartStandalone())
                return;

            var game = new GameObject("GameController").AddComponent<GameController>();
            DiagnosticsHooks.AttachToGame?.Invoke(game);
        }
    }
}
