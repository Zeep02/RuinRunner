using UnityEngine;

namespace RuinRunner
{
    public sealed partial class PrototypeHud : MonoBehaviour
    {
        private GameController game;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle centerStyle;
        private GUIStyle buttonStyle;
        private GUIStyle disabledButtonStyle;
        private GUIStyle detailStyle;
        private GUIStyle quickKeyStyle;
        private GUIStyle quickAmmoStyle;
        private GUIStyle quickNameStyle;
        private GUIStyle panelStyle;
        private GUIStyle slotStyle;
        private GUIStyle selectedSlotStyle;
        private GUIStyle selectedTabStyle;
        private GUIStyle gaugeTextStyle;
        private GUIStyle gaugeFrameStyle;
        private readonly GUIContent hudNameContent = new GUIContent();
        private string inventoryDragKey = string.Empty;
        private Texture2D inventoryDragIcon;
        private Vector2 inventoryDragStart;
        private bool inventoryDragMoved;
        private int optionsTab;
        private int controlsPage;
        private GameAction? rebindingAction;
        private bool rebindingAlternate;
        private int rebindStartedFrame;
        private Vector2 activeSetScroll;
        private Vector2 equipmentScroll;
        private bool inventorySetTab;
        private string tooltipScrollKey;
        private Vector2 tooltipScroll;
        private readonly DungeonMapView mapView = new DungeonMapView();
        private void OnDestroy() => mapView.Dispose();

        private static readonly AudioChannel[] AudioChannels =
        {
            AudioChannel.Master,
            AudioChannel.Music,
            AudioChannel.Interface,
            AudioChannel.Effects,
            AudioChannel.Ambience
        };

        private static readonly GameAction[] ConfigurableActions =
        {
            GameAction.MoveLeft,
            GameAction.MoveRight,
            GameAction.MoveUp,
            GameAction.MoveDown,
            GameAction.Attack,
            GameAction.Dash,
            GameAction.Guard,
            GameAction.Interact,
            GameAction.Inventory,
            GameAction.Map,
            GameAction.Stats,
            GameAction.Weapon1,
            GameAction.Weapon2,
            GameAction.Weapon3,
            GameAction.Reward4,
            GameAction.Reward5,
            GameAction.Reward6,
            GameAction.Reload,
            GameAction.Dismantle,
            GameAction.Pause
        };

        public void Initialize(GameController controller)
        {
            game = controller;
        }

        private void OnGUI()
        {
            if (game == null) return;

            ClearViewportMargins();
            PrepareStyles();
            var scale = GameTypography.LayoutScale;
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var width = Screen.width / scale;
            var height = Screen.height / scale;

            if (!game.IsRunActive)
            {
                DrawFrontEnd(width, height);
                DrawSessionOverlay(width,height);
                GUI.matrix = oldMatrix;
                return;
            }

            if (game.IsStageTransitioning)
            {
                var oldColor=GUI.color;GUI.color=new Color(.035f,.045f,.065f,1);
                GUI.DrawTexture(new Rect(0,0,width,height),Texture2D.whiteTexture);GUI.color=oldColor;
                DrawPanel(new Rect(width/2-300,height/2-70,600,140));
                GUI.Label(new Rect(width/2-280,height/2-48,560,96),game.CurrentStageIndex==0?"원정을 준비하는 중…":"봉인 너머로…\n장비와 성장은 이어집니다",titleStyle);
                GUI.matrix = oldMatrix;
                return;
            }

            var hasModal = game.IsGameOver || game.IsChoosingAccessory || game.IsInventoryOpen || game.IsStatsOpen ||
                           game.IsUnlockOpen || game.IsManuallyPaused || game.IsMapOpen;
            if (!hasModal && !game.IsTown && !game.IsServiceOpen)
            {
                DrawRunHud();
                DrawMiniMap(width);
                DrawQuickSlots(width, height);
                DrawBossHealth(width);
                DrawNotifications(width);
                DrawStageGuidance(width, height);
            }
            if (game.IsMapOpen) mapView.DrawExpanded(game, width, height, bodyStyle, centerStyle);

            if (game.IsGameOver)
            {
                ClearInventoryDrag();
                if (!game.IsVictory && game.Player != null && game.Player.IsDead && game.Player.DeathProgress < 1f)
                { GUI.matrix = oldMatrix; return; }
                if (game.IsUnlockOpen) DrawMetaUnlock(width, height);
                else DrawResult(width, height);
            }
            else
            {
                if (game.IsChoosingAccessory) DrawAccessorySelection(width, height);
                if (game.IsInventoryOpen)
                    DrawInventory(width, height);
                else
                    ClearInventoryDrag();
                if (game.IsStatsOpen) DrawStats(width, height);
                if (game.IsUnlockOpen) DrawMetaUnlock(width, height);
                if (game.IsManuallyPaused) DrawPause(width, height);
            }

            DrawSessionOverlay(width,height);
            GUI.matrix = oldMatrix;
        }

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
                GUI.DrawTexture(new Rect(width / 2f - 250f, height / 2f - 286f, 500f, 232f), logo, ScaleMode.ScaleToFit, true);

            var x = width / 2f - 150f;
            var y = height / 2f + 14f;
            if (GUI.Button(new Rect(x, y, 300, 62), "시작", buttonStyle)) game.OpenSaveSlots();
            if (GUI.Button(new Rect(x, y + 78, 300, 62), "옵션", buttonStyle)) game.OpenOptions();
            if (GUI.Button(new Rect(x, y + 156, 300, 62), "나가기", buttonStyle)) game.QuitGame();
        }

        private void DrawSaveSlots(float width, float height)
        {
            PrepareTownStyles();
            var panel = new Rect(width / 2f - 440, height / 2f - 285, 880, 570);
            DrawPanel(panel);
            GUI.Label(new Rect(panel.x + 40, panel.y + 20, panel.width - 80, 48), "모험가 기록", titleStyle);
            GUI.Label(new Rect(panel.x + 45, panel.y + 61, panel.width - 90, 25), "마을에서 준비한 뒤 출정합니다. 유산과 해금은 서버에 보존됩니다.", centerStyle);

            bool canSelect=GUI.enabled;
            GUI.enabled=canSelect&&!game.SessionOperationPending&&!game.BackendBusy&&game.ResetProfileSlot==0;

            for (var slotNumber = 1; slotNumber <= 3; slotNumber++)
            {
                var summary = game.SaveSlots.GetSummary(slotNumber);
                var row = new Rect(panel.x + 45, panel.y + 88 + (slotNumber - 1) * 128, panel.width - 90, 108);
                GUI.Box(row, string.Empty, slotStyle);
                GUI.Label(new Rect(row.x + 18, row.y + 34, 115, 34), "SAVE " + slotNumber, bodyStyle);

                if (!summary.Exists)
                {
                    GUI.Label(new Rect(row.x + 145, row.y + 29, 330, 54), "빈 슬롯\n새로운 유산을 시작합니다.", detailStyle);
                    if (GUI.Button(new Rect(row.x + row.width - 195, row.y + 27, 165, 54), "마을 입장", buttonStyle))
                    {
                        game.StartSlot(slotNumber);
                    }
                    continue;
                }

                var details = (summary.IsServerProfile ? summary.Name : "기존 기록 · 마을 입장 시 이관") +
                              "\n도전 " + summary.TotalRuns + "회  ·  엔딩 " + summary.CampaignVictories + "회  ·  봉인 " + summary.BestCampaignStage + "/3" +
                              "\n처치 " + summary.TotalKills + "  ·  유산 조각 " + summary.LegacyShards;
                GUI.Label(new Rect(row.x + 145, row.y + 12, 390, 88), details, detailStyle);
                if (GUI.Button(new Rect(row.x + row.width - 225, row.y + 13, 195, 40), "마을로 입장", buttonStyle))
                {
                    game.StartSlot(slotNumber);
                }
                if(summary.IsServerProfile&&GUI.Button(new Rect(row.x+row.width-225,row.y+61,195,32),"프로필 초기화",compactButton))
                    game.RequestProfileReset(slotNumber);
            }
            GUI.enabled=canSelect;
            if(game.ResetProfileSlot>0){DrawProfileReset(width,height);return;}
            GUI.enabled=canSelect&&!game.SessionOperationPending&&!game.BackendBusy;
            if (GUI.Button(new Rect(panel.x + 325, panel.y + 495, 230, 48), "뒤로", buttonStyle))
            {
                game.BackToMainMenuPage();
            }
            GUI.enabled=canSelect;
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

            if (optionsTab == 0) DrawAudioOptions(panel);
            else if (optionsTab == 1) DrawControlOptions(panel);
            else DrawDisplayOptions(panel);

            if (GUI.Button(new Rect(panel.x + 355, panel.y + 550, 230, 48), "뒤로", buttonStyle))
            {
                rebindingAction = null;
                game.BackToMainMenuPage();
            }
        }

        private void DrawOptionTab(Rect rect, string label, int tab)
        {
            if (GUI.Button(rect, label, buttonStyle)) optionsTab = tab;
            if (optionsTab != tab) return;
            var marker = PixelWorldArt.GetTexture(WorldInteractionArt.DashReady);
            if (marker == null) return;
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
                if (GUI.Button(new Rect(panel.x + 360, y, 52, 38), "−", buttonStyle)) game.ChangeVolume(channel, -.1f);
                var value = Mathf.RoundToInt(game.Settings.GetVolume(channel) * 100f);
                GUI.Label(new Rect(panel.x + 430, y, 110, 38), value + "%", centerStyle);
                if (GUI.Button(new Rect(panel.x + 560, y, 52, 38), "+", buttonStyle)) game.ChangeVolume(channel, .1f);
                DrawVolumeBar(new Rect(panel.x + 650, y + 8, 224, 22), value / 100f);
            }
        }

        private void DrawControlOptions(Rect panel)
        {
            GUI.Label(new Rect(panel.x + 70, panel.y + 143, 760, 30), "행동을 선택한 뒤 새 키나 마우스 버튼을 누르세요.  ESC: 취소 · Backspace: 해제", detailStyle);
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
            if (GUI.Button(new Rect(panel.x + 440, panel.y + 202, 300, 56), screenLabel, buttonStyle)) game.ToggleFullscreen();
            GUI.Label(new Rect(panel.x + 130, panel.y + 295, 610, 60), "화면과 음량, 조작키 설정은 세이브 슬롯과 분리되어 모든 프로필에 공통 적용됩니다.", detailStyle);
        }

        private void DrawBindingButton(Rect rect, GameAction action, KeyCode key, bool alternate)
        {
            var waiting = rebindingAction == action && rebindingAlternate == alternate;
            var label = waiting ? "입력 대기..." : GameSettingsService.GetKeyName(key);
            if (!GUI.Button(rect, label, buttonStyle)) return;
            rebindingAction = action;
            rebindingAlternate = alternate;
            rebindStartedFrame = Time.frameCount;
        }

        private void CaptureRebindEvent()
        {
            if (!rebindingAction.HasValue || Time.frameCount <= rebindStartedFrame) return;
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
            else return;

            game.SetBinding(rebindingAction.Value, key, rebindingAlternate);
            rebindingAction = null;
            current.Use();
        }

        private static string GetAudioChannelName(AudioChannel channel)
        {
            switch (channel)
            {
                case AudioChannel.Music: return "음악";
                case AudioChannel.Interface: return "인터페이스";
                case AudioChannel.Ambience: return "환경음";
                case AudioChannel.Effects: return "효과음";
                case AudioChannel.Player: return "플레이어 소리";
                case AudioChannel.Monster: return "몬스터·피격음";
                case AudioChannel.Inventory: return "인벤토리·상자";
                default: return "전체 음량";
            }
        }

        private static void DrawVolumeBar(Rect rect, float ratio)
        {
            VolumeGaugeSkin.Draw(rect, ratio);
        }

        private void DrawRunHud()
        {
            if (game.Player != null)
            {
                var healthRatio = Mathf.Clamp01(game.Player.Health / game.Player.MaxHealth);
                DrawPixelBar(new Rect(20, 20, 248, 24), healthRatio, PixelHudBarType.Health,
                    Mathf.CeilToInt(game.Player.Health) + " / " + Mathf.CeilToInt(game.Player.MaxHealth));

                var manaRatio = Mathf.Clamp01(game.Player.Mana / game.Player.MaxMana);
                DrawPixelBar(new Rect(20, 43, 248, 22), manaRatio, PixelHudBarType.Mana,
                    Mathf.CeilToInt(game.Player.Mana) + " / " + Mathf.CeilToInt(game.Player.MaxMana));
            }

            if (game.Progression != null)
            {
                var xpRatio = Mathf.Clamp01(game.Progression.Experience / (float)game.Progression.RequiredExperience);
                DrawPixelBar(new Rect(20, 68, 248, 12), xpRatio, PixelHudBarType.Experience, string.Empty);
                GUI.Label(new Rect(22, 82, 100, 28), "Lv " + game.Progression.Level, detailStyle);
            }
        }

        private void DrawPixelBar(Rect rect, float ratio, PixelHudBarType type, string label)
        {
            ratio = Mathf.Clamp01(ratio);
            // The same image frame as the rest of the interface, preserving its corners.
            GUI.Box(rect, GUIContent.none, gaugeFrameStyle);
            var oldColor = GUI.color;
            GUI.color = type == PixelHudBarType.Mana ? new Color(.48f, .73f, .94f) :
                type == PixelHudBarType.Experience ? new Color(.72f, .88f, .38f) : new Color(.81f, .25f, .27f);
            const float padding = 5f;
            var fill = new Rect(rect.x + padding, rect.y + padding, Mathf.Floor((rect.width - padding * 2) * ratio), rect.height - padding * 2);
            if (fill.width > 0) GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = oldColor;
            if (!string.IsNullOrEmpty(label))
            {
                GUI.Label(rect, label, gaugeTextStyle);
            }
        }

        private void DrawQuickSlots(float width, float height)
        {
            if (game.Inventory == null || game.IsGameOver) return;

            var activeWeapon = game.Inventory.EquippedWeapon;
            var nameY = game.IsStageResultReady && game.IsNearStageExit ? height - 196f : height - 136f;
            if (activeWeapon != null)
                DrawHudName(new Rect(width * .5f - 230f, nameY, 460f, 24f), activeWeapon.Name, quickNameStyle);

            var startX = width * .5f - 132f;
            for (var index = 0; index < game.Inventory.QuickSlots.Count; index++)
            {
                var slot = game.Inventory.QuickSlots[index];
                var active = index == game.Inventory.ActiveSlotIndex;
                var rect = new Rect(startX + index * 92f, height - 108f, 80f, 92f);
                DrawInventorySlot(rect, active);
                var action = index == 0 ? GameAction.Weapon1 : index == 1 ? GameAction.Weapon2 : GameAction.Weapon3;
                GUI.Label(new Rect(rect.x + 6f, rect.y + 3f, 68f, 14f), Key(action), quickKeyStyle);
                if (slot.Weapon == null)
                {
                    GUI.Label(rect, "—", quickNameStyle);
                    continue;
                }

                var icon = InventoryIconFactory.Get(slot.Weapon.Id, slot.Weapon.Color, InventoryIconKind.Weapon);
                ItemIconLibrary.DrawGUI(new Rect(rect.center.x - 26f, rect.y + 20f, 52f, 54f), icon);
                if (slot.Weapon.AttackType == WeaponAttackType.Ranged)
                {
                    var ammo = slot.IsReloading ? "R" : slot.Ammo.ToString();
                    GUI.Label(new Rect(rect.x + 6f, rect.y + 76f, 68f, 14f), ammo, quickAmmoStyle);
                }
            }
        }

        private void DrawMiniMap(float width) => mapView.DrawMini(game, width, bodyStyle);

        private void DrawWaveBanner(float width)
        {
            if (Time.unscaledTime >= game.WaveBannerUntil) return;

            var oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01((game.WaveBannerUntil - Time.unscaledTime) * 2f));
            GUI.Label(new Rect(width / 2f - 300, 125, 600, 70), game.WaveBanner, titleStyle);
            GUI.color = oldColor;
        }

        private void DrawBossHealth(float width)
        {
            if (game.Boss == null || game.IsGameOver) return;

            var bossRatio = Mathf.Clamp01(game.Boss.Health / game.Boss.MaxHealth);
            DrawHudName(new Rect(width / 2f - 240, 14, 480, 30), game.CurrentStage.BossName, titleStyle);
            DrawPixelBar(new Rect(width / 2f - 300, 48, 600, 42), bossRatio, PixelHudBarType.Boss,
                Mathf.CeilToInt(game.Boss.Health) + " / " + Mathf.CeilToInt(game.Boss.MaxHealth));
        }

        private void DrawAccessorySelection(float width, float height)
        {
            var oldColor = GUI.color;
            GUI.color = new Color(.025f, .035f, .035f, .9f);
            GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);
            GUI.color = oldColor;
            GUI.Label(new Rect(width / 2f - 300, height / 2f - 282, 600, 44), "LEVEL UP — 장신구 선택", titleStyle);
            var hovered = -1;
            for (var index = 0; index < game.CurrentAccessoryChoices.Length; index++)
            {
                var definition = game.CurrentAccessoryChoices[index];
                var angle = index * Mathf.PI * 2f / game.CurrentAccessoryChoices.Length;
                var rect = new Rect(width / 2f + Mathf.Sin(angle) * 190f - 48f,
                    height / 2f - 5f - Mathf.Cos(angle) * 155f - 48f, 96f, 96f);
                var isHovered = rect.Contains(Event.current.mousePosition);
                GUI.color = Color.Lerp(Color.white, definition.Color, .5f);
                GUI.Box(rect, string.Empty, isHovered ? selectedSlotStyle : slotStyle);
                GUI.color = oldColor;
                var icon = InventoryIconFactory.Get(definition.Id, definition.Color, InventoryIconKind.Accessory);
                ItemIconLibrary.DrawGUI(new Rect(rect.x + 16, rect.y + 16, 64, 64), icon);
                GUI.Label(new Rect(rect.x-40,rect.yMax+3,rect.width+80,30),
                    "["+Key(GameSettingsService.AccessoryChoiceAction(index))+"]",centerStyle);
                if (isHovered) hovered = index;
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                {
                    game.SelectAccessory(definition);
                    return;
                }
            }
            GUI.Label(new Rect(width / 2f - 380, height / 2f + 235, 760, 64),
                "장신구를 클릭하거나 표시된 키로 선택하세요.\n마우스를 올리면 효과와 세트 정보를 확인할 수 있습니다.", centerStyle);
            if (hovered >= 0) DrawItemTooltip(width, height, "C:" + hovered);
        }

        private void DrawNotifications(float width)
        {
            if (Time.unscaledTime < game.LootMessageUntil)
            {
                var lineCount = game.LootMessage.Split('\n').Length;
                var toastHeight = Mathf.Clamp(28f + lineCount * 24f, 76f, 190f);
                DrawPanel(new Rect(width / 2f - 270, 88, 540, toastHeight));
                GUI.Label(new Rect(width / 2f - 250, 94, 500, toastHeight - 12f), game.LootMessage, centerStyle);
            }
        }

        private void DrawStageGuidance(float width, float height)
        {
            var goal = game.DungeonMap?.GetObjectiveText(game.DungeonMap.PhysicalRegion,game.IsStageResultReady,game.PendingStageRewards>0)??"유적을 탐색하세요.";
            GUI.Label(new Rect(20, 112, 300, 30), (game.CurrentStageIndex + 1) + "/3  " + game.CurrentStage.Name, bodyStyle);
            GUI.Label(new Rect(20, 146, 300, 54), goal, detailStyle);
            if (Time.unscaledTime < game.StageIntroUntil)
            {
                var instructions = Key(GameAction.Interact)+" 상호작용   ·   "+Key(GameAction.Map)+" 지도   ·   "+Key(GameAction.Inventory)+" 가방";
                var intro = new Rect(width/2-290, 28, 580, 84);
                DrawPanel(intro);
                GUI.Label(new Rect(intro.x+20,intro.y+8,540,30), game.CurrentStage.Name, bodyStyle);
                GUI.Label(new Rect(intro.x+20,intro.y+43,540,28), instructions, centerStyle);
            }
            if (!game.IsStageResultReady || !game.IsNearStageExit) return;
            var prompt = new Rect(width / 2f - 270f, height - 168f, 540f, 58f);
            if (game.PendingStageRewards > 0)
            {
                var hold = game.LeaveEquipmentProgress > 0 ? "  " + Mathf.CeilToInt(game.LeaveEquipmentProgress * 100) + "%" : "";
                if (GUI.Button(prompt, "남은 장비 " + game.PendingStageRewards + "개 두고 가기  [" + Key(GameAction.Interact) + " 길게]" + hold, buttonStyle)) game.LeaveUncollectedStageEquipment();
                return;
            }
            var action = game.CurrentStage.IsFinal ? "마지막 봉인 해제" : CampaignStage.Get(game.CurrentStageIndex + 1).Name + " 진입";
            if (GUI.Button(prompt, action + "  [" + Key(GameAction.Interact) + "]", buttonStyle)) game.ShowStageResult();
        }


        private void DrawInventory(float width, float height)
        {
            PrepareTownStyles();
            var panel = new Rect(width / 2f - 500, height / 2f - 310, 1000, 620);
            DrawPanel(panel);
            GUI.Label(new Rect(panel.x + 30, panel.y + 12, 620, 40), "INVENTORY", titleStyle);

            GUI.Label(new Rect(panel.x + 30, panel.y + 52, 620, 30), "QUICK SLOTS", detailStyle);
            var hoverKey = string.Empty;
            var quickSlotRects = new Rect[game.Inventory.QuickSlots.Count];
            for (var slotIndex = 0; slotIndex < game.Inventory.QuickSlots.Count; slotIndex++)
            {
                var slot = game.Inventory.QuickSlots[slotIndex];
                var slotRect = new Rect(panel.x + 30 + slotIndex * 205, panel.y + 80, 190, 76);
                quickSlotRects[slotIndex] = slotRect;
                var slotHovered = slotRect.Contains(Event.current.mousePosition);
                DrawWideInventorySlot(slotRect, slotHovered || slotIndex == game.InventoryFocusQuickSlot);
                GUI.Label(new Rect(slotRect.x + 8, slotRect.y + 4, 26, 16), Key((GameAction)((int)GameAction.Weapon1+slotIndex)), quickKeyStyle);
                if (slot.Weapon == null)
                {
                    GUI.Label(new Rect(slotRect.x + 40, slotRect.y + 12, 140, 38), "비어 있음", quickNameStyle);
                    continue;
                }
                var icon = InventoryIconFactory.Get(slot.Weapon.Id, slot.Weapon.Color, InventoryIconKind.Weapon);
                ItemIconLibrary.DrawGUI(new Rect(slotRect.x+8,slotRect.y+6,68,68),icon);
                GUI.Label(new Rect(slotRect.x+80,slotRect.y+10,100,54),slot.Weapon.Name,talentText);
                DrawUpgradeBadge(slotRect, slot.Item);
                if (slotHovered) hoverKey = "Q:" + slotIndex;
                if (slotHovered && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                    game.FocusInventoryQuickSlot(slotIndex);
                TryBeginInventoryDrag(slotRect, "Q:" + slotIndex, icon);
            }

            var weapons = game.Inventory.StoredWeapons;
            var accessories = game.Inventory.Accessories;
            var inventoryCells = new Rect[game.Inventory.UnlockedCapacity];
            for (var slotIndex = 0; slotIndex < InventorySystem.MaximumCapacity; slotIndex++)
            {
                // Base 5x4 indices never shift when the additional columns unlock.
                var column = InventorySystem.BagColumn(slotIndex);
                var row = InventorySystem.BagRow(slotIndex);
                var cell = new Rect(panel.x + 30 + column * 78, panel.y + 164 + row * 78, 68, 68);
                if (slotIndex >= game.Inventory.UnlockedCapacity)
                {
                    var previousColor = GUI.color;
                    GUI.color = new Color(.6f, .6f, .6f, 1f);
                    DrawInventorySlot(cell, false);
                    GUI.color = previousColor;
                    GUI.DrawTexture(new Rect(cell.center.x - 16, cell.center.y - 16, 32, 32),
                        IndividualArt.Texture("Meadow/UI/InventoryLock"), ScaleMode.ScaleToFit, true);
                    continue;
                }
                Texture2D icon = null;
                inventoryCells[slotIndex] = cell;
                var entry = game.Inventory.GetCell(slotIndex);
                var key = "B:" + slotIndex;
                var itemIndex = entry == null ? -1 : entry.ItemIndex;
                if (entry != null && entry.Kind == InventoryEntryKind.Weapon)
                {
                    var weapon = weapons[itemIndex];
                    icon = InventoryIconFactory.Get(weapon.Id, weapon.Color, InventoryIconKind.Weapon);
                }
                else if (entry != null && entry.Kind == InventoryEntryKind.Accessory)
                {
                    var item = accessories[itemIndex];
                    icon = InventoryIconFactory.Get(item.Id, item.Color, InventoryIconKind.Accessory);
                }

                var hovered = cell.Contains(Event.current.mousePosition);
                DrawInventorySlot(cell, hovered || game.InventoryFocusQuickSlot < 0 && slotIndex == game.InventoryFocusCell);
                if (hovered && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                    game.FocusInventoryCell(slotIndex);
                if (icon == null)
                    continue;

                ItemIconLibrary.DrawGUI(new Rect(cell.x + 7, cell.y + 7, cell.width - 14, cell.height - 14), icon);
                DrawUpgradeBadge(cell, game.Inventory.GetOwnedCell(slotIndex));
                if (hovered) hoverKey = key;
                TryBeginInventoryDrag(cell, key, icon);
            }

            if (GUI.Button(new Rect(panel.x + 650, panel.y + 12, 154, 36), "장비", inventorySetTab ? buttonStyle : selectedTabStyle)) inventorySetTab = false;
            if (GUI.Button(new Rect(panel.x + 816, panel.y + 12, 154, 36), "세트", inventorySetTab ? selectedTabStyle : buttonStyle)) inventorySetTab = true;
            GUI.Label(new Rect(panel.x + 650, panel.y + 50, 320, 28), "골드 " + game.RunGold, detailStyle);
            var equipmentRect = new Rect(panel.x + 650, panel.y + 80, 320, 410);
            if (inventorySetTab) DrawActiveSetEffects(equipmentRect); else DrawEquipmentMaintenance(equipmentRect);
            GUI.Label(new Rect(panel.x + 42, panel.y + 514, 916, 66),
                "이동 키: 가방·장착 장비 선택  ·  " + Key(GameAction.Weapon1) + "/" + Key(GameAction.Weapon2) + "/" + Key(GameAction.Weapon3) + ": 선택 무기 장착\n" +
                Key(GameAction.Dismantle) + ": 버리기  ·  드래그: 이동·교환  ·  " + Key(GameAction.Inventory) + ": 닫기", detailStyle);
            if (!string.IsNullOrEmpty(hoverKey)) DrawItemTooltip(width, height, hoverKey);
            ProcessInventoryDrag(inventoryCells, quickSlotRects);
        }

        private void ClearViewportMargins()
        {
            if (!game.IsRunActive || Event.current.type != EventType.Repaint || game.CameraFollow == null) return;
            var camera = game.CameraFollow.GetComponent<Camera>();
            if (camera == null) return;
            var viewport = camera.pixelRect;
            // PixelPerfectCamera clears its cropped world viewport. IMGUI may draw a
            // tooltip into the bars; clear those margins before this frame's UI so a
            // closed/moved tooltip cannot leave a fragment outside the world image.
            var matrix = GUI.matrix; var color = GUI.color;
            GUI.matrix = Matrix4x4.identity; GUI.color = Color.black;
            if (viewport.y > 0) GUI.DrawTexture(new Rect(0, Screen.height - viewport.y, Screen.width, viewport.y), Texture2D.whiteTexture);
            if (viewport.yMax < Screen.height) GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height - viewport.yMax), Texture2D.whiteTexture);
            if (viewport.x > 0) GUI.DrawTexture(new Rect(0, 0, viewport.x, Screen.height), Texture2D.whiteTexture);
            if (viewport.xMax < Screen.width) GUI.DrawTexture(new Rect(viewport.xMax, 0, Screen.width - viewport.xMax, Screen.height), Texture2D.whiteTexture);
            GUI.color = color; GUI.matrix = matrix;
        }

        private void DrawUpgradeBadge(Rect rect, OwnedEquipment item)
        {
            if (!(item is OwnedWeapon w) || w.Mutation==EquipmentMutation.None && w.Oath==EquipmentOath.None) return;
            // Both badges are in Galmuri; missing glyphs can load OS fallback fonts in Player builds.
            GUI.Label(new Rect(rect.xMax-24,rect.yMax-24,20,20),w.Awakened?"★":"◆",compactCenter??centerStyle);
        }

        private OwnedEquipment lastEquipmentDescription;
        private bool lastEquipmentDetails;
        private void DrawEquipmentMaintenance(Rect rect)
        {
            PrepareTownStyles();GUI.Box(rect,"",slotStyle);var item=game.SelectedEquipment;
            if(item==null){GUI.Label(new Rect(rect.x+20,rect.y+25,rect.width-40,100),"장비를 선택하면 효과와 변형을 확인합니다.",compactStyle);return;}
            if(lastEquipmentDescription!=item||lastEquipmentDetails!=game.ShowItemDetails)
            {equipmentScroll=Vector2.zero;lastEquipmentDescription=item;lastEquipmentDetails=game.ShowItemDetails;}
            string id=item is OwnedWeapon w?w.Definition.Id:((OwnedAccessory)item).Definition.Id;
            DrawContentIcon(new Rect(rect.x+12,rect.y+12,72,72),id,item is OwnedWeapon?"weapon":"accessory");
            GUI.Label(new Rect(rect.x+96,rect.y+20,rect.width-112,60),WrapPanelText(item.Name,rect.width-112),compactStyle);
            string description=item is OwnedWeapon?ContentDescription(id,"weapon"):EquipmentUpgradeRules.AccessoryDescription((OwnedAccessory)item,game.Inventory);
            if(!game.ShowItemDetails)description=ShortEffect(description);
            // Mutation and oath progress remain visible in both modes; details expand the base action.
            if(item is OwnedWeapon weapon)description+="\n"+EquipmentUpgradeRules.WeaponUpgradeText(weapon);
            var viewport=new Rect(rect.x+20,rect.y+94,rect.width-40,190);
            description=WrapPanelText(description,viewport.width-20);
            float textHeight=compactStyle.CalcHeight(new GUIContent(description),viewport.width-20);
            equipmentScroll=GUI.BeginScrollView(viewport,equipmentScroll,new Rect(0,0,viewport.width-20,textHeight));GUI.Label(new Rect(0,0,viewport.width-20,textHeight),description,compactStyle);GUI.EndScrollView();
            if(GUI.Button(new Rect(rect.x+20,rect.y+292,rect.width-40,32),game.ShowItemDetails?"간단히":"상세 보기",compactButton))game.ShowItemDetails=!game.ShowItemDetails;
            if(game.HasDisassemblyConfirmation)
            {
                GUI.Label(new Rect(rect.x+20,rect.y+330,rect.width-40,26),"버린 장비는 사라집니다.",compactStyle);
                if(GUI.Button(new Rect(rect.x+20,rect.y+361,132,32),"버리기 확정",compactButton))game.ConfirmEquipmentDisassembly();
                if(GUI.Button(new Rect(rect.x+164,rect.y+361,132,32),"취소",compactButton))game.CancelEquipmentDisassembly();
            }
            else
            {
                bool old=GUI.enabled;GUI.enabled=old&&game.CanManageEquipment&&game.Inventory.CanDisassemble(item.Identity);
                if(GUI.Button(new Rect(rect.x+20,rect.y+350,rect.width-40,38),"장비 버리기",compactButton))game.RequestEquipmentDisassembly(item.Identity,item.UpgradeLevel);GUI.enabled=old;
            }
        }

        private void DrawInventorySlot(Rect rect, bool selected)
        {
            var style = selected ? selectedSlotStyle : slotStyle;
            if (style == null || style.normal.background == null)
            {
                var oldColor = GUI.color;
                GUI.color = selected ? new Color(1f, .82f, .25f) : new Color(.3f, .33f, .42f, .8f);
                GUI.Box(rect, string.Empty);
                GUI.color = oldColor;
                return;
            }

            GUI.Box(rect, string.Empty, style);
        }

        private void DrawWideInventorySlot(Rect rect, bool selected)
        {
            GUI.Box(rect, string.Empty, selected ? selectedSlotStyle : slotStyle);
        }

        private void TryBeginInventoryDrag(Rect rect, string key, Texture2D icon)
        {
            var current = Event.current;
            if (current.type != EventType.MouseDown || current.button != 0 || !rect.Contains(current.mousePosition)) return;
            inventoryDragKey = key;
            inventoryDragIcon = icon;
            inventoryDragStart = current.mousePosition;
            inventoryDragMoved = false;
            current.Use();
        }

        private void ProcessInventoryDrag(Rect[] inventoryCells, Rect[] quickSlotRects)
        {
            if (string.IsNullOrEmpty(inventoryDragKey)) return;
            var current = Event.current;
            if (current.type == EventType.MouseDrag)
            {
                inventoryDragMoved |= Vector2.Distance(inventoryDragStart, current.mousePosition) > 5f;
                current.Use();
            }
            else if (current.type == EventType.MouseUp && current.button == 0)
            {
                if (inventoryDragMoved)
                {
                    var targetQuickSlot = -1;
                    for (var index = 0; index < quickSlotRects.Length; index++)
                        if (quickSlotRects[index].Contains(current.mousePosition)) targetQuickSlot = index;
                    var targetCell = -1;
                    for (var index = 0; index < inventoryCells.Length; index++)
                        if (inventoryCells[index].Contains(current.mousePosition)) targetCell = index;

                    var parts = inventoryDragKey.Split(':');
                    int sourceIndex;
                    if (parts.Length == 2 && int.TryParse(parts[1], out sourceIndex))
                    {
                        var sourceCell = parts[0] == "B" ? game.Inventory.GetCell(sourceIndex) : null;
                        if (sourceCell != null && targetQuickSlot >= 0 && sourceCell.Kind == InventoryEntryKind.Weapon)
                            game.AssignWeaponToSlot(sourceCell.ItemIndex, targetQuickSlot);
                        else if (sourceCell != null && targetCell >= 0)
                            game.Inventory.MoveBagItem(sourceIndex, targetCell);
                        else if (parts[0] == "Q" && targetQuickSlot >= 0 && targetQuickSlot != sourceIndex)
                            game.SwapWeaponSlots(sourceIndex, targetQuickSlot);
                        else if (parts[0] == "Q" && targetCell >= 0)
                            game.UnequipWeaponSlot(sourceIndex, targetCell);
                    }
                }

                ClearInventoryDrag();
                current.Use();
            }
            else if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                ClearInventoryDrag();
                current.Use();
            }

            if (Event.current.type != EventType.Repaint || inventoryDragIcon == null) return;
            var mouse = Event.current.mousePosition;
            var oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, .9f);
            ItemIconLibrary.DrawGUI(new Rect(mouse.x - 28f, mouse.y - 28f, 56f, 56f), inventoryDragIcon);
            GUI.color = oldColor;
        }

        private void ClearInventoryDrag()
        {
            inventoryDragKey = string.Empty;
            inventoryDragIcon = null;
            inventoryDragMoved = false;
        }

        private void DrawItemTooltip(float width, float height, string key)
        {
            if (key.StartsWith("B:") && int.TryParse(key.Substring(2), out var cellIndex))
            {
                var cell = game.Inventory.GetCell(cellIndex);
                if (cell == null) return;
                key = cell.TooltipKey;
            }
            var parts = key.Split(':');
            int index;
            if (parts.Length != 2 || !int.TryParse(parts[1], out index)) return;

            string title;
            string category;
            string description;
            ItemSetType setType;
            Color rarityColor;
            Texture2D icon;
            if ((parts[0] == "W" && index >= 0 && index < game.Inventory.StoredWeapons.Count) ||
                (parts[0] == "Q" && index >= 0 && index < game.Inventory.QuickSlots.Count && game.Inventory.QuickSlots[index].Weapon != null))
            {
                var weapon = parts[0] == "Q" ? game.Inventory.QuickSlots[index].Weapon : game.Inventory.StoredWeapons[index];
                var owned = parts[0] == "Q" ? game.Inventory.QuickSlots[index].Item : game.Inventory.OwnedWeapons[index];
                title = weapon.Name;
                category = weapon.Rarity + " · " + WeaponFamilyCatalog.GetName(weapon.Family);
                description = "무기 공격력 " + weapon.AttackPower.ToString("0.0") + "   공격속도 x" + (1f / weapon.AttackDelayMultiplier).ToString("0.00");
                if (weapon.AttackType == WeaponAttackType.Ranged)
                    description += "\n탄창 " + weapon.MagazineSize + "   장전 " + weapon.ReloadSeconds.ToString("0.0") + "초   관통 " + weapon.ProjectilePierce;
                else
                    description += "\n" + MeleeComboCatalog.Get(weapon).LightSteps.Length + "연속 기본 콤보";
                    if(System.Array.Exists(MeleeComboCatalog.Get(weapon).LightSteps,step=>step.Motion==MeleeAttackMotion.CrossCut))
                        description+="\n마지막 교차베기 2타 · 총 피해 분산";
                description += "\n무기 스킬: " + WeaponActionDescription(weapon, owned.UpgradeLevel);
                description += "\n" + EquipmentUpgradeRules.WeaponUpgradeText(owned);
                setType = weapon.SetType;
                rarityColor = weapon.Color;
                icon = InventoryIconFactory.Get(weapon.Id, weapon.Color, InventoryIconKind.Weapon);
            }
            else if ((parts[0] == "A" && index >= 0 && index < game.Inventory.Accessories.Count) ||
                     (parts[0] == "C" && index >= 0 && index < game.CurrentAccessoryChoices.Length))
            {
                var item = parts[0] == "C" ? game.CurrentAccessoryChoices[index] : game.Inventory.Accessories[index];
                var owned = parts[0] == "A" ? game.Inventory.OwnedAccessories[index] : null;
                title = owned == null ? item.Name : owned.DisplayName;
                category = item.Rarity + " · 장신구";
                description = owned == null ? EquipmentUpgradeRules.AccessoryDescription(item, 0, game.Inventory) : EquipmentUpgradeRules.AccessoryDescription(owned, game.Inventory);
                if (parts[0] == "C" && game.Inventory.StoredItemCount >= game.Inventory.UnlockedCapacity)
                    description += "\n가방 가득 참 · 선택 시 필드에 드롭";
                setType = item.SetType;
                rarityColor = item.Color;
                icon = InventoryIconFactory.Get(item.Id, item.Color, InventoryIconKind.Accessory);
            }
            else return;

            if(!game.ShowItemDetails)description=ShortEffect(description);
            float tooltipWidth = Mathf.Min(440f,width-16f);
            float contentWidth = tooltipWidth-66f;
            var descriptionHeight = detailStyle.CalcHeight(new GUIContent(description), contentWidth);
            var setText = game.ShowItemDetails ? GetSetProgressText(setType) : "세트 · "+setType+"\n가방 장비 패널에서 상세 보기";
            var setHeight = detailStyle.CalcHeight(new GUIContent(setText), contentWidth);
            var titleHeight=Mathf.Max(32,detailStyle.CalcHeight(new GUIContent(title),tooltipWidth-122));
            var categoryHeight=detailStyle.CalcHeight(new GUIContent(category),tooltipWidth-122);
            var layout=new ItemTooltipLayout(width,height,Event.current.mousePosition,titleHeight,categoryHeight,descriptionHeight,setHeight);
            if(tooltipScrollKey!=key){tooltipScrollKey=key;tooltipScroll=Vector2.zero;}
            var rect=layout.Panel;
            var oldColor = GUI.color;
            GUI.color = Color.white;
            GUI.Box(rect, string.Empty, slotStyle);
            GUI.color = oldColor;
            ItemIconLibrary.DrawGUI(layout.Icon, icon);
            GUI.color = rarityColor;
            GUI.Label(layout.Title, title, detailStyle);
            GUI.color = oldColor;
            GUI.Label(layout.Category, category, detailStyle);
            if(layout.NeedsScroll&&Event.current.type==EventType.ScrollWheel)
            {
                tooltipScroll.y=Mathf.Clamp(tooltipScroll.y+Event.current.delta.y*24,0,Mathf.Max(0,layout.ContentHeight-layout.Viewport.height));
                Event.current.Use();
            }
            tooltipScroll=GUI.BeginScrollView(layout.Viewport,tooltipScroll,new Rect(0,0,layout.ContentWidth,layout.ContentHeight));
            GUI.Label(new Rect(0,0,layout.ContentWidth,descriptionHeight),description,detailStyle);
            GUI.Label(new Rect(0,layout.SetTop,layout.ContentWidth,setHeight),setText,detailStyle);
            GUI.EndScrollView();
            if(layout.NeedsScroll)GUI.Label(new Rect(rect.x+24,layout.Viewport.yMax+3,rect.width-48,24),"휠로 자세히 보기",detailStyle);
        }

        private string GetSetProgressText(ItemSetType setType)
        {
            if (setType == ItemSetType.None) return "세트 없음";

            var count = Mathf.Min(3, game.Inventory.GetSetPieceCount(setType));
            var text = ItemSetCatalog.GetName(setType) + "  " + count + "/3";
            foreach (var bonus in ItemSetCatalog.Bonuses)
                if (bonus.SetType == setType)
                    text += "\n" + (count >= bonus.RequiredPieces ? "● " : "○ ") + bonus.RequiredPieces + "세트: " + bonus.Description;
            return text;
        }

        private void DrawActiveSetEffects(Rect rect)
        {
            GUI.Box(rect, string.Empty, slotStyle);
            GUI.Label(new Rect(rect.x + 24, rect.y + 36, rect.width - 48, 36), "활성 세트 효과", bodyStyle);
            var text = string.Empty;
            var activeCount = 0;
            foreach (ItemSetType setType in new[] { ItemSetType.BloodMoon, ItemSetType.Storm, ItemSetType.Guardian, ItemSetType.Arcanist })
            {
                var count = Mathf.Min(3, game.Inventory.GetSetPieceCount(setType));
                if (count < 2) continue;
                if (activeCount > 0) text += "\n\n";
                text += "● " + ItemSetCatalog.GetName(setType) + " " + count + "/3";
                foreach (var bonus in ItemSetCatalog.Bonuses)
                    if (bonus.SetType == setType && count >= bonus.RequiredPieces) text += "\n   " + bonus.Description;
                activeCount++;
            }
            if (activeCount == 0) text = "활성 효과 없음";
            var viewport = new Rect(rect.x + 24, rect.y + 82, rect.width - 48, rect.height - 126);
            var contentWidth = viewport.width - 18;
            var contentHeight = detailStyle.CalcHeight(new GUIContent(text), contentWidth);
            activeSetScroll = GUI.BeginScrollView(viewport, activeSetScroll, new Rect(0, 0, contentWidth, contentHeight));
            GUI.Label(new Rect(0, 0, contentWidth, contentHeight), text, detailStyle);
            GUI.EndScrollView();
        }

        private void DrawStats(float width, float height)
        {
            if (game.Player == null || game.Player.Stats == null) return;
            var stats = game.Player.Stats;
            DrawPanel(new Rect(width / 2f - 330, height / 2f - 265, 660, 530));
            GUI.Label(new Rect(width / 2f - 275, height / 2f - 240, 550, 45), "CHARACTER STATS", titleStyle);

            var left = width / 2f - 270;
            var top = height / 2f - 165;
            GUI.Label(new Rect(left, top, 280, 34), "체력", bodyStyle);
            GUI.Label(new Rect(left + 300, top, 240, 34), stats.Health.ToString("0") + " / " + stats.MaxHealth.ToString("0"), bodyStyle);
            GUI.Label(new Rect(left, top + 38, 280, 34), "마나", bodyStyle);
            GUI.Label(new Rect(left + 300, top + 38, 240, 34), stats.Mana.ToString("0") + " / " + stats.MaxMana.ToString("0"), bodyStyle);
            GUI.Label(new Rect(left, top + 76, 280, 34), "장착 무기 공격력", bodyStyle);
            GUI.Label(new Rect(left + 300, top + 76, 240, 34), game.Player.Damage.ToString("0.0"), bodyStyle);
            GUI.Label(new Rect(left, top + 114, 280, 34), "치명타 확률 / 피해", bodyStyle);
            GUI.Label(new Rect(left + 300, top + 114, 240, 34), (stats.CriticalChance * 100f).ToString("0") + "% / " + (stats.CriticalDamage * 100f).ToString("0") + "%", bodyStyle);
            GUI.Label(new Rect(left, top + 152, 280, 34), "방어력", bodyStyle);
            GUI.Label(new Rect(left + 300, top + 152, 240, 34), stats.Armor.ToString("0.0"), bodyStyle);
            GUI.Label(new Rect(left, top + 190, 280, 34), "운", bodyStyle);
            GUI.Label(new Rect(left + 300, top + 190, 240, 34), stats.Luck.ToString("0.0") + "  (추가 무기 " + (WeaponLootTable.GetDoubleDropChance(stats.Luck) * 100f).ToString("0") + "%)", bodyStyle);
            GUI.Label(new Rect(left, top + 228, 280, 34), "이동 속도", bodyStyle);
            GUI.Label(new Rect(left + 300, top + 228, 240, 34), stats.MoveSpeed.ToString("0.00"), bodyStyle);
            GUI.Label(new Rect(left, top + 266, 280, 34), "마나 재생 / 특수공격", bodyStyle);
            GUI.Label(new Rect(left + 300, top + 266, 240, 34), stats.ManaRegeneration.ToString("0.0") + "/s  / x" + stats.SpecialDamageMultiplier.ToString("0.00"), bodyStyle);

            var weapon = game.Player.EquippedWeapon;
            var weaponText = weapon == null
                ? "무기 없음 — 가방의 무기를 퀵슬롯으로 드래그하세요"
                : weapon.Rarity + " " + weapon.Name + "\n" + Key(GameAction.Guard) + ": " + WeaponActionDescription(weapon, game.Player.WeaponUpgradeLevel).Split('\n')[0];
            GUI.Label(new Rect(left, top + 310, 540, 72), weaponText, detailStyle);
            GUI.Label(new Rect(width / 2f - 260, height / 2f + 218, 520, 30), Key(GameAction.Stats) + " 또는 " + Key(GameAction.Pause) + "로 닫기", centerStyle);
        }

        private void DrawMetaUnlock(float width,float height)
        { GUI.Label(new Rect(width/2-300,height/2-40,600,80),"재능은 마을 수련지기에게 준비합니다.",centerStyle); }

        private void DrawResult(float width, float height)
        {
            DrawPanel(new Rect(width / 2f - 330, height / 2f - 190, 660, 380));
            GUI.Label(new Rect(width / 2f - 280, height / 2f - 155, 560, 60), game.IsVictory ? "유적의 봉인이 풀렸다" : "도전 실패", titleStyle);
            var reward = game.ResultSettlementConfirmed
                ? "서버 정산 완료 · 유산 +"+game.LastMetaReward+"  (보유 "+game.MetaProgression.LegacyShards+")"
                : "정산 확인 중 · 유산 보상은 아직 미확정";
            var result = "TIME  " + FormatTime(game.Stats.ElapsedSeconds) + "    KILLS  " + game.Stats.EnemiesDefeated + "\n" + reward;
            var message = game.IsVictory
                ? "세 수호자의 빛이 잦아들고, 유적에 새벽이 돌아왔습니다.\n당신이 찾은 유산은 다음 탐험가의 길이 됩니다.\n" + result
                : "회피 타이밍을 익혀 다시 도전하세요.\n" + result;
            GUI.Label(new Rect(width / 2f - 270, height / 2f - 75, 540, 120), message, detailStyle);
            bool enabled=GUI.enabled;GUI.enabled=!game.SessionOperationPending&&!game.BackendBusy;
            if(GUI.Button(new Rect(width/2-240,height/2+60,225,52),game.ResultSettlementConfirmed?"마을로 귀환":"정산 확인 후 귀환",buttonStyle))game.ReturnToTown();
            if(GUI.Button(new Rect(width/2+15,height/2+60,225,52),"메인 메뉴",buttonStyle))game.ReturnToMainMenu();GUI.enabled=enabled;
            GUI.Label(new Rect(width/2-270,height/2+124,540,36),"런 골드는 소멸 · 유산과 해금은 보존",detailStyle);

        }

        private void DrawPause(float width, float height)
        {
            DrawPanel(new Rect(width / 2f - 260, height / 2f - 150, 520, 300));
            GUI.Label(new Rect(width / 2f - 210, height / 2f - 115, 420, 48), "일시정지", titleStyle);
            if (GUI.Button(new Rect(width / 2f - 150, height / 2f - 35, 300, 56), "계속하기", buttonStyle)) game.ResumeGame();
            if (GUI.Button(new Rect(width / 2f - 150, height / 2f + 40, 300, 56), "메인 메뉴", buttonStyle)) game.ReturnToMainMenu();
            GUI.Label(new Rect(width / 2f - 200, height / 2f + 105, 400, 28), Key(GameAction.Pause) + " 키로 계속", centerStyle);
        }

        private void PrepareStyles()
        {
            if (titleStyle != null) return;

            var koreanFont = GameTypography.Regular;
            GUI.skin.font = koreanFont;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = GameTypography.Emphasis,
                fontSize = GameTypography.BodySize,
                fontStyle = FontStyle.Normal,
                padding = new RectOffset(),
                clipping = TextClipping.Overflow,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = Color.white;

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                font = koreanFont,
                fontSize = GameTypography.BodySize,
                fontStyle = FontStyle.Normal,
                padding = new RectOffset(),
                clipping = TextClipping.Overflow,
                alignment = TextAnchor.MiddleLeft
            };
            bodyStyle.normal.textColor = new Color(.94f, .91f, .86f);

            centerStyle = new GUIStyle(bodyStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };

            detailStyle = new GUIStyle(bodyStyle)
            {
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                font = GameTypography.Detail,
                fontSize = GameTypography.DetailSize
            };

            quickKeyStyle = new GUIStyle(detailStyle) { font = GameTypography.Regular, fontSize = 12, wordWrap = false,
                alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            quickAmmoStyle = new GUIStyle(quickKeyStyle) { alignment = TextAnchor.MiddleRight };
            quickNameStyle = new GUIStyle(centerStyle) { font = GameTypography.Detail, fontSize = 20, wordWrap = false,
                clipping = TextClipping.Clip };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                font = koreanFont,
                fontSize = GameTypography.BodySize,
                fontStyle = FontStyle.Normal,
                wordWrap = true
            };

            var panelBackground = PixelUiSkin.GetInterfaceElement(PixelUiElement.Panel);
            var slotBackground = PixelUiSkin.GetInterfaceElement(PixelUiElement.Slot);
            var selectedBackground = PixelUiSkin.GetInterfaceElement(PixelUiElement.SelectedSlot);
            gaugeTextStyle = new GUIStyle(centerStyle) { font = GameTypography.Detail, fontSize = 20,
                padding = new RectOffset(), alignment = TextAnchor.MiddleCenter };
            gaugeFrameStyle = new GUIStyle { border = PixelUiSkin.FrameBorder };
            gaugeFrameStyle.normal.background = slotBackground;
            var buttonBackground = PixelUiSkin.GetButtonState(PixelUiButtonState.Normal);
            var buttonHoverBackground = PixelUiSkin.GetButtonState(PixelUiButtonState.Hover);
            var buttonPressedBackground = PixelUiSkin.GetButtonState(PixelUiButtonState.Pressed);
            var buttonDisabledBackground = PixelUiSkin.GetButtonState(PixelUiButtonState.Disabled);

            panelStyle = new GUIStyle(GUI.skin.box);
            slotStyle = new GUIStyle(GUI.skin.box);
            selectedSlotStyle = new GUIStyle(GUI.skin.box);
            if (panelBackground != null)
            {
                panelStyle.normal.background = panelBackground;
                panelStyle.border = CreateNineSliceBorder(panelBackground, .12f);
                panelStyle.normal.textColor = Color.white;
            }
            if (slotBackground != null)
            {
                slotStyle.normal.background = slotBackground;
                slotStyle.border = CreateNineSliceBorder(slotBackground, .2f);
                GUI.skin.box.normal.background = slotBackground;
                GUI.skin.box.border = CreateNineSliceBorder(slotBackground, .2f);
                GUI.skin.box.normal.textColor = Color.white;
            }
            if (selectedBackground != null)
            {
                selectedSlotStyle.normal.background = selectedBackground;
                selectedSlotStyle.border = CreateNineSliceBorder(selectedBackground, .2f);
            }

            if (buttonBackground != null)
            {
                buttonStyle.normal.background = buttonBackground;
                buttonStyle.border = CreateNineSliceBorder(buttonBackground, .16f);
            }
            if (buttonHoverBackground != null) buttonStyle.hover.background = buttonHoverBackground;
            if (buttonPressedBackground != null) buttonStyle.active.background = buttonPressedBackground;
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = new Color(1f, .92f, .7f);
            buttonStyle.active.textColor = new Color(.92f, .82f, .65f);
            buttonStyle.focused.background = buttonStyle.hover.background;
            buttonStyle.focused.textColor = buttonStyle.hover.textColor;
            buttonStyle.onNormal.background = buttonStyle.normal.background;
            buttonStyle.onHover.background = buttonStyle.hover.background;
            buttonStyle.onActive.background = buttonStyle.active.background;
            buttonStyle.onFocused.background = buttonStyle.focused.background;
            selectedTabStyle = new GUIStyle(buttonStyle);
            selectedTabStyle.normal.background = selectedBackground == null ? buttonBackground : selectedBackground;
            selectedTabStyle.border = CreateNineSliceBorder(selectedBackground, .2f);

            disabledButtonStyle = new GUIStyle(buttonStyle);
            disabledButtonStyle.normal.background = buttonDisabledBackground == null ? buttonBackground : buttonDisabledBackground;
            disabledButtonStyle.hover.background = disabledButtonStyle.normal.background;
            disabledButtonStyle.active.background = disabledButtonStyle.normal.background;
            disabledButtonStyle.focused.background = disabledButtonStyle.normal.background;
            disabledButtonStyle.normal.textColor = new Color(.48f, .52f, .58f);
            disabledButtonStyle.hover.textColor = disabledButtonStyle.normal.textColor;
            disabledButtonStyle.active.textColor = disabledButtonStyle.normal.textColor;

            ApplySkinBackground(GUI.skin.verticalScrollbar, slotBackground, .22f);
            ApplySkinBackground(GUI.skin.verticalScrollbarThumb, selectedBackground, .22f);
            ApplySkinBackground(GUI.skin.verticalScrollbarUpButton, buttonBackground, .2f);
            ApplySkinBackground(GUI.skin.verticalScrollbarDownButton, buttonBackground, .2f);
            ApplySkinBackground(GUI.skin.horizontalScrollbar, slotBackground, .22f);
            ApplySkinBackground(GUI.skin.horizontalScrollbarThumb, selectedBackground, .22f);
            ApplySkinBackground(GUI.skin.horizontalScrollbarLeftButton, buttonBackground, .2f);
            ApplySkinBackground(GUI.skin.horizontalScrollbarRightButton, buttonBackground, .2f);
        }

        private void DrawPanel(Rect rect)
        {
            if (panelStyle != null && panelStyle.normal.background != null)
                GUI.Box(rect, string.Empty, panelStyle);
            else
                GUI.Box(rect, string.Empty);
        }

        private static RectOffset CreateNineSliceBorder(Texture2D texture, float ratio)
        {
            if (texture == null) return new RectOffset();
            return PixelUiSkin.FrameBorder;
        }

        private static void ApplySkinBackground(GUIStyle style, Texture2D texture, float borderRatio)
        {
            if (style == null || texture == null) return;
            style.normal.background = texture;
            style.hover.background = texture;
            style.active.background = texture;
            style.focused.background = texture;
            style.border = CreateNineSliceBorder(texture, borderRatio);
        }

        private string Key(GameAction action)
        {
            var binding = game.Settings.GetBinding(action);
            var key = binding.primary != KeyCode.None ? binding.primary : binding.alternate;
            return GameSettingsService.GetKeyName(key);
        }

        private static string WeaponActionDescription(WeaponDefinition weapon, int level = 0)
        {
            if (weapon == null) return "없음";
            return WeaponActionCatalog.Get(weapon).Description;
        }

        private void DrawHudName(Rect bounds, string name, GUIStyle textStyle)
        {
            hudNameContent.text = name;
            // Reuse the opaque pixel frame so bright world effects cannot erase the label.
            // Fit the name instead of covering the whole available HUD row.
            var width = Mathf.Min(bounds.width, Mathf.Ceil((textStyle.CalcSize(hudNameContent).x + 28f) / 4f) * 4f);
            var plate = new Rect(bounds.center.x - width * .5f, bounds.y - 2f, width, bounds.height + 4f);
            var scale = GameTypography.LayoutScale;
            plate.x = Mathf.Round(plate.x * scale) / scale;
            plate.y = Mathf.Round(plate.y * scale) / scale;
            GUI.Box(plate, GUIContent.none, gaugeFrameStyle);
            GUI.Label(new Rect(plate.x + 14f, bounds.y, plate.width - 28f, bounds.height), hudNameContent, textStyle);
        }

        private static string FormatTime(float seconds)
        {
            var totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (totalSeconds / 60).ToString("00") + ":" + (totalSeconds % 60).ToString("00");
        }
    }
}
