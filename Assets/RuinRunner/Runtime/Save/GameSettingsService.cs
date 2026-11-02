using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RuinRunner
{
    public sealed class GameSettingsService
    {
        [Serializable]
        private sealed class SettingsData
        {
            public int version = 6;
            public float masterVolume = .8f;
            public float musicVolume = .65f;
            public float interfaceVolume = .8f;
            public float ambienceVolume = .55f;
            public float playerVolume = .8f;
            public float monsterVolume = .8f;
            public float inventoryVolume = .8f;
            public float effectsVolume = .8f;
            public bool fullscreen;
            public List<InputBindingData> bindings = new List<InputBindingData>();
        }

        private readonly string settingsPath;
        private readonly bool applyToDevice;
        private SettingsData data = new SettingsData();
        private bool primaryFileHealthy;

        public float MasterVolume => data.masterVolume;
        public bool Fullscreen => data.fullscreen;

        public GameSettingsService(string isolatedSettingsPath = null, bool applyToDevice = true)
        {
            this.applyToDevice = applyToDevice;
            settingsPath = isolatedSettingsPath ?? Path.Combine(Application.persistentDataPath, "RuinRunner", "settings.json");
            Load();
            // Explicit Unity launch options take precedence for this session. A saved
            // windowed preference must not silently undo a requested full-screen run.
            if (applyToDevice)
            {
                var args = Environment.GetCommandLineArgs();
                var index = Array.IndexOf(args, "-screen-fullscreen");
                if (index >= 0 && index + 1 < args.Length && (args[index+1] == "0" || args[index+1] == "1"))
                    data.fullscreen = args[index+1] == "1";
            }
            Apply();
        }

        public float GetVolume(AudioChannel channel)
        {
            switch (channel)
            {
                case AudioChannel.Music: return data.musicVolume;
                case AudioChannel.Interface: return data.interfaceVolume;
                case AudioChannel.Ambience: return data.ambienceVolume;
                case AudioChannel.Player:
                case AudioChannel.Monster:
                case AudioChannel.Inventory:
                case AudioChannel.Effects: return data.effectsVolume;
                default: return data.masterVolume;
            }
        }

        public void ChangeVolume(AudioChannel channel, float delta)
        {
            var value = Mathf.Clamp01(Mathf.Round((GetVolume(channel) + delta) * 10f) / 10f);
            switch (channel)
            {
                case AudioChannel.Music: data.musicVolume = value; break;
                case AudioChannel.Interface: data.interfaceVolume = value; break;
                case AudioChannel.Ambience: data.ambienceVolume = value; break;
                case AudioChannel.Player:
                case AudioChannel.Monster:
                case AudioChannel.Inventory:
                case AudioChannel.Effects:
                    data.effectsVolume = value;
                    // Older demo builds can still understand this file if reopened.
                    data.playerVolume = data.monsterVolume = data.inventoryVolume = value;
                    break;
                default: data.masterVolume = value; break;
            }
            Apply();
            Save();
        }

        public bool GetKey(GameAction action)
        {
            if (StageValidationInput.Enabled) return StageValidationInput.GetKey(action);
            var binding = GetBinding(action);
            return IsPressed(binding.primary) || IsPressed(binding.alternate);
        }

        public bool GetKeyDown(GameAction action)
        {
            if (StageValidationInput.Enabled) return StageValidationInput.GetKeyDown(action);
            var binding = GetBinding(action);
            return IsPressedThisFrame(binding.primary) || IsPressedThisFrame(binding.alternate);
        }

        public bool GetKeyUp(GameAction action)
        {
            if (StageValidationInput.Enabled) return StageValidationInput.GetKeyUp(action);
            var binding = GetBinding(action);
            return IsReleasedThisFrame(binding.primary) || IsReleasedThisFrame(binding.alternate);
        }

        public InputBindingData GetBinding(GameAction action)
        {
            foreach (var binding in data.bindings)
                if (binding.action == action) return binding;
            var fallback = CreateDefaultBinding(action);
            // Adding an action must not steal a key from an existing custom binding.
            foreach (var owned in data.bindings)
            {
                if (fallback.primary != KeyCode.None && (owned.primary == fallback.primary || owned.alternate == fallback.primary)) fallback.primary = KeyCode.None;
                if (fallback.alternate != KeyCode.None && (owned.primary == fallback.alternate || owned.alternate == fallback.alternate)) fallback.alternate = KeyCode.None;
            }
            data.bindings.Add(fallback);
            return fallback;
        }

        public void SetBinding(GameAction action, KeyCode key, bool alternate)
        {
            if (key != KeyCode.None)
            {
                foreach (var owned in data.bindings)
                {
                    if (owned.action == action) continue;
                    if (owned.primary == key) owned.primary = KeyCode.None;
                    if (owned.alternate == key) owned.alternate = KeyCode.None;
                }
            }

            var binding = GetBinding(action);
            if (alternate)
            {
                if (binding.primary == key) binding.primary = KeyCode.None;
                binding.alternate = key;
            }
            else
            {
                if (binding.alternate == key) binding.alternate = KeyCode.None;
                binding.primary = key;
            }
            Save();
        }

        public void ResetBindings()
        {
            data.bindings = CreateDefaultBindings();
            Save();
        }

        public static string GetActionName(GameAction action)
        {
            switch (action)
            {
                case GameAction.MoveLeft: return "왼쪽 이동";
                case GameAction.MoveRight: return "오른쪽 이동";
                case GameAction.MoveUp: return "위쪽 이동";
                case GameAction.MoveDown: return "아래쪽 이동";
                case GameAction.Jump: return "사용 안 함 (레거시 점프)";
                case GameAction.DropDown: return "사용 안 함 (레거시 하강)";
                case GameAction.Attack: return "일반 공격";
                case GameAction.SpecialAttack: return "사용 안 함 (레거시 기술)";
                case GameAction.Dash: return "대시";
                case GameAction.Guard: return "무기 고유 행동";
                case GameAction.Interact: return "상호작용";
                case GameAction.Inventory: return "인벤토리";
                case GameAction.Map: return "탐사 지도";
                case GameAction.Stats: return "상세 스탯";
                case GameAction.Legacy: return "유산 특성";
                case GameAction.Weapon1: return "무기 슬롯 1";
                case GameAction.Weapon2: return "무기 슬롯 2";
                case GameAction.Weapon3: return "무기 슬롯 3";
                case GameAction.Reward4: return "성장 선택 4";
                case GameAction.Reward5: return "성장 선택 5";
                case GameAction.Reward6: return "성장 선택 6";
                case GameAction.Reload: return "장전·강화";
                case GameAction.Dismantle: return "장비 분해";
                default: return "일시정지";
            }
        }

        public static string GetActionCategory(GameAction action)
        {
            switch (action)
            {
                case GameAction.MoveLeft:
                case GameAction.MoveRight:
                case GameAction.MoveUp:
                case GameAction.MoveDown:
                case GameAction.Dash:
                    return "이동";
                case GameAction.Guard:
                case GameAction.Attack:
                case GameAction.SpecialAttack:
                    return "전투";
                case GameAction.Weapon1:
                case GameAction.Weapon2:
                case GameAction.Weapon3:
                case GameAction.Reload:
                    return "무기";
                case GameAction.Dismantle:
                    return "정비";
                case GameAction.Reward4:
                case GameAction.Reward5:
                case GameAction.Reward6:
                    return "성장";
                case GameAction.Interact:
                    return "상호작용";
                default:
                    return "인터페이스";
            }
        }

        public static string GetKeyName(KeyCode key)
        {
            if (key == KeyCode.None) return "미지정";
            if (key == KeyCode.Mouse0) return "마우스 왼쪽";
            if (key == KeyCode.Mouse1) return "마우스 오른쪽";
            if (key == KeyCode.Mouse2) return "마우스 가운데";
            if (key == KeyCode.LeftShift) return "왼쪽 Shift";
            if (key == KeyCode.RightShift) return "오른쪽 Shift";
            if (key == KeyCode.LeftControl) return "왼쪽 Ctrl";
            if (key == KeyCode.RightControl) return "오른쪽 Ctrl";
            if (key == KeyCode.LeftArrow) return "←";
            if (key == KeyCode.RightArrow) return "→";
            if (key == KeyCode.UpArrow) return "↑";
            if (key == KeyCode.DownArrow) return "↓";
            if (key == KeyCode.Alpha0) return "0";
            if (key == KeyCode.Alpha1) return "1";
            if (key == KeyCode.Alpha2) return "2";
            if (key == KeyCode.Alpha3) return "3";
            if (key == KeyCode.Alpha4) return "4";
            if (key == KeyCode.Alpha5) return "5";
            if (key == KeyCode.Alpha6) return "6";
            if (key == KeyCode.Alpha7) return "7";
            if (key == KeyCode.Alpha8) return "8";
            if (key == KeyCode.Alpha9) return "9";
            return key.ToString();
        }

        public void ToggleFullscreen()
        {
            data.fullscreen = !data.fullscreen;
            Apply();
            Save();
        }

        private void Apply()
        {
            if (!applyToDevice) return; // Isolated persistence tests must not change user audio/display.
            AudioListener.volume = data.masterVolume;
            GameDisplayPolicy.Apply(data.fullscreen);
        }

        private void Load()
        {
            var migrateCombatBindings = false;
            var migrateMapBinding = false;
            try
            {
                var loaded = ReadSettings(settingsPath);
                primaryFileHealthy = loaded != null;
                data = loaded ?? ReadSettings(settingsPath + ".bak") ?? new SettingsData();
                if (data.version < 2)
                {
                    data.version = 2;
                    data.musicVolume = .65f;
                    data.interfaceVolume = .8f;
                    data.ambienceVolume = .55f;
                    data.playerVolume = .8f;
                    data.monsterVolume = .8f;
                    data.inventoryVolume = .8f;
                }
                if (data.version < 3)
                {
                    data.version = 3;
                    migrateCombatBindings = true;
                }
                if (data.version < 4)
                {
                    data.version = 4;
                    migrateCombatBindings = true;
                }
                if (data.version < 5)
                {
                    data.version = 5;
                    migrateMapBinding = true;
                }
                if (data.version < 6)
                {
                    data.effectsVolume = (Mathf.Clamp01(data.playerVolume) + Mathf.Clamp01(data.monsterVolume) +
                        Mathf.Clamp01(data.inventoryVolume)) / 3f;
                    data.version = 6;
                }
                data.masterVolume = ValidVolume(data.masterVolume,.8f);
                data.musicVolume = ValidVolume(data.musicVolume,.65f);
                data.interfaceVolume = ValidVolume(data.interfaceVolume,.8f);
                data.ambienceVolume = ValidVolume(data.ambienceVolume,.55f);
                data.playerVolume = ValidVolume(data.playerVolume,.8f);
                data.monsterVolume = ValidVolume(data.monsterVolume,.8f);
                data.inventoryVolume = ValidVolume(data.inventoryVolume,.8f);
                data.effectsVolume = ValidVolume(data.effectsVolume,.8f);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Settings] Load failed: " + exception.Message);
            }
            EnsureBindings();
            if (migrateCombatBindings) ApplyCombatBindingMigration();
            if (migrateMapBinding)
            {
                var inventory = GetBinding(GameAction.Inventory);
                // Only migrate the old default. Custom assignments remain owned by the user.
                if (inventory.primary == KeyCode.I && inventory.alternate == KeyCode.Tab)
                    inventory.alternate = KeyCode.None;
                foreach (var binding in data.bindings)
                    if (binding.action != GameAction.Map &&
                        (binding.primary == KeyCode.Tab || binding.alternate == KeyCode.Tab))
                        GetBinding(GameAction.Map).primary = KeyCode.None;
                Save();
            }
        }

        private void ApplyCombatBindingMigration()
        {
            var reserved = new[]
            {
                KeyCode.LeftControl,
                KeyCode.RightControl,
                KeyCode.LeftShift,
                KeyCode.RightShift,
                KeyCode.Mouse1,
                KeyCode.Space,
                KeyCode.W,
                KeyCode.S,
                KeyCode.UpArrow,
                KeyCode.DownArrow
            };
            foreach (var binding in data.bindings)
            {
                foreach (var key in reserved)
                {
                    if (binding.primary == key) binding.primary = KeyCode.None;
                    if (binding.alternate == key) binding.alternate = KeyCode.None;
                }
            }

            var moveUp = GetBinding(GameAction.MoveUp);
            moveUp.primary = KeyCode.W;
            moveUp.alternate = KeyCode.UpArrow;
            var moveDown = GetBinding(GameAction.MoveDown);
            moveDown.primary = KeyCode.S;
            moveDown.alternate = KeyCode.DownArrow;
            var dash = GetBinding(GameAction.Dash);
            dash.primary = KeyCode.Space;
            dash.alternate = KeyCode.None;
            var guard = GetBinding(GameAction.Guard);
            guard.primary = KeyCode.Mouse1;
            guard.alternate = KeyCode.None;
            var jump = GetBinding(GameAction.Jump);
            jump.primary = KeyCode.None;
            jump.alternate = KeyCode.None;
            var drop = GetBinding(GameAction.DropDown);
            drop.primary = KeyCode.None;
            drop.alternate = KeyCode.None;
            var special = GetBinding(GameAction.SpecialAttack);
            special.primary = KeyCode.None;
            special.alternate = KeyCode.None;
            Save();
        }

        private void EnsureBindings()
        {
            if (data.bindings == null) data.bindings = new List<InputBindingData>();
            var seen = new HashSet<GameAction>();
            data.bindings.RemoveAll(binding => binding == null || !Enum.IsDefined(typeof(GameAction),binding.action) || !seen.Add(binding.action));
            foreach (var binding in data.bindings)
            {
                if (!Enum.IsDefined(typeof(KeyCode),binding.primary)) binding.primary=KeyCode.None;
                if (!Enum.IsDefined(typeof(KeyCode),binding.alternate)) binding.alternate=KeyCode.None;
            }
            foreach (GameAction action in Enum.GetValues(typeof(GameAction))) GetBinding(action);
        }

        public static GameAction AccessoryChoiceAction(int index)
        {
            switch(index)
            {
                case 0: return GameAction.Weapon1;
                case 1: return GameAction.Weapon2;
                case 2: return GameAction.Weapon3;
                case 3: return GameAction.Reward4;
                case 4: return GameAction.Reward5;
                case 5: return GameAction.Reward6;
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        private static float ValidVolume(float value,float fallback)
            => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp01(value);

        private static SettingsData ReadSettings(string path)
        {
            if (!File.Exists(path)) return null;
            try { return JsonUtility.FromJson<SettingsData>(MarkNullBindings(File.ReadAllText(path))); }
            catch (Exception exception) { Debug.LogWarning("[Settings] Recovering unreadable file " + Path.GetFileName(path) + ": " + exception.Message); return null; }
        }

        // JsonUtility turns a null list entry into zero-valued InputBindingData,
        // which is indistinguishable from an intentionally unbound MoveLeft.
        // Mark null array tokens before deserialization; leave strings and null
        // object fields untouched. Settings has only one array: bindings.
        private static string MarkNullBindings(string json)
        {
            var text=new System.Text.StringBuilder(json.Length);
            var quoted=false;var escaped=false;var previous='\0';
            for(var i=0;i<json.Length;i++)
            {
                var c=json[i];
                if(!quoted && (previous=='[' || previous==',') && i+4<=json.Length &&
                    string.CompareOrdinal(json,i,"null",0,4)==0)
                {
                    var next=i+4;while(next<json.Length && char.IsWhiteSpace(json[next]))next++;
                    if(next<json.Length && (json[next]==',' || json[next]==']'))
                    { text.Append("{\"action\":-1}");i+=3;previous='}';continue; }
                }
                text.Append(c);
                if(quoted)
                {
                    if(escaped)escaped=false;
                    else if(c=='\\')escaped=true;
                    else if(c=='\"')quoted=false;
                }
                else if(c=='\"')quoted=true;
                if(!char.IsWhiteSpace(c))previous=c;
            }
            return text.ToString();
        }

        private static List<InputBindingData> CreateDefaultBindings()
        {
            var bindings = new List<InputBindingData>();
            foreach (GameAction action in Enum.GetValues(typeof(GameAction))) bindings.Add(CreateDefaultBinding(action));
            return bindings;
        }

        private static InputBindingData CreateDefaultBinding(GameAction action)
        {
            switch (action)
            {
                case GameAction.MoveLeft: return new InputBindingData(action, KeyCode.A, KeyCode.LeftArrow);
                case GameAction.MoveRight: return new InputBindingData(action, KeyCode.D, KeyCode.RightArrow);
                case GameAction.MoveUp: return new InputBindingData(action, KeyCode.W, KeyCode.UpArrow);
                case GameAction.MoveDown: return new InputBindingData(action, KeyCode.S, KeyCode.DownArrow);
                case GameAction.Jump: return new InputBindingData(action, KeyCode.None);
                case GameAction.DropDown: return new InputBindingData(action, KeyCode.None);
                case GameAction.Attack: return new InputBindingData(action, KeyCode.Mouse0, KeyCode.J);
                case GameAction.SpecialAttack: return new InputBindingData(action, KeyCode.None);
                case GameAction.Dash: return new InputBindingData(action, KeyCode.Space);
                case GameAction.Guard: return new InputBindingData(action, KeyCode.Mouse1);
                case GameAction.Interact: return new InputBindingData(action, KeyCode.E);
                case GameAction.Inventory: return new InputBindingData(action, KeyCode.I);
                case GameAction.Map: return new InputBindingData(action, KeyCode.Tab);
                case GameAction.Stats: return new InputBindingData(action, KeyCode.C);
                case GameAction.Legacy: return new InputBindingData(action, KeyCode.U);
                case GameAction.Weapon1: return new InputBindingData(action, KeyCode.Alpha1);
                case GameAction.Weapon2: return new InputBindingData(action, KeyCode.Alpha2);
                case GameAction.Weapon3: return new InputBindingData(action, KeyCode.Alpha3);
                case GameAction.Reward4: return new InputBindingData(action, KeyCode.Alpha4);
                case GameAction.Reward5: return new InputBindingData(action, KeyCode.Alpha5);
                case GameAction.Reward6: return new InputBindingData(action, KeyCode.Alpha6);
                case GameAction.Reload: return new InputBindingData(action, KeyCode.R);
                case GameAction.Dismantle: return new InputBindingData(action, KeyCode.X);
                default: return new InputBindingData(action, KeyCode.Escape);
            }
        }

        private static bool IsPressed(KeyCode key) => key != KeyCode.None && Input.GetKey(key);
        private static bool IsPressedThisFrame(KeyCode key) => key != KeyCode.None && Input.GetKeyDown(key);
        private static bool IsReleasedThisFrame(KeyCode key) => key != KeyCode.None && Input.GetKeyUp(key);

        private void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(settingsPath);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                var temporary = settingsPath + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(data, true));
                if (File.Exists(settingsPath))
                    File.Replace(temporary, settingsPath, primaryFileHealthy ? settingsPath + ".bak" : null);
                else File.Move(temporary, settingsPath);
                primaryFileHealthy = true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Settings] Save failed: " + exception.Message);
            }
        }
    }
}
