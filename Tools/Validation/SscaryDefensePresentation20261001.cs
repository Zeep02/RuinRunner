using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using RuinRunner;

// Staged visual review on the real first-region map. Items/targets supplied for review, not a normal run.
public static class SscaryDefensePresentation20261001
{
    static string folder; static GameController game; static Vector2 center;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
    static EditorWindow view; static object sizes; static int oldSize, oldCustomCount;
    static Type autoTick; static bool oldTickEnabled; static long oldTickInterval;
    static readonly List<int> customSizes = new List<int>();
    static readonly List<string> checks = new List<string>();
    static readonly StringBuilder recording = new StringBuilder();
    sealed class Frame { public int Index, Width, Height; public Color32[] Pixels; public string Showcase; }
    static readonly List<Frame> buffered = new List<Frame>(); static int sample;
    static int pending; static bool readbackError, flipReadback;
    static readonly StringBuilder telemetry=new StringBuilder();
    static readonly HashSet<Sprite> phaseSprites=new HashSet<Sprite>();
    static bool sawMelee,sawMobileMelee;
    static readonly HashSet<string> phasePoses=new HashSet<string>();
    static float traveled;static Vector2 previousPosition;
    static void ResetObservations(){sawMelee=sawMobileMelee=false;phaseSprites.Clear();phasePoses.Clear();traveled=0;previousPosition=game.Player.transform.position;}
    static string previousPose;static Vector2 previousHand;static float previousAngle;static int seamChecks;
    static void Track(string phase)
    {
        var p=game.Player;var a=p.GetComponent<PlayerAnimationController>();var body=p.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();var held=p.GetComponent<HeldWeaponView>();
        traveled+=Vector2.Distance(previousPosition,p.transform.position);previousPosition=p.transform.position;
        foreach(var fx in UnityEngine.Object.FindObjectsByType<NativeCombatEffect>())if(fx.IsBelowActor&&fx.GetComponent<SpriteRenderer>().sortingOrder>=body.sortingOrder)throw new Exception("action effect covers the moving actor "+phase);
        if(a.AuthoredWeaponRotationActive)
        {
            if(Vector2.Distance(p.transform.Find("HeldWeaponPivot").localPosition,a.HandLocalPosition)>.002f)throw new Exception("defense blade grip detached "+phase);
            if(a.HandBehindBody&&p.transform.Find("HeldWeaponPivot/HeldWeaponSprite").GetComponent<SpriteRenderer>().sortingOrder>=body.sortingOrder)throw new Exception("occluded hand blade crosses face/pack "+phase);
            if(body.sprite.rect.size!=new Vector2(48,64)||body.sprite.pivot!=new Vector2(24,22)||body.sprite.pixelsPerUnit!=32)throw new Exception("native body geometry "+phase);
            if(Mathf.Abs(Mathf.DeltaAngle(a.AuthoredWeaponRotation,held.DisplayedActionRotation))>.01f)throw new Exception("body/blade clocks disagree "+phase);
            phaseSprites.Add(body.sprite);phasePoses.Add(a.PoseName.Replace("-move",""));
            if(previousPose=="charge"||previousPose=="charge-move")
            {
                if(a.PoseName.StartsWith("charge-cleave"))
                {
                    var jump=Vector2.Distance(previousHand,a.HandLocalPosition)*32;
                    if(jump>3.2f||Mathf.Abs(Mathf.DeltaAngle(previousAngle,held.DisplayedActionRotation))>22)throw new Exception("release snaps from its actual partial/full charge "+phase+" pixels="+jump);
                    seamChecks++;
                }
            }
        }
        previousPose=a.PoseName;previousHand=a.HandLocalPosition;previousAngle=held.DisplayedActionRotation;
        telemetry.AppendLine(string.Join(",",sample,phase,p.AttackAnimationSequence,a.AuthoredWeaponRotationActive,a.PoseName,a.FacingDirection,a.FrameIndex,body.sprite.name,
            p.AttackAnimationProgress.ToString("F4",CultureInfo.InvariantCulture),p.transform.position.x.ToString("F4",CultureInfo.InvariantCulture),p.transform.position.y.ToString("F4",CultureInfo.InvariantCulture),
            a.HandLocalPosition.x.ToString("F4",CultureInfo.InvariantCulture),a.HandLocalPosition.y.ToString("F4",CultureInfo.InvariantCulture),a.HandBehindBody,p.ActiveMeleeMotion,p.MeleeContactsResolved,held.DisplayedActionRotation.ToString("F4",CultureInfo.InvariantCulture)));
    }
    static void Require(bool okay, string label) { if (!okay) throw new Exception(label); checks.Add("PASS " + label); }
    static void ClearEffects()
    {
        game.Player.ItemEffects.CancelTransient();
        foreach(var fx in UnityEngine.Object.FindObjectsByType<NativeCombatEffect>())UnityEngine.Object.Destroy(fx.gameObject);
        foreach(var fx in UnityEngine.Object.FindObjectsByType<WeaponSecondaryParticles>())UnityEngine.Object.Destroy(fx.gameObject);
        foreach (var e in UnityEngine.Object.FindObjectsByType<EnemyController>()) { e.gameObject.SetActive(false); UnityEngine.Object.Destroy(e.gameObject); }
        foreach (var p in UnityEngine.Object.FindObjectsByType<PlayerProjectile>()) { p.gameObject.SetActive(false); UnityEngine.Object.Destroy(p.gameObject); }
    }
    static void Equip(string id, params AccessoryType[] accessories)
    {
        ClearEffects(); var bag = game.Inventory; bag.Reset(); bag.AddWeapon(WeaponLootTable.Find(id));
        foreach (var type in accessories) bag.AddAccessory(AccessoryLootTable.Find(type));
        game.Player.EquipWeapon(bag.EquippedWeapon); game.Player.Teleport(center + Vector2.left * 1.4f);
        StageValidationInput.BeginFrame(); StageValidationInput.ReleaseAll(); StageValidationInput.SetAim(Vector2.right);
    }
    static EnemyController Enemy(Vector2 offset)
    {
        var enemy = EnemyController.Create((EnemyType)0, (Vector2)game.Player.transform.position + offset, game.Player, () => false,
            dead => game.Player.OnEnemyDefeated(dead), game.CameraFollow);
        enemy.enabled = false; enemy.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
        typeof(EnemyController).GetProperty("MaxHealth").SetValue(enemy, 3000f); typeof(EnemyController).GetProperty("Health").SetValue(enemy, 3000f);
        return enemy;
    }
    static void Pulse(GameAction action)
    { StageValidationInput.BeginFrame(); StageValidationInput.ReleaseAll(); StageValidationInput.SetHeld(action, true); StageValidationInput.SetHeld(action, false); }
    public static string Main()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode required");
        folder = "F:/codex/LoglikeCap/Logs/Validation/2026-10-01-hero-defense/presentation-" + DateTime.Now.ToString("HHmmss"); Directory.CreateDirectory(folder + "/frames");
        checks.Clear(); customSizes.Clear(); buffered.Clear(); sample = 0; recording.Clear(); recording.AppendLine("frame,realtime,phase"); telemetry.Clear();telemetry.AppendLine("frame,phase,sequence,melee,pose,direction,key,sprite,progress,x,y,handX,handY,behind,motion,contacts,angle");
        game = UnityEngine.Object.FindAnyObjectByType<GameController>(); game.ReturnToMainMenu(); game.SaveSlots.OpenTransientForTests();
        game.enabled = true; foreach (var hud in UnityEngine.Object.FindObjectsByType<PrototypeHud>()) hud.enabled = true;
        typeof(GameController).GetMethod("BeginRunForActiveSlot", Private).Invoke(game, null);
        typeof(StageValidationInput).GetField("enabled", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
        typeof(StageValidationInput).GetField("initialized", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
        StageValidationInput.ReleaseAll(); Application.runInBackground = true; Application.targetFrameRate = 60; Time.captureFramerate = 0;
        view = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")); view.Focus();
        oldSize = (int)view.GetType().GetProperty("selectedSizeIndex", All).GetValue(view);
        var t = Type.GetType("UnityEditor.GameViewSizes,UnityEditor"); var instance = t.GetProperty("instance", All).GetValue(null);
        sizes = t.GetMethod("GetGroup", All).Invoke(instance, new[] { Enum.Parse(Type.GetType("UnityEditor.GameViewSizeGroupType,UnityEditor"), "Standalone") });
        oldCustomCount = (int)sizes.GetType().GetMethod("GetCustomCount", All).Invoke(sizes, null);
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        { autoTick = assembly.GetType("Unity.Pipeline.Editor.Commands.AutoTickCommand"); if (autoTick != null) break; }
        if (autoTick == null) throw new Exception("Installed editor auto-tick command not found");
        oldTickEnabled = (bool)autoTick.GetProperty("IsEnabled", All).GetValue(null);
        oldTickInterval = (long)autoTick.GetProperty("CurrentIntervalMs", All).GetValue(null);
        autoTick.GetMethod("SetAutoTick", All).Invoke(null, new object[] { true, 0, false });
        game.CameraFollow.StartCoroutine(Guard(Run())); return folder;
    }
    static void Resolution(int width, int height)
    {
        var t = Type.GetType("UnityEditor.GameViewSize,UnityEditor");
        var size = Activator.CreateInstance(t, new object[] { Enum.Parse(Type.GetType("UnityEditor.GameViewSizeType,UnityEditor"), "FixedResolution"), width, height, "RuinRunner review " + width + "x" + height });
        int index = (int)sizes.GetType().GetMethod("GetTotalCount", All).Invoke(sizes, null);
        sizes.GetType().GetMethod("AddCustomSize", All).Invoke(sizes, new[] { size }); customSizes.Add(index);
        view.GetType().GetProperty("selectedSizeIndex", All).SetValue(view, index); view.Repaint();
    }
    static void RestoreView()
    {
        if (autoTick != null) autoTick.GetMethod("SetAutoTick", All).Invoke(null, new object[] { oldTickEnabled, (int)oldTickInterval, false });
        if (view == null || sizes == null) return;
        view.GetType().GetProperty("selectedSizeIndex", All).SetValue(view, oldSize);
        for (int i = customSizes.Count - 1; i >= 0; i--)
        {
            sizes.GetType().GetMethod("RemoveCustomSize", All).Invoke(sizes, new object[] { customSizes[i] });
        }
        customSizes.Clear();
        Require((int)sizes.GetType().GetMethod("GetCustomCount", All).Invoke(sizes, null) == oldCustomCount,
            "review restores original Game View size selection and removes only its own temporary presets");
    }
    static void Capture(string name)
    { var texture = ScreenCapture.CaptureScreenshotAsTexture(); File.WriteAllBytes(folder + "/" + name + ".png", texture.EncodeToPNG()); UnityEngine.Object.Destroy(texture); }
    static IEnumerator Record(float seconds, string phase, float showcase)
    {
        double start = Time.realtimeSinceStartupAsDouble; bool taken = false;
        var target = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32); target.Create();
        pending = 0; readbackError = false;
        while (Time.realtimeSinceStartupAsDouble - start < seconds)
        {
            yield return new WaitForEndOfFrame(); double time = Time.realtimeSinceStartupAsDouble; Track(phase);
            string name = null;
            if (!taken && time - start >= showcase) { taken = true; name = phase + "-active"; }
            var frame = new Frame { Index = sample, Width = target.width, Height = target.height, Showcase = name }; buffered.Add(frame);
            ScreenCapture.CaptureScreenshotIntoRenderTexture(target); pending++;
            AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32, request =>
            {
                if (request.hasError) readbackError = true;
                else frame.Pixels = request.GetData<Color32>().ToArray();
                pending--;
            });
            recording.AppendLine(sample++ + "," + time.ToString("F6", CultureInfo.InvariantCulture) + "," + phase);
        }
        while (pending > 0) yield return null;
        target.Release(); UnityEngine.Object.Destroy(target);
        Require(!readbackError, "native asynchronous screen readback completed without dropped/failed frames " + phase);
    }
    static IEnumerator VerifyReadback()
    {
        Require(SystemInfo.supportsAsyncGPUReadback, "GPU supports asynchronous native screen readback");
        Time.timeScale = 0; yield return new WaitForEndOfFrame();
        var reference = ScreenCapture.CaptureScreenshotAsTexture(); var referencePixels = reference.GetPixels32();
        File.WriteAllBytes(folder + "/capture-reference.png", reference.EncodeToPNG()); UnityEngine.Object.Destroy(reference);
        var target = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32); target.Create();
        ScreenCapture.CaptureScreenshotIntoRenderTexture(target); Color32[] pixels = null; bool failed = false;
        AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32, request =>
        { if (request.hasError) failed = true; else pixels = request.GetData<Color32>().ToArray(); });
        while (pixels == null && !failed) yield return null;
        Require(!failed, "native asynchronous reference capture succeeds");
        int normal = 0, flipped = 0, width = target.width, height = target.height;
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            var expected = referencePixels[y * width + x]; var a = pixels[y * width + x]; var b = pixels[(height - 1 - y) * width + x];
            if (a.r != expected.r || a.g != expected.g || a.b != expected.b) normal++;
            if (b.r != expected.r || b.g != expected.g || b.b != expected.b) flipped++;
        }
        flipReadback = flipped < normal;
        File.WriteAllText(folder + "/capture-readback-qa.txt", "normalDifferent=" + normal + " flippedDifferent=" + flipped + " verticalOrientationCorrection=" + flipReadback + "\nPixel comparison with synchronous native composited screen; no resampling.");
        Require(Mathf.Min(normal, flipped) < width * height * .01f, "asynchronous capture agrees with native composited screen pixels/orientation");
        target.Release(); UnityEngine.Object.Destroy(target); Time.timeScale = 1;
    }
    static void Flush()
    {
        // Compress between separately timestamped phases, never during the recorded motion.
        foreach (var f in buffered)
        {
            var texture = new Texture2D(f.Width, f.Height, TextureFormat.RGBA32, false); texture.SetPixels32(f.Pixels); texture.Apply();
            if (flipReadback)
            {
                var oriented = new Color32[f.Pixels.Length];
                for (int y = 0; y < f.Height; y++) Array.Copy(f.Pixels, y * f.Width, oriented, (f.Height - 1 - y) * f.Width, f.Width);
                texture.SetPixels32(oriented); texture.Apply();
            }
            string path = folder + "/frames/frame-" + f.Index.ToString("D6") + ".png"; File.WriteAllBytes(path, texture.EncodeToPNG());
            if (f.Showcase != null) File.Copy(path, folder + "/" + f.Showcase + ".png");
            UnityEngine.Object.DestroyImmediate(texture); f.Pixels = null;
        }
        buffered.Clear(); File.WriteAllText(folder + "/recording.csv", recording.ToString(), new UTF8Encoding(false));File.WriteAllText(folder+"/body-telemetry.csv",telemetry.ToString(),new UTF8Encoding(false));
    }
    static IEnumerator Guard(IEnumerator run)
    {
        while (true)
        {
            object value = null; bool next;
            try { next = run.MoveNext(); if (next) value = run.Current; }
            catch (Exception error) { try{Flush();}catch{} RestoreView(); Time.timeScale = 1; File.WriteAllText(folder + "/result.txt", "FAIL " + error + "\n" + string.Join("\n", checks)); Debug.LogException(error); yield break; }
            if (!next) break; yield return value;
        }
        RestoreView(); File.WriteAllText(folder + "/result.txt", "PASS staged supplied-weapon/target motion review; not normal campaign, Windows build or 60fps performance evidence\n" + string.Join("\n", checks));
        Debug.Log("[Sscary defense presentation] PASS " + folder);
    }
    static IEnumerator Motion(float seconds,string phase,Action<float> input=null)
    {
        double start=Time.timeAsDouble;var motion=Record(seconds,phase,.10f);
        while(motion.MoveNext()){yield return motion.Current;StageValidationInput.BeginFrame();input?.Invoke((float)(Time.timeAsDouble-start));}
    }
    static void FreshObservations(){ResetObservations();previousPose=null;seamChecks=0;}
    static void ClearImmunity(){typeof(PlayerController).GetField("invincibilityTicks",Private).SetValue(game.Player,0);}
    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;typeof(GameController).GetProperty("StageIntroUntil").SetValue(game,0f);
        foreach(var e in UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>())UnityEngine.Object.Destroy(e.gameObject);
        center=game.DungeonMap.GetRoomCenter(3);game.CameraFollow.FocusRoom(center,new Vector2(20,14),true);
        Resolution(1280,720);yield return null;yield return new WaitForEndOfFrame();
        Require(Screen.width==1280&&Screen.height==720,"native 1280x720 real composited screen");
        var verify=VerifyReadback();while(verify.MoveNext())yield return verify.Current;
        foreach(var direction in new[]{0,1,2,4,5,7})
        {
            var aim=new Vector2(Mathf.Sin(direction*Mathf.PI/4),Mathf.Cos(direction*Mathf.PI/4));
            Equip("rust_sword");yield return new WaitForSeconds(.85f);StageValidationInput.SetAim(aim);ClearImmunity();FreshObservations();
            StageValidationInput.SetHeld(GameAction.Guard,true);yield return null;StageValidationInput.BeginFrame();float hp=game.Player.Health;bool struck=false;
            var motion=Motion(.52f,"guard-view-"+direction,t=>{if(t>.29f&&!struck){struck=true;game.Player.TakeDamage(10,-aim);}});
            while(motion.MoveNext())yield return motion.Current;
            Require(phasePoses.Contains("guard")&&phaseSprites.Count>=4,"guard raises native arms/sets stance view "+direction+" poses="+phaseSprites.Count);
            Require(struck&&game.Player.Health<hp&&game.Player.Health>hp-10,"real frontal guard reduces damage view "+direction);
            StageValidationInput.SetHeld(GameAction.Guard,false);motion=Motion(.25f,"guard-return-view-"+direction);while(motion.MoveNext())yield return motion.Current;
            Require(!game.Player.IsGuarding&&game.Player.GetComponent<PlayerAnimationController>().PoseName=="idle","guard visibly returns without extending defense view "+direction);Flush();
            foreach(var success in new[]{false,true})
            {
                Equip("training_katana");yield return new WaitForSeconds(.85f);StageValidationInput.SetAim(aim);ClearImmunity();var enemy=Enemy(aim*1.2f);yield return null;Physics2D.SyncTransforms();
                hp=game.Player.Health;float enemyHp=enemy.Health;struck=false;FreshObservations();Pulse(GameAction.Guard);StageValidationInput.SetAim(aim);yield return null;StageValidationInput.BeginFrame();
                motion=Motion(.46f,"parry-"+(success?"success":"miss")+"-view-"+direction,t=>{if(success&&t>.065f&&!struck){Require(game.Player.IsParrying,"successful strike is inside real parry window");struck=true;game.Player.TakeDamage(10,-aim);}});
                while(motion.MoveNext())yield return motion.Current;
                Require(phasePoses.Contains("parry")&&phaseSprites.Count>=4,"parry has separate fast preparation/recovery view "+direction);
                if(success){Require(phasePoses.Contains("parry-success"),"deflection continues from current native hand key");Require(game.Player.Health==hp&&enemy.Health<enemyHp,"parry protects and counters once using actual combat view "+direction);}
                else Require(enemy.Health==enemyHp,"missed parry adds no phantom counter view "+direction);
                Require(!game.Player.IsParrying&&!game.Player.IsParryPresentationActive,"parry window and cosmetic recovery both end view "+direction);Flush();
            }
            foreach(var full in new[]{false,true})
            {
                Equip("iron_greatsword");yield return new WaitForSeconds(.85f);StageValidationInput.SetAim(aim);var enemy=Enemy(aim*1.3f);yield return null;Physics2D.SyncTransforms();float enemyHp=enemy.Health;
                FreshObservations();int contacts=game.Player.MeleeContactsResolved;bool released=false;float hold=full?1.32f:.23f;
                StageValidationInput.SetHeld(GameAction.Guard,true);yield return null;StageValidationInput.BeginFrame();
                motion=Motion(hold+.62f,"charge-"+(full?"full":"partial")+"-view-"+direction,t=>{if(t>hold&&!released){if(full)Require(game.Player.GetComponent<HeldWeaponView>().DisplayedActionRotation>=119,"full charge holds the raised blade before downward release");released=true;StageValidationInput.SetHeld(GameAction.Guard,false);}});
                while(motion.MoveNext())yield return motion.Current;
                Require(phasePoses.Contains("charge")&&phasePoses.Contains("charge-cleave")&&phaseSprites.Count>=8,"native two-hand preparation/release view "+direction+" full="+full+" poses="+phaseSprites.Count);
                Require(seamChecks==1,"release begins at actual visible hand/angle, once view "+direction+" full="+full);
                Require(enemy.Health<enemyHp&&game.Player.MeleeContactsResolved==contacts+1,"one real charged contact at existing damage clock view "+direction+" full="+full);
                Require(!game.Player.IsGreatswordCharging&&!game.Player.IsMeleeSwingActive&&game.Player.GetComponent<PlayerAnimationController>().PoseName=="idle","charged cut recovers to original idle view "+direction);Flush();
            }
        }
        foreach(var id in new[]{"rust_sword","training_katana","iron_greatsword"})
        {
            Equip(id);yield return new WaitForSeconds(.85f);FreshObservations();StageValidationInput.SetHeld(GameAction.MoveRight,true);StageValidationInput.SetHeld(GameAction.Guard,true);yield return null;StageValidationInput.BeginFrame();bool released=false;
            var motion=Motion(1.18f,id+"-mobile-defense",t=>{if(t>.45f){StageValidationInput.SetHeld(GameAction.MoveRight,false);StageValidationInput.SetHeld(GameAction.MoveLeft,true);}if(t>.64f&&!released){released=true;StageValidationInput.SetHeld(GameAction.Guard,false);}});
            while(motion.MoveNext())yield return motion.Current;
            Require(traveled>3,"defense/preparation keeps normal movement "+id+" distance="+traveled);
            Require(phaseSprites.Count>=8,"defense body keeps independent native gait "+id+" poses="+phaseSprites.Count);StageValidationInput.ReleaseAll();Flush();
        }
        Equip("iron_greatsword");yield return new WaitForSeconds(.85f);StageValidationInput.SetHeld(GameAction.Guard,true);yield return null;StageValidationInput.BeginFrame();yield return new WaitForSeconds(.26f);yield return new WaitForEndOfFrame();
        var anim=game.Player.GetComponent<PlayerAnimationController>();var sprite=game.Player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();var pivot=game.Player.transform.Find("HeldWeaponPivot");
        Require(anim.PoseName=="charge","pause scenario has real authored charge");var savedSprite=sprite.sprite;var savedHand=anim.HandLocalPosition;var savedAngle=pivot.localRotation;Time.timeScale=0;
        yield return new WaitForSecondsRealtime(.15f);yield return new WaitForEndOfFrame();Require(sprite.sprite==savedSprite&&anim.HandLocalPosition==savedHand&&pivot.localRotation==savedAngle,"pause freezes body/hand/blade and preparation together");Time.timeScale=1;
        int before=game.Player.MeleeContactsResolved;Pulse(GameAction.Dash);yield return null;StageValidationInput.BeginFrame();yield return new WaitForEndOfFrame();
        Require(!game.Player.IsGreatswordCharging&&anim.PoseName=="dash","dash immediately cancels preparation with native evasion pose");
        var cancel=Motion(.48f,"charge-dash-cancel");while(cancel.MoveNext())yield return cancel.Current;
        Require(game.Player.MeleeContactsResolved==before,"cancelled charge produces no late damage");Require(UnityEngine.Object.FindObjectsByType<NativeCombatEffect>().Length==0,"dash cancels charge glow completely");Flush();
        Equip("rust_sword");yield return new WaitForSeconds(.85f);StageValidationInput.SetHeld(GameAction.Guard,true);yield return new WaitForSeconds(.24f);
        StageValidationInput.SetHeld(GameAction.Guard,false);Pulse(GameAction.Attack);yield return null;StageValidationInput.BeginFrame();yield return new WaitForEndOfFrame();
        Require(game.Player.IsMeleeSwingActive&&anim.PoseName.StartsWith("sweep"),"guard release recovery never blocks immediate primary attack");yield return new WaitForSeconds(.75f);
        StageValidationInput.SetHeld(GameAction.Guard,true);yield return new WaitForSeconds(.20f);game.Inventory.AddWeapon(WeaponLootTable.Find("scrap_pistol"));game.Player.EquipWeapon(game.Inventory.SelectQuickSlot(1));yield return new WaitForEndOfFrame();
        Require(game.Player.EquippedWeapon.Family==WeaponFamily.Pistol&&anim.PoseName.StartsWith("aim-pistol"),"weapon swap removes stale guard and native blade posture");StageValidationInput.ReleaseAll();
        game.ReturnToMainMenu();yield return null;Require(UnityEngine.Object.FindObjectsByType<NativeCombatEffect>().Length==0&&UnityEngine.Object.FindObjectsByType<PlayerController>().Length==0,"menu clears player/preparation/action effects");
    }
}
