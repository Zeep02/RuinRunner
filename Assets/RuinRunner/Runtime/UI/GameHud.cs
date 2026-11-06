using UnityEngine;

namespace RuinRunner
{
    public sealed partial class GameHud : MonoBehaviour
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
            useGUILayout = false;
        }

        private void OnGUI()
        {
            if (game == null)
                return;

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
                DrawSessionOverlay(width, height);
                GUI.matrix = oldMatrix;
                return;
            }

            if (game.IsStageTransitioning)
            {
                GUI.matrix = oldMatrix;
                return;
            }

            var hasModal = game.IsGameOver || game.IsChoosingAccessory || game.IsInventoryOpen || game.IsStatsOpen ||
                           game.IsManuallyPaused || game.IsMapOpen;
            if (!hasModal && !game.IsTown && !game.IsServiceOpen)
            {
                DrawRunHud();
                DrawMiniMap(width);
                DrawQuickSlots(width, height);
                DrawBossHealth(width);
                DrawWaveBanner(width);
                DrawNotifications(height);
                DrawStageGuidance(width, height);
            }
            if (game.IsMapOpen)
                mapView.DrawExpanded(game, width, height);

            if (!game.IsChoosingAccessory)
                choiceOpenedAt = -1f;
            if (!game.IsGameOver)
                resultOpenedAt = -1f;
            if (game.IsGameOver)
            {
                ClearInventoryDrag();
                if (!game.IsVictory && game.Player != null && game.Player.IsDead && game.Player.DeathProgress < 1f)
                {
                    GUI.matrix = oldMatrix;
                    return;
                }
                if (!game.IsVictory && game.Player != null && !InkTransition.CoverForResult(game.Player.FeetPoint, () => game.IsGameOver))
                {
                    GUI.matrix = oldMatrix;
                    return;
                }
                DrawResult(width, height);
            }
            else
            {
                if (game.IsChoosingAccessory)
                    DrawAccessorySelection(width, height);
                if (game.IsInventoryOpen)
                    DrawInventory(width, height);
                else
                    ClearInventoryDrag();
                if (game.IsStatsOpen)
                    DrawStats(width, height);
                if (game.IsManuallyPaused)
                    DrawPause(width, height);
            }

            DrawSessionOverlay(width, height);
            GUI.matrix = oldMatrix;
        }
    }
}
