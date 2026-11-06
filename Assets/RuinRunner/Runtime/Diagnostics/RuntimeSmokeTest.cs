#if UNITY_STANDALONE && !UNITY_EDITOR
using System.Collections;
using UnityEngine;

namespace RuinRunner
{
    public sealed class RuntimeSmokeTest : MonoBehaviour
    {
        private GameController game;

        private static bool ValidateSpriteMatte()
        {
            var white = new Color32(255, 255, 255, 255);
            var cyan = new Color32(120, 245, 255, 255);
            var outline = new Color32(25, 20, 35, 255);
            for (var row = 0; row < 8; row++)
                if (PixelArtAtlas.GetBossFrames(row).Length != BossSpriteAnimator.FrameCount) return false;
            var passed = SpriteMatte.ChromaCutout(new Color32(140, 55, 145, 255)).a == 0 &&
                         SpriteMatte.ChromaCutout(new Color32(250, 190, 245, 255)).a == 0 &&
                         SpriteMatte.ChromaCutout(white).Equals(white) &&
                         SpriteMatte.ChromaCutout(cyan).Equals(cyan) &&
                         SpriteMatte.ChromaCutout(outline).Equals(outline) &&
                         SpriteMatte.AlphaCutout(new Color32(255, 255, 255, 35)).a == 0 &&
                         SpriteMatte.AlphaCutout(new Color32(170, 40, 230, 255)).a == 255;
            Debug.Log("[Sprite Matte] " + (passed ? "PASS" : "FAIL") + " 64 supplied boss poses, purple matte, white/cyan foreground, dark outline, alpha residue, purple equipment.");
            return passed;
        }

        public void Initialize(GameController controller)
        {
            game = controller;
            StartCoroutine(ValidateAfterStartup());
        }

        private IEnumerator ValidateAfterStartup()
        {
            var startupDeadline=Time.realtimeSinceStartup+15f;
            while((game==null || game.Player==null || game.IsEncounterTransitioning) && Time.realtimeSinceStartup<startupDeadline)
                yield return null;
            var passed = true;
            passed &= Probe("game != null", () => game != null);
            passed &= Probe("game.Player != null", () => game.Player != null);
            passed &= Probe("game.Progression != null", () => game.Progression != null);
            passed &= Probe("game.Inventory != null", () => game.Inventory != null);
            passed &= Probe("game.MetaProgression != null", () => game.MetaProgression != null);
            passed &= Probe("game.SaveSlots != null", () => game.SaveSlots != null);
            passed &= Probe("game.SaveSlots.ActiveData != null", () => game.SaveSlots.ActiveData != null);
            passed &= Probe("game.Settings != null", () => game.Settings != null);
            passed &= Probe("ValidateSettings(game.Settings)", () => ValidateSettings(game.Settings));
            passed &= Probe("RuntimeSceneObjects.FindAll<GameAudioController>().Length == 1", () => RuntimeSceneObjects.FindAll<GameAudioController>().Length == 1);
            passed &= Probe("game.IsRunActive", () => game.IsRunActive);
            passed &= Probe("game.CameraFollow != null", () => game.CameraFollow != null);
            passed &= Probe("startup role and enemies agree", () => game.DungeonMap.RequiresCombat(game.CurrentRoom)?game.EnemiesAlive>0:game.EnemiesAlive==0);
            passed &= Probe("game.Player.Stats != null", () => game.Player.Stats != null);
            passed &= Probe("game.Player.MaxMana > 0f", () => game.Player.MaxMana > 0f);
            passed &= Probe("GameTick.TicksPerSecond == 60", () => GameTick.TicksPerSecond == 60);
            passed &= Probe("Mathf.Approximately(Time.fixedDeltaTime, GameTick.Duration)", () => Mathf.Approximately(Time.fixedDeltaTime, GameTick.Duration));
            passed &= Probe("PlayerController.HitInvincibilityTicks == 60", () => PlayerController.HitInvincibilityTicks == 60);
            passed &= Probe("ValidateBalanceData()", () => ValidateBalanceData());
            passed &= Probe("game.Player.DashCharges == 2", () => game.Player.DashCharges == 2);
            passed &= Probe("InventorySystem.Capacity == 20", () => InventorySystem.Capacity == 20);
            passed &= Probe("ValidateInventoryExpansion()", () => ValidateInventoryExpansion());
            passed &= Probe("ValidateWeaponOwnedDamage()", () => ValidateWeaponOwnedDamage());
            passed &= Probe("ValidateIndividualResources()", () => ValidateIndividualResources());
            passed &= Probe("ValidateNativeHeroCatalogs()", () => ValidateNativeHeroCatalogs());
            passed &= Probe("game.Inventory.QuickSlots.Count == 3", () => game.Inventory.QuickSlots.Count == 3);
            passed &= Probe("ValidateEquippedWeaponsDoNotUseCapacity()", () => ValidateEquippedWeaponsDoNotUseCapacity());
            passed &= Probe("ValidateInventoryEquipFlow()", () => ValidateInventoryEquipFlow());
            passed &= Probe("ValidateBagLayout()", () => ValidateBagLayout());
            passed &= Probe("game.Player.GetComponent<PlayerAnimationController>() != null", () => game.Player.GetComponent<PlayerAnimationController>() != null);
            passed &= Probe("ValidateHeldWeapon(game.Player)", () => ValidateHeldWeapon(game.Player));
            passed &= Probe("game.Player.GetComponent<PlayerResourceDisplay>() != null", () => game.Player.GetComponent<PlayerResourceDisplay>() != null);
            passed &= Probe("ValidateActorAnchors(game)", () => ValidateActorAnchors(game));
            passed &= Probe("IndividualArt.Exists(\"External/SscaryMale/walk/up/00\")", () => IndividualArt.Exists("External/SscaryMale/walk/up/00"));
            passed &= Probe("IndividualArt.Exists(\"External/SscaryMale/dash/up/00\")", () => IndividualArt.Exists("External/SscaryMale/dash/up/00"));
            passed &= Probe("IndividualArt.Exists(\"External/SscaryMale/death/up/07\")", () => IndividualArt.Exists("External/SscaryMale/death/up/07"));
            passed &= Probe("PixelArtAtlas.HasEnemyAtlas", () => PixelArtAtlas.HasEnemyAtlas);
            passed &= Probe("PixelArtAtlas.HasTerrainAtlas", () => PixelArtAtlas.HasTerrainAtlas);
            passed &= Probe("PixelArtAtlas.HasMeleeAttackAtlas", () => PixelArtAtlas.HasMeleeAttackAtlas);
            passed &= Probe("ValidateWeaponPresentationFrames()", () => ValidateWeaponPresentationFrames());
            passed &= Probe("ValidateAuthoredThrustArt()", () => ValidateAuthoredThrustArt());
            passed &= Probe("ValidateAuthoredMeleeArt()", () => ValidateAuthoredMeleeArt());
            passed &= Probe("ValidateAuthoredRangedArt()", () => ValidateAuthoredRangedArt());
            passed &= Probe("ValidateWeaponStandingArt()", () => ValidateWeaponStandingArt());
            passed &= Probe("PixelHudSkin.Available", () => PixelHudSkin.Available);
            passed &= Probe("ValidateSpriteMatte()", () => ValidateSpriteMatte());
            passed &= Probe("ValidatePixelInterfaceAssets()", () => ValidatePixelInterfaceAssets());
            passed &= Probe("PixelWorldArt.Available", () => PixelWorldArt.Available);
            passed &= Probe("ValidateWorldInteractionArt()", () => ValidateWorldInteractionArt());
            passed &= Probe("game.DungeonMap != null", () => game.DungeonMap != null);
            passed &= Probe("DemoContractValidation.Validate(game.DungeonMap)", () => DemoContractValidation.Validate(game.DungeonMap));
            passed &= Probe("normal run uses generated role graph", () => game.DungeonMap.IsProcedural && game.DungeonMap.RoomCount>=8);
            passed &= Probe("boss has stable role ID", () => game.DungeonMap.GetRoom(game.DungeonMap.BossRoomIndex).bossRoom);
            passed &= Probe("game.DungeonMap.HasUniqueRooms()", () => game.DungeonMap.HasUniqueRooms());
            passed &= Probe("ValidateRandomRoomSeeds(10)", () => ValidateRandomRoomSeeds(10));
            passed &= Probe("ValidateAuthoredRoomAccess()", () => ValidateAuthoredRoomAccess());
            passed &= Probe("ValidateRoomTopology(game.DungeonMap)", () => ValidateRoomTopology(game.DungeonMap));
            passed &= Probe("ValidateRoomCollision(game.DungeonMap)", () => ValidateRoomCollision(game.DungeonMap));
            passed &= Probe("WeaponLootTable.Roll(game.Player.Stats.Luck) != null", () => WeaponLootTable.Roll(game.Player.Stats.Luck) != null);
            passed &= Probe("Active weapon pool excludes retired bows", () => WeaponLootTable.Count == 70 && WeaponLootTable.FindFirst(WeaponFamily.Bow) == null);
            passed &= Probe("ValidateWeaponTypesAndAmmo()", () => ValidateWeaponTypesAndAmmo());
            passed &= Probe("ValidateWeaponFamilies()", () => ValidateWeaponFamilies());
            passed &= Probe("PassiveItemLootTable.Roll(game.Player.Stats.Luck) != null", () => PassiveItemLootTable.Roll(game.Player.Stats.Luck) != null);
            passed &= Probe("AccessoryLootTable.Count >= 8", () => AccessoryLootTable.Count >= 8);
            passed &= Probe("AccessoryLootTable.Roll(game.Player.Stats.Luck) != null", () => AccessoryLootTable.Roll(game.Player.Stats.Luck) != null);
            passed &= Probe("ItemSetCatalog.Bonuses.Length >= 8", () => ItemSetCatalog.Bonuses.Length >= 8);
            passed &= Probe("ValidateSetThresholds()", () => ValidateSetThresholds());
            passed &= Probe("ValidateOutOfCombatManaRecovery()", () => ValidateOutOfCombatManaRecovery());
            passed &= Probe("MetaUpgradeCatalog.Definitions.Length >= 6", () => MetaUpgradeCatalog.Definitions.Length >= 6);
            passed &= Probe("ValidateAccessoryChoicesAndCopies()", () => ValidateAccessoryChoicesAndCopies());
            passed &= Probe("runtime connections match generated adjacency", () => RuntimeSceneObjects.FindAll<RoomExit>().Length == game.DungeonMap.ConnectionCount);
            passed &= Probe("ArenaBuilder.HasBackgroundAsset()", () => ArenaBuilder.HasBackgroundAsset());

            if (passed)
            {
                yield return ValidateLevelAccessoryFlow();
                passed &= levelAccessoryFlowPassed;
            }

            if (passed)
                Debug.Log("[Smoke Test] PASS — campaign startup, three stage data, authored hero poses, collision, equipment, queued growth and persistence services. Isolated startup fixture, not normal completion.");
            else
                Debug.LogError("[Smoke Test] FAIL — a random-run, progression, inventory, or runtime object was not created.");

            Application.Quit(passed ? 0 : 2);
        }

        private static bool Probe(string name, System.Func<bool> test)
        {
            try
            {
                if(test()) return true;
                Debug.LogError("[Smoke Check] FAIL " + name);
            }
            catch(System.Exception error) { Debug.LogError("[Smoke Check] EXCEPTION " + name + ": " + error); }
            return false;
        }

        private static bool ValidateWeaponTypesAndAmmo()
        {
            var melee = WeaponLootTable.FindFirst(WeaponAttackType.Melee);
            var ranged = WeaponLootTable.FindFirst(WeaponAttackType.Ranged);
            if (melee == null || ranged == null || ranged.MagazineSize <= 0 || ranged.ReloadSeconds <= 0f)
                return false;

            var slot = new WeaponSlotState();
            slot.Assign(ranged);
            var initialAmmo = slot.Ammo;
            return slot.TryConsumeAmmo() && slot.Ammo == initialAmmo - 1;
        }

        private static bool ValidateInventoryExpansion()
        {
            var inventory = new InventorySystem();
            var item = PassiveItemLootTable.Roll(0f);
            var passed = inventory.UnlockedCapacity == 20;
            for (var rank = 0; rank <= 3; rank++)
            {
                inventory.ConfigureExpansion(rank);
                passed &= inventory.UnlockedCapacity == 20 + rank * 4;
                while (inventory.StoredItemCount < inventory.UnlockedCapacity)
                    if (!inventory.AddPassiveItem(item)) return false;
                passed &= !inventory.AddPassiveItem(item);
            }
            passed &= inventory.StoredItemCount == 32;
            try { inventory.ConfigureExpansion(0); passed = false; }
            catch (System.InvalidOperationException) { }
            inventory.Reset();
            passed &= inventory.UnlockedCapacity == 20 && inventory.StoredItemCount == 0;
            passed &= (int)MetaUpgradeType.ReinforcedSoul == 5 && (int)MetaUpgradeType.InventoryExpansion == 6;
            Debug.Log("[Inventory Expansion] " + (passed ? "PASS" : "FAIL") + " 20/24/28/32 capacity, full rejection, occupied-lock protection, reset, appended save enum.");
            return passed;
        }

        private static bool ValidateWeaponOwnedDamage()
        {
            var stats = new PlayerStats();
            var passed = Mathf.Approximately(stats.WeaponDamageMultiplier, 1f) && typeof(PlayerStats).GetProperty("AttackPower") == null;
            foreach (var weapon in WeaponLootTable.All) passed &= weapon.AttackPower > 0f;
            var sword = WeaponLootTable.FindFirst(WeaponFamily.Katana);
            var dagger = WeaponLootTable.FindFirst(WeaponFamily.Dagger);
            passed &= !Mathf.Approximately(sword.AttackPower, dagger.AttackPower);
            var before = sword.AttackPower;
            stats.ApplyTrait(UpgradeType.PowerTraining);
            passed &= Mathf.Approximately(stats.WeaponDamageMultiplier, 1.18f) && Mathf.Approximately(sword.AttackPower, before);
            var oldConfig = ScriptableObject.CreateInstance<GameBalanceConfig>();
            oldConfig.player.legacyAttackPower = 30f;
            oldConfig.weapons = new[] { new WeaponBalanceData { id = sword.Id, damageMultiplier = 1.25f } };
            passed &= oldConfig.MigrateWeaponDamage() && Mathf.Approximately(oldConfig.weapons[0].attackPower, 37.5f);
            passed &= !oldConfig.MigrateWeaponDamage() && Mathf.Approximately(oldConfig.weapons[0].attackPower, 37.5f);
            Destroy(oldConfig);
            Debug.Log("[Weapon Damage] " + (passed ? "PASS" : "FAIL") + " weapon-owned values, modifier-only traits, one-time legacy balance migration.");
            return passed;
        }

        private static bool ValidateIndividualResources()
        {
            var sprites = Resources.LoadAll<Sprite>(IndividualArt.Root.TrimEnd('/'));
            var textures = new System.Collections.Generic.HashSet<Texture2D>();
            var terrainSprites = new System.Collections.Generic.HashSet<Sprite>();
            foreach(var theme in new[]{"Outer","Flooded","Core"})foreach(var folder in new[]{"Ribbon"})
                terrainSprites.UnionWith(Resources.LoadAll<Sprite>(IndividualArt.Root+"Campaign/Terrain/"+theme+"/"+folder));
            var externalSprites=new System.Collections.Generic.HashSet<Sprite>(Resources.LoadAll<Sprite>(IndividualArt.Root+"External"));
            var passed = sprites.Length >= 405;
            foreach (var sprite in sprites)
            {
                if(sprite==null || sprite.texture==null){passed=false;continue;}
                passed &= sprite.rect.position==Vector2.zero && sprite.texture.filterMode==FilterMode.Point && sprite.texture.mipmapCount==1;
                if(sprite.packed)
                    passed &= (terrainSprites.Contains(sprite)||externalSprites.Contains(sprite)) && sprite.packingRotation==SpritePackingRotation.None &&
                        sprite.textureRectOffset==Vector2.zero && sprite.textureRect.size==sprite.rect.size;
                else passed &= textures.Add(sprite.texture) && sprite.rect.size==new Vector2(sprite.texture.width,sprite.texture.height);
            }
            // Atlas packing may share GPU textures, but cannot replace the source
            // PNGs, crop their rectangles, rotate them, or change visible pixels.
            passed &= ValidateTerrainAtlasSources();
            passed &= ValidateExternalAtlasSources(externalSprites);
            foreach (var source in new[] { "Enemies/enemy_animation_atlas", "Enemies/boss_animation_atlas", "UI/interface_flat_atlas_v2", "UI/world_interaction_atlas_v1", "TopDown/player_topdown_locomotion_8dir_v2" })
                passed &= Resources.Load<Texture2D>("Art/Pixel/" + source) == null;
            Debug.Log("[Individual Art Runtime] " + (passed ? "PASS" : "FAIL") + " " + sprites.Length + " individual sprites, full-image sources, point imports; terrain/external lossless atlas packing; source sheets excluded.");
            return passed;
        }

        private static bool ValidateNativeHeroCatalogs()
        {
            var pixels = new System.Collections.Generic.Dictionary<Texture2D,Color32[]>();
            var observed = new System.Collections.Generic.HashSet<Sprite>();
            bool passed = true; int compared = 0;
            for (int version = 0; version < NativeHeroFrameCatalog.CurrentVersions.Length; version++)
            {
                var catalog = NativeHeroFrameCatalog.Load(version);
                int expected = NativeHeroFrameCatalog.ExpectedFrames(version);
                if (catalog == null || catalog.frames == null || catalog.frames.Length != expected ||
                    catalog.pngSha256.Length != expected || catalog.visiblePixelSha256.Length != expected ||
                    catalog.logicalRoot != NativeHeroFrameCatalog.LogicalRoot + NativeHeroFrameCatalog.CurrentVersions[version])
                { Debug.LogError("[Native Hero Atlas] Missing or incomplete catalog " + version); passed = false; continue; }
                for (int i = 0; i < expected; i++)
                {
                    var sprite = catalog.frames[i];
                    string id = catalog.logicalRoot + "/" + i.ToString("D5");
                    if (sprite == null || !observed.Add(sprite) || sprite != IndividualArt.Sprite(id) ||
                        !sprite.packed || sprite.packingRotation != SpritePackingRotation.None ||
                        sprite.rect != new Rect(0,0,48,64) || sprite.textureRectOffset != Vector2.zero ||
                        sprite.textureRect.size != sprite.rect.size || sprite.pivot != new Vector2(24,22) ||
                        sprite.pixelsPerUnit != 32 || !sprite.texture.isReadable ||
                        sprite.texture.filterMode != FilterMode.Point || sprite.texture.mipmapCount != 1 ||
                        catalog.pngSha256[i].Length != 64 || catalog.visiblePixelSha256[i].Length != 64 ||
                        Resources.Load<Texture2D>(IndividualArt.Root + id) != null)
                    { Debug.LogError("[Native Hero Atlas] Invalid packed frame " + id); passed = false; continue; }
                    if (!pixels.TryGetValue(sprite.texture, out var atlasPixels))
                    { atlasPixels = sprite.texture.GetPixels32(); pixels.Add(sprite.texture, atlasPixels); }
                    var rect = sprite.textureRect;
                    if (NativeHeroFrameCatalog.VisiblePixelHash(atlasPixels, sprite.texture.width,
                        new RectInt((int)rect.x,(int)rect.y,48,64)) != catalog.visiblePixelSha256[i])
                    { Debug.LogError("[Native Hero Atlas] Source PNG pixel mismatch " + id); passed = false; }
                    compared += 48 * 64;
                }
            }
            foreach (string archived in NativeHeroFrameCatalog.ArchivedVersions)
                passed &= Resources.LoadAll<Sprite>(IndividualArt.Root + NativeHeroFrameCatalog.LogicalRoot + archived).Length == 0;
            Debug.Log("[Native Hero Atlas] " + (passed ? "PASS" : "FAIL") + " " + observed.Count +
                " source PNG identities, " + compared + " full packed pixels, " + pixels.Count +
                " atlas pages; no raw frame textures or retired poses bundled.");
            return passed;
        }

        private static bool ValidateExternalAtlasSources(System.Collections.Generic.HashSet<Sprite> expected)
        {
            var manifest=Resources.Load<TextAsset>("Art/ExternalSourcePaths");
            if(manifest==null){Debug.LogError("[External Atlas] Missing prebuild source manifest");return false;}
            var passed=true;var compared=0;
            var observed=new System.Collections.Generic.HashSet<Sprite>();
            var rawSources=new System.Collections.Generic.HashSet<Texture2D>();
            var packedPixels=new System.Collections.Generic.Dictionary<Texture2D,Color32[]>();
            foreach(var path in manifest.text.Split(new[]{'\r','\n'},System.StringSplitOptions.RemoveEmptyEntries))
            {
                var sprite=Resources.Load<Sprite>(path);var source=Resources.Load<Texture2D>(path);
                if(!path.StartsWith(IndividualArt.Root+"External/",System.StringComparison.Ordinal)||sprite==null||source==null||
                    !source.isReadable||!rawSources.Add(source)||!observed.Add(sprite)||sprite.rect.size!=new Vector2(source.width,source.height))
                {Debug.LogError("[External Atlas] Invalid individual source "+path);passed=false;continue;}
                if(!sprite.packed)continue;
                if(!packedPixels.TryGetValue(sprite.texture,out var pixels))
                {pixels=sprite.texture.GetPixels32();packedPixels.Add(sprite.texture,pixels);}
                var raw=source.GetPixels32();var rect=sprite.textureRect;var mismatch=false;
                for(var y=0;y<source.height&&!mismatch;y++)for(var x=0;x<source.width;x++)
                {
                    var a=raw[y*source.width+x];var b=pixels[((int)rect.y+y)*sprite.texture.width+(int)rect.x+x];
                    if(a.a!=b.a||a.a!=0&&(a.r!=b.r||a.g!=b.g||a.b!=b.b)){mismatch=true;break;}
                    compared++;
                }
                if(mismatch){passed=false;Debug.LogError("[External Atlas] Packed pixel mismatch "+path);}
            }
            passed &= observed.SetEquals(expected)&&observed.Count>0;
            Debug.Log("[External Atlas] "+(passed?"PASS":"FAIL")+" "+observed.Count+" independent PNG sources, "+compared+" packed pixels checked.");
            return passed;
        }

        private static bool ValidateTerrainAtlasSources()
        {
            var passed=true;var count=0;var compared=0;
            var rawSources=new System.Collections.Generic.HashSet<Texture2D>();
            var packedPixels=new System.Collections.Generic.Dictionary<Texture2D,Color32[]>();
            foreach(var theme in new[]{"Outer","Flooded","Core"})
            {
                var root=IndividualArt.Root+"Campaign/Terrain/"+theme;
                // Generated 1024-cell fields were replaced by native floor
                // sprites. Loading their old sheets again is a regression.
                passed &= Resources.LoadAll<Sprite>(root+"/Field").Length==0;
                foreach(var folder in new[]{"Floor","Masonry","Props","Ribbon",""})
                foreach(var sprite in Resources.LoadAll<Sprite>(folder==""?root:root+"/"+folder))
                {
                    // LoadAll(root) recurses. Only the root-level cistern belongs
                    // to this final pass; all other sprites have their own folder.
                    if(folder=="" && sprite.name!="Cistern")continue;
                    var path=root+"/"+(folder==""?"":folder+"/")+sprite.name;
                    var source=Resources.Load<Texture2D>(path);
                    if(source==null || !source.isReadable || !rawSources.Add(source) ||
                        sprite.rect.size!=new Vector2(source.width,source.height))
                    {Debug.LogError("[Terrain Atlas] Invalid individual source "+path);passed=false;continue;}
                    count++;
                    if(!sprite.packed)continue;
                    if(!packedPixels.TryGetValue(sprite.texture,out var pixels))
                    {pixels=sprite.texture.GetPixels32();packedPixels.Add(sprite.texture,pixels);}
                    var raw=source.GetPixels32();var rect=sprite.textureRect;var mismatch=false;
                    for(var y=0;y<source.height&&!mismatch;y++)for(var x=0;x<source.width;x++)
                    {
                        var a=raw[y*source.width+x];var b=pixels[((int)rect.y+y)*sprite.texture.width+(int)rect.x+x];
                        if(a.a!=b.a || a.a!=0 && (a.r!=b.r || a.g!=b.g || a.b!=b.b)){mismatch=true;break;}
                        compared++;
                    }
                    if(mismatch){passed=false;Debug.LogError("[Terrain Atlas] Packed pixel mismatch "+path);}
                }
            }
            passed &= count==133;
            passed &= ValidateNativeGroundSources("RafaelRuins",10);
            passed &= ValidateNativeGroundSources("FoozleDungeon",9);
            passed &= ValidateCoreGroundSources();
            Debug.Log("[Terrain Atlas] "+(passed?"PASS":"FAIL")+" "+count+" independent PNG sources, "+compared+" packed pixels checked.");
            return passed;
        }

        private static bool ValidateAuthoredThrustArt()
        {
            // Verify the actual bundled sprites, including every independent gait
            // combination. This catches missing imports and bad pivots in Player builds.
            SscaryArmedFrames.Preload();
            for(int direction=0;direction<8;direction++)
            for(int gait=-1;gait<8;gait++)
            for(int key=0;key<SscaryThrustFrames.KeyCount;key++)
            {
                Vector2 hand;var sprite=SscaryArmedFrames.Thrust(direction,gait,key,out hand);
                if(sprite==null||sprite.rect.size!=new Vector2(48,64)||sprite.pivot!=new Vector2(24,22)||sprite.pixelsPerUnit!=32
                    ||sprite.texture.filterMode!=FilterMode.Point||hand.x<0||hand.x>=48||hand.y<0||hand.y>=64)return false;
            }
            return true;
        }

        private static bool ValidateAuthoredRangedArt()
        {
            SscaryArmedFrames.Preload();
            for(int clip=0;clip<6;clip++)for(int direction=0;direction<8;direction++)
            for(int source=0;source<16;source++)for(int key=0;key<16;key++)
            {
                Vector2 hand;bool behind;var sprite=SscaryArmedFrames.Ranged(clip,direction,source,key,out hand,out behind);
                if(sprite==null||sprite.rect.size!=new Vector2(48,64)||sprite.pivot!=new Vector2(24,22)||sprite.pixelsPerUnit!=32
                    ||sprite.texture.filterMode!=FilterMode.Point||hand.x<0||hand.x>=48||hand.y<0||hand.y>=64)return false;
                if(key==15)
                {
                    Vector2 readyHand;bool readyBehind;var ready=SscaryArmedFrames.Ranged(clip,direction,source,0,out readyHand,out readyBehind);
                    if(ready!=sprite||readyHand!=hand||readyBehind!=behind)return false;
                }
            }
            return true;
        }

        private static bool ValidateAuthoredMeleeArt()
        {
            SscaryArmedFrames.Preload();
            for(int clip=0;clip<3;clip++)for(int direction=0;direction<8;direction++)
            for(int source=0;source<16;source++)for(int key=0;key<SscaryMeleeFrames.KeyCount;key++)
            {
                Vector2 hand;bool behind;var sprite=SscaryArmedFrames.Melee(clip,direction,source,key,out hand,out behind);
                if(sprite==null||sprite.rect.size!=new Vector2(48,64)||sprite.pivot!=new Vector2(24,22)||sprite.pixelsPerUnit!=32
                    ||sprite.texture.filterMode!=FilterMode.Point||hand.x<0||hand.x>=48||hand.y<0||hand.y>=64)return false;
                if(clip<2)
                {
                    sprite=SscaryArmedFrames.Short(clip,direction,source,key,out hand,out behind);
                    if(sprite==null||sprite.rect.size!=new Vector2(48,64)||sprite.pivot!=new Vector2(24,22)||sprite.pixelsPerUnit!=32
                        ||sprite.texture.filterMode!=FilterMode.Point||hand.x<0||hand.x>=48||hand.y<0||hand.y>=64)return false;
                }
            }
            SscaryArmedFrames.Preload();
            for(int clip=0;clip<4;clip++)for(int direction=0;direction<8;direction++)
            for(int source=0;source<16;source++)for(int key=0;key<16;key++)
            {
                Vector2 hand;bool behind;var sprite=SscaryArmedFrames.Defense(clip,direction,source,key,out hand,out behind);
                if(sprite==null||sprite.rect.size!=new Vector2(48,64)||sprite.pivot!=new Vector2(24,22)||sprite.pixelsPerUnit!=32
                    ||sprite.texture.filterMode!=FilterMode.Point||hand.x<0||hand.x>=48||hand.y<0||hand.y>=64)return false;
                if(clip==2&&key==15)
                {
                    Vector2 cutHand;bool cutBehind;var cut=SscaryArmedFrames.Defense(3,direction,source,0,out cutHand,out cutBehind);
                    if(sprite!=cut||hand!=cutHand||behind!=cutBehind)return false;
                }
            }
            return Mathf.Abs(MeleeMotionTiming.ActionAngle(0,false,0,MeleeAttackMotion.Cleave))<.001f
                &&Mathf.Abs(MeleeMotionTiming.ActionAngle(MeleeMotionTiming.Contact,false,0,MeleeAttackMotion.Cleave))<.001f
                &&Mathf.Abs(MeleeMotionTiming.ActionAngle(1,false,0,MeleeAttackMotion.Cleave))<.001f;
        }

        private static bool ValidateWeaponStandingArt()
        {
            WeaponStandingFrames.Preload();
            for(int profile=0;profile<6;profile++)for(int direction=0;direction<8;direction++)
            for(int variant=0;variant<16;variant++)for(int key=0;key<9;key++)
            {
                Vector2 hand;bool behind;var sprite=WeaponStandingFrames.Sample(profile,direction,variant,key,out hand,out behind);
                if(sprite==null||sprite.rect.size!=new Vector2(48,64)||sprite.pivot!=new Vector2(24,22)||sprite.pixelsPerUnit!=32
                    ||sprite.texture.filterMode!=FilterMode.Point||hand.x<0||hand.x>=48||hand.y<0||hand.y>=64)return false;
                if(key==0)
                {
                    Vector2 readyHand;bool readyBehind;
                    var ready=profile<3?SscaryArmedFrames.Carry(profile==2,direction,variant,out readyHand,out readyBehind)
                        :SscaryArmedFrames.Ranged(profile==3?0:profile==4?2:3,direction,variant,15,out readyHand,out readyBehind);
                    // Source-pixel equality is checked by the offline import QA;
                    // separate resource copies cannot be compared by Sprite identity.
                    if(ready==null||hand!=readyHand||behind!=readyBehind)return false;
                }
            }
            return WeaponStandingFrames.Profile(WeaponFamily.Bow)!=WeaponStandingFrames.Profile(WeaponFamily.Pistol)
                &&WeaponStandingFrames.Profile(WeaponFamily.Dagger)!=WeaponStandingFrames.Profile(WeaponFamily.Greatsword);
        }

        private static bool ValidateWeaponPresentationFrames()
        {
            var ids=new[]{"RifleFlash","RifleWake","ParryCross","PiercingHit","RicochetHit","VoidBolt","PowderFlash","CannonBlast","GroundDust","TwinCrescent","CutStreak","MetalSpark","StoneChip","LeafChip","Slug","HeavyBlast","SlamDust","GreatswordCut"};
            var expected=new[]{5,4,5,7,7,6,8,7,10,4,4,7,5,8,1,10,10,9};
            for(int i=0;i<ids.Length;i++)if(NativeCombatEffect.Frames(ids[i]).Length!=expected[i])
            {Debug.LogError("[Weapon FX] source count mismatch "+ids[i]+": "+NativeCombatEffect.Frames(ids[i]).Length+" expected "+expected[i]);return false;}
            foreach(var clip in new[]{"LongswordStab","DaggerStab"})
            {
                var cells=IndividualArt.Frames("External/CombatFX/"+clip,clip=="LongswordStab"?8:5);
                foreach(var cell in cells)
                    if(cell==null||cell.rect.size!=new Vector2(32,32)||cell.pixelsPerUnit!=32||
                        cell.pivot!=new Vector2(clip=="LongswordStab"?7:2,16)||cell.texture.filterMode!=FilterMode.Point||cell.texture.mipmapCount!=1)return false;
            }
            // The source CanonBall sheet is 80x16: five unique 16px poses,
            // preserved by the original adoption/compaction manifest.
            return NativeCombatEffect.Frames("Shell").Length==5&&Resources.Load<Shader>("Shaders/PixelTrail")!=null;
        }
        private static bool ValidateNativeGroundSources(string pack,int expectedCount)
        {
            var root=IndividualArt.Root+"External/"+pack+"/Floor";
            var sprites=Resources.LoadAll<Sprite>(root);
            if(sprites.Length!=expectedCount)return false;
            foreach(var sprite in sprites)
            {
                var texture=Resources.Load<Texture2D>(root+"/"+sprite.name);
                if(texture==null||sprite.rect.size!=new Vector2(32,32)||sprite.pixelsPerUnit!=32||
                   texture.filterMode!=FilterMode.Point||texture.mipmapCount!=1||!texture.isReadable)return false;
                foreach(var pixel in texture.GetPixels32())if(pixel.a!=255)return false;
            }
            Debug.Log("[Native Ground] PASS "+pack+": "+sprites.Length+" opaque 32px/32PPU floor sources; packed pixels checked by External Atlas validation.");
            return true;
        }

        private static bool ValidateCoreGroundSources()
        {
            var root=IndividualArt.Root+"External/ChampiDungeon";
            // Seventeen adopted floor sources and one preserved seal comparison.
            // The old exclusion guard predates adoption of this verified pack.
            if(Resources.LoadAll<Sprite>(root).Length!=18)return false;
            for(int index=0;index<18;index++)
            {
                var path=root+"/"+(index<16?"Paving/"+index.ToString("D2"):index==16?"Ground":"Seal");
                int size=index<16?16:index==16?32:128;
                var sprite=Resources.Load<Sprite>(path);var source=Resources.Load<Texture2D>(path);
                if(sprite==null||source==null||source.width!=size||source.height!=size||!source.isReadable||
                    sprite.rect.size!=Vector2.one*size||sprite.pivot!=Vector2.one*(size*.5f)||sprite.pixelsPerUnit!=32||
                    source.filterMode!=FilterMode.Point||source.mipmapCount!=1)return false;
                foreach(var pixel in source.GetPixels32())if(pixel.a!=255)return false;
            }
            var material=NativeCoreGround.Material;
            if(material==null||material.shader!=Resources.Load<Shader>("Shaders/CoreStone"))return false;
            Debug.Log("[Core Ground] PASS 16 native paving variants, opaque base, preserved seal source, fixed pivots/32PPU and shared palette material; packed RGBA checked by External Atlas validation.");
            return true;
        }

        private static bool ValidateEquippedWeaponsDoNotUseCapacity()
        {
            var inventory = new InventorySystem();
            if (!inventory.AddWeapon(WeaponLootTable.All[0]) ||
                !inventory.AddWeapon(WeaponLootTable.All[1]) ||
                !inventory.AddWeapon(WeaponLootTable.All[2]))
                return false;

            if (inventory.StoredItemCount != 0 || inventory.StoredWeapons.Count != 0)
                return false;

            var firstEquipped = inventory.QuickSlots[0].Weapon;
            if (!inventory.AddWeapon(WeaponLootTable.All[3]) || inventory.StoredItemCount != 1)
                return false;

            inventory.AssignToQuickSlot(0, 0);
            return inventory.StoredItemCount == 1 &&
                   inventory.StoredWeapons.Count == 1 &&
                   inventory.StoredWeapons[0] == firstEquipped &&
                   inventory.QuickSlots[0].Weapon == WeaponLootTable.All[3];
        }

        private static bool ValidateHeldWeapon(PlayerController player)
        {
            var view = player == null ? null : player.GetComponent<HeldWeaponView>();
            return view != null &&
                   player.EquippedWeapon != null &&
                   view.CurrentWeaponId == player.EquippedWeapon.Id &&
                   view.RendererSprite != null;
        }

        private static bool ValidateInventoryEquipFlow()
        {
            var inventory = new InventorySystem();
            inventory.AddWeapon(WeaponLootTable.All[0]);
            inventory.AddWeapon(WeaponLootTable.All[1]);
            inventory.AddWeapon(WeaponLootTable.All[2]);

            // A ranged weapon with its own icon.
            var siegeCrossbow = WeaponLootTable.Find("frost_bolt_rifle");
            if (!inventory.AddWeapon(siegeCrossbow) || inventory.StoredWeapons.Count != 1)
                return false;
            if (ItemIconLibrary.Get(siegeCrossbow.Id, InventoryIconKind.Weapon) == null ||
                ItemIconLibrary.Get(siegeCrossbow.Id, InventoryIconKind.Weapon) == ItemIconLibrary.Get("scout_carbine", InventoryIconKind.Weapon))
                return false;

            inventory.AssignToQuickSlot(0, 1);
            if (inventory.QuickSlots[1].Weapon != siegeCrossbow || inventory.StoredWeapons.Count != 1)
                return false;
            var ammoBeforeShot = inventory.QuickSlots[1].Ammo;
            if (!inventory.TryConsumeAmmo()) return false;
            var ammoAfterShot = inventory.QuickSlots[1].Ammo;
            var reloadAfterShot = inventory.QuickSlots[1].ReloadRemaining;
            if (ammoAfterShot != ammoBeforeShot - 1) return false;
            inventory.SwapQuickSlots(1, 2);
            if (inventory.QuickSlots[2].Weapon != siegeCrossbow ||
                inventory.QuickSlots[2].Ammo != ammoAfterShot ||
                inventory.QuickSlots[2].ReloadRemaining != reloadAfterShot)
                return false;

            inventory.UnequipQuickSlot(2);
            var storedSiegeCrossbowIndex = -1;
            for (var index = 0; index < inventory.StoredWeapons.Count; index++)
                if (inventory.StoredWeapons[index] == siegeCrossbow) storedSiegeCrossbowIndex = index;
            if (storedSiegeCrossbowIndex < 0) return false;
            inventory.AssignToQuickSlot(storedSiegeCrossbowIndex, 0);
            return inventory.QuickSlots[0].Weapon == siegeCrossbow &&
                   inventory.QuickSlots[0].Ammo == ammoAfterShot &&
                   inventory.QuickSlots[0].ReloadRemaining == reloadAfterShot &&
                   inventory.EquippedWeapon == siegeCrossbow;
        }

        private static bool ValidatePixelInterfaceAssets()
        {
            if (!PixelUiSkin.Available || !PixelUiSkin.HasMenuBackground || !PixelUiSkin.HasAccessoryAtlas)
                return false;
            foreach (PixelUiElement element in System.Enum.GetValues(typeof(PixelUiElement)))
                if (PixelUiSkin.GetInterfaceElement(element) == null) return false;
            foreach (var accessory in AccessoryLootTable.All)
            {
                var icon = PixelUiSkin.GetAccessoryIcon(accessory.Id);
                // Native icons have different canvas sizes (the Kyrise ring is 16px).
                // Validate their actual source and visible content, not the retired
                // generated icon canvas size or an upscaled substitute.
                var source = IndividualArt.Sprite("Equipment/Accessory/" + accessory.Id);
                if (icon == null || source == null || icon != source.texture ||
                    icon.filterMode != FilterMode.Point || icon.mipmapCount != 1) return false;
                var hasVisiblePixel = false;
                foreach (var pixel in icon.GetPixels32())
                    if (pixel.a > 0) { hasVisiblePixel = true; break; }
                if (!hasVisiblePixel) return false;
            }
            return true;
        }

        private static bool ValidateWorldInteractionArt()
        {
            foreach (WorldInteractionArt art in System.Enum.GetValues(typeof(WorldInteractionArt)))
                if (PixelWorldArt.GetSprite(art) == null) return false;
            return true;
        }

        private static bool ValidateBalanceData()
        {
            var balance = GameBalance.Current;
            if (balance == null || balance.player == null || !Mathf.Approximately(balance.player.maxHealth, 40f)) return false;
            foreach (EnemyType type in System.Enum.GetValues(typeof(EnemyType)))
            {
                var enemy = balance.GetEnemy(type);
                if (enemy == null || enemy.maxHealth <= 0f || enemy.attackPower < 0f || enemy.moveSpeed <= 0f) return false;
            }
            return true;
        }

        private static bool ValidateWeaponFamilies()
        {
            foreach (WeaponFamily family in System.Enum.GetValues(typeof(WeaponFamily)))
            {
                var weapon = WeaponLootTable.FindFirst(family) ?? System.Array.Find(SignatureWeaponCatalog.All, item => item.Family == family);
                // Families no signature weapon uses stay in the enum so saved values keep their meaning.
                if(family==WeaponFamily.Bow||family==WeaponFamily.Longsword||family==WeaponFamily.Mace||family==WeaponFamily.Crossbow){if(weapon!=null)return false;continue;}
                if (weapon == null) return false;
                if (weapon.AttackType == WeaponAttackType.Melee)
                {
                    var profile = MeleeComboCatalog.Get(weapon);
                    if (profile == null || profile.LightSteps == null || profile.LightSteps.Length < 2 || profile.HeavyStep == null)
                        return false;
                    if (weapon.GuardDamageReduction <= 0f) return false;
                }
                else if (weapon.MagazineSize <= 0 || weapon.GuardDamageReduction != 0f)
                    return false;
            }
            return true;
        }

        private static bool ValidateOutOfCombatManaRecovery()
        {
            var combat = new PlayerStats();
            var resting = new PlayerStats();
            combat.TrySpendMana(30f);
            resting.TrySpendMana(30f);
            combat.Tick(1f, false);
            resting.Tick(1f, true);
            return resting.Mana > combat.Mana;
        }

        private static bool ValidateSetThresholds()
        {
            foreach (var bonus in ItemSetCatalog.Bonuses)
                if (bonus.RequiredPieces != 2 && bonus.RequiredPieces != 3) return false;
            return true;
        }

        private static bool ValidateBagLayout()
        {
            var bag = new InventorySystem();
            for (var i = 0; i < 5; i++) bag.AddWeapon(WeaponLootTable.All[i]);
            bag.AddPassiveItem(PassiveItemLootTable.Roll(0));
            bag.AddAccessory(AccessoryLootTable.All[0]);
            bag.AddAccessory(AccessoryLootTable.All[0]);
            var weaponCell = bag.GetCell(0);
            var passiveCell = bag.GetCell(2);
            var duplicateCell = bag.GetCell(4);
            var passed = bag.MoveBagItem(0, 19) && bag.GetCell(0) == null && bag.GetCell(19) == weaponCell;
            bag.AddAccessory(AccessoryLootTable.All[1]);
            passed &= bag.GetCell(0).Kind == InventoryEntryKind.Accessory && bag.GetCell(2) == passiveCell && bag.GetCell(4) == duplicateCell;
            passed &= bag.MoveBagItem(19, 3) && bag.GetCell(3) == weaponCell;
            var previous = bag.QuickSlots[0].Weapon;
            bag.AssignToQuickSlot(weaponCell.ItemIndex, 0);
            passed &= bag.GetCell(3) == weaponCell && bag.StoredWeapons[weaponCell.ItemIndex] == previous;
            bag.UnequipQuickSlot(2, 18);
            passed &= bag.GetCell(18).Kind == InventoryEntryKind.Weapon && bag.QuickSlots[2].Weapon == null;
            bag.AssignToQuickSlot(bag.GetCell(18).ItemIndex, 2);
            passed &= bag.GetCell(18) == null && bag.GetCell(2) == passiveCell && bag.GetCell(4) == duplicateCell;
            passed &= !bag.MoveBagItem(3, 20) && !bag.MoveBagItem(-1, 0);
            bag.ConfigureExpansion(1);
            bag.MoveBagItem(19, 23);
            try { bag.ConfigureExpansion(0); passed = false; } catch (System.InvalidOperationException) { }
            var randomState = Random.state;
            try
            {
                Random.InitState(9432);
                for (var step = 0; step < 400; step++)
                {
                    var source = Random.Range(0, bag.UnlockedCapacity);
                    var target = Random.Range(0, bag.UnlockedCapacity);
                    if (step % 3 == 0) bag.MoveBagItem(source, target);
                    else if (step % 3 == 1) bag.UnequipQuickSlot(Random.Range(0, 3), target);
                    else if (bag.GetCell(source)?.Kind == InventoryEntryKind.Weapon)
                        bag.AssignToQuickSlot(bag.GetCell(source).ItemIndex, Random.Range(0, 3));
                    var keys = new System.Collections.Generic.HashSet<string>();
                    var occupied = 0;
                    for (var cellIndex = 0; cellIndex < bag.UnlockedCapacity; cellIndex++)
                    {
                        var cell = bag.GetCell(cellIndex);
                        if (cell == null) continue;
                        occupied++;
                        passed &= keys.Add(cell.TooltipKey) && cell.ItemIndex >= 0;
                        passed &= cell.ItemIndex < (cell.Kind == InventoryEntryKind.Weapon ? bag.StoredWeapons.Count : bag.Accessories.Count);
                    }
                    passed &= occupied == bag.StoredItemCount;
                    passed &= bag.GetCell(2) == passiveCell || keys.Contains(passiveCell.TooltipKey);
                }
            }
            finally { Random.state = randomState; }
            Debug.Log("[Bag Layout] " + (passed ? "PASS" : "FAIL") + " explicit positions, cross-kind swap, first empty pickup, exact unequip, locked rejection, 400 moves/equips preserving unique cell indices.");
            return passed;
        }

        private static bool ValidateAccessoryChoicesAndCopies()
        {
            var randomState = Random.state;
            var passed = AccessoryLootTable.Count >= 24;
            try
            {
                var types = new System.Collections.Generic.HashSet<AccessoryType>();
                foreach (var definition in AccessoryLootTable.All)
                {
                    passed &= types.Add(definition.Type);
                    passed &= InventoryIconFactory.Get(definition.Id, definition.Color, InventoryIconKind.Accessory) != null;
                    if (!definition.UsesLegacyPassiveIcon) continue;
                    var legacyStats = new PlayerStats();
                    var unifiedStats = new PlayerStats();
                    legacyStats.ApplyItem((PassiveItemType)((int)definition.Type - 8));
                    unifiedStats.ApplyAccessory(definition.Type);
                    foreach (var property in typeof(PlayerStats).GetProperties())
                        if (property.PropertyType == typeof(float))
                            passed &= Mathf.Approximately((float)property.GetValue(legacyStats), (float)property.GetValue(unifiedStats));
                    var compatibilityBag = new InventorySystem();
                    compatibilityBag.AddPassiveItem(new PassiveItemDefinition((PassiveItemType)((int)definition.Type - 8),
                        definition.Name, definition.Description, definition.Rarity, definition.Color, definition.SetType));
                    passed &= compatibilityBag.Accessories.Count == 1 && compatibilityBag.GetCell(0).Kind == InventoryEntryKind.Accessory;
                }
                for (var seed = 0; seed < 50; seed++)
                {
                    Random.InitState(seed);
                    var choices = AccessoryLootTable.CreateChoices(6, seed);
                    var ids = new System.Collections.Generic.HashSet<string>();
                    passed &= choices.Length == 6;
                    foreach (var item in choices) passed &= ids.Add(item.Id);
                }
                passed &= AccessoryLootTable.CreateChoices(-1, 0).Length == 0;
                passed &= AccessoryLootTable.CreateChoices(999, 0).Length == AccessoryLootTable.Count;
                var inventory = new InventorySystem();
                var accessory = AccessoryLootTable.All[1];
                passed &= inventory.AddAccessory(accessory) && inventory.AddAccessory(accessory);
                passed &= inventory.StoredItemCount == 2 && inventory.Accessories.Count == 2;
                passed &= inventory.GetSetPieceCount(accessory.SetType) == 1; // distinct set pieces, not copies
                var stats = new PlayerStats();
                var speed = stats.MoveSpeed;
                foreach (var copy in inventory.Accessories) stats.ApplyAccessory(copy.Type);
                passed &= Mathf.Approximately(stats.MoveSpeed, speed + .8f);
                while (inventory.StoredItemCount < inventory.UnlockedCapacity)
                    if (!inventory.AddAccessory(accessory)) return false;
                passed &= !inventory.AddAccessory(accessory) && inventory.Accessories.Count == 20;
                inventory.Reset();
                passed &= inventory.Accessories.Count == 0;
            }
            finally { Random.state = randomState; }
            Debug.Log("[Accessory Rewards] " + (passed ? "PASS" : "FAIL") + " " + AccessoryLootTable.Count + " unified accessories/icons, 10 legacy effect equivalence checks, 50 seeded unique offers, separate duplicate slots/effects, unique set pieces, full bag, reset.");
            return passed;
        }

        private bool levelAccessoryFlowPassed;
        private string levelFlowFailures = "";
        private bool FlowCheck(bool ok, int check) { if (!ok) levelFlowFailures += check + " "; return ok; }
        private IEnumerator ValidateLevelAccessoryFlow()
        {
            game.Progression.AddExperience(72); // Level 2 + 3: queued choices, not lost callbacks.
            var passed = FlowCheck(game.Progression.PendingLevelRewards == 2, 100) & FlowCheck(game.IsChoosingAccessory, 101) & FlowCheck(game.IsPaused, 102);
            passed &= FlowCheck(game.CurrentAccessoryChoices.Length == 6, 1);
            AccessoryDefinition invalid = null;
            foreach (var option in AccessoryLootTable.All)
                if (System.Array.IndexOf(game.CurrentAccessoryChoices, option) < 0) { invalid = option; break; }
            game.SelectAccessory(invalid);
            passed &= FlowCheck(game.Progression.PendingLevelRewards == 2, 2);
            var count = game.Inventory.Accessories.Count;
            game.SelectAccessory(game.CurrentAccessoryChoices[0]);
            passed &= FlowCheck(game.Progression.PendingLevelRewards == 1 && game.Inventory.Accessories.Count == count + 1, 3);
            game.SelectAccessory(game.CurrentAccessoryChoices[0]);
            passed &= FlowCheck(game.Progression.PendingLevelRewards == 1, 4); // no same-frame double consumption
            yield return null;
            game.SelectAccessory(game.CurrentAccessoryChoices[0]);
            passed &= FlowCheck(game.Progression.PendingLevelRewards == 0 && !game.IsChoosingAccessory && !game.IsPaused, 5);
            passed &= FlowCheck(game.CurrentAccessoryChoices.Length == 0 && game.Inventory.Accessories.Count == count + 2, 6);
            yield return null;
            var copy = AccessoryLootTable.All[0];
            while (game.Inventory.StoredItemCount < game.Inventory.UnlockedCapacity)
                if (!game.Inventory.AddAccessory(copy)) { passed = false; break; }
            var pickups = RuntimeSceneObjects.FindAll<FieldLootPickup>().Length;
            game.Progression.AddExperience(game.Progression.RequiredExperience);
            game.SelectAccessory(game.CurrentAccessoryChoices[0]);
            passed &= FlowCheck(game.Progression.PendingLevelRewards == 0 && !game.IsChoosingAccessory, 7);
            passed &= FlowCheck(game.Inventory.StoredItemCount == game.Inventory.UnlockedCapacity, 8);
            passed &= FlowCheck(RuntimeSceneObjects.FindAll<FieldLootPickup>().Length == pickups + 1, 9);
            levelAccessoryFlowPassed = passed;
            Debug.Log("[Level Accessory Flow] " + (passed ? "PASS" : "FAIL (" + levelFlowFailures + ")") + " two queued levels, invalid/double selection rejection, receipt, unpause, full-bag field reward. Isolated smoke fixture, not normal play.");
        }

        private static bool ValidateSettings(GameSettingsService settings)
        {
            foreach (AudioChannel channel in System.Enum.GetValues(typeof(AudioChannel)))
            {
                var volume = settings.GetVolume(channel);
                if (volume < 0f || volume > 1f) return false;
            }

            foreach (GameAction action in System.Enum.GetValues(typeof(GameAction)))
                if (settings.GetBinding(action) == null) return false;
            return settings.GetBinding(GameAction.MoveUp).primary == KeyCode.W &&
                   settings.GetBinding(GameAction.MoveDown).primary == KeyCode.S &&
                   settings.GetBinding(GameAction.Dash).primary == KeyCode.Space &&
                   settings.GetBinding(GameAction.Guard).primary == KeyCode.Mouse1 &&
                   settings.GetBinding(GameAction.Jump).primary == KeyCode.None &&
                   settings.GetBinding(GameAction.SpecialAttack).primary == KeyCode.None;
        }

        private static bool ValidateRoomTopology(DungeonMap map)
        {
            if(map.IsProcedural)
            {
                if(RuinRunner.Generation.GeneratedStageGeometry.Validate(map.Graph,true).Errors.Count!=0)return false;
                for(int i=0;i<map.RoomCount;i++)
                {
                    var room=map.GetRoom(i);
                    if(map.RequiresCombat(i)?room.WaveCount<1:room.WaveCount!=0)return false;
                    foreach(int neighbor in map.GetNeighbors(i))if(!map.AreAdjacent(neighbor,i)||map.GetConnectionBetween(i,neighbor)==null)return false;
                }
                return true;
            }
            var usesVerticalTravel = false;
            for (var index = 0; index < map.RoomCount; index++)
            {
                var room = map.GetRoom(index);
                if (room.bossRoom ? room.WaveCount != 1 : room.WaveCount < 2) return false;
                if (map.GetRoomGridPosition(index).y != 0) usesVerticalTravel = true;
            }
            return usesVerticalTravel;
        }

        private static bool ValidateRandomRoomSeeds(int seedCount)
        {
            for(int stage=0;stage<CampaignStage.Count;stage++)
            {
                var signatures=new System.Collections.Generic.HashSet<string>();
                for(int seed=0;seed<seedCount;seed++)
                {
                    var graph=RuinRunner.Generation.ProceduralStageGenerator.Generate(9100+seed,stage);
                    if(!signatures.Add(graph.StructureSignature)||graph.Diagnostics.UsedFallback||RuinRunner.Generation.GeneratedStageGeometry.Validate(graph,false).Errors.Count!=0)return false;
                }
            }
            return true;
        }

        private static bool ValidateRoomCollision(DungeonMap map)
        {
            if (map.Terrain == null) return false;
            var edges = RuntimeSceneObjects.FindAll<EdgeCollider2D>();
            var wallCount = 0;
            if (map.IsAuthored)
            {
                Physics2D.SyncTransforms();
                foreach (var box in map.Root.GetComponentsInChildren<BoxCollider2D>())
                {
                    if (box.name != "Wall collision" && box.name != "Corner collision") continue;
                    if (box.gameObject.layer != GameLayers.Ground || box.size != Vector2.one || box.isTrigger) return false;
                    var center = (Vector2)box.transform.position;
                    if (map.Terrain.IsOpen(center) || Physics2D.OverlapPoint(center, 1 << GameLayers.Ground) == null) return false;
                    wallCount++;
                }
                if (wallCount < 12) return false;
            }
            else
            {
                if (edges.Length < 12) return false;
                foreach (var edge in edges)
                    if (edge.gameObject.layer != GameLayers.Ground || edge.pointCount < 2) return false;
                wallCount = edges.Length;
            }
            for (var i = 0; i < map.RoomCount; i++)
            {
                if (!map.Terrain.IsOpen(map.GetSpawnPoint(i))) return false;
                foreach (var enemy in map.GetRoom(i).enemies)
                    if (!map.Terrain.IsOpen(map.GetRoomCenter(i) + enemy.position)) return false;
            }
            for(int i=0;i<map.ConnectionCount;i++)
            {var connection=map.GetConnection(i);if(!map.Terrain.IsOpen(connection.DeparturePosition)||!map.Terrain.IsOpen(connection.ArrivalPosition))return false;}
            Debug.Log("[Continuous Terrain] PASS shared walk mask, spawn/connection points and " + wallCount + " solid boundary colliders.");
            return true;
        }

        private static bool ValidateAuthoredRoomAccess()
        {
            var rooms = Resources.LoadAll<RoomTemplate>("Rooms");
            var passed = true;
            foreach (var room in rooms)
            {
                var issues = RoomLayoutRules.Validate(room);
                foreach (var issue in issues) Debug.LogError("[Room Access] " + room.roomId + ": " + issue);
                passed &= issues.Count == 0;
            }
            if (passed) Debug.Log("[Room Access] PASS " + rooms.Length + " templates; four entrances/exits, reward, enemy points; conservative data-grid check, not physical traversal.");
            return passed && rooms.Length > 0;
        }

        private static bool ValidateActorAnchors(GameController game)
        {
            var playerVisual = game.Player.transform.Find("VisualRoot");
            if (!HasStableVisual(playerVisual)) return false;
            var enemies = RuntimeSceneObjects.FindAll<EnemyController>();
            if (enemies.Length == 0) return !game.DungeonMap.RequiresCombat(game.CurrentRoom);
            foreach (var enemy in enemies)
                if (!HasStableVisual(enemy.transform.Find("VisualRoot"))) return false;
            return true;
        }

        private static bool HasStableVisual(Transform visual)
        {
            if (visual == null) return false;
            var renderer = visual.GetComponent<SpriteRenderer>();
            return renderer != null && renderer.sprite != null;
        }
    }
}
#endif

