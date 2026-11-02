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
using Newtonsoft.Json.Linq;

// Staged visual review on the real first-region map. Items/targets supplied for review, not a normal run.
public static class MeleeRangeProbe20261001
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
    static readonly JArray measurements=new JArray();
    static void Track(string phase){}
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
        folder = "F:/codex/LoglikeCap/Logs/Validation/2026-10-01-melee-fx-range/probe-" + DateTime.Now.ToString("HHmmss"); Directory.CreateDirectory(folder + "/frames");
        measurements.Clear(); checks.Clear(); customSizes.Clear(); buffered.Clear(); sample = 0; recording.Clear(); recording.AppendLine("frame,realtime,phase"); telemetry.Clear();telemetry.AppendLine("frame,phase,sequence,melee,pose,direction,key,sprite,progress,x,y,handX,handY,behind,motion,contacts,angle,bodyAsset,originSX,originSY,scaleX,scaleY,fxClip,fxKey,editorForeground,gameFocused,delta");
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
            if(!UnityEditorInternal.InternalEditorUtility.isApplicationActive){var focus=autoTick.Assembly.GetType("Unity.Pipeline.Editor.Commands.FocusEditorCommand").GetMethod("FocusEditor",All);focus.Invoke(null,null);view.Focus();}view.Repaint();EditorApplication.QueuePlayerLoopUpdate();yield return new WaitForEndOfFrame(); double time = Time.realtimeSinceStartupAsDouble; Track(phase);
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
            if(f.Pixels==null)continue;var texture = new Texture2D(f.Width, f.Height, TextureFormat.RGBA32, false); texture.SetPixels32(f.Pixels); texture.Apply();
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
            catch (Exception error) { File.WriteAllText(folder+"/partial-recording.csv",recording.ToString(),new UTF8Encoding(false));File.WriteAllText(folder+"/partial-body-telemetry.csv",telemetry.ToString(),new UTF8Encoding(false));try{Flush();}catch{} RestoreView(); Time.timeScale = 1; File.WriteAllText(folder + "/result.txt", "FAIL " + error + "\n" + string.Join("\n", checks)); Debug.LogException(error); yield break; }
            if (!next) break; yield return value;
        }
        RestoreView(); File.WriteAllText(folder + "/result.txt", (AllMatched()?"PASS ":"FAIL visual/actual outer reach mismatch; ")+"actual input/physics contact ruler completed. Supplied 0.04-unit hurt proxies and paused contact stills; not normal NPC/campaign/motion/performance.\n"+string.Join("\n",checks));
        Debug.Log("[Melee range probe] " + (AllMatched()?"PASS ":"FAIL visible/actual range mismatch ") + folder);
    }
    static bool AllMatched(){foreach(JObject row in measurements)if(!row.Value<bool>("visibleMatchesOuterRange"))return false;return true;}
    static IEnumerator Advance(float seconds)
    {
        var stop=Time.timeAsDouble+seconds;
        while(Time.timeAsDouble<stop){yield return null;StageValidationInput.BeginFrame();}
    }
    static IEnumerator Ready()
    {
        var deadline=Time.realtimeSinceStartupAsDouble+5;
        while((float)typeof(PlayerController).GetField("attackTimer",Private).GetValue(game.Player)>0)
        {if(Time.realtimeSinceStartupAsDouble>deadline)throw new Exception("attack readiness deadline");yield return null;StageValidationInput.BeginFrame();}
    }
    static EnemyController Needle(Vector2 point)
    {
        var enemy=Enemy(point-(Vector2)game.Player.transform.position);
        var collider=(BoxCollider2D)enemy.DamageCollider;
        collider.offset=Vector2.zero;collider.size=Vector2.one*.04f;
        return enemy;
    }
    static SpriteRenderer CurrentArc()
    {
        foreach(var fx in UnityEngine.Object.FindObjectsByType<MeleeSlashEffect>())if(fx.FollowsMeleeAction)return fx.GetComponent<SpriteRenderer>();
        foreach(var fx in UnityEngine.Object.FindObjectsByType<NativeCombatEffect>())if(fx.FollowsMeleeAction)return fx.GetComponent<SpriteRenderer>();
        throw new Exception("The real active contact arc is missing");
    }
    static Vector2 VisibleSpan(SpriteRenderer view,Vector2 axis,Vector2 actor,bool opaque)
    {
        var sprite=view.sprite;var path=AssetDatabase.GetAssetPath(sprite);
        var source=new Texture2D(2,2,TextureFormat.RGBA32,false);source.LoadImage(File.ReadAllBytes(path));
        var pixels=source.GetPixels32();float min=float.PositiveInfinity,max=float.NegativeInfinity;
        for(int y=0;y<source.height;y++)for(int x=0;x<source.width;x++)
        {
            var alpha=pixels[y*source.width+x].a;if(opaque?alpha!=255:alpha==0)continue;
            // Project each original pixel quad, not padded Sprite.bounds, onto
            // the committed attack axis. No fitted camera offset or art edits.
            foreach(var corner in new[]{new Vector2(x,y),new Vector2(x+1,y),new Vector2(x,y+1),new Vector2(x+1,y+1)})
            {
                var local=(corner-sprite.pivot)/sprite.pixelsPerUnit;
                if(view.flipX)local.x=-local.x;if(view.flipY)local.y=-local.y;
                float d=Vector2.Dot((Vector2)view.transform.TransformPoint(local)-actor,axis);min=Mathf.Min(min,d);max=Mathf.Max(max,d);
            }
        }
        UnityEngine.Object.DestroyImmediate(source);return new Vector2(min,max);
    }
    static IEnumerator Case(string id,int direction,int mode)
    {
        Equip(id);var p=game.Player;var ready=Ready();while(ready.MoveNext())yield return ready.Current;
        var aim=new Vector2(Mathf.Sin(direction*Mathf.PI/4),Mathf.Cos(direction*Mathf.PI/4));StageValidationInput.SetAim(aim);
        var profile=MeleeComboCatalog.Get(p.EquippedWeapon);var step=mode<0?profile.HeavyStep:profile.LightSteps[mode];
        var angle=Mathf.Atan2(aim.y,aim.x)*Mathf.Rad2Deg+step.AngleOffset;
        var axis=new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad));
        var actor=(Vector2)p.transform.position;float previousOuter=step.Reach+step.HitboxSize.x*.5f;
        var hand=p.GetComponent<PlayerAnimationController>().MeleeHandPosition(aim,step.Motion,mode,mode<0,.42f);
        Require(MeleeSweepGeometry.TryEnvelope(step,p.EquippedWeapon.Family,hand,axis,out var damageCenter,out var damageSize),"active bound-cut descriptor "+id);
        float outer=Vector2.Dot(damageCenter,axis)+damageSize.x*.5f;
        var inside=Needle(actor+axis*(outer-.12f));var outside=Needle(actor+axis*(outer+.12f));yield return null;Physics2D.SyncTransforms();
        Require(Physics2D.Linecast(actor,inside.DamageCollider.bounds.center,1<<GameLayers.Ground).collider==null,"real range ray is not occluded "+id+"/"+direction+"/"+mode);
        for(int preparation=0;preparation<mode;preparation++)
        {
            Pulse(GameAction.Attack);yield return null;StageValidationInput.BeginFrame();
            ready=Ready();while(ready.MoveNext())yield return ready.Current;
        }
        int contacts=p.MeleeContactsResolved,sequence=p.AttackAnimationSequence;float before=inside.Health,outsideBefore=outside.Health;
        if(mode<0)
        {
            StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Guard,true);
            var charge=Advance(.75f);while(charge.MoveNext())yield return charge.Current;
            Require(p.IsGreatswordCharging,"native full charge "+id);StageValidationInput.SetHeld(GameAction.Guard,false);
        }
        else Pulse(GameAction.Attack);
        var deadline=Time.realtimeSinceStartupAsDouble+3;
        while(p.MeleeContactsResolved==contacts)
        {if(Time.realtimeSinceStartupAsDouble>deadline)throw new Exception("real contact input deadline "+id);yield return null;StageValidationInput.BeginFrame();}
        yield return new WaitForEndOfFrame();Time.timeScale=0;
        Require(p.MeleeContactsResolved==contacts+1&&p.AttackAnimationSequence==sequence+1,"one real contact "+id+"/"+direction+"/"+mode);
        Require(inside.Health<before&&outside.Health==outsideBefore,"actual inner/outer tiny hurt proxy damage boundary "+id+"/"+direction+"/"+mode);
        var arc=CurrentArc();var visible=VisibleSpan(arc,axis,p.transform.position,false);var opaque=VisibleSpan(arc,axis,p.transform.position,true);
        string phase=id+"-view-"+direction+"-mode-"+mode;Capture(phase+"-contact");
        var record=new JObject{{"phase",phase},{"weapon",id},{"direction",direction},{"mode",mode},{"motion",step.Motion.ToString()},
            {"reach",step.Reach},{"previousOuterRay",previousOuter},{"boxWidth",damageSize.x},{"boxHeight",damageSize.y},{"actualOuterRay",outer},
            {"visibleForwardMin",visible.x},{"visibleForwardMax",visible.y},{"opaqueForwardMax",opaque.y},{"shortfall",outer-visible.y},
            {"visibleMatchesOuterRange",Mathf.Abs(outer-visible.y)<=.15f},{"insideDamage",before-inside.Health},{"outsideDamage",outsideBefore-outside.Health},
            {"source",AssetDatabase.GetAssetPath(arc.sprite)},{"sourcePivot",new JArray(arc.sprite.pivot.x,arc.sprite.pivot.y)},
            {"sourcePPU",arc.sprite.pixelsPerUnit},{"scale",new JArray(arc.transform.lossyScale.x,arc.transform.lossyScale.y)},
            {"actor",new JArray(p.transform.position.x,p.transform.position.y)},{"grip",new JArray(p.GetComponent<HeldWeaponView>().BladePoint(0).x,p.GetComponent<HeldWeaponView>().BladePoint(0).y)},
            {"fxCenter",new JArray(arc.transform.position.x,arc.transform.position.y)},{"progress",p.AttackAnimationProgress},
            {"scope","Supplied 0.04-unit damage probes, real input/physics and paused original contact still; not normal NPC/campaign/motion/performance."}};
        measurements.Add(record);File.WriteAllText(folder+"/range-measurements.json",measurements.ToString(),new UTF8Encoding(false));
        Time.timeScale=1;StageValidationInput.ReleaseAll();var settle=Advance(.5f);while(settle.MoveNext())yield return settle.Current;
    }
    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;typeof(GameController).GetProperty("StageIntroUntil").SetValue(game,0f);
        foreach(var e in UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>())UnityEngine.Object.Destroy(e.gameObject);
        center=game.DungeonMap.GetRoomCenter(3);game.CameraFollow.FocusRoom(center,new Vector2(20,14),true);
        Resolution(1280,720);yield return null;yield return new WaitForEndOfFrame();
        Require(Screen.width==1280&&Screen.height==720,"native 1280x720 geometry stills");
        foreach(var weapon in WeaponLootTable.All)if(weapon.AttackType==WeaponAttackType.Melee)
        {
            var steps=MeleeComboCatalog.Get(weapon).LightSteps;
            for(int stepIndex=0;stepIndex<steps.Length;stepIndex++)if(steps[stepIndex].Motion==MeleeAttackMotion.Sweep||
                steps[stepIndex].Motion==MeleeAttackMotion.Cleave&&weapon.Family==WeaponFamily.Greatsword)
            foreach(var direction in new[]{0,1,2,3,4,5,6,7})
            {var test=Case(weapon.Id,direction,stepIndex);while(test.MoveNext())yield return test.Current;}
        }
        foreach(var id in new[]{"iron_greatsword","dragon_slayer","blood_reaver"})foreach(var direction in new[]{0,1,2,3,4,5,6,7})
        {var test=Case(id,direction,-1);while(test.MoveNext())yield return test.Current;}
        game.ReturnToMainMenu();yield return null;Require(measurements.Count==304,"every supported normal cut and three greatsword charged cleaves measured in all eight directions");
    }
}
