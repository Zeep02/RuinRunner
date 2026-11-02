using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Explicit opt-in rendered UI fixtures. No user slot is started or saved.</summary>
    [DefaultExecutionOrder(2000)]
    public sealed class UiValidation : MonoBehaviour
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private string directory;
        private MethodInfo screenshot;
        private GameController game;
        private PrototypeHud hud;
        private string tooltipFixture;
        private bool validateTooltipLayouts;
        private bool tooltipLayoutsChecked;
        private float tooltipFixtureHeight;

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            var args = Environment.GetCommandLineArgs();
            var demoOnly = Array.IndexOf(args, "--demo-ui-validation") >= 0;
            var index = Array.IndexOf(args, "--validation-capture-dir");
            if (index < 0 || index + 1 >= args.Length)
            {
                Debug.LogError("[UI Validation] Missing --validation-capture-dir");
                Application.Quit(3);
                yield break;
            }
            directory = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(directory);
            screenshot = Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule")?
                .GetMethod("CaptureScreenshot", new[] { typeof(string), typeof(int) });
            if (screenshot == null) throw new InvalidOperationException("ScreenCapture unavailable");
            game = GetComponent<GameController>();
            hud = GetComponent<PrototypeHud>();
            foreach (PixelUiElement element in Enum.GetValues(typeof(PixelUiElement)))
            {
                var frame = PixelUiSkin.GetInterfaceElement(element);
                if (frame == null || frame.width != 64 || frame.height != 64)
                    throw new InvalidOperationException("Missing or incorrectly packed UI frame: " + element);
                if (frame.GetPixel(32, 32).a < .99f)
                    throw new InvalidOperationException("UI frame interior must remain opaque: " + element);
            }
            if (PixelUiSkin.MenuLogo == null || PixelUiSkin.MenuBackground == null)
                throw new InvalidOperationException("Missing menu image assets");
            Debug.Log("[UI Validation] Four 64px image frames, opaque interiors, logo and background OK.");
            foreach (var font in new[] { GameTypography.Regular, GameTypography.Emphasis, GameTypography.Detail })
            {
                AssertBundledGlyphs(font,"체력 마나 치명타 피해 방어력 운 이동속도 새 게임 저장 장착 해제 [E] Ctrl Space 0123456789% / · → — ★ ◆");
                Debug.Log("[UI Validation] Bundled font and sample glyphs OK: " + font.name);
            }
            yield return Capture("01-main-menu");
            game.OpenSaveSlots();
            yield return Capture("02-save-slots-readonly");
            game.OpenOptions();
            yield return Capture("03-audio-options");
            typeof(PrototypeHud).GetField("optionsTab", PrivateInstance).SetValue(hud, 1);
            yield return Capture("04-control-options");
            typeof(PrototypeHud).GetField("optionsTab", PrivateInstance).SetValue(hud, 2);
            yield return Capture("05-display-options");

            // Use the existing transient save implementation. Private reflection
            // only selects fixture states; this is NOT gameplay completion evidence.
            game.SaveSlots.OpenTransientForTests();
            typeof(GameController).GetMethod("BeginRunForActiveSlot", PrivateInstance).Invoke(game, null);
            yield return new WaitForSecondsRealtime(.2f);
            Time.timeScale = 0f;
            yield return Capture("06-run-hud");
            SetModal("IsMapOpen", true);
            yield return Capture("23-map-first-region-text");
            for (var region = 0; region < game.DungeonMap.RoomCount; region++) game.DungeonMap.UnlockExit(region);
            yield return Capture("24-map-all-complete-text");
            SetModal("IsMapOpen", false);
            SetModal("IsStatsOpen", true);
            yield return Capture("07-character-stats");
            SetModal("IsStatsOpen", false);
            SetModal("IsInventoryOpen", true);
            yield return Capture("08-empty-inventory");
            if (demoOnly)
            {
                game.Inventory.AddWeapon(DemoContent.Weapon("hand_crossbow"));
                foreach (var id in DemoContent.AccessoryIds) game.Inventory.AddAccessory(AccessoryLootTable.Find(id));
            }
            else foreach (var weapon in WeaponLootTable.All) game.Inventory.AddWeapon(weapon);
            yield return Capture("09-full-inventory");
            tooltipFixture = "Q:0";
            yield return Capture("12-equipped-tooltip-corner");
            tooltipFixture = demoOnly ? "A:0" : "W:3";
            yield return Capture("13-ranged-tooltip-corner");
            tooltipFixture = null;
            yield return Capture("14-tooltip-cleared-fixture");
            game.Inventory.ConfigureExpansion(3);
            if (!demoOnly) foreach (var weapon in WeaponLootTable.All) game.Inventory.AddWeapon(weapon);
            yield return Capture("17-expanded-inventory-fixture");
            SetModal("IsInventoryOpen", false);
            SetModal("IsUnlockOpen", true);
            yield return Capture("10-legacy");
            typeof(PrototypeHud).GetField("legacyScroll", PrivateInstance).SetValue(hud, new Vector2(0, 180));
            yield return Capture("18-legacy-expansion-fixture");
            SetModal("IsUnlockOpen", false);
            SetModal("IsManuallyPaused", true);
            yield return Capture("11-pause");
            SetModal("IsManuallyPaused", false);
            typeof(GameController).GetProperty("CurrentAccessoryChoices").GetSetMethod(true).Invoke(game,
                new object[] { demoOnly ? DemoContent.Choices(0) : AccessoryLootTable.CreateChoices(6, 0f) });
            SetModal("IsChoosingAccessory", true);
            yield return Capture("15-level-up-fixture");
            tooltipFixture = "C:0";
            yield return Capture("19-accessory-choice-tooltip-full");
            tooltipFixture = null;
            yield return Capture("20-accessory-choice-tooltip-cleared");
            SetModal("IsChoosingAccessory", false);
            game.Inventory.Reset();
            var duplicate = demoOnly ? AccessoryLootTable.Find(DemoContent.AccessoryIds[0]) : AccessoryLootTable.All[0];
            game.Inventory.AddAccessory(duplicate);
            game.Inventory.AddAccessory(duplicate);
            SetModal("IsInventoryOpen", true);
            yield return Capture("21-duplicate-accessories-separate-slots");
            game.Inventory.MoveBagItem(0, 19);
            game.Inventory.MoveBagItem(1, 8);
            if (demoOnly) game.Inventory.AddAccessory(AccessoryLootTable.Find("crimson_ring"));
            else game.Inventory.AddPassiveItem(PassiveItemLootTable.Roll(0));
            yield return Capture("22-user-positioned-bag-fixture");
            if (!demoOnly)
            {
                // Current item expansion, supplied inventory only. No user save or normal run claim.
                game.Inventory.Reset();
                game.Inventory.AddWeapon(WeaponLootTable.Find("scrap_pistol"));
                foreach(var id in new[]{"mana_thread","guard_bell","rush_spur","steady_locket","powder_satchel","last_light_pendant"})
                    game.Inventory.AddAccessory(AccessoryLootTable.Find(id));
                game.Inventory.AwardMaterials(999,"ui-fixture-only");
                foreach(var item in game.Inventory.OwnedAccessories)
                    for(int level=0;level<3;level++)if(!game.Inventory.TryUpgrade(item.Identity,level))throw new Exception("Fixture upgrade failed");
                game.FocusEquipment(game.Inventory.OwnedAccessories[4].Identity);
                yield return Capture("27-new-items-inventory");
                validateTooltipLayouts=true;tooltipFixture="A:0";
                yield return Capture("28-new-tooltip-mana_thread");
                if(!tooltipLayoutsChecked)throw new Exception("Real OnGUI typography layout checks did not run");
                for(int n=1;n<6;n++)
                {
                    tooltipFixture="A:"+n;
                    yield return Capture("28-new-tooltip-"+game.Inventory.OwnedAccessories[n].Definition.Id);
                }
                tooltipFixture="A:0";tooltipFixtureHeight=560;yield return Capture("29-new-tooltip-constrained-560-top");
                typeof(PrototypeHud).GetField("tooltipScroll",PrivateInstance).SetValue(hud,new Vector2(0,10000));
                yield return Capture("30-new-tooltip-constrained-560-bottom");
                tooltipFixture=null;tooltipFixtureHeight=0;
            }
            if (Array.IndexOf(args, "--ui-validation-interactive") >= 0)
            {
                Debug.Log("[UI Validation] Interactive bag fixture ready for 120 seconds; transient inventory only.");
                yield return new WaitForSecondsRealtime(120f);
                yield return Capture("23-native-drag-result");
                for (var cell = 0; cell < game.Inventory.UnlockedCapacity; cell++)
                    if (game.Inventory.GetCell(cell) != null)
                        Debug.Log("[Native Bag Layout] " + cell + "=" + game.Inventory.GetCell(cell).TooltipKey);
            }
            SetModal("IsInventoryOpen", false);
            // Explicit stage/actor fixtures for HUD composition at the requested resolution.
            // These bypass encounters and cannot be mistaken for a campaign clear.
            game.Inventory.Reset();
            foreach (var id in new[] { "rust_sword", "storm_rifle", "dragon_cannon" })
                game.Inventory.AddWeapon(WeaponLootTable.Find(id));
            game.Player.EquipWeapon(game.Inventory.SelectQuickSlot(1));
            game.Inventory.ActiveSlot.Assign(game.Inventory.EquippedWeapon, 12, 0);
            yield return Capture("31-centered-quickbar-ammo");
            if (!game.Inventory.StartReload()) throw new Exception("Quickbar reload fixture failed");
            game.Inventory.Tick(game.Inventory.EquippedWeapon.ReloadSeconds * .4f);
            yield return Capture("32-stacked-reload-gauge");
            var ammoGauge = game.Player.transform.Find("AmmoGauge");
            var dashGauge = game.Player.transform.Find("DashCharges");
            if (Mathf.Abs(ammoGauge.position.x - dashGauge.position.x) > .001f ||
                ammoGauge.GetChild(0).GetComponent<SpriteRenderer>().bounds.max.y >= dashGauge.GetComponent<MeshRenderer>().bounds.min.y)
                throw new Exception("Resource gauges overlap or are not centered");
            SetModal("IsInventoryOpen", true);
            yield return Capture("33-centered-inventory-quickbar");
            SetModal("IsInventoryOpen", false);
            Debug.Log("[UI Validation] Centered quickbars and stacked ammo/reload/dash gauges OK.");
            if (Array.IndexOf(args, "--player-hit-fixtures") >= 0)
            {
                var hitFixtures = PlayerHitFixtures();
                while (hitFixtures.MoveNext()) yield return hitFixtures.Current;
            }
            if (Array.IndexOf(args, "--enemy-reaction-fixtures") >= 0)
            {
                var reactions = EnemyReactionFixtures();
                while (reactions.MoveNext()) yield return reactions.Current;
            }
            if (Array.IndexOf(args, "--charger-motion-fixtures") >= 0)
            {
                var charger = ChargerMotionFixtures();
                while (charger.MoveNext()) yield return charger.Current;
            }
            Time.timeScale = 1;
            if (Array.IndexOf(args, "--tidekeeper-motion-fixtures") >= 0)
            {
                var tidekeeper = TidekeeperMotionFixtures();
                while (tidekeeper.MoveNext()) yield return tidekeeper.Current;
            }
            for (var stage = 0; stage < CampaignStage.Count; stage++)
            {
                if (stage > 0)
                {
                    typeof(GameController).GetMethod("BeginNextStage", PrivateInstance).Invoke(game, null);
                    yield return new WaitForSecondsRealtime(.9f);
                }
                game.StopAllCoroutines();
                RuntimeSceneObjects.ClearSession();
                yield return null;
                var last = game.DungeonMap.RoomCount - 1;
                var center = game.DungeonMap.GetRoomCenter(last);
                game.Player.Teleport(center + new Vector2(4, -2));
                game.Player.SetStageClearSafety(true);
                typeof(GameController).GetProperty("CurrentRoom").GetSetMethod(true).Invoke(game, new object[] { last });
                var boss = EnemyController.Create(CampaignStage.Get(stage).BossType, center, game.Player, () => game.IsPaused, _ => {}, game.CameraFollow);
                boss.ConfigureArena(new Rect(center - game.DungeonMap.GetRoom(last).Size * .5f, game.DungeonMap.GetRoom(last).Size));
                typeof(GameController).GetProperty("Boss").GetSetMethod(true).Invoke(game, new object[] { boss });
                typeof(GameController).GetProperty("EnemiesAlive").GetSetMethod(true).Invoke(game, new object[] { 1 });
                typeof(GameController).GetProperty("StageIntroUntil").GetSetMethod(true).Invoke(game, new object[] { 0f });
                yield return new WaitForSecondsRealtime(.7f);
                yield return Capture("25-stage-" + (stage+1) + "-boss-hud-fixture");
                SetModal("IsMapOpen", true);
                for (var region = 0; region < game.DungeonMap.RoomCount; region++) game.DungeonMap.UnlockExit(region);
                yield return Capture("26-stage-" + (stage+1) + "-map-fixture");
                SetModal("IsMapOpen", false);
                if(stage==0 && Array.IndexOf(args,"--enemy-leap-fixtures")>=0)
                {
                    var leapReview=EnemyLeapFixtures(boss,center);
                    while(leapReview.MoveNext())yield return leapReview.Current;
                }
                if(stage==0 && Array.IndexOf(args,"--warden-pattern-fixtures")>=0)
                {
                    var patternReview=WardenPatternFixtures(center);
                    while(patternReview.MoveNext())yield return patternReview.Current;
                }
                if(stage>0 && Array.IndexOf(args,"--boss-hazard-fixtures")>=0)
                {
                    var hazardReview=BossHazardFixtures(stage,boss,center);
                    while(hazardReview.MoveNext())yield return hazardReview.Current;
                }
                if(stage==1 && Array.IndexOf(args,"--tide-pool-placement-fixtures")>=0)
                {
                    var placementReview=TidePoolPlacementFixtures(boss,center);
                    while(placementReview.MoveNext())yield return placementReview.Current;
                }
                if(stage==2 && Array.IndexOf(args,"--sealheart-motion-fixtures")>=0)
                {
                    var nativeReview=SealheartMotionFixtures(boss,center);
                    while(nativeReview.MoveNext())yield return nativeReview.Current;
                }
            }
            typeof(GameController).GetProperty("StagesCleared").GetSetMethod(true).Invoke(game, new object[] { CampaignStage.Count });
            SetModal("IsGameOver", true);
            SetModal("IsVictory", true);
            yield return Capture("16-stage-result-fixture");
            Debug.Log("[UI Validation] CAPTURED front-end, modal and all three boss/map fixtures at " + Screen.width + "x" + Screen.height +
                "; layout scale " + GameTypography.LayoutScale + "; visual review required; no completion/save test.");
            yield return new WaitForSecondsRealtime(.5f);
            Application.Quit(0);
        }

        private void SetModal(string property, bool value)
        {
            typeof(GameController).GetProperty(property).GetSetMethod(true).Invoke(game, new object[] { value });
        }


        private IEnumerator WardenPatternFixtures(Vector2 center)
        {
            bool enabledBefore=game.enabled;game.enabled=false;Time.timeScale=1;
            game.Player.SetStageClearSafety(true);
            game.Player.Teleport(center+Vector2.right*1.9f);
            var boss=EnemyController.Create(EnemyType.Boss,center,game.Player,()=>game.IsPaused,_=>{},game.CameraFollow);
            typeof(GameController).GetProperty("Boss").GetSetMethod(true).Invoke(game,new object[]{boss});
            var animation=boss.GetComponent<BossSpriteAnimator>();
            var seen=new HashSet<BossPose>();var phases=new HashSet<int>();
            bool wasActing=false;int actions=0;float start=Time.time;
            while(actions<4||boss.IsActing)
            {
                if(Time.time-start>20)throw new Exception("Warden close-range pattern timeout");
                if(!boss.IsActing)
                {
                    boss.GetComponent<Rigidbody2D>().position=center;boss.transform.position=center;
                    game.Player.Teleport(center+Vector2.right*1.9f);
                }
                yield return new WaitForEndOfFrame();
                if(boss.IsActing&&!wasActing)
                {
                    if(!seen.Add(animation.Pose))throw new Exception("Warden repeated close-range pattern "+animation.Pose);
                    actions++;phases.Clear();
                    screenshot.Invoke(null,new object[]{Path.Combine(directory,"50-warden-cycle-"+actions+"-"+animation.Pose+".png"),1});
                }
                if(boss.IsActing)phases.Add(boss.AttackVisualPhase);
                if(!boss.IsActing&&wasActing&&!phases.SetEquals(new[]{0,1,2}))throw new Exception("Warden skipped attack phase");
                wasActing=boss.IsActing;
            }
            if(!seen.SetEquals(new[]{BossPose.Charge,BossPose.Leap,BossPose.Radial,BossPose.Volley}))throw new Exception("Warden missing close-range pattern");
            Destroy(boss.gameObject);foreach(var shot in RuntimeSceneObjects.FindAll<EnemyProjectile>())Destroy(shot.gameObject);
            yield return null;game.enabled=enabledBefore;
            Debug.Log("[Warden Patterns Windows] PASS four distinct close-range attacks with anticipation/contact/recovery; supplied arena, distance and protection, no combat-win claim.");
        }

        private IEnumerator EnemyLeapFixtures(EnemyController previous, Vector2 center)
        {
            game.enabled=false;Destroy(previous.gameObject);yield return null;
            var leap=typeof(EnemyController).GetMethod("LeapSlam",PrivateInstance);
            foreach(var type in new[]{EnemyType.Boss,EnemyType.Leaper})
            {
                game.Player.Teleport(center+Vector2.right*3);
                int deaths=0;
                var actor=EnemyController.Create(type,center,game.Player,()=>game.IsPaused,_=>deaths++,game.CameraFollow);
                actor.enabled=false;
                var visual=actor.transform.Find("VisualRoot");
                float rest=visual.position.y-actor.transform.position.y,peak=0;
                var foot=actor.GetComponent<BoxCollider2D>();var size=foot.size;var offset=foot.offset;
                actor.StartCoroutine((IEnumerator)leap.Invoke(actor,new object[]{Vector2.right,type==EnemyType.Boss}));
                float started=Time.time;
                while(actor.IsActing&&Time.time-started<2f)
                {
                    yield return null;
                    peak=Mathf.Max(peak,visual.position.y-actor.transform.position.y-rest);
                }
                if(actor.IsActing||Mathf.Abs(peak-(type==EnemyType.Boss?1.6f:1.1f))>.05f||
                   Mathf.Abs(visual.position.y-actor.transform.position.y-rest)>.0001f||foot.size!=size||foot.offset!=offset||deaths!=0)
                    throw new Exception("Windows leap height, landing or footprint mismatch: "+type+" peak="+peak);
                float duration=Time.time-started;
                if(duration<(type==EnemyType.Boss?1.18f:.96f)||duration>(type==EnemyType.Boss?1.45f:1.25f))
                    throw new Exception("Windows leap combat timing changed: "+type+" duration="+duration);
                Destroy(actor.gameObject);yield return null;
                actor=EnemyController.Create(type,center,game.Player,()=>game.IsPaused,_=>deaths++,game.CameraFollow);
                actor.enabled=false;visual=actor.transform.Find("VisualRoot");rest=visual.position.y-actor.transform.position.y;
                actor.StartCoroutine((IEnumerator)leap.Invoke(actor,new object[]{Vector2.right,type==EnemyType.Boss}));
                yield return new WaitForSeconds(type==EnemyType.Boss?.60f:.44f);yield return new WaitForEndOfFrame();
                float height=visual.position.y-actor.transform.position.y-rest;
                if(height<.6f)throw new Exception("Windows airborne fixture missed actual leap");
                var position=visual.localPosition;actor.TakeDamage(100000,Vector2.zero);actor.TakeDamage(100000,Vector2.zero);
                if(!actor.IsDead||actor.DamageCollider.enabled||actor.GetComponent<Rigidbody2D>().simulated||visual.localPosition!=position)
                    throw new Exception("Windows lethal contact teleported airborne pose or retained collision");
                yield return new WaitForSeconds(.06f);
                typeof(GameController).GetMethod("SetManualPause",PrivateInstance).Invoke(game,new object[]{true});yield return new WaitForEndOfFrame();
                position=visual.localPosition;var sprite=visual.GetComponent<SpriteRenderer>().sprite;int pausedDeaths=deaths;
                yield return new WaitForSecondsRealtime(.20f);
                if(visual.localPosition!=position||visual.GetComponent<SpriteRenderer>().sprite!=sprite||deaths!=pausedDeaths)
                    throw new Exception("Windows pause advanced falling death");
                game.ResumeGame();yield return new WaitForSeconds(.30f);yield return new WaitForEndOfFrame();
                if(actor==null||Mathf.Abs(visual.position.y-actor.transform.position.y-rest)>.0001f)
                    throw new Exception("Windows corpse remained airborne: "+type);
                Time.timeScale=0;var capture=Capture("49-grounded-airborne-death-"+type);while(capture.MoveNext())yield return capture.Current;
                Time.timeScale=1;yield return new WaitForSeconds(1.1f);
                if(actor!=null||deaths!=1)throw new Exception("Windows airborne death callback duplicated or actor leaked");
                Debug.Log("[Enemy Leap Windows] PASS "+type+" peak="+peak+" actionSeconds="+duration+" ground restored, pause respected, callback once. Supplied fixture.");
            }
            game.enabled=true;
        }

        private IEnumerator BossHazardFixtures(int stage, EnemyController boss, Vector2 center)
        {
            bool gameWasEnabled=game.enabled;game.enabled=false;boss.StopAllCoroutines();
            boss.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;
            typeof(EnemyController).GetMethod("EndAction",PrivateInstance).Invoke(boss,null);
            typeof(EnemyController).GetField("speed",PrivateInstance).SetValue(boss,0f);
            typeof(EnemyController).GetField("specialTimer",PrivateInstance).SetValue(boss,10000f);
            foreach(var old in RuntimeSceneObjects.FindAll<ArenaHazard>())Destroy(old.gameObject);
            game.Player.Teleport(center+Vector2.down*2);game.CameraFollow.FocusRoom(center,new Vector2(20,14),true);
            Time.timeScale=1;yield return new WaitForSeconds(.2f);
            int patterns=stage==1?1:2;
            for(int pattern=0;pattern<patterns;pattern++)
            {
                if(pattern==1)boss.TakeDamage(boss.MaxHealth*.6f,Vector2.zero);
                yield return new WaitForSeconds(.3f);
                boss.StartCoroutine((IEnumerator)typeof(EnemyController).GetMethod(stage==1?"CallTide":"SealCross",PrivateInstance).Invoke(boss,stage==1?new object[]{true}:null));
                var hazards=RuntimeSceneObjects.FindAll<ArenaHazard>();
                int count=0;
                foreach(var h in hazards)
                {
                    if(h.IsActive)throw new Exception("Hazard warning already harmful");
                    foreach(var v in h.GetComponentsInChildren<SpriteRenderer>())if(v.name.StartsWith("HazardFX_"))
                    {
                        count++;
                        if(v.enabled||v.sortingLayerName!="WorldFloor"||v.sortingOrder!=9||v.transform.lossyScale!=Vector3.one||v.sprite.pixelsPerUnit!=32)
                            throw new Exception("Native ground effect scaling, initial visibility or sorting mismatch");
                    }
                    if(h.GetComponentsInChildren<Collider2D>().Length!=0)throw new Exception("Effect created damage collider");
                }
                if(hazards.Length!=(stage==1?3:2)||count!=(stage==1?12:17))throw new Exception("Ground effect count or duplicate crossing center");
                yield return new WaitForSeconds((stage==1?.95f:1.05f)+.12f);
                foreach(var h in hazards)if(!h.IsActive)throw new Exception("Ground effect activation changed gameplay deadline");
                string sound=stage==1?"hazard-water":"heavy-impact";bool playing=false;
                foreach(var a in FindObjectsByType<AudioSource>())if(a.clip==CombatAudio.Clip(sound)&&a.isPlaying)playing=true;
                if(!playing)throw new Exception("Ground release sound did not play through bounded audio voices");
                Time.timeScale=0;
                var capture=Capture("43-native-hazard-stage-"+stage+"-pattern-"+pattern);while(capture.MoveNext())yield return capture.Current;
                Time.timeScale=1;yield return new WaitForSeconds(stage==1?.40f:.32f);
                foreach(var h in hazards)if(h==null||h.IsActive||h.transform.Find("WarningContour").GetComponent<SpriteRenderer>().enabled)throw new Exception("Harmless tail extended damage or warning");
                yield return new WaitForSeconds(.30f);
                foreach(var h in hazards)if(h!=null)throw new Exception("Ground effect hierarchy leaked after tail");
                yield return new WaitForSeconds(1.5f);
                typeof(EnemyController).GetField("specialTimer",PrivateInstance).SetValue(boss,10000f);
            }
            var point=center+Vector2.right*4;
            var test=ArenaHazard.Create(boss,game.Player,point,Vector2.one*3.8f,0,true,.4f,.3f,10,Color.cyan,ArenaHazardStyle.TidePool);
            yield return new WaitForSeconds(.46f);
            typeof(GameController).GetMethod("SetManualPause",PrivateInstance).Invoke(game,new object[]{true});
            yield return new WaitForEndOfFrame();
            var elapsedField=typeof(ArenaHazard).GetField("elapsed",PrivateInstance);float clock=(float)elapsedField.GetValue(test);
            var renderers=test.GetComponentsInChildren<SpriteRenderer>();var saved=new Sprite[renderers.Length];for(int i=0;i<saved.Length;i++)saved[i]=renderers[i].sprite;
            yield return new WaitForSecondsRealtime(.2f);
            if((float)elapsedField.GetValue(test)!=clock)throw new Exception("Paused hazard clock advanced");
            for(int i=0;i<saved.Length;i++)if(saved[i]!=renderers[i].sprite)throw new Exception("Paused native hazard pose advanced");
            game.ResumeGame();yield return new WaitForSeconds(.30f);
            game.Player.Teleport(point);float hp=game.Player.Health;yield return null;yield return null;
            if(test==null||test.IsActive||(bool)typeof(ArenaHazard).GetField("attempted",PrivateInstance).GetValue(test)||game.Player.Health!=hp)
                throw new Exception("Entering harmless tail attempted late damage");
            yield return new WaitForSeconds(.3f);if(test!=null)throw new Exception("Tail hierarchy did not expire");
            game.enabled=gameWasEnabled;
            Debug.Log("[Boss Hazard Windows] PASS stage "+stage+": original warning/active deadlines, distinct native frames, one crossing center, upright 32PPU below actor, no FX collision, release sound playback, pause, harmless late entry and cleanup. Supplied fixture, not normal campaign or sound audition.");
        }

        private IEnumerator TidePoolPlacementFixtures(EnemyController boss,Vector2 center)
        {
            bool gameWasEnabled=game.enabled;game.enabled=false;
            boss.ConfigureNavigation(game.DungeonMap.Terrain);
            var targets=new[]{new Vector2(-12,0),new Vector2(-9,8),new Vector2(9,-8)};
            var labels=new[]{"west-wall","northwest-corner","southeast-corner"};
            var elapsed=typeof(ArenaHazard).GetField("elapsed",PrivateInstance);
            for(int scenario=0;scenario<targets.Length;scenario++)
            {
                boss.StopAllCoroutines();typeof(EnemyController).GetMethod("EndAction",PrivateInstance).Invoke(boss,null);
                boss.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;
                typeof(EnemyController).GetField("specialTimer",PrivateInstance).SetValue(boss,10000f);
                typeof(EnemyController).GetField("speed",PrivateInstance).SetValue(boss,0f);
                foreach(var old in RuntimeSceneObjects.FindAll<ArenaHazard>())Destroy(old.gameObject);
                yield return null;
                var target=center+targets[scenario];
                if(!StageNavigation.IsClear(game.DungeonMap.Terrain,target))throw new Exception("Tide placement fixture target is not reachable");
                game.Player.Teleport(target);game.CameraFollow.FocusRoom(target,new Vector2(20,14),true);
                yield return new WaitForSeconds(.15f);Physics2D.SyncTransforms();
                boss.StartCoroutine((IEnumerator)typeof(EnemyController).GetMethod("CallTide",PrivateInstance).Invoke(boss,new object[]{true}));
                var hazards=RuntimeSceneObjects.FindAll<ArenaHazard>();
                if(hazards.Length!=3)throw new Exception("Tide pattern lost a well near "+labels[scenario]);
                var positions=new Vector2[hazards.Length];float gap=float.PositiveInfinity;
                for(int i=0;i<hazards.Length;i++)
                {
                    positions[i]=hazards[i].transform.position;
                    if(hazards[i].IsActive||Physics2D.OverlapCircle(positions[i],1.9f,1<<GameLayers.Ground)!=null)
                        throw new Exception("Tide well starts active or crosses actual solid geometry");
                    for(int j=0;j<i;j++)
                    {
                        float separation=Vector2.Distance(positions[i],positions[j])-3.8f;gap=Mathf.Min(gap,separation);
                        if(separation<.949f)throw new Exception("Tide pool borders overlap or leave no readable gap");
                    }
                }
                Time.timeScale=0;yield return Capture("48-tide-"+labels[scenario]+"-warning");
                // Supplied relocation tests target locking only; this is not an escape-play claim.
                game.Player.Teleport(target+Vector2.down*.5f);Time.timeScale=1;
                while(hazards[0]!=null&&(float)elapsed.GetValue(hazards[0])<.95f)yield return null;
                yield return new WaitForEndOfFrame();
                for(int i=0;i<hazards.Length;i++)
                    if(hazards[i]==null||!hazards[i].IsActive||(Vector2)hazards[i].transform.position!=positions[i]||!hazards[i].Contains(positions[i]))
                        throw new Exception("Tide pool changed its locked warning center/damage shape or missed activation");
                Time.timeScale=0;yield return Capture("49-tide-"+labels[scenario]+"-active");Time.timeScale=1;
                yield return new WaitForSeconds(.85f);
                foreach(var hazard in hazards)if(hazard!=null)throw new Exception("Tide pool left an effect after its tail");
                Debug.Log("[Tide Pool Placement Windows] PASS "+labels[scenario]+": 3 locked circles, actual wall/cover clearance, minimum border gap="+gap+", original activation/damage center, complete expiry. Supplied fixture, not normal campaign.");
            }
            boss.StopAllCoroutines();typeof(EnemyController).GetMethod("EndAction",PrivateInstance).Invoke(boss,null);
            typeof(EnemyController).GetField("specialTimer",PrivateInstance).SetValue(boss,10000f);
            game.Player.Teleport(center+Vector2.down*2);game.CameraFollow.FocusRoom(center,new Vector2(20,14),true);game.enabled=gameWasEnabled;
        }

        private IEnumerator SealheartMotionFixtures(EnemyController previous,Vector2 center)
        {
            game.enabled=false;previous.StopAllCoroutines();Destroy(previous.gameObject);
            foreach(var hazard in RuntimeSceneObjects.FindAll<ArenaHazard>())Destroy(hazard.gameObject);
            yield return null;
            int deaths=0;
            var boss=EnemyController.Create(EnemyType.SealHeart,center,game.Player,()=>game.IsPaused,_=>deaths++,game.CameraFollow);
            typeof(GameController).GetProperty("Boss").GetSetMethod(true).Invoke(game,new object[]{boss});
            var body=boss.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
            var physics=boss.GetComponent<Rigidbody2D>();var box=boss.DamageCollider as BoxCollider2D;
            var originalSize=box.size;var originalOffset=box.offset;
            Vector2[] vectors={Vector2.down,Vector2.up,Vector2.left,Vector2.right};
            string[] names={"down","up","left","right"};
            for(int direction=0;direction<4;direction++)
            {
                typeof(EnemyController).GetField("specialTimer",PrivateInstance).SetValue(boss,10000f);
                typeof(EnemyController).GetField("speed",PrivateInstance).SetValue(boss,3.2f);
                physics.position=center;physics.linearVelocity=Vector2.zero;boss.transform.position=center;
                game.Player.Teleport(center+vectors[direction]*8);Time.timeScale=1;
                yield return new WaitForSeconds(.3f);yield return new WaitForEndOfFrame();
                if(Array.IndexOf(SealHeartArt.Frames(1,direction),body.sprite)<0 || Vector2.Dot(physics.linearVelocity,vectors[direction])<.1f)
                    throw new Exception("Windows native SealHeart walk direction does not match actual AI velocity");
                typeof(EnemyController).GetField("speed",PrivateInstance).SetValue(boss,0f);
                physics.position=center;physics.linearVelocity=Vector2.zero;boss.transform.position=center;
                game.Player.Teleport(center+vectors[direction]*3);yield return new WaitForSeconds(.15f);
                if(body.sprite.rect.size!=new Vector2(96,96)||body.sprite.pivot!=new Vector2(48,32)||body.sprite.pixelsPerUnit!=32||body.flipX||body.transform.lossyScale!=Vector3.one)
                    throw new Exception("Windows native SealHeart source geometry differs");
                Physics2D.SyncTransforms();bool torsoHit=false;
                foreach(var hit in Physics2D.RaycastAll((Vector2)box.bounds.center+Vector2.left*3,Vector2.right,6,1<<GameLayers.Enemy))
                    if(EnemyController.DamageTarget(hit.collider)==boss)torsoHit=true;
                if(!torsoHit)throw new Exception("Native SealHeart torso is not hittable");
                float hitStarted=Time.time;int hitFrame=Time.frameCount;
                boss.TakeDamage(1,Vector2.zero);
                // Sample the first rendered reaction, after LateUpdate. A timed wait
                // near the end of this 0.14s clip can skip it on a slow capture frame.
                yield return new WaitForEndOfFrame();
                int recoilFrame=Array.IndexOf(SealHeartArt.Clip("hurt",direction),body.sprite);
                if(recoilFrame<0)throw new Exception("Native SealHeart recoil direction missing: direction="+names[direction]+" elapsed="+(Time.time-hitStarted)+" frames="+(Time.frameCount-hitFrame)+" acting="+boss.IsActing+" hp="+boss.Health+" sprite="+(body.sprite==null?"null":body.sprite.name));
                Debug.Log("[SealHeart Recoil Sample] direction="+names[direction]+" elapsed="+(Time.time-hitStarted)+" frames="+(Time.frameCount-hitFrame)+" source="+recoilFrame);
                Time.timeScale=0;var capture=Capture("44-sealheart-recoil-"+names[direction]);while(capture.MoveNext())yield return capture.Current;
                Time.timeScale=1;yield return new WaitForSeconds(.3f);
                foreach(bool cross in new[]{true,false})
                {
                    physics.position=center;physics.linearVelocity=Vector2.zero;boss.transform.position=center;
                    typeof(EnemyController).GetField("specialTimer",PrivateInstance).SetValue(boss,10000f);
                    boss.StartCoroutine((IEnumerator)typeof(EnemyController).GetMethod(cross?"SealCross":"SealCharge",PrivateInstance).Invoke(boss,cross?null:new object[]{vectors[direction]}));
                    yield return new WaitForSeconds(.2f);boss.TakeDamage(1,Vector2.zero);yield return new WaitForSeconds(.08f);yield return new WaitForEndOfFrame();
                    if(Array.IndexOf(SealHeartArt.Clip("hurt",direction),body.sprite)>=0)throw new Exception("Hurt hides committed SealHeart attack");
                    yield return new WaitForSeconds((cross?1.05f:.8f)-.28f+.12f);yield return new WaitForEndOfFrame();
                    if(boss.AttackVisualPhase!=1||Array.IndexOf(SealHeartArt.Clip(cross?"attack02":"attack03",direction),body.sprite)<3)
                        throw new Exception("Native SealHeart release disagrees with active damage phase");
                    if(box.size!=originalSize||box.offset!=originalOffset)throw new Exception("Native SealHeart action resized torso damage bounds");
                    Time.timeScale=0;capture=Capture((cross?"45-sealheart-cross-":"46-sealheart-charge-")+names[direction]);while(capture.MoveNext())yield return capture.Current;
                    Time.timeScale=1;yield return new WaitForSeconds(2f);
                    if(boss.IsActing)throw new Exception("Native SealHeart body extended action recovery");
                }
            }
            boss.TakeDamage(100000,Vector2.zero);boss.TakeDamage(100000,Vector2.zero);yield return new WaitForSeconds(.15f);
            typeof(GameController).GetMethod("SetManualPause",PrivateInstance).Invoke(game,new object[]{true});yield return new WaitForEndOfFrame();
            var held=body.sprite;float hp=boss.Health;yield return new WaitForSecondsRealtime(.25f);
            if(body.sprite!=held||boss.Health!=hp||deaths!=0||box.enabled||physics.simulated)throw new Exception("Native SealHeart paused death state is not isolated");
            game.ResumeGame();yield return new WaitForSeconds(.4f);Time.timeScale=0;
            var deathCapture=Capture("47-sealheart-native-death");while(deathCapture.MoveNext())yield return deathCapture.Current;
            Time.timeScale=1;yield return new WaitForSeconds(.7f);
            if(boss!=null||deaths!=1)throw new Exception("Native SealHeart death settled more or less than once");
            game.enabled=true;
            Debug.Log("[SealHeart Native Windows] PASS four-direction actual walking, recoil, two committed action clips, source scale/anchor, physical torso hits, fixed damage bounds, pause and once-only death. Supplied fixture, not normal campaign.");
        }

        private IEnumerator TidekeeperMotionFixtures()
        {
            game.StopAllCoroutines(); game.enabled = false; RuntimeSceneObjects.ClearSession();
            var center = game.DungeonMap.GetRoomCenter(3);
            game.Player.SetStageClearSafety(true); game.Player.Teleport(center + Vector2.down * 3);
            game.CameraFollow.FocusRoom(center, new Vector2(20, 14), true);
            Time.timeScale = 1; yield return new WaitForSeconds(.15f);
            int deaths = 0;
            var boss = EnemyController.Create(EnemyType.Tidekeeper, center, game.Player, () => game.IsPaused, _ => deaths++, game.CameraFollow);
            typeof(EnemyController).GetField("speed", PrivateInstance).SetValue(boss, 0f);
            typeof(EnemyController).GetField("specialTimer", PrivateInstance).SetValue(boss, 10000f);
            yield return new WaitForSeconds(.15f);
            var body = boss.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
            if (body.sprite.rect.size != new Vector2(100, 100) || body.sprite.pivot != new Vector2(49, 6) || body.sprite.pixelsPerUnit != 32)
                throw new Exception("Windows Tidekeeper native canvas/ground pivot changed");
            var bounds = boss.DamageCollider.bounds;
            Physics2D.SyncTransforms();
            bool bodyHit = false, emptyFeetHit = false;
            foreach (var hit in Physics2D.RaycastAll((Vector2)bounds.center + Vector2.left * 3, Vector2.right, 6, 1 << GameLayers.Enemy))
                if (EnemyController.DamageTarget(hit.collider) == boss) bodyHit = true;
            foreach (var hit in Physics2D.RaycastAll(center + Vector2.left * 3, Vector2.right, 6, 1 << GameLayers.Enemy))
                if (EnemyController.DamageTarget(hit.collider) == boss) emptyFeetHit = true;
            if (!bodyHit || emptyFeetHit) throw new Exception("Floating body damage ray hits empty feet or misses body");
            boss.TakeDamage(1, Vector2.zero); yield return new WaitForSeconds(.11f);
            var recoil = IndividualArt.Frames(TidekeeperArt.Root + "immune", 8);
            if (Array.IndexOf(recoil, body.sprite) < 0 || deaths != 0) throw new Exception("Windows Tidekeeper recoil missing");
            Time.timeScale = 0; var capture = Capture("39-tidekeeper-native-recoil"); while (capture.MoveNext()) yield return capture.Current;
            Time.timeScale = 1; yield return new WaitForSeconds(.3f);
            foreach (bool lanes in new[] { true, false })
            {
                typeof(EnemyController).GetField("specialTimer", PrivateInstance).SetValue(boss, 10000f);
                boss.StartCoroutine((IEnumerator)typeof(EnemyController).GetMethod(lanes ? "TideLanes" : "CallTide", PrivateInstance).Invoke(boss, lanes ? null : new object[] { true }));
                yield return new WaitForSeconds(.2f); boss.TakeDamage(1, Vector2.zero); yield return new WaitForSeconds(.08f);
                if (Array.IndexOf(recoil, body.sprite) >= 0) throw new Exception("Recoil hid native boss anticipation");
                yield return new WaitForSeconds((lanes ? TideLanePattern.WarningSeconds : .95f) - .28f + .12f);
                var cast = IndividualArt.Frames(TidekeeperArt.Root + (lanes ? "shield_cast" : "laser_cast"), lanes ? 10 : 7);
                if (!boss.IsActing || boss.AttackVisualPhase != 1 || Array.IndexOf(cast, body.sprite) < 4)
                    throw new Exception("Windows native cast does not coincide with active hazard");
                if (boss.DamageCollider.bounds != bounds) throw new Exception("Windows cast changed target body envelope");
                Time.timeScale = 0; capture = Capture(lanes ? "40-tidekeeper-native-lanes" : "41-tidekeeper-native-pools");
                while (capture.MoveNext()) yield return capture.Current;
                Time.timeScale = 1; yield return new WaitForSeconds(lanes ? 2.2f : 2.05f);
                if (boss.IsActing || deaths != 0) throw new Exception("Windows native cast extended original recovery");
            }
            boss.TakeDamage(100000, Vector2.zero); boss.TakeDamage(100000, Vector2.zero);
            if (!boss.IsDead || deaths != 0 || boss.DamageCollider.enabled || boss.GetComponent<Rigidbody2D>().simulated)
                throw new Exception("Windows native boss death combat state mismatch");
            yield return new WaitForSeconds(.6f); Time.timeScale = 0;
            capture = Capture("42-tidekeeper-native-collapse"); while (capture.MoveNext()) yield return capture.Current;
            Time.timeScale = 1; yield return new WaitForSeconds(.7f);
            if (boss != null || deaths != 1) throw new Exception("Windows native boss collapse did not settle exactly once");
            game.enabled = true;
            Debug.Log("[Tidekeeper Windows] PASS native recoil, two distinct casts on real hazard phases, original recovery, common ground pivot, real body/empty-feet raycasts, fixed damage envelope and once-only collapse. Actors/contacts supplied; not normal campaign.");
        }

        private IEnumerator ChargerMotionFixtures()
        {
            game.StopAllCoroutines(); game.enabled = false; RuntimeSceneObjects.ClearSession();
            var center = game.DungeonMap.GetRoomCenter(3);
            game.Player.SetStageClearSafety(true);
            game.CameraFollow.FocusRoom(center, new Vector2(20, 14), true);
            var aims = new[] { Vector2.down, Vector2.up, Vector2.left, Vector2.right };
            var names = new[] { "down", "up", "left", "right" };
            for (int direction = 0; direction < aims.Length; direction++)
            {
                Time.timeScale = 1; game.Player.Teleport(center + aims[direction] * 5.5f);
                int deaths = 0;
                var enemy = EnemyController.Create(EnemyType.Charger, center, game.Player, () => game.IsPaused, _ => deaths++, game.CameraFollow);
                typeof(EnemyController).GetField("specialTimer", PrivateInstance).SetValue(enemy, 10000f);
                typeof(EnemyController).GetField("speed", PrivateInstance).SetValue(enemy, 0f);
                yield return new WaitForSeconds(.15f);
                var renderer = enemy.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
                var idle = ExternalCreatureArt.Frames(EnemyType.Charger, 0, direction);
                var hurt = ExternalCreatureArt.HurtFrames(EnemyType.Charger, direction);
                var death = ExternalCreatureArt.Frames(EnemyType.Charger, 3, direction);
                if (idle.Length != 4 || hurt.Length != 6 || death.Length != 8 || Array.IndexOf(idle, renderer.sprite) < 0)
                    throw new Exception("Windows charger source frames/direction mismatch " + names[direction]);
                if (renderer.sprite.rect.size != new Vector2(64, 64) || renderer.sprite.pivot != new Vector2(32, 24) || renderer.sprite.pixelsPerUnit != 32)
                    throw new Exception("Windows charger common native canvas/foot pivot changed");
                if (Vector2.Distance(enemy.DamageCollider.bounds.size, new Vector2(.70f, .98f)) > .02f)
                    throw new Exception("Windows charger upright body damage bounds mismatch");
                enemy.TakeDamage(1, Vector2.zero); yield return new WaitForSeconds(.045f);
                if (Array.IndexOf(hurt, renderer.sprite) < 0 || deaths != 0)
                    throw new Exception("Windows charger native hurt/reward mismatch");
                yield return new WaitForSeconds(.2f);
                enemy.StartCoroutine((IEnumerator)typeof(EnemyController).GetMethod("ChargeAttack", PrivateInstance).Invoke(enemy, new object[] { aims[direction], false }));
                yield return new WaitForSeconds(.56f);
                var moving = ExternalCreatureArt.AttackFrames(EnemyType.Charger, direction, 1);
                if (!enemy.IsActing || enemy.AttackVisualPhase != 1 || Array.IndexOf(moving, renderer.sprite) < 0)
                    throw new Exception("Windows charger running strike is not on real collision phase " + names[direction]);
                Time.timeScale = 0;
                var capture = Capture("37-charger-native-rush-" + names[direction]);
                while (capture.MoveNext()) yield return capture.Current;
                Time.timeScale = 1; yield return new WaitForSeconds(.75f);
                if (enemy.IsActing || deaths != 0) throw new Exception("Windows charger failed original recovery time");
                enemy.TakeDamage(100000, Vector2.zero); enemy.TakeDamage(100000, Vector2.zero);
                if (!enemy.IsDead || deaths != 1 || enemy.DamageCollider.enabled || enemy.GetComponent<Rigidbody2D>().simulated)
                    throw new Exception("Windows charger death combat/once-only settlement mismatch");
                yield return new WaitForSeconds(.24f);
                typeof(GameController).GetMethod("SetManualPause", PrivateInstance).Invoke(game, new object[] { true });
                yield return new WaitForEndOfFrame(); var frozen = renderer.sprite;
                yield return new WaitForSecondsRealtime(.25f);
                if (enemy == null || renderer.sprite != frozen || deaths != 1)
                    throw new Exception("Windows charger death advanced during pause");
                game.ResumeGame(); Time.timeScale = 0;
                capture = Capture("38-charger-native-collapse-" + names[direction]);
                while (capture.MoveNext()) yield return capture.Current;
                Time.timeScale = 1; yield return new WaitForSeconds(.75f);
                if (enemy != null || deaths != 1) throw new Exception("Windows charger collapse cleanup mismatch");
                if (FindObjectsByType<FadingSpark>().Length != 0) throw new Exception("Charger death still emits generic square burst");
            }
            game.enabled = true;
            Debug.Log("[Charger Windows] PASS four-direction native standing/hurt/running-strike/collapse, common foot pivot, body damage bounds, original charge recovery, pause and once-only cleanup. Actors/contacts supplied; not normal campaign.");
        }


        private IEnumerator EnemyReactionFixtures()
        {
            game.StopAllCoroutines(); game.enabled = false; RuntimeSceneObjects.ClearSession();
            var center = game.DungeonMap.GetRoomCenter(3);
            game.Player.Teleport(center + Vector2.right * 2); game.Player.SetStageClearSafety(true);
            game.CameraFollow.FocusRoom(center, new Vector2(20, 14), true);
            Time.timeScale = 1; yield return new WaitForSeconds(.15f);
            var grunt = EnemyController.Create(EnemyType.Grunt, center, game.Player, () => game.IsPaused, _ => {}, game.CameraFollow);
            var body = grunt.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
            typeof(EnemyController).GetField("specialTimer", PrivateInstance).SetValue(grunt,10000f);
            yield return new WaitForSeconds(.15f);
            yield return new WaitForEndOfFrame();
            grunt.StartCoroutine((IEnumerator)typeof(EnemyController).GetMethod("GruntLunge", PrivateInstance).Invoke(grunt, new object[] { Vector2.right }));
            grunt.TakeDamage(1, Vector2.zero); yield return new WaitForSeconds(.09f);
            // Tint ownership is resolved in LateUpdate; inspect the rendered
            // state, not the coroutine-resumption slot before that update.
            yield return new WaitForEndOfFrame();
            if (!grunt.IsActing || grunt.AttackVisualPhase != 0 || Mathf.Abs(body.color.g - .9f) > .001f)
                throw new Exception("Windows enemy hit did not restore anticipation: acting="+grunt.IsActing+" phase="+grunt.AttackVisualPhase+" color="+body.color+" remaining="+typeof(EnemyController).GetField("hitFlashRemaining",PrivateInstance).GetValue(grunt));
            Time.timeScale = 0;
            var capture = Capture("35-enemy-anticipation-restored");
            while (capture.MoveNext()) yield return capture.Current;
            Destroy(grunt.gameObject); Time.timeScale = 1; yield return null;
            grunt = EnemyController.Create(EnemyType.Grunt, center, game.Player, () => game.IsPaused, _ => {}, game.CameraFollow);
            body = grunt.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
            // This is a timer-ownership fixture, not an animation-speed review.
            // Give sub-55ms scaled intervals enough rendered frames on slow hosts.
            Time.timeScale=.1f;
            float firstHitAt=Time.time;
            grunt.TakeDamage(1, Vector2.zero); yield return new WaitForSeconds(.035f);
            float secondHitAt=Time.time;grunt.TakeDamage(1, Vector2.zero);
            yield return new WaitForSeconds(.03f);
            yield return new WaitForEndOfFrame();
            float firstAge=Time.time-firstHitAt,secondAge=Time.time-secondHitAt;
            if(firstAge<.055f||secondAge>=.055f)
                throw new Exception("Windows repeated-hit sampling missed overlap window: first="+firstAge+" second="+secondAge);
            if (Mathf.Abs(body.color.g - .68f) > .001f) throw new Exception("Windows older hit canceled the newest flash: first="+firstAge+" second="+secondAge+" color="+body.color+" remaining="+typeof(EnemyController).GetField("hitFlashRemaining",PrivateInstance).GetValue(grunt));
            Debug.Log("[Repeated Hit Window] first="+firstAge+" second="+secondAge+" color="+body.color+" scale="+Time.timeScale+"; controlled clock fixture, not normal-speed review.");
            yield return new WaitForSeconds(.08f);
            yield return new WaitForEndOfFrame();
            if (body.color != Color.white) throw new Exception("Windows enemy tint remains after repeated hit");
            Time.timeScale=1;Destroy(grunt.gameObject); yield return null;
            foreach (var type in new[] { EnemyType.Boss, EnemyType.Tidekeeper, EnemyType.SealHeart })
            {
                int deaths = 0;
                var boss = EnemyController.Create(type, center, game.Player, () => game.IsPaused, _ => deaths++, game.CameraFollow);
                body = boss.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
                boss.TakeDamage(100000, Vector2.zero); yield return new WaitForSeconds(.08f);
                typeof(GameController).GetMethod("SetManualPause", PrivateInstance).Invoke(game, new object[] { true });
                yield return new WaitForEndOfFrame();
                float hp = boss.Health; var frame = body.sprite;
                yield return new WaitForSecondsRealtime(.25f);
                if (boss == null || boss.Health != hp || body.sprite != frame || deaths != 0)
                    throw new Exception("Windows boss death advanced during pause " + type);
                game.ResumeGame(); yield return new WaitForSeconds(.1f); Time.timeScale = 0;
                capture = Capture("36-unmasked-boss-death-" + type);
                while (capture.MoveNext()) yield return capture.Current;
                Time.timeScale = 1; yield return new WaitForSeconds(1.2f);
                if (boss != null || deaths != 1) throw new Exception("Windows boss death did not settle once " + type);
                if (FindObjectsByType<FadingSpark>().Length != 0) throw new Exception("Authored boss death still emits generic square bursts");
            }
            game.enabled = true;
            Debug.Log("[Enemy Reaction Windows] PASS anticipation restoration, overlapping-hit ownership, all three boss death pause/resume/once-only callbacks and no square burst. Actors/contacts supplied; not normal campaign.");
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(tooltipFixture) || hud == null) return;
            var scale = GameTypography.LayoutScale;
            var oldMatrix = GUI.matrix;
            GUI.depth = -1000;
            var oldMouse = Event.current.mousePosition;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            Event.current.mousePosition = new Vector2(Screen.width / scale - 12, Screen.height / scale - 12);
            typeof(PrototypeHud).GetMethod("DrawItemTooltip", PrivateInstance).Invoke(hud,
                new object[] { Screen.width / scale, tooltipFixtureHeight>0?tooltipFixtureHeight:Screen.height / scale, tooltipFixture });
            if(validateTooltipLayouts&&!tooltipLayoutsChecked&&Event.current.type==EventType.Repaint)
                ValidateTooltipLayouts(Screen.width/scale,Screen.height/scale);
            Event.current.mousePosition = oldMouse;
            GUI.matrix = oldMatrix;
        }

        // Explicitly supplied equipment/contact: verifies the built renderer and
        // real damage path, never counts as normal campaign or balance evidence.
        private IEnumerator PlayerHitFixtures()
        {
            game.StopAllCoroutines(); game.enabled = false;
            var inputType = typeof(StageValidationInput);
            var staticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
            inputType.GetField("initialized", staticPrivate).SetValue(null, true);
            inputType.GetField("enabled", staticPrivate).SetValue(null, true);
            var center = game.DungeonMap.GetRoomCenter(3);
            game.CameraFollow.FocusRoom(center, new Vector2(20, 14), true);
            SetModal("IsInventoryOpen", false);
            typeof(GameController).GetProperty("StageIntroUntil").SetValue(game, 0f);
            for (int protection = 0; protection < 3; protection++)
            {
                RuntimeSceneObjects.ClearSession();
                var oldPlayer = game.Player;
                oldPlayer.gameObject.SetActive(false); Destroy(oldPlayer.gameObject);
                var bag = new InventorySystem(); bag.AddWeapon(WeaponLootTable.Find("storm_rifle"));
                if (protection == 1)
                {
                    bag.AddAccessory(AccessoryLootTable.Find("guardian_seal"));
                    bag.AddAccessory(AccessoryLootTable.Find("bulwark_brooch"));
                }
                if (protection == 2) bag.AddAccessory(AccessoryLootTable.Find("last_light_pendant"));
                var player = PlayerController.Create(center, () => game.IsPaused, () => false, () => {}, game.CameraFollow, bag, game.Settings);
                player.EquipWeapon(bag.EquippedWeapon);
                typeof(GameController).GetProperty("Inventory").SetValue(game, bag);
                typeof(GameController).GetProperty("Player").SetValue(game, player);
                game.CameraFollow.Target = player.transform;
                StageValidationInput.ReleaseAll(); StageValidationInput.SetAim(new Vector2(1, -1));
                Time.timeScale = 1; yield return new WaitForSeconds(.15f);
                if (protection == 1) player.ItemEffects.OnDefenseSuccess(player.AimDirection);
                if (protection == 2) player.Stats.ReceiveDamage(player.MaxHealth * .75f);
                CombatAudio.StopCombat(); var hp = player.Health;
                var position = player.transform.position;
                player.TakeDamage(4, -player.AimDirection);
                if ((protection == 0 ? player.Health >= hp : player.Health != hp) ||
                    player.InvincibilityTicksRemaining != PlayerController.HitInvincibilityTicks)
                    throw new Exception("Windows hit/shield health or immunity mismatch");
                var tint = player.BodyRenderer.color;
                if (protection == 0 ? tint.r <= tint.b : tint.b <= tint.r)
                    throw new Exception("Windows damage/shield flash mismatch");
                bool sound = false;
                var clip = CombatAudio.Clip(protection == 0 ? "hurt" : "block");
                foreach (var source in FindObjectsByType<AudioSource>())
                    if (source.isPlaying && source.clip == clip) sound = true;
                if (!sound) throw new Exception("Windows hit/shield audio did not play");
                yield return new WaitForSeconds(.035f);
                Time.timeScale = 0; yield return new WaitForEndOfFrame();
                int contacts = 0;
                foreach (var fx in FindObjectsByType<NativeCombatEffect>())
                {
                    if (fx.ClipId != "PlayerHit" && fx.ClipId != "ItemShield") continue;
                    if (fx.GetComponent<SpriteRenderer>().sortingOrder >= player.BodyRenderer.sortingOrder)
                        throw new Exception("Windows contact/shield covers body");
                    if (fx.ClipId == "PlayerHit") contacts++;
                }
                if (contacts != (protection == 0 ? 1 : 0)) throw new Exception("Windows injury contact count mismatch");
                if (protection != 0 && Vector3.Distance(position, player.transform.position) > .001f)
                    throw new Exception("Windows fully absorbed contact moved player");
                var capture = Capture("34-player-hit-protection-" + protection);
                while (capture.MoveNext()) yield return capture.Current;
                Time.timeScale = 1; yield return new WaitForSeconds(.25f);
                foreach (var fx in FindObjectsByType<NativeCombatEffect>())
                    if (fx.ClipId == "PlayerHit") throw new Exception("Windows injury contact did not finish");
            }
            StageValidationInput.ReleaseAll();
            inputType.GetField("enabled", staticPrivate).SetValue(null, false);
            game.enabled = true;
            Debug.Log("[Player Hit Windows] PASS actual damage, Guardian, Last Light, color, playback, occlusion, no full-shield shove and short-effect cleanup. Supplied fixture; no campaign claim.");
        }

        [Serializable] private sealed class TooltipRow { public string id;public int level;public float titleHeight,descriptionHeight,panelHeight;public bool scroll; }
        [Serializable] private sealed class TooltipReport { public string scope="Actual Galmuri OnGUI metrics and production ItemTooltipLayout; staged fixture, not native input or campaign.";public int width,height;public List<TooltipRow> rows=new List<TooltipRow>(); }
        private void ValidateTooltipLayouts(float width,float height)
        {
            var style=(GUIStyle)typeof(PrototypeHud).GetField("detailStyle",PrivateInstance).GetValue(hud);
            var report=new TooltipReport{width=Screen.width,height=Screen.height};
            foreach(var d in AccessoryLootTable.All)
            {
                var bag=new InventorySystem();bag.AddAccessory(d);bag.AwardMaterials(100,"layout-fixture");var item=bag.OwnedAccessories[0];
                for(int level=0;level<=3;level++)
                {
                    if(level>0&&!bag.TryUpgrade(item.Identity,level-1))throw new Exception("Layout fixture upgrade failed");
                    string description=EquipmentUpgradeRules.AccessoryDescription(item,bag);
                    string set=ItemSetCatalog.GetName(d.SetType)+"  3/3";
                    foreach(var bonus in ItemSetCatalog.Bonuses)if(bonus.SetType==d.SetType)set+="\n● "+bonus.RequiredPieces+"세트: "+bonus.Description;
                    float panelWidth=Mathf.Min(440,width-16),contentWidth=panelWidth-66;
                    float titleHeight=Mathf.Max(32,style.CalcHeight(new GUIContent(item.DisplayName),panelWidth-122));
                    float categoryHeight=style.CalcHeight(new GUIContent(d.Rarity+" · 장신구"),panelWidth-122);
                    float descriptionHeight=style.CalcHeight(new GUIContent(description),contentWidth);
                    float setHeight=style.CalcHeight(new GUIContent(set),contentWidth);
                    var layout=new ItemTooltipLayout(width,height,new Vector2(width-12,height-12),titleHeight,categoryHeight,descriptionHeight,setHeight);
                    if(layout.Panel.x<8||layout.Panel.y<8||layout.Panel.xMax>width-7.99f||layout.Panel.yMax>height-7.99f||layout.Title.yMax>layout.Category.y||layout.Category.yMax>layout.Viewport.y||layout.Icon.yMax>layout.Viewport.y||layout.SetTop<descriptionHeight+17||layout.Viewport.height<=0||layout.ContentWidth>layout.Viewport.width-17)
                        throw new Exception("Tooltip overlap/bounds failure: "+d.Id+" +"+level+" "+width+"x"+height);
                    AssertBundledGlyphs(style.font,item.DisplayName+description+set);
                    report.rows.Add(new TooltipRow{id=d.Id,level=level,titleHeight=titleHeight,descriptionHeight=descriptionHeight,panelHeight=layout.Panel.height,scroll=layout.NeedsScroll});
                }
            }
            File.WriteAllText(Path.Combine(directory,"tooltip-layout.json"),JsonUtility.ToJson(report,true));
            tooltipLayoutsChecked=true;
            Debug.Log("[UI Validation] PASS "+report.rows.Count+" current accessory/upgrade layouts: separate title/category/icon/body/set, bounded panel, scrollbar reserve and complete glyphs.");
        }

        private static void AssertBundledGlyphs(Font font,string text)
        {
            // Font.HasCharacter can succeed through an installed fallback font.
            // Validate the shipped face itself so missing glyphs cannot pass silently.
            if(UnityEngine.TextCore.LowLevel.FontEngine.LoadFontFace(font,20)!=UnityEngine.TextCore.LowLevel.FontEngineError.Success)
                throw new InvalidOperationException("Cannot read bundled font: "+font.name);
            foreach(char c in text)
                if(!char.IsWhiteSpace(c)&&!UnityEngine.TextCore.LowLevel.FontEngine.TryGetGlyphIndex(c,out _))
                    throw new InvalidOperationException(font.name+" missing bundled glyph U+"+((int)c).ToString("X4"));
        }

        private IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(.25f);
            screenshot.Invoke(null, new object[] { Path.Combine(directory, name + ".png"), 1 });
            yield return new WaitForSecondsRealtime(.2f);
        }
    }
}
