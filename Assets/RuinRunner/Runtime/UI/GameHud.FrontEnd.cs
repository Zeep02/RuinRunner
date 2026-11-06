using UnityEngine;

namespace RuinRunner
{
    /// <summary>Main menu, save slots and options.</summary>
    public sealed partial class GameHud
    {
        private void DrawFrontEnd(float width, float height)
        {
            var background = PixelUiSkin.MenuBackground;
            if (background != null)
            {
                GUI.DrawTexture(new Rect(0, 0, width, height), background, ScaleMode.ScaleAndCrop, true);
                var oldColor = GUI.color;
                GUI.color = new Color(.08f, .065f, .1f, .18f);
                GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);
                GUI.color = oldColor;
            }
            else
            {
                var oldColor = GUI.color;
                GUI.color = new Color(.018f, .025f, .055f, 1f);
                GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);
                GUI.color = oldColor;
            }
            MenuAmbience.Draw(width, height);

            if (game.CurrentFrontEndPage == FrontEndPage.SaveSlots)
                DrawSaveSlots(width, height);
            else if (game.CurrentFrontEndPage == FrontEndPage.Options)
                DrawOptions(width, height);
            else
                DrawMainMenu(width, height);
        }

        private void DrawMainMenu(float width, float height)
        {
            var logo = PixelUiSkin.MenuLogo;
            if (logo != null)
                GUI.DrawTexture(new Rect(width / 2f - 250f, height / 2f - 286f + Mathf.Round(Mathf.Sin(Time.unscaledTime * 1.1f) * 4f), 500f, 232f), logo, ScaleMode.ScaleToFit, true);

            var x = width / 2f - 150f;
            var y = height / 2f + 14f;
            if (GUI.Button(new Rect(x, y, 300, 62), "시작", buttonStyle))
                game.OpenSaveSlots();
            if (GUI.Button(new Rect(x, y + 78, 300, 62), "옵션", buttonStyle))
                game.OpenOptions();
            if (GUI.Button(new Rect(x, y + 156, 300, 62), "나가기", buttonStyle))
                game.QuitGame();
        }

        private void DrawSaveSlots(float width, float height)
        {
            PrepareTownStyles();
            var panel = new Rect(width / 2f - 440, height / 2f - 285, 880, 570);
            DrawPanel(panel);
            GUI.Label(new Rect(panel.x + 40, panel.y + 20, panel.width - 80, 48), "모험가 기록", titleStyle);

            bool canSelect = GUI.enabled;
            GUI.enabled = canSelect && !game.SessionOperationPending && !game.BackendBusy && game.ResetProfileSlot == 0;

            for (var slotNumber = 1; slotNumber <= 3; slotNumber++)
            {
                var summary = game.SaveSlots.GetSummary(slotNumber);
                var row = new Rect(panel.x + 45, panel.y + 88 + (slotNumber - 1) * 128, panel.width - 90, 108);
                GUI.Box(row, string.Empty, slotStyle);
                GUI.Label(new Rect(row.x + 18, row.y + 34, 115, 34), "SAVE " + slotNumber, bodyStyle);

                if (!summary.Exists)
                {
                    GUI.Label(new Rect(row.x + 145, row.y + 29, 330, 54), "빈 슬롯", detailStyle);
                    if (GUI.Button(new Rect(row.x + row.width - 195, row.y + 27, 165, 54), "마을 입장", buttonStyle))
                    {
                        game.StartSlot(slotNumber);
                    }
                    continue;
                }

                var details = (summary.IsServerProfile ? summary.Name : "기존 기록") +
                              "\n도전 " + summary.TotalRuns + "회  ·  엔딩 " + summary.CampaignVictories + "회  ·  봉인 " + summary.BestCampaignStage + "/3" +
                              "\n처치 " + summary.TotalKills + "  ·  유산 조각 " + summary.LegacyShards;
                GUI.Label(new Rect(row.x + 145, row.y + 12, 390, 88), details, detailStyle);
                if (GUI.Button(new Rect(row.x + row.width - 225, row.y + 13, 195, 40), "마을로 입장", buttonStyle))
                {
                    game.StartSlot(slotNumber);
                }
                if (summary.IsServerProfile && GUI.Button(new Rect(row.x + row.width - 225, row.y + 61, 195, 32), "프로필 초기화", compactButton))
                    game.RequestProfileReset(slotNumber);
            }
            GUI.enabled = canSelect;
            if (game.ResetProfileSlot > 0)
            {
                DrawProfileReset(width, height);
                return;
            }
            GUI.enabled = canSelect && !game.SessionOperationPending && !game.BackendBusy;
            if (GUI.Button(new Rect(panel.x + 325, panel.y + 495, 230, 48), "뒤로", buttonStyle))
            {
                game.BackToMainMenuPage();
            }
            GUI.enabled = canSelect;
        }

        private void DrawOptions(float width, float height)
        {
            CaptureRebindEvent();
            var panel = new Rect(width / 2f - 470, height / 2f - 310, 940, 620);
            DrawPanel(panel);
            GUI.Label(new Rect(panel.x + 40, panel.y + 22, panel.width - 80, 50), "OPTIONS", titleStyle);

            DrawOptionTab(new Rect(panel.x + 55, panel.y + 82, 250, 46), "음량", 0);
            DrawOptionTab(new Rect(panel.x + 345, panel.y + 82, 250, 46), "조작키", 1);
            DrawOptionTab(new Rect(panel.x + 635, panel.y + 82, 250, 46), "화면", 2);

            if (optionsTab == 0)
                DrawAudioOptions(panel);
            else if (optionsTab == 1)
                DrawControlOptions(panel);
            else
                DrawDisplayOptions(panel);

            if (GUI.Button(new Rect(panel.x + 355, panel.y + 550, 230, 48), "뒤로", buttonStyle))
            {
                rebindingAction = null;
                game.BackToMainMenuPage();
            }
        }

        private void DrawOptionTab(Rect rect, string label, int tab)
        {
            if (GUI.Button(rect, label, buttonStyle))
                optionsTab = tab;
            if (optionsTab != tab)
                return;
            var marker = PixelWorldArt.GetTexture(WorldInteractionArt.DashReady);
            if (marker == null)
                return;
            GUI.DrawTexture(new Rect(rect.x + 14f, rect.y + 12f, 22f, 22f), marker, ScaleMode.ScaleToFit, true);
            GUI.DrawTexture(new Rect(rect.xMax - 36f, rect.y + 12f, 22f, 22f), marker, ScaleMode.ScaleToFit, true);
        }

        private void DrawAudioOptions(Rect panel)
        {
            GUI.Label(new Rect(panel.x + 70, panel.y + 145, 760, 30), "소리 종류별 음량", bodyStyle);
            for (var index = 0; index < AudioChannels.Length; index++)
            {
                var channel = AudioChannels[index];
                var y = panel.y + 192 + index * 64;
                GUI.Label(new Rect(panel.x + 95, y, 230, 40), GetAudioChannelName(channel), bodyStyle);
                if (GUI.Button(new Rect(panel.x + 360, y, 52, 38), "−", buttonStyle))
                    game.ChangeVolume(channel, -.1f);
                var value = Mathf.RoundToInt(game.Settings.GetVolume(channel) * 100f);
                GUI.Label(new Rect(panel.x + 430, y, 110, 38), value + "%", centerStyle);
                if (GUI.Button(new Rect(panel.x + 560, y, 52, 38), "+", buttonStyle))
                    game.ChangeVolume(channel, .1f);
                DrawVolumeBar(new Rect(panel.x + 650, y + 8, 224, 22), value / 100f);
            }
        }

        private void DrawControlOptions(Rect panel)
        {
            GUI.Label(new Rect(panel.x + 350, panel.y + 171, 195, 28), "기본 키", centerStyle);
            GUI.Label(new Rect(panel.x + 565, panel.y + 171, 195, 28), "보조 키", centerStyle);

            const int actionsPerPage = 6;
            var pageCount = Mathf.CeilToInt(ConfigurableActions.Length / (float)actionsPerPage);
            controlsPage = Mathf.Clamp(controlsPage, 0, pageCount - 1);
            var firstAction = controlsPage * actionsPerPage;
            var lastAction = Mathf.Min(firstAction + actionsPerPage, ConfigurableActions.Length);
            for (var index = firstAction; index < lastAction; index++)
            {
                var action = ConfigurableActions[index];
                var binding = game.Settings.GetBinding(action);
                var y = panel.y + 202 + (index - firstAction) * 46;
                var actionLabel = "[" + GameSettingsService.GetActionCategory(action) + "]  " + GameSettingsService.GetActionName(action);
                GUI.Label(new Rect(panel.x + 75, y + 4, 265, 38), actionLabel, bodyStyle);
                DrawBindingButton(new Rect(panel.x + 350, y + 2, 195, 38), action, binding.primary, false);
                DrawBindingButton(new Rect(panel.x + 565, y + 2, 195, 38), action, binding.alternate, true);
            }

            if (!rebindingAction.HasValue)
            {
                if (GUI.Button(new Rect(panel.x + 65, panel.y + 495, 125, 40), "이전", buttonStyle))
                    controlsPage = Mathf.Max(0, controlsPage - 1);
                GUI.Label(new Rect(panel.x + 195, panel.y + 500, 120, 32), (controlsPage + 1) + " / " + pageCount, centerStyle);
                if (GUI.Button(new Rect(panel.x + 320, panel.y + 495, 125, 40), "다음", buttonStyle))
                    controlsPage = Mathf.Min(pageCount - 1, controlsPage + 1);
                if (GUI.Button(new Rect(panel.x + 680, panel.y + 495, 195, 40), "기본값 복원", buttonStyle))
                {
                    rebindingAction = null;
                    game.ResetBindings();
                }
            }

            if (rebindingAction.HasValue)
            {
                var text = GameSettingsService.GetActionName(rebindingAction.Value) + " — 새 입력 대기 중...";
                GUI.Box(new Rect(panel.x + 220, panel.y + 492, 500, 44), text, slotStyle);
            }
        }

        private void DrawDisplayOptions(Rect panel)
        {
            GUI.Label(new Rect(panel.x + 130, panel.y + 205, 250, 52), "화면 모드", bodyStyle);
            var screenLabel = game.Settings.Fullscreen ? "전체 화면" : "창 모드";
            if (GUI.Button(new Rect(panel.x + 440, panel.y + 202, 300, 56), screenLabel, buttonStyle))
                game.ToggleFullscreen();
        }

        private void DrawBindingButton(Rect rect, GameAction action, KeyCode key, bool alternate)
        {
            var waiting = rebindingAction == action && rebindingAlternate == alternate;
            var label = waiting ? "입력 대기..." : GameSettingsService.GetKeyName(key);
            if (!GUI.Button(rect, label, buttonStyle))
                return;
            rebindingAction = action;
            rebindingAlternate = alternate;
            rebindStartedFrame = Time.frameCount;
        }

        private void CaptureRebindEvent()
        {
            if (!rebindingAction.HasValue || Time.frameCount <= rebindStartedFrame)
                return;
            var current = Event.current;
            KeyCode key;
            if (current.type == EventType.KeyDown)
            {
                if (current.keyCode == KeyCode.Escape)
                {
                    rebindingAction = null;
                    current.Use();
                    return;
                }
                key = current.keyCode == KeyCode.Backspace ? KeyCode.None : current.keyCode;
            }
            else if (current.type == EventType.MouseDown && current.button >= 0 && current.button <= 6)
                key = (KeyCode)((int)KeyCode.Mouse0 + current.button);
            else
                return;

            game.SetBinding(rebindingAction.Value, key, rebindingAlternate);
            rebindingAction = null;
            current.Use();
        }

        private static string GetAudioChannelName(AudioChannel channel)
        {
            switch (channel)
            {
                case AudioChannel.Music:
                    return "음악";
                case AudioChannel.Interface:
                    return "인터페이스";
                case AudioChannel.Ambience:
                    return "환경음";
                case AudioChannel.Effects:
                    return "효과음";
                case AudioChannel.Player:
                    return "플레이어 소리";
                case AudioChannel.Monster:
                    return "몬스터·피격음";
                case AudioChannel.Inventory:
                    return "인벤토리·상자";
                default:
                    return "전체 음량";
            }
        }

        private static void DrawVolumeBar(Rect rect, float ratio)
        {
            VolumeGaugeSkin.Draw(rect, ratio);
        }
    }
}
