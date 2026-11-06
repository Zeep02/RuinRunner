using UnityEngine;

namespace RuinRunner
{
    /// <summary>Shows what the enemy AI is thinking, for demonstrating it. Only exists with --ai-debug on the command line; F10 hides it.</summary>
    public sealed class AiDebugOverlay : MonoBehaviour
    {
        const float GraphSeconds = 30f;
        readonly float[] stress = new float[240];
        int head;
        float nextSample;
        bool visible = true;
        GUIStyle label;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F10))
                visible = !visible;
            var director = EnemyAiDirector.Current;
            if (director == null || Time.unscaledTime < nextSample)
                return;
            nextSample = Time.unscaledTime + GraphSeconds / stress.Length;
            stress[head] = director.Stress;
            head = (head + 1) % stress.Length;
        }

        void OnGUI()
        {
            var director = EnemyAiDirector.Current;
            if (!visible || director == null || director.Player == null)
                return;
            if (label == null)
                label = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, wordWrap = false };
            var old = GUI.color;
            GUI.color = new Color(0, 0, 0, .78f);
            var panel = new Rect(10, Screen.height - 214, 390, 204);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = old;
            var y = panel.y + 6;
            void Line(string text)
            {
                GUI.Label(new Rect(panel.x + 8, y, panel.width - 16, 20), text, label);
                y += 18;
            }
            Line("<b>적 AI 디렉터</b>" + (EnemyAiDirector.Baseline ? "  (비교 기준: 지휘 끔)" : ""));
            Line("강도 " + director.Phase + "  ·  긴장도 " + director.Stress.ToString("0.00") + "  ·  쿨타임 x" + director.CooldownScale.ToString("0.00"));
            Line("공격권  근접 " + director.MeleeTokensInUse + "/" + director.MeleeTokenLimit + "  원거리 " + director.RangedTokensInUse + "/" + director.RangedTokenLimit + "  ·  적 " + director.Members.Count);
            Line("플레이어  체력 " + director.HealthRatio.ToString("P0") + "  속도 " + director.PlayerSpeed.ToString("0.0") + "  거리 유지 " + director.KeptDistance.ToString("0.0"));
            Line("판단 입력  " + (director.PlayerDashedRecently ? "방금 대시 " : "") + (director.PlayerStandingStill ? "정지 " : "") + (director.PlayerKeepsAway ? "거리 벌림 " : ""));
            DrawGraph(new Rect(panel.x + 8, y + 4, panel.width - 16, 64));
            var camera = Camera.main;
            if (camera == null)
                return;
            foreach (var enemy in director.Members)
            {
                if (enemy == null)
                    continue;
                var point = camera.WorldToScreenPoint(enemy.transform.position + Vector3.up * 1.1f);
                var text = "<b>" + enemy.Type + "</b> " + enemy.Role + "\n" + enemy.LastDecision;
                GUI.Label(new Rect(point.x - 120, Screen.height - point.y - 40, 300, 40), "<color=#ffe08a>" + text + "</color>", label);
            }
        }

        void DrawGraph(Rect area)
        {
            var old = GUI.color;
            GUI.color = new Color(1, 1, 1, .12f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = new Color(1f, .45f, .35f);
            var step = area.width / stress.Length;
            for (var i = 0; i < stress.Length; i++)
            {
                var value = stress[(head + i) % stress.Length];
                var height = Mathf.Max(1f, value * area.height);
                GUI.DrawTexture(new Rect(area.x + i * step, area.yMax - height, Mathf.Max(1f, step), height), Texture2D.whiteTexture);
            }
            GUI.color = old;
        }
    }
}
