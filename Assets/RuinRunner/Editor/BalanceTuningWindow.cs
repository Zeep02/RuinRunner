#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RuinRunner.EditorTools
{
    public sealed class BalanceTuningWindow : EditorWindow
    {
        private const string AssetPath = "Assets/RuinRunner/Resources/Balance/GameBalanceConfig.asset";
        private GameBalanceConfig config;
        private Vector2 scroll;
        private string weaponSearch = string.Empty;
        private bool autoApply = true;
        private bool showPlayer = true;
        private bool showEnemies = true;
        private bool showWeapons = true;

        [MenuItem("RuinRunner/Balance Tuning", priority = 3)]
        public static void Open()
        {
            GetWindow<BalanceTuningWindow>("Balance Tuning").minSize = new Vector2(560f, 620f);
        }

        private void OnEnable()
        {
            LoadOrCreate();
        }

        private void OnGUI()
        {
            if (config == null) LoadOrCreate();
            if (config == null) return;

            EditorGUILayout.LabelField("RUIN RUNNER — 실시간 밸런스 조정", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Play Mode에서도 값을 바꾸면 현재 플레이어·몬스터·무기에 즉시 반영됩니다. 체력 비율과 이미 획득한 강화는 유지됩니다.", MessageType.Info);
            autoApply = EditorGUILayout.ToggleLeft("Play Mode에서 변경 즉시 적용", autoApply);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("현재 씬에 적용", GUILayout.Height(30f))) ApplyNow();
            if (GUILayout.Button("에셋 선택", GUILayout.Height(30f))) Selection.activeObject = config;
            if (GUILayout.Button("무기 목록 동기화", GUILayout.Height(30f)))
            {
                Undo.RecordObject(config, "Sync weapon balance entries");
                EnsureWeaponEntries();
                SaveAndApply();
            }
            EditorGUILayout.EndHorizontal();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUI.BeginChangeCheck();
            DrawPlayer();
            DrawEnemies();
            DrawWeapons();
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                GameBalance.UseConfig(config);
                if (autoApply && Application.isPlaying) GameBalance.ApplyLive();
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawPlayer()
        {
            showPlayer = EditorGUILayout.BeginFoldoutHeaderGroup(showPlayer, "플레이어 기본 스탯");
            if (showPlayer)
            {
                var value = config.player;
                value.maxHealth = Positive("최대 체력", value.maxHealth);
                value.maxMana = Positive("최대 마나", value.maxMana);
                value.armor = NonNegative("방어력", value.armor);
                value.moveSpeed = Positive("이동 속도", value.moveSpeed);
                value.criticalChance = EditorGUILayout.Slider("치명타 확률", value.criticalChance, 0f, .85f);
                value.criticalDamage = Positive("치명타 피해 배율", value.criticalDamage);
                value.luck = NonNegative("운", value.luck);
                value.manaRegeneration = NonNegative("초당 마나 회복", value.manaRegeneration);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawEnemies()
        {
            showEnemies = EditorGUILayout.BeginFoldoutHeaderGroup(showEnemies, "몬스터 스탯");
            if (showEnemies && config.enemies != null)
            {
                config.enemyHealthMultiplier = EditorGUILayout.Slider("일반 몬스터 체력 배율", config.enemyHealthMultiplier, .5f, 3f);
                config.bossHealthMultiplier = EditorGUILayout.Slider("보스 체력 배율", config.bossHealthMultiplier, .5f, 3f);
                config.experienceGainMultiplier = EditorGUILayout.Slider("경험치 획득 배율", config.experienceGainMultiplier, .1f, 2f);
                foreach (var enemy in config.enemies)
                {
                    if (enemy == null) continue;
                    EditorGUILayout.LabelField(enemy.type.ToString(), EditorStyles.boldLabel);
                    using (new EditorGUI.IndentLevelScope())
                    {
                        enemy.maxHealth = Positive("체력", enemy.maxHealth);
                        enemy.attackPower = NonNegative("공격력", enemy.attackPower);
                        enemy.armor = NonNegative("방어력", enemy.armor);
                        enemy.moveSpeed = Positive("이동 속도", enemy.moveSpeed);
                        enemy.experienceReward = Mathf.Max(0, EditorGUILayout.IntField("경험치", enemy.experienceReward));
                        enemy.hurtboxScale = EditorGUILayout.Slider("그림 대비 피격 영역", enemy.hurtboxScale, .6f, 1.5f);
                        enemy.hurtboxPadding = EditorGUILayout.Slider("피격 판정 여유", enemy.hurtboxPadding, 0f, .5f);
                        enemy.hurtboxOffset = EditorGUILayout.Vector2Field("피격 영역 오프셋", enemy.hurtboxOffset);
                        enemy.movementFootSize = EditorGUILayout.Vector2Field("발밑 이동 충돌 크기", enemy.movementFootSize);
                        enemy.movementFootOffset = EditorGUILayout.Vector2Field("발밑 이동 충돌 위치", enemy.movementFootOffset);
                        enemy.healthBarGap = EditorGUILayout.Slider("체력바 위쪽 여백", enemy.healthBarGap, .05f, 1f);
                        EditorGUILayout.HelpBox("Scene에서 몬스터 선택: 청록=발밑 이동 충돌, 주황=피격 영역. 피격 영역은 벽 충돌 크기를 바꾸지 않습니다.", MessageType.None);
                        if (Application.isPlaying && GUILayout.Button("씬에서 " + enemy.type + " 선택"))
                            foreach (var actor in UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                                if (actor.Type == enemy.type) 
{ 
Selection.activeGameObject = actor.gameObject; 
SceneView.FrameLastActiveSceneView(); 
break; 
}
                    }
                    EditorGUILayout.Space(5f);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawWeapons()
        {
            showWeapons = EditorGUILayout.BeginFoldoutHeaderGroup(showWeapons, "무기 스탯");
            if (showWeapons)
            {
                weaponSearch = EditorGUILayout.TextField("이름/ID 검색", weaponSearch);
                var definitions = WeaponLootTable.All;
                for (var index = 0; index < config.weapons.Length; index++)
                {
                    var value = config.weapons[index];
                    if (value == null) continue;
                    WeaponDefinition definition = null;
                    foreach (var candidate in definitions)
                        if (candidate.Id == value.id) 
{ 
definition = candidate; 
break; 
}
                    var title = definition == null ? value.id : definition.Name + "  [" + WeaponFamilyCatalog.GetName(definition.Family) + "]";
                    if (!string.IsNullOrEmpty(weaponSearch) && title.IndexOf(weaponSearch, System.StringComparison.OrdinalIgnoreCase) < 0 && value.id.IndexOf(weaponSearch, System.StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                    using (new EditorGUI.IndentLevelScope())
                    {
                        value.attackPower = Positive("무기 공격력", value.attackPower);
                        value.attackDelayMultiplier = Positive("공격 딜레이 배율", value.attackDelayMultiplier);
                        if (definition != null && definition.AttackType == WeaponAttackType.Melee)
                        {
                            value.heavyDamageMultiplier = Mathf.Max(1f, EditorGUILayout.FloatField("강공격 추가 배율", value.heavyDamageMultiplier));
                            value.guardDamageReduction = EditorGUILayout.Slider("가드 피해 감소", value.guardDamageReduction, 0f, .8f);
                        }
                        else
                        {
                            value.magazineSize = Mathf.Max(1, EditorGUILayout.IntField("탄창", value.magazineSize));
                            value.reloadSeconds = Positive("장전 시간", value.reloadSeconds);
                            value.projectileSpeed = Positive("투사체 속도", value.projectileSpeed);
                        }
                    }
                    EditorGUILayout.Space(5f);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void LoadOrCreate()
        {
            config = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(AssetPath);
            if (config == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
                AssetDatabase.Refresh();
                config = CreateInstance<GameBalanceConfig>();
                AssetDatabase.CreateAsset(config, AssetPath);
                EnsureWeaponEntries();
                AssetDatabase.SaveAssets();
            }
            GameBalance.UseConfig(config);
        }

        private void EnsureWeaponEntries()
        {
            var previous = new Dictionary<string, WeaponBalanceData>();
            if (config.weapons != null)
                foreach (var entry in config.weapons)
                    if (entry != null && !string.IsNullOrEmpty(entry.id)) previous[entry.id] = entry;

            var values = new List<WeaponBalanceData>();
            foreach (var weapon in WeaponLootTable.All)
            {
                WeaponBalanceData value;
                if (!previous.TryGetValue(weapon.Id, out value))
                {
                    value = new WeaponBalanceData
                    {
                        id = weapon.Id,
                        attackPower = weapon.BaseAttackPower,
                        attackDelayMultiplier = weapon.BaseAttackDelayMultiplier,
                        heavyDamageMultiplier = weapon.BaseHeavyDamageMultiplier,
                        guardDamageReduction = weapon.BaseGuardDamageReduction,
                        magazineSize = weapon.BaseMagazineSize,
                        reloadSeconds = weapon.BaseReloadSeconds,
                        projectileSpeed = weapon.BaseProjectileSpeed
                    };
                }
                values.Add(value);
            }
            config.weapons = values.ToArray();
            EditorUtility.SetDirty(config);
        }

        private void ApplyNow()
        {
            GameBalance.UseConfig(config);
            GameBalance.ApplyLive();
            SceneView.RepaintAll();
        }

        private void SaveAndApply()
        {
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            ApplyNow();
        }

        private static float Positive(string label, float value) => Mathf.Max(.01f, EditorGUILayout.FloatField(label, value));
        private static float NonNegative(string label, float value) => Mathf.Max(0f, EditorGUILayout.FloatField(label, value));
    }
}
#endif
