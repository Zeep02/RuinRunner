using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Short, skippable entrance with explicit restoration on every exit.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class BossEntrancePresentation : MonoBehaviour
    {
        static readonly Dictionary<GameController, BossEntrancePresentation> active = new Dictionary<GameController, BossEntrancePresentation>();
        GameController game;
        EnemyController boss;
        Action completed;
        string title, subtitle;
        float elapsed;
        bool restored, landed;
        GUIStyle titleStyle, detailStyle;
        const float ReturnAt = 1.45f, Duration = 2.15f;
        public static bool IsActive(GameController controller) => controller != null && active.ContainsKey(controller);
        public static void Begin(GameController game, EnemyController boss, string title, string subtitle, Action completed)
        {
            Cancel(game);
            if (game == null || boss == null)
            {
                completed?.Invoke();
                return;
            }
            var root = new GameObject("BossEntrancePresentation");
            var presentation = root.AddComponent<BossEntrancePresentation>();
            presentation.useGUILayout = false;
            presentation.game = game;
            presentation.boss = boss;
            presentation.title = title;
            presentation.subtitle = subtitle;
            presentation.completed = completed;
            active[game] = presentation;
            boss.SetIntroLocked(true);
            game.Player?.CancelCombatForPresentation();
            game.SetBossIntroPhase(true);
            game.CameraFollow.FocusPresentation((Vector2)boss.transform.position + Vector2.up * .5f);
            // Incoming attacks from an earlier room cannot become delayed hits
            // after the camera returns. Nothing earns damage/rewards during this purge.
            foreach (var hazard in FindObjectsByType<ArenaHazard>())
                Destroy(hazard.gameObject);
            foreach (var shot in FindObjectsByType<EnemyProjectile>())
                Destroy(shot.gameObject);
            foreach (var shot in FindObjectsByType<BossRuneProjectile>())
                Destroy(shot.gameObject);
            foreach (var shot in FindObjectsByType<PlayerProjectile>())
                Destroy(shot.gameObject);
            CombatAudio.Native("heavy-impact", AudioChannel.Monster, .28f, .8f);
            UiFx.FilmBars(true);
        }
        public static void Cancel(GameController game)
        {
            if (game != null && active.TryGetValue(game, out var presentation))
                presentation.Finish(false);
        }
        void Update()
        {
            if (game == null || boss == null || boss.IsDead || !game.IsRunActive)
            {
                Finish(false);
                return;
            }
            if (game.IsManuallyPaused)
                return;
            if (game.Settings != null && (game.Settings.GetKeyDown(GameAction.Interact) || game.Settings.GetKeyDown(GameAction.Dash)))
            {
                Finish(true);
                return;
            }
            elapsed += Time.unscaledDeltaTime;
            boss.SampleEntrance(Mathf.Max(0, elapsed - .35f));
            if (!landed && elapsed >= .35f)
            {
                landed = true;
                SigFx.Play("BreakStone", boss.transform.position, 0, -1, true, false, .8f);
                game.CameraFollow.Shake(.32f, .3f);
            }
            if (elapsed >= ReturnAt)
                game.CameraFollow.ReleasePresentation();
            if (elapsed >= Duration)
                Finish(true);
        }
        void Finish(bool invoke)
        {
            if (restored)
                return;
            restored = true;
            UiFx.FilmBars(false);
            if (game != null)
            {
                active.Remove(game);
                if (game.CameraFollow != null)
                    game.CameraFollow.ReleasePresentation();
                game.SetBossIntroPhase(false);
            }
            if (boss != null)
                boss.SetIntroLocked(false);
            var callback = completed;
            completed = null;
            if (invoke)
                callback?.Invoke();
            Destroy(gameObject);
        }
        void OnDestroy()
        {
            if (!restored)
                Finish(false);
        }
        void OnGUI()
        {
            if (restored || game == null || game.IsManuallyPaused || elapsed < .25f || elapsed > 1.8f)
                return;
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { font = GameTypography.Emphasis, fontSize = 30, alignment = TextAnchor.MiddleCenter, wordWrap = false };
                detailStyle = new GUIStyle(GUI.skin.label) { font = GameTypography.Detail, fontSize = 18, alignment = TextAnchor.MiddleCenter, wordWrap = false };
                titleStyle.normal.textColor = new Color(1, .88f, .55f);
                detailStyle.normal.textColor = Color.white;
            }
            var savedMatrix = GUI.matrix;
            var savedColor = GUI.color;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) * .5f, (Screen.height - 720 * scale) * .5f), Quaternion.identity, Vector3.one * scale);
            float alpha = Mathf.Min(Mathf.Clamp01((elapsed - .25f) / .18f), Mathf.Clamp01((1.8f - elapsed) / .2f));
            GUI.color = Color.white;
            UiFx.BrushStroke(new Rect(240, 112, 800, 110), elapsed - .25f, 1.8f - elapsed);
            GUI.color = new Color(1, 1, 1, alpha);
            GUI.Label(new Rect(330, 125, 620, 43), title, titleStyle);
            GUI.Label(new Rect(330, 167, 620, 26), subtitle, detailStyle);
            GUI.matrix = savedMatrix;
            GUI.color = savedColor;
        }
    }
}
