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
public static class WeaponStandingRanged20261001
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
    static int observedEmission,emissions;static Vector2 previousHand;static float maxHandStep,emissionMana;
    static void Track(string phase)
    {
        var p=game.Player;var a=p.GetComponent<PlayerAnimationController>();var body=p.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();var held=p.GetComponent<HeldWeaponView>();
        var pivot=p.transform.Find("HeldWeaponPivot");var barrel=pivot.Find("HeldWeaponSprite").GetComponent<SpriteRenderer>();
        traveled+=Vector2.Distance(previousPosition,p.transform.position);previousPosition=p.transform.position;
        if(p.MotionState!=PlayerMotionState.Dash&&p.EquippedWeapon.AttackType==WeaponAttackType.Ranged)
        {
            if(!a.AuthoredWeaponRotationActive||!a.PoseName.StartsWith(p.IsRangedPresentationActive?"shoot-":"aim-"))throw new Exception("missing native ranged pose "+phase);
            if(Vector2.Distance(pivot.localPosition,a.HandLocalPosition)>.002f)throw new Exception("body/held grip detached "+phase);
            if(body.sprite.rect.size!=new Vector2(48,64)||body.sprite.pivot!=new Vector2(24,22)||body.sprite.pixelsPerUnit!=32)throw new Exception("ranged native body geometry "+phase);
            if(Mathf.Abs(Mathf.DeltaAngle(a.AuthoredWeaponRotation,held.DisplayedActionRotation))>.01f)throw new Exception("barrel/body release clocks disagree "+phase);
            if(barrel.sortingOrder>=body.sortingOrder)throw new Exception("concealed hand barrel crosses body "+phase);
            if(p.IsRangedPresentationActive)
            {
                phaseSprites.Add(body.sprite);phasePoses.Add(a.PoseName.Replace("-move",""));
                if(observedEmission!=p.RangedPresentationSequence)
                {
                    if(p.RangedPresentationProgress>.001f)throw new Exception("missed actual first emission frame "+phase+" "+p.RangedPresentationProgress);
                    observedEmission=p.RangedPresentationSequence;emissions++;emissionMana=Mathf.Min(emissionMana,p.Mana);
                    var origin=(Vector2)typeof(PlayerController).GetField("committedShotOrigin",Private).GetValue(p);
                    RangedWeaponArt.Landmarks landmarks;if(!RangedWeaponArt.TryGet(p.EquippedWeapon.Id,out landmarks))throw new Exception("missing muzzle art");
                    var local=(new Vector2(landmarks.Muzzle.x,barrel.sprite.rect.height-landmarks.Muzzle.y)-barrel.sprite.pivot)/barrel.sprite.pixelsPerUnit;
                    if(barrel.flipY)local.y=-local.y;
                    var visible=(Vector2)pivot.TransformPoint(local);
                    if(Vector2.Distance(origin,visible)>.002f)throw new Exception("actual emitted projectile/barrel detach "+phase+" "+Vector2.Distance(origin,visible));
                }
            }
            if(previousHand!=Vector2.zero)maxHandStep=Mathf.Max(maxHandStep,Vector2.Distance(previousHand,a.HandLocalPosition)*32);
            previousHand=a.HandLocalPosition;
        }
        var camera=Camera.main;var pixelOrigin=camera.WorldToScreenPoint(body.transform.TransformPoint(new Vector3(-24/32f,42/32f,0)));var across=camera.WorldToScreenPoint(body.transform.TransformPoint(new Vector3(-23/32f,42/32f,0)));var down=camera.WorldToScreenPoint(body.transform.TransformPoint(new Vector3(-24/32f,41/32f,0)));
        telemetry.AppendLine(string.Join(",",sample,phase,p.RangedPresentationSequence,p.IsRangedPresentationActive,a.PoseName,a.FacingDirection,a.FrameIndex,body.sprite.name,
            p.RangedPresentationProgress.ToString("F4",CultureInfo.InvariantCulture),p.transform.position.x.ToString("F4",CultureInfo.InvariantCulture),p.transform.position.y.ToString("F4",CultureInfo.InvariantCulture),
            a.HandLocalPosition.x.ToString("F4",CultureInfo.InvariantCulture),a.HandLocalPosition.y.ToString("F4",CultureInfo.InvariantCulture),a.HandBehindBody,p.ActiveMeleeMotion,p.MeleeContactsResolved,held.DisplayedActionRotation.ToString("F4",CultureInfo.InvariantCulture),AssetDatabase.GetAssetPath(body.sprite),pixelOrigin.x.ToString("F6",CultureInfo.InvariantCulture),pixelOrigin.y.ToString("F6",CultureInfo.InvariantCulture),(across.x-pixelOrigin.x).ToString("F6",CultureInfo.InvariantCulture),(pixelOrigin.y-down.y).ToString("F6",CultureInfo.InvariantCulture)));
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
        folder = "F:/codex/LoglikeCap/Logs/Validation/2026-10-01-weapon-carry/ranged-" + DateTime.Now.ToString("HHmmss"); Directory.CreateDirectory(folder + "/frames");
        checks.Clear(); customSizes.Clear(); buffered.Clear(); sample = 0; recording.Clear(); recording.AppendLine("frame,realtime,phase"); telemetry.Clear();telemetry.AppendLine("frame,phase,sequence,melee,pose,direction,key,sprite,progress,x,y,handX,handY,behind,motion,contacts,angle,bodyAsset,originSX,originSY,scaleX,scaleY");
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
            view.Repaint();EditorApplication.QueuePlayerLoopUpdate();yield return new WaitForEndOfFrame(); double time = Time.realtimeSinceStartupAsDouble; Track(phase);
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
        Debug.Log("[Sscary ranged presentation] PASS " + folder);
    }
    static IEnumerator Motion(float seconds,string phase,Action<float> input=null)
    {
        double start=Time.timeAsDouble;var motion=Record(seconds,phase,.11f);
        while(motion.MoveNext()){yield return motion.Current;StageValidationInput.BeginFrame();input?.Invoke((float)(Time.timeAsDouble-start));}
    }
    static void Fresh(){ResetObservations();observedEmission=game.Player.RangedPresentationSequence;emissions=0;emissionMana=float.PositiveInfinity;previousHand=Vector2.zero;maxHandStep=0;}
    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;typeof(GameController).GetProperty("StageIntroUntil").SetValue(game,0f);
        foreach(var e in UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>())UnityEngine.Object.Destroy(e.gameObject);
        center=game.DungeonMap.GetRoomCenter(3);game.CameraFollow.FocusRoom(center,new Vector2(20,14),true);
        Resolution(1280,720);yield return null;yield return new WaitForEndOfFrame();
        Require(Screen.width==1280&&Screen.height==720,"native 1280x720 composited screen");var verify=VerifyReadback();while(verify.MoveNext())yield return verify.Current;
        foreach(var id in new[]{"hunter_bow","hand_crossbow","scrap_pistol","storm_rifle","scrap_shotgun","dragon_cannon"})
        foreach(var direction in new[]{0,1,2,3,4,5,6,7})
        foreach(var special in new[]{false,true})
        {
            Equip(id);yield return new WaitForSeconds(.85f);var p=game.Player;var anim=p.GetComponent<PlayerAnimationController>();
            var aim=new Vector2(Mathf.Sin(direction*Mathf.PI/4),Mathf.Cos(direction*Mathf.PI/4));StageValidationInput.SetAim(aim);yield return new WaitForEndOfFrame();Fresh();
            p.Stats.RestoreMana(p.MaxMana);int ammo=p.ActiveWeaponState.Ammo,seq=p.AttackAnimationSequence;float mana=p.Mana;bool started=false,stopped=false;
            var phase=id+"-"+(special?"special":"normal")+"-view-"+direction;
            var motion=Motion(.48f,phase,t=>{if(t>.025f&&!started){started=true;StageValidationInput.SetHeld(special?GameAction.Guard:GameAction.Attack,true);}if(started&&!stopped&&p.AttackAnimationSequence>seq){stopped=true;StageValidationInput.SetHeld(special?GameAction.Guard:GameAction.Attack,false);}});
            while(motion.MoveNext())yield return motion.Current;
            Require(p.AttackAnimationSequence==seq+1,"one actual GameAction initiates action "+phase);
            Require(emissions==(special&&p.EquippedWeapon.Family==WeaponFamily.Pistol?3:1),"every actual emission gets its own body/barrel clock "+phase+" emissions="+emissions);
            Require(phaseSprites.Count>=3,"real native release/recovery pixels "+phase+" images="+phaseSprites.Count);
            Require(p.ActiveWeaponState.Ammo==ammo-(special?0:1),"unchanged owned ammo consumption "+phase);
            if(special)Require(Mathf.Abs(emissionMana-(mana-p.EquippedWeapon.SpecialManaCost))<.01f,"actual special mana spent once "+phase);
            Require(!p.IsRangedPresentationActive&&anim.PoseName.StartsWith("aim-"),"recovery returns to family ready stance "+phase);
            Require(maxHandStep<=5.5f,"bounded actual socket movement "+phase+" pixels="+maxHandStep);Flush();
        }
        foreach(var id in new[]{"hunter_bow","scrap_pistol","storm_rifle","scrap_shotgun","dragon_cannon"})
        {
            Equip(id);yield return new WaitForSeconds(.85f);StageValidationInput.SetAim(Vector2.right);Fresh();bool started=false;
            var motion=Motion(.72f,id+"-strafe-backpedal",t=>{if(!started){started=true;StageValidationInput.SetHeld(GameAction.Attack,true);StageValidationInput.SetHeld(GameAction.MoveRight,true);}if(t>.3f){StageValidationInput.SetHeld(GameAction.MoveRight,false);StageValidationInput.SetHeld(GameAction.MoveLeft,true);}if(t>.62f)StageValidationInput.ReleaseAll();});
            while(motion.MoveNext())yield return motion.Current;
            Require(traveled>1.5f&&phaseSprites.Count>=5,"independent gait while firing/backpedaling "+id);StageValidationInput.ReleaseAll();Flush();
        }
        Equip("scrap_pistol");yield return new WaitForSeconds(.85f);bool fired=false,paused=false,resumed=false;Sprite pauseSprite=null;Vector2 pauseHand=Vector2.zero;Quaternion pauseAngle=Quaternion.identity;float pauseProgress=0;double pauseStart=0;
        var record=Record(.52f,"pistol-pause",.1f);
        while(record.MoveNext())
        {
            yield return record.Current;StageValidationInput.BeginFrame();
            if(!fired){fired=true;StageValidationInput.SetHeld(GameAction.Attack,true);}
            else if(!paused&&game.Player.IsRangedPresentationActive)
            {
                StageValidationInput.SetHeld(GameAction.Attack,false);paused=true;var anim=game.Player.GetComponent<PlayerAnimationController>();
                pauseSprite=game.Player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().sprite;pauseHand=anim.HandLocalPosition;pauseAngle=game.Player.transform.Find("HeldWeaponPivot").localRotation;pauseProgress=game.Player.RangedPresentationProgress;
                pauseStart=Time.realtimeSinceStartupAsDouble;Time.timeScale=0;
            }
            else if(paused&&!resumed&&Time.realtimeSinceStartupAsDouble-pauseStart>.15)
            {
                var anim=game.Player.GetComponent<PlayerAnimationController>();
                Require(pauseSprite==game.Player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().sprite&&pauseHand==anim.HandLocalPosition&&pauseAngle==game.Player.transform.Find("HeldWeaponPivot").localRotation&&pauseProgress==game.Player.RangedPresentationProgress,"pause freezes body/socket/barrel/recoil clock");Time.timeScale=1;resumed=true;
            }
        }
        Require(resumed,"pause scenario ran");Flush();
        Equip("scrap_pistol");yield return new WaitForSeconds(.85f);StageValidationInput.SetHeld(GameAction.Guard,true);yield return null;StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Guard,false);Pulse(GameAction.Dash);yield return null;StageValidationInput.BeginFrame();yield return new WaitForEndOfFrame();
        Require(game.Player.MotionState==PlayerMotionState.Dash&&!game.Player.IsRangedPresentationActive,"dash cancels burst recovery immediately");yield return new WaitForSeconds(.35f);
        int cancelled=game.Player.RangedPresentationSequence;yield return new WaitForSeconds(.2f);Require(game.Player.RangedPresentationSequence==cancelled,"no delayed burst after dash cancellation");
        Equip("scrap_pistol");yield return new WaitForSeconds(.85f);StageValidationInput.SetHeld(GameAction.Attack,true);yield return null;StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Attack,false);game.Inventory.AddWeapon(WeaponLootTable.Find("rust_sword"));game.Player.EquipWeapon(game.Inventory.SelectQuickSlot(1));yield return new WaitForEndOfFrame();
        Require(!game.Player.IsRangedPresentationActive&&!game.Player.GetComponent<PlayerAnimationController>().PoseName.StartsWith("shoot-"),"swap cancels firearm body/socket state");StageValidationInput.ReleaseAll();
        game.ReturnToMainMenu();yield return null;Require(UnityEngine.Object.FindObjectsByType<PlayerController>().Length==0&&UnityEngine.Object.FindObjectsByType<PlayerProjectile>().Length==0,"menu clears body and projectiles");
    }
}
