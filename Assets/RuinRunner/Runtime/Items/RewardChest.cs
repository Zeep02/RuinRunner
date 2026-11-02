using System;
using System.Collections;
using UnityEngine;

namespace RuinRunner
{
    public sealed class RewardChest : MonoBehaviour
    {
        public bool PlayerInRange { get; private set; }
        public bool IsOpened { get; private set; }
        public int FrameIndex { get; private set; }
        public WeaponRarity Rarity { get; private set; }
        public int ExplorationRegion { get; set; } = -1;
        public string HintName { get; set; } = "보상 상자";
        public bool CanOpen => !IsOpened && (canInteract == null || canInteract());
        private Action onOpened;
        private SpriteRenderer chestRenderer;
        private Sprite[] frames;
        private GameSettingsService settings;
        private Func<bool> canInteract;
        private Func<WeaponRarity> rarityQuery;
        private GUIStyle promptStyle;

        public static RewardChest Create(Vector2 position, Action openedCallback, GameSettingsService gameSettings, Func<bool> interactionQuery = null, WeaponRarity rarity = WeaponRarity.Common, Func<WeaponRarity> gradeQuery = null)
        {
            var root = new GameObject("RewardChest");
            root.transform.position = position;
            var chest = root.AddComponent<RewardChest>();
            chest.Rarity = rarity;
            chest.frames = IndividualArt.Frames("RewardChest/" + rarity, 6);
            chest.chestRenderer = PrototypeVisuals.AddSprite(root, Color.white, 1000 - Mathf.RoundToInt(position.y * 100f));
            chest.chestRenderer.sprite = chest.frames[0];
            chest.onOpened = openedCallback;
            chest.settings = gameSettings;
            chest.canInteract = interactionQuery;
            chest.rarityQuery = gradeQuery;
            var trigger = root.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(2f, 1.8f);
            trigger.offset = new Vector2(0, .25f);
            return chest;
        }

        private void Update()
        {
            if (Time.timeScale <= 0 || canInteract != null && !canInteract()) return;
            if (!IsOpened) RefreshRarity();
            if (!IsOpened && PlayerInRange && settings.GetKeyDown(GameAction.Interact)) Open();
        }

        private void OnGUI()
        {
            if (!CanOpen || !PlayerInRange || Time.timeScale <= 0 || Camera.main == null) return;
            var point = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.05f);
            if (point.z <= 0) return;
            var style = promptStyle ?? (promptStyle = PixelUiSkin.CreateWorldPromptStyle(RewardPresentation.Color(Rarity), 24, false));
            var binding = settings.GetBinding(GameAction.Interact);
            var key = binding.primary != KeyCode.None ? binding.primary : binding.alternate;
            var text = "[" + GameSettingsService.GetKeyName(key) + "] " + RewardPresentation.Name(Rarity) + " · " + HintName;
            var width = Mathf.Min(Screen.width-16, style.CalcSize(new GUIContent(text)).x+16);
            GUI.Box(new Rect(Mathf.Clamp(point.x-width/2,8,Screen.width-width-8),Screen.height-point.y-22,width,44),text,style);
        }
        private void Open()
        {
            if (!CanOpen) return;
            RefreshRarity();
            IsOpened = true;
            StartCoroutine(OpenSequence());
        }
        private void RefreshRarity()
        {
            if (rarityQuery == null) return;
            var grade = rarityQuery();
            if (grade == Rarity) return;
            Rarity = grade;
            frames = IndividualArt.Frames("RewardChest/" + grade, 6);
            chestRenderer.sprite = frames[0];
            promptStyle = null;
        }

        private IEnumerator OpenSequence()
        {
            var interval = RewardPresentation.OpenDuration(Rarity) / (frames.Length - 1);
            for (var frame = 1; frame < frames.Length; frame++)
            {
                yield return new WaitForSeconds(interval);
                FrameIndex = frame;
                chestRenderer.sprite = frames[frame];
                if (frame == 3)
                {
                    RewardPresentation.Reveal(transform.position + Vector3.up * .42f, Rarity, chestRenderer.sortingOrder + 1, true);
                    var callback = onOpened; onOpened = null;
                    callback?.Invoke();
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController>() != null) PlayerInRange = true;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController>() != null) PlayerInRange = false;
        }
    }
}
