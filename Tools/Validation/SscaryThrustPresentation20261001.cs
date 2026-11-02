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
public static class SscaryThrustPresentation20261001
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
    static bool sawThrust,sawMobileThrust;
    static void Track(string phase)
    {
        var p=game.Player;var a=p.GetComponent<PlayerAnimationController>();var body=p.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
        foreach(var fx in UnityEngine.Object.FindObjectsByType<MeleeThrustEffect>())
            foreach(var streak in fx.GetComponentsInChildren<SpriteRenderer>())
                if(streak.sortingOrder>=body.sortingOrder)throw new Exception("moving thrust trail paints over player body "+phase);
        if(p.IsThrustActive)
        {
            if(!a.PoseName.StartsWith("thrust"))throw new Exception("real thrust still uses fallback "+phase);
            if(Vector2.Distance(p.transform.Find("HeldWeaponPivot").localPosition,a.HandLocalPosition)>.002f)throw new Exception("held grip detached from native hand "+phase);
            if(body.sprite.rect.width!=48||body.sprite.rect.height!=64||body.sprite.pivot!=new Vector2(24,22)||body.sprite.pixelsPerUnit!=32)throw new Exception("native body geometry "+phase);
            sawThrust=true;sawMobileThrust|=a.PoseName=="thrust-move";phaseSprites.Add(body.sprite);
        }
        telemetry.AppendLine(string.Join(",",sample,phase,p.AttackAnimationSequence,p.IsThrustActive,a.PoseName,a.FacingDirection,a.FrameIndex,body.sprite.name,
            p.AttackAnimationProgress.ToString("F4",CultureInfo.InvariantCulture),p.transform.position.x.ToString("F4",CultureInfo.InvariantCulture),p.transform.position.y.ToString("F4",CultureInfo.InvariantCulture),
            a.HandLocalPosition.x.ToString("F4",CultureInfo.InvariantCulture),a.HandLocalPosition.y.ToString("F4",CultureInfo.InvariantCulture)));
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
        folder = "F:/codex/LoglikeCap/Logs/Validation/2026-10-01-hero-thrust/presentation-" + DateTime.Now.ToString("HHmmss"); Directory.CreateDirectory(folder + "/frames");
        checks.Clear(); customSizes.Clear(); buffered.Clear(); sample = 0; recording.Clear(); recording.AppendLine("frame,realtime,phase"); telemetry.Clear();telemetry.AppendLine("frame,phase,sequence,thrust,pose,direction,key,sprite,progress,x,y,handX,handY");
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
        Debug.Log("[Sscary thrust presentation] PASS " + folder);
    }
    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;typeof(GameController).GetProperty("StageIntroUntil").SetValue(game,0f);
        foreach(var e in UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>())UnityEngine.Object.Destroy(e.gameObject);
        center=game.DungeonMap.GetRoomCenter(3);game.CameraFollow.FocusRoom(center,new Vector2(20,14),true);
        Resolution(1280,720);yield return null;yield return new WaitForEndOfFrame();
        Require(Screen.width==1280&&Screen.height==720,"recording is actual native 1280x720 screen, no camera rescale or interpolation");
        var verify=VerifyReadback();while(verify.MoveNext())yield return verify.Current;

        foreach(var direction in new[]{0,1,2,4,5,7})
        {
            Equip("worn_dagger");game.Player.Stats.RestoreMana(game.Player.MaxMana);
            Vector2 aim=new Vector2(Mathf.Sin(direction*Mathf.PI/4),Mathf.Cos(direction*Mathf.PI/4));StageValidationInput.SetAim(aim);
            yield return new WaitForSeconds(.8f);var enemy=Enemy(aim*1.25f);yield return null;Physics2D.SyncTransforms();float hp=enemy.Health;
            sawThrust=false;sawMobileThrust=false;phaseSprites.Clear();
            StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Guard,true);yield return null;StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Guard,false);
            var motion=Record(.55f,"dagger-thrust-"+direction,.09f);
            while(motion.MoveNext()){yield return motion.Current;StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Guard,false);}
            Require(sawThrust&&phaseSprites.Count>=5,"real RMB uses multiple native thrust poses in view "+direction+" samples="+phaseSprites.Count);
            Require(enemy.Health<hp,"real RMB contact damages target in view "+direction);
            Flush();
        }
        foreach(var id in new[]{"worn_dagger","rust_sword"})
        foreach(var moving in new[]{false,true})
        {
            Equip(id);game.Player.Stats.RestoreMana(game.Player.MaxMana);yield return new WaitForSeconds(.85f);
            // A frozen target directly in the movement lane is a real collision obstacle.
            // Keep movement reviews clear; stationary contact reviews retain a target in front.
            var enemy=Enemy(new Vector2(1.35f,moving?1.8f:0));yield return null;Physics2D.SyncTransforms();float hp=enemy.Health;
            sawThrust=false;sawMobileThrust=false;phaseSprites.Clear();
            StageValidationInput.BeginFrame();StageValidationInput.ReleaseAll();StageValidationInput.SetAim(Vector2.right);
            if(moving)StageValidationInput.SetHeld(GameAction.MoveRight,true);
            StageValidationInput.SetHeld(id=="worn_dagger"?GameAction.Guard:GameAction.Attack,true);
            yield return null;StageValidationInput.BeginFrame();if(id=="worn_dagger")StageValidationInput.SetHeld(GameAction.Guard,false);
            Vector2 before=game.Player.transform.position;
            var phase=id+(moving?"-moving":"-standing");var motion=Record(id=="worn_dagger"?.62f:1.55f,phase,.09f);
            while(motion.MoveNext())
            {
                yield return motion.Current;StageValidationInput.BeginFrame();
                if(id=="worn_dagger")StageValidationInput.SetHeld(GameAction.Guard,false);
            }
            StageValidationInput.ReleaseAll();Require(sawThrust&&phaseSprites.Count>=5,"actual "+phase+" contains keyed thrust/contact/recovery poses");
            if(moving){Require(sawMobileThrust,"moving thrust combines independent original gait "+id);Require(Vector2.Distance(before,game.Player.transform.position)>.9f,"thrust input keeps actual movement "+id);}
            else Require(enemy.Health<hp,"normal combo/RMB contact still damages target "+id);
            Flush();
        }
        // Dash cancels the body and blade together through the real GameAction route.
        Equip("worn_dagger");game.Player.Stats.RestoreMana(game.Player.MaxMana);yield return new WaitForSeconds(.9f);
        StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Guard,true);yield return null;
        StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Guard,false);
        double readyBy=Time.realtimeSinceStartupAsDouble+.35;
        while(!game.Player.IsThrustActive&&Time.realtimeSinceStartupAsDouble<readyBy){yield return null;StageValidationInput.BeginFrame();}
        Require(game.Player.IsThrustActive,"dash scenario starts an actual thrust");
        Pulse(GameAction.Dash);yield return null;StageValidationInput.BeginFrame();yield return new WaitForEndOfFrame();
        Require(!game.Player.IsThrustActive&&game.Player.GetComponent<PlayerAnimationController>().PoseName=="dash","dash immediately leaves thrust body/blade together");
        var dashRecord=Record(.45f,"thrust-dash-return",.07f);while(dashRecord.MoveNext()){yield return dashRecord.Current;StageValidationInput.BeginFrame();}Flush();
        Require(!game.Player.GetComponent<PlayerAnimationController>().PoseName.StartsWith("thrust"),"cancelled thrust does not replay after dash");
        // Pause through the existing menu method, not a custom animation-only pause.
        Equip("worn_dagger");game.Player.Stats.RestoreMana(game.Player.MaxMana);yield return new WaitForSeconds(.9f);
        StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Guard,true);yield return null;StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Guard,false);
        readyBy=Time.realtimeSinceStartupAsDouble+.35;
        while(!game.Player.IsThrustActive&&Time.realtimeSinceStartupAsDouble<readyBy){yield return null;StageValidationInput.BeginFrame();}
        yield return new WaitForSeconds(.035f);yield return new WaitForEndOfFrame();
        Require(game.Player.IsThrustActive,"pause scenario reaches the actual contact/recovery clock after dagger lunge windup");
        var anim=game.Player.GetComponent<PlayerAnimationController>();var frozen=game.Player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().sprite;var hand=anim.HandLocalPosition;int key=anim.FrameIndex;
        typeof(GameController).GetMethod("SetManualPause",Private).Invoke(game,new object[]{true});Time.timeScale=0;
        yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();
        Require(anim.FrameIndex==key&&anim.HandLocalPosition==hand&&game.Player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().sprite==frozen,"menu pause freezes authored pose/held grip without phase drift");
        typeof(GameController).GetMethod("SetManualPause",Private).Invoke(game,new object[]{false});Time.timeScale=1;yield return new WaitForSeconds(.4f);
        Require(!game.Player.IsThrustActive&&!anim.PoseName.StartsWith("thrust"),"resuming finishes recovery and restores native idle");
        ClearEffects();yield return new WaitForSeconds(.8f);
        Require(WeaponSecondaryParticles.ActiveCount==0,"all secondary fragments expire after attacks");
        Require(NativeCombatEffect.ActiveCount==0,"all attack/projectile native effects expire after attacks");
        StageValidationInput.ReleaseAll();game.ReturnToMainMenu();game.enabled=true;
    }
}
