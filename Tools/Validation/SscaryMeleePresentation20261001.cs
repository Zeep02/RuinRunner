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
public static class SscaryMeleePresentation20261001
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
    static void Track(string phase)
    {
        var p=game.Player;var a=p.GetComponent<PlayerAnimationController>();var body=p.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
        traveled+=Vector2.Distance(previousPosition,p.transform.position);previousPosition=p.transform.position;
        foreach(var fx in UnityEngine.Object.FindObjectsByType<MeleeSlashEffect>())if(fx.GetComponent<SpriteRenderer>().sortingOrder>=body.sortingOrder)throw new Exception("large arc hides the body "+phase);
        foreach(var fx in UnityEngine.Object.FindObjectsByType<ExternalCombatEffect>())if(fx.GetComponent<SpriteRenderer>().sortingOrder>=body.sortingOrder)throw new Exception("finisher hides the body "+phase);
        foreach(var fx in UnityEngine.Object.FindObjectsByType<NativeCombatEffect>())if(fx.IsBelowActor&&fx.GetComponent<SpriteRenderer>().sortingOrder>=body.sortingOrder)throw new Exception("ground impact paints above its moving actor "+phase);
        if(p.IsMeleeSwingActive)
        {
            string expected=p.IsChargedCleaveActive?"charge-cleave":p.IsThrustActive?"thrust":p.ActiveMeleeMotion==MeleeAttackMotion.Cleave||p.ActiveMeleeMotion==MeleeAttackMotion.Impact?"cleave":p.EquippedWeapon.Family==WeaponFamily.Dagger?(p.ActiveComboAnimationStep&1)==1?"dagger-backcut":"dagger-cut":(p.ActiveComboAnimationStep&1)==1?"reverse-sweep":"sweep";
            if(!a.PoseName.StartsWith(expected))throw new Exception("active melee uses missing/wrong body clip "+phase+" expected="+expected+" observed="+a.PoseName);
            if(Vector2.Distance(p.transform.Find("HeldWeaponPivot").localPosition,a.HandLocalPosition)>.002f)throw new Exception("blade grip detached from exact keyed hand "+phase);
            if(a.HandBehindBody&&p.transform.Find("HeldWeaponPivot/HeldWeaponSprite").GetComponent<SpriteRenderer>().sortingOrder>=body.sortingOrder)throw new Exception("occluded hand blade paints across face/pack "+phase);
            if(body.sprite.rect.size!=new Vector2(48,64)||body.sprite.pivot!=new Vector2(24,22)||body.sprite.pixelsPerUnit!=32)throw new Exception("native body geometry "+phase);
            sawMelee=true;sawMobileMelee|=a.PoseName.EndsWith("-move");phaseSprites.Add(body.sprite);phasePoses.Add(expected);
        }
        telemetry.AppendLine(string.Join(",",sample,phase,p.AttackAnimationSequence,p.IsMeleeSwingActive,a.PoseName,a.FacingDirection,a.FrameIndex,body.sprite.name,
            p.AttackAnimationProgress.ToString("F4",CultureInfo.InvariantCulture),p.transform.position.x.ToString("F4",CultureInfo.InvariantCulture),p.transform.position.y.ToString("F4",CultureInfo.InvariantCulture),
            a.HandLocalPosition.x.ToString("F4",CultureInfo.InvariantCulture),a.HandLocalPosition.y.ToString("F4",CultureInfo.InvariantCulture),a.HandBehindBody,p.ActiveMeleeMotion,p.MeleeContactsResolved));
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
        folder = "F:/codex/LoglikeCap/Logs/Validation/2026-10-01-hero-melee/presentation-" + DateTime.Now.ToString("HHmmss"); Directory.CreateDirectory(folder + "/frames");
        checks.Clear(); customSizes.Clear(); buffered.Clear(); sample = 0; recording.Clear(); recording.AppendLine("frame,realtime,phase"); telemetry.Clear();telemetry.AppendLine("frame,phase,sequence,melee,pose,direction,key,sprite,progress,x,y,handX,handY,behind,motion,contacts");
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
        Debug.Log("[Sscary melee presentation] PASS " + folder);
    }
    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;typeof(GameController).GetProperty("StageIntroUntil").SetValue(game,0f);
        foreach(var e in UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>())UnityEngine.Object.Destroy(e.gameObject);
        center=game.DungeonMap.GetRoomCenter(3);game.CameraFollow.FocusRoom(center,new Vector2(20,14),true);
        Resolution(1280,720);yield return null;yield return new WaitForEndOfFrame();
        Require(Screen.width==1280&&Screen.height==720,"native 1280x720 composited screen, fixed camera policy");
        var verify=VerifyReadback();while(verify.MoveNext())yield return verify.Current;
        foreach(var direction in new[]{0,1,2,4,5,7})
        foreach(var heavy in new[]{false,true})
        {
            Equip(heavy?"iron_greatsword":"rust_sword");game.Player.Stats.RestoreMana(game.Player.MaxMana);
            var aim=new Vector2(Mathf.Sin(direction*Mathf.PI/4),Mathf.Cos(direction*Mathf.PI/4));StageValidationInput.SetAim(aim);
            yield return new WaitForSeconds(.85f);var enemy=Enemy(aim*1.3f);yield return null;Physics2D.SyncTransforms();float hp=enemy.Health;
            ResetObservations();
            if(heavy){StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Guard,true);yield return new WaitForSeconds(.12f);StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Guard,false);}
            else Pulse(GameAction.Attack);
            yield return null;StageValidationInput.BeginFrame();
            var motion=Record(heavy?.60f:.42f,(heavy?"cleave":"sweep")+"-view-"+direction,.14f);
            while(motion.MoveNext()){yield return motion.Current;StageValidationInput.BeginFrame();}
            Require(sawMelee&&phaseSprites.Count>=5,"multiple true body poses in "+(heavy?"cleave":"sweep")+" view "+direction+" observed="+phaseSprites.Count);
            Require(enemy.Health<hp,"real contact damages target in "+(heavy?"cleave":"sweep")+" view "+direction);Flush();
        }
        foreach(var id in new[]{"rust_sword","worn_dagger","training_katana","iron_greatsword","iron_mace"})
        foreach(var moving in new[]{false,true})
        {
            Equip(id);game.Player.Stats.RestoreMana(game.Player.MaxMana);yield return new WaitForSeconds(.9f);
            var enemy=Enemy(new Vector2(1.35f,moving?1.8f:0));yield return null;Physics2D.SyncTransforms();float hp=enemy.Health;
            ResetObservations();StageValidationInput.BeginFrame();StageValidationInput.SetAim(Vector2.right);
            if(moving)StageValidationInput.SetHeld(GameAction.MoveRight,true);StageValidationInput.SetHeld(GameAction.Attack,true);
            yield return null;StageValidationInput.BeginFrame();var before=(Vector2)game.Player.transform.position;
            var phase=id+(moving?"-moving":"-standing");var motion=Record(1.85f,phase,.13f);double moveStart=Time.realtimeSinceStartupAsDouble;
            while(motion.MoveNext())
            {
                yield return motion.Current;StageValidationInput.BeginFrame();
                // Return along the same clear lane so every combo remains in the actual viewport.
                if(moving&&Time.realtimeSinceStartupAsDouble-moveStart>.8){StageValidationInput.SetHeld(GameAction.MoveRight,false);StageValidationInput.SetHeld(GameAction.MoveLeft,true);}
            }
            StageValidationInput.ReleaseAll();Require(sawMelee&&phaseSprites.Count>=8,"actual multiple keyed combo poses "+phase+" observed="+phaseSprites.Count);
            Require(phasePoses.Contains(id=="worn_dagger"?"dagger-cut":"sweep")&&phasePoses.Contains(id=="worn_dagger"?"dagger-backcut":"reverse-sweep")||id=="iron_greatsword"&&phasePoses.Contains("cleave"),"forward/reverse or two-hand finisher body clips "+phase);
            if(id=="rust_sword"||id=="worn_dagger")Require(phasePoses.Contains("thrust"),"combo still connects to previously authored thrust "+phase);
            if(id=="iron_greatsword"||id=="iron_mace")Require(phasePoses.Contains("cleave"),"real downward finisher "+phase);
            if(moving){Require(sawMobileMelee,"attack uses independent gait "+id);Require(traveled>3f,"continuous attack movement along real return path "+id+" distance="+traveled);}
            else Require(enemy.Health<hp,"normal combo damage remains functional "+id);Flush();
        }
        Equip("rust_sword");yield return new WaitForSeconds(.9f);var untouched=Enemy(Vector2.right*1.3f);yield return null;Physics2D.SyncTransforms();float originalHp=untouched.Health;
        Pulse(GameAction.Attack);yield return null;StageValidationInput.BeginFrame();yield return new WaitForEndOfFrame();
        Require(game.Player.IsMeleeSwingActive,"cancel scenario begins a real slash before contact");int contacts=game.Player.MeleeContactsResolved;
        Pulse(GameAction.Dash);yield return null;StageValidationInput.BeginFrame();yield return new WaitForEndOfFrame();
        Require(!game.Player.IsMeleeSwingActive&&game.Player.GetComponent<PlayerAnimationController>().PoseName=="dash","dash cancels body and committed blade together");
        var dashRecord=Record(.45f,"slash-dash-return",.07f);while(dashRecord.MoveNext()){yield return dashRecord.Current;StageValidationInput.BeginFrame();}Flush();
        Require(untouched.Health==originalHp&&game.Player.MeleeContactsResolved==contacts,"cancelled pre-contact slash never deals delayed damage");
        Equip("iron_greatsword");game.Player.Stats.RestoreMana(game.Player.MaxMana);yield return new WaitForSeconds(.9f);
        StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Guard,true);yield return new WaitForSeconds(.12f);StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Guard,false);
        yield return new WaitForSeconds(.09f);yield return new WaitForEndOfFrame();Require(game.Player.IsMeleeSwingActive,"pause scenario starts actual cleave");
        var anim=game.Player.GetComponent<PlayerAnimationController>();var renderer=game.Player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();var frozen=renderer.sprite;var hand=anim.HandLocalPosition;int key=anim.FrameIndex;
        typeof(GameController).GetMethod("SetManualPause",Private).Invoke(game,new object[]{true});Time.timeScale=0;yield return new WaitForSecondsRealtime(.25f);yield return new WaitForEndOfFrame();
        Require(anim.FrameIndex==key&&anim.HandLocalPosition==hand&&renderer.sprite==frozen,"pause freezes actual keyed body/hand/weapon");
        typeof(GameController).GetMethod("SetManualPause",Private).Invoke(game,new object[]{false});Time.timeScale=1;yield return new WaitForSeconds(.6f);
        Require(!game.Player.IsMeleeSwingActive&&anim.PoseName=="idle"&&!anim.HandBehindBody,"recovery returns to breathing original idle/socket/occlusion");
        Pulse(GameAction.Attack);yield return null;StageValidationInput.BeginFrame();game.Inventory.AddWeapon(WeaponLootTable.Find("rust_sword"));game.Player.EquipWeapon(WeaponLootTable.Find("rust_sword"));yield return new WaitForEndOfFrame();
        Require(!game.Player.IsMeleeSwingActive&&!anim.PoseName.StartsWith("cleave"),"weapon change drops old combat pose immediately");
        ClearEffects();yield return new WaitForSeconds(.8f);Require(WeaponSecondaryParticles.ActiveCount==0&&NativeCombatEffect.ActiveCount==0,"native attack aftermath cleans up");
        StageValidationInput.ReleaseAll();game.ReturnToMainMenu();game.enabled=true;
    }
}
