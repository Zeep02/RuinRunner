using UnityEngine;

namespace RuinRunner
{
    /// <summary>End-of-run result and pause screens.</summary>
    public sealed partial class GameHud
    {
        const float ResultPopSeconds = .22f, RewardCountAfter = .3f, RewardCountSeconds = .8f;
        private void DrawResult(float width, float height)
        {
            if (resultOpenedAt < 0f)
            {
                resultOpenedAt = Time.unscaledTime;
                resultRewardShown = false;
                if (game.IsVictory)
                    UiFx.Play("UiConfetti", new Vector2(width / 2f, height / 2f - 130f), 720f);
            }
            var opened = Time.unscaledTime - resultOpenedAt;
            var settle = Mathf.Clamp01(opened / ResultPopSeconds);
            var outerMatrix = GUI.matrix;
            GUIUtility.ScaleAroundPivot(Vector2.one * Mathf.Lerp(.93f, 1f, 1f - (1f - settle) * (1f - settle)), new Vector2(width / 2f, height / 2f));
            DrawResultPanel(width, height, opened);
            GUI.matrix = outerMatrix;
        }

        private void DrawResultPanel(float width, float height, float opened)
        {
            DrawPanel(new Rect(width / 2f - 330, height / 2f - 190, 660, 380));
            GUI.Label(new Rect(width / 2f - 280, height / 2f - 155, 560, 60), game.IsVictory ? "유적의 봉인이 풀렸다" : "도전 실패", titleStyle);
            var reward = game.ResultSettlementConfirmed
                ? "유산 +" + Mathf.RoundToInt(game.LastMetaReward * Mathf.Clamp01((opened - RewardCountAfter) / RewardCountSeconds))
                : "";
            if (!resultRewardShown && game.ResultSettlementConfirmed && game.LastMetaReward > 0 && opened >= RewardCountAfter + RewardCountSeconds)
            {
                resultRewardShown = true;
                UiFx.Play("UiPick", new Vector2(width / 2f, height / 2f - 20f), 300f);
                CombatAudio.Native("coin", AudioChannel.Interface, .4f);
            }
            var result = "TIME  " + FormatTime(game.Stats.ElapsedSeconds) + "    KILLS  " + game.Stats.EnemiesDefeated + "\n" + reward;
            var message = game.IsVictory
                ? "세 수호자의 빛이 잦아들고, 유적에 새벽이 돌아왔습니다.\n당신이 찾은 유산은 다음 탐험가의 길이 됩니다.\n" + result
                : result;
            GUI.Label(new Rect(width / 2f - 270, height / 2f - 75, 540, 120), message, detailStyle);
            bool enabled = GUI.enabled;
            GUI.enabled = !game.SessionOperationPending && !game.BackendBusy;
            if (GUI.Button(new Rect(width / 2 - 240, height / 2 + 60, 225, 52), "마을로 귀환", buttonStyle))
                game.ReturnToTown();
            if (GUI.Button(new Rect(width / 2 + 15, height / 2 + 60, 225, 52), "메인 메뉴", buttonStyle))
                game.ReturnToMainMenu();
            GUI.enabled = enabled;

        }

        private void DrawPause(float width, float height)
        {
            DrawPanel(new Rect(width / 2f - 260, height / 2f - 150, 520, 300));
            GUI.Label(new Rect(width / 2f - 210, height / 2f - 115, 420, 48), "일시정지", titleStyle);
            if (GUI.Button(new Rect(width / 2f - 150, height / 2f - 35, 300, 56), "계속하기", buttonStyle))
                game.ResumeGame();
            if (GUI.Button(new Rect(width / 2f - 150, height / 2f + 40, 300, 56), "메인 메뉴", buttonStyle))
                game.ReturnToMainMenu();
            GUI.Label(new Rect(width / 2f - 200, height / 2f + 105, 400, 28), Key(GameAction.Pause) + " 키로 계속", centerStyle);
        }
    }
}
