using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using RuinRunner;

// Supplied state / actual GameAction functional regression. Manual item callbacks are
// explicitly identified. This is not normal campaign, motion smoothness or audio evidence.
public static class CombatExpansionRegression20261004
{
    const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance|BindingFlags.FlattenHierarchy;
    static readonly Dictionary<GameObject,bool> roots=new Dictionary<GameObject,bool>();
    static readonly Dictionary<string,GameAction[]> inputs=new Dictionary<string,GameAction[]>();
    static readonly List<string> checks=new List<string>();
    static readonly List<CombatGrowthEvent> events=new List<CombatGrowthEvent>();
    static Scene originalScene,scene;static PlayerController player;static EnemyController target,other;
    static InventorySystem bag;static CameraFollow camera;static GameSettingsService settings,oldAudio;
    static bool paused,running,oldEnabled,oldInitialized,oldBackground;static float oldScale;static int oldRate;
    static Vector2 oldAim;static UnityEngine.Random.State oldRandom;static Type autoTick;static bool oldTick;static int oldInterval;
    static EditorWindow oldFocus;static string folder;static double began;
    static FieldInfo Input(string name)=>typeof(StageValidationInput).GetField(name,All);
    static T Read<T>(object instance,string name)=>(T)instance.GetType().GetField(name,All).GetValue(instance);
    static void Set(object instance,string name,object value)=>instance.GetType().GetField(name,All).SetValue(instance,value);
    static void Check(bool ok,string label){checks.Add((ok?"PASS ":"FAIL ")+label);if(!ok)throw new Exception(label);}
    static IEnumerator Frame(){yield return null;StageValidationInput.BeginFrame();}
    static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return Frame();}
    static IEnumerator Press(GameAction action,float hold=.035f)
    {StageValidationInput.SetHeld(action,true);yield return Wait(hold);StageValidationInput.SetHeld(action,false);yield return Frame();}
    static void Remove(UnityEngine.Object obj){if(obj is Component c)c.gameObject.SetActive(false);if(obj!=null)UnityEngine.Object.Destroy(obj is Component cmp?cmp.gameObject:obj);}
    static EnemyController Target(Vector2 point)
    {
        var e=EnemyController.Create(EnemyType.Grunt,point,player,()=>true,_=>{},null);
        e.ApplyBalance(new EnemyBalanceData(EnemyType.Grunt,10000,0,0,0,0));e.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;
        return e;
    }
    static void Fresh(string id,string accessory=null)
    {
        StageValidationInput.ReleaseAll();StageValidationInput.BeginFrame();Remove(player);Remove(target);Remove(other);other=null;
        foreach(var shot in UnityEngine.Object.FindObjectsByType<PlayerProjectile>())Remove(shot);
        paused=false;events.Clear();bag=new InventorySystem();bag.AddWeapon(WeaponLootTable.Find(id));if(accessory!=null)bag.AddAccessory(AccessoryLootTable.Find(accessory));
        player=PlayerController.Create(Vector2.zero,()=>paused,()=>false,()=>{},camera,bag,settings);player.IsTrainingCombat=true;
        player.ConfigureCombatGrowth(_=>false,e=>events.Add(e));player.EquipWeapon(bag.EquippedWeapon);camera.Target=player.transform;
        target=Target(new Vector2(player.EquippedWeapon.AttackType==WeaponAttackType.Ranged?3.2f:1.2f,0));
        StageValidationInput.SetAim(Vector2.right);Physics2D.SyncTransforms();
    }
    public static string Main()
    {
        if(running||!Application.isPlaying)throw new Exception("Play Mode required, one fixture at a time");
        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();if(game==null||game.IsRunActive)throw new Exception("Start from main menu; no campaign state is replaced");
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-10-04-combat-expansion/"+DateTime.Now.ToString("HHmmss");Directory.CreateDirectory(folder);
        checks.Clear();roots.Clear();inputs.Clear();originalScene=SceneManager.GetActiveScene();oldFocus=EditorWindow.focusedWindow;
        oldScale=Time.timeScale;oldRate=Application.targetFrameRate;oldBackground=Application.runInBackground;oldRandom=UnityEngine.Random.state;
        oldEnabled=(bool)Input("enabled").GetValue(null);oldInitialized=(bool)Input("initialized").GetValue(null);oldAim=StageValidationInput.AimDirection;
        foreach(var name in new[]{"Held","Pressed","Released"}){var set=(HashSet<GameAction>)Input(name).GetValue(null);inputs[name]=new List<GameAction>(set).ToArray();set.Clear();}
        oldAudio=(GameSettingsService)typeof(CombatAudio).GetField("settings",All).GetValue(null);
        foreach(var asm in AppDomain.CurrentDomain.GetAssemblies()){autoTick=asm.GetType("Unity.Pipeline.Editor.Commands.AutoTickCommand");if(autoTick!=null)break;}
        oldTick=(bool)autoTick.GetProperty("IsEnabled",All).GetValue(null);oldInterval=Convert.ToInt32(autoTick.GetProperty("CurrentIntervalMs",All).GetValue(null));
        running=true;began=EditorApplication.timeSinceStartup;
        try
        {
            settings=new GameSettingsService(folder+"/review-settings.json",false);foreach(AudioChannel channel in Enum.GetValues(typeof(AudioChannel)))settings.ChangeVolume(channel,-1);CombatAudio.Configure(settings);
            Input("enabled").SetValue(null,true);Input("initialized").SetValue(null,true);Application.runInBackground=true;Application.targetFrameRate=60;Time.timeScale=1;
            autoTick.GetMethod("SetAutoTick",All).Invoke(null,new object[]{true,0,false});EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
            for(int i=0;i<SceneManager.sceneCount;i++)foreach(var root in SceneManager.GetSceneAt(i).GetRootGameObjects())roots[root]=root.activeSelf;
            foreach(var root in roots)if(root.Value)root.Key.SetActive(false);
            scene=SceneManager.CreateScene("CombatExpansion-SuppliedFixture");SceneManager.SetActiveScene(scene);camera=CameraFollow.Create();camera.FocusRoom(Vector2.zero,new Vector2(100,100),true);
            EditorApplication.update+=Watchdog;EditorApplication.playModeStateChanged+=PlayState;camera.StartCoroutine(Guard(Run()));
        }catch(Exception error){Finish("FAIL setup "+error);}
        return folder;
    }
    static IEnumerator Guard(IEnumerator run)
    {
        var stack=new Stack<IEnumerator>();stack.Push(run);string error=null;
        while(stack.Count>0&&running)
        {bool next;object value=null;try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception e){error=e.ToString();break;}if(!next){stack.Pop();continue;}if(value is IEnumerator child){stack.Push(child);continue;}yield return value;}
        if(running)Finish(error==null?"PASS":"FAIL "+error);
    }
    static IEnumerator Run()
    {
        yield return Frame();
        foreach(var id in new[]{"worn_dagger","rust_sword","iron_mace","training_katana","iron_greatsword","hand_crossbow","scrap_pistol","storm_rifle","scrap_shotgun","siege_mortar"})
        {
            Fresh(id);yield return Frame();float hp=target.Health,mana=player.Mana;
            yield return Press(GameAction.Guard,id=="iron_greatsword"?.24f:.035f);yield return Wait(.72f);
            Check(target.Health<hp,"actual RMB damages supplied collider: "+id);
            Check(player.Mana<mana,"actual RMB paid mana: "+id);
            Check(!player.IsGuarding&&!player.IsGreatswordCharging&&!player.IsParrying,"finite special returns to controllable state: "+id);
        }
        foreach(var id in new[]{"shadow_fang","blood_moon_katana","blood_reaver","void_revolver","dragon_cannon","sun_oath","starbreaker","returning_edge","gravity_maul","tether_carbine","bastion_shotgun"})
        {
            Fresh(id);yield return Frame();float hp=target.Health;
            yield return Press(GameAction.Guard);
            if(id=="sun_oath"){Check(player.IsLongswordCatchActive,"sun bank uses native catch");player.TakeDamage(10,Vector2.left);}
            if(id=="bastion_shotgun")
            {Check(player.TryCatchHostileProjectile(new Vector2(1.1f,0),Vector2.left,10),"supplied bastion projectile captured");}
            yield return Wait(.6f);
            if(id=="blood_reaver"||id=="gravity_maul"||id=="tether_carbine")yield return Press(GameAction.Attack,1.3f);
            else if(id!="returning_edge")yield return Press(GameAction.Guard);
            yield return Wait(.55f);
            Check(target.Health<hp,"unique action path contacts collider: "+id);
            Check(id=="sun_oath"||player.UniqueActionsStarted==1,"unique ID started once: "+id);
            if(id!="returning_edge"&&id!="tether_carbine")Check(player.UniqueActionsReleased==1,"unique ID completed its distinct release: "+id);
            checks.Add("DATA "+id+" targetDamage="+(hp-target.Health).ToString("F3")+" contacts="+player.MeleeContactsResolved+" originalEvents="+events.Count);
        }
        Fresh("void_revolver");yield return Frame();yield return Press(GameAction.Guard);yield return Press(GameAction.Dash);yield return Wait(.25f);
        float afterCancel=target.Health;yield return Press(GameAction.Guard);yield return Wait(.4f);
        Check(player.ActiveUniqueAction==UniqueWeaponAction.None&&target.Health==afterCancel,"dash cancels stored/delayed unique release and retains paid cooldown");
        Fresh("returning_edge");yield return Frame();yield return Press(GameAction.Guard);paused=true;yield return Wait(.12f);float pauseHp=target.Health;paused=false;yield return Wait(.4f);
        Check(target.Health==pauseHp,"pause discards returning blade instead of delayed resume hit");
        Fresh("void_revolver");yield return Frame();yield return Press(GameAction.Guard);bag.AddWeapon(WeaponLootTable.Find("rust_sword"));bag.SelectQuickSlot(1);player.EquipWeapon(bag.EquippedWeapon);yield return Wait(.35f);
        Check(player.ActiveUniqueAction==UniqueWeaponAction.None,"equip discards previous copy's planted special session");
        Fresh("void_revolver");yield return Frame();yield return Press(GameAction.Guard);player.TakeDamage(10000,Vector2.left);float deadHp=target.Health;yield return Wait(.35f);
        Check(player.IsDead&&target.Health==deadHp,"death cancels a special projectile before its delayed contact");
        Fresh("storm_rifle");yield return Frame();bag.TryConsumeAmmo();bag.StartReload();yield return Press(GameAction.Guard,.018f);
        float paidMana=player.Mana,paidCooldown=Read<float>(player,"weaponActionCooldown"),paidAttack=Read<float>(player,"attackTimer"),reload=bag.ActiveSlot.ReloadRemaining;int ammo=bag.ActiveSlot.Ammo,epoch=player.CombatEpoch;float introHp=target.Health;
        player.CancelCombatForPresentation();
        Check(player.CombatEpoch>epoch&&player.Mana==paidMana&&bag.ActiveSlot.Ammo==ammo&&bag.ActiveSlot.ReloadRemaining==reload,"presentation cancellation increments epoch and preserves paid mana, owned ammo and reload");
        Check(Read<float>(player,"weaponActionCooldown")==paidCooldown&&Read<float>(player,"attackTimer")==paidAttack,"presentation cancellation preserves special and attack recovery clocks");
        yield return Wait(.4f);Check(target.Health==introHp&&!player.IsDashing,"presentation cancellation leaves no delayed rifle contact");

        Fresh("rust_sword");yield return Frame();var wall=new GameObject("Supplied wall");wall.layer=GameLayers.Ground;wall.transform.position=new Vector2(.5f,0);wall.AddComponent<BoxCollider2D>().size=new Vector2(.15f,4);Physics2D.SyncTransforms();
        float blockedHp=target.Health;yield return Press(GameAction.Attack,.8f);yield return Wait(.25f);Check(target.Health==blockedHp,"wall blocks basic multi-contact damage");Remove(wall);yield return Frame();
        int count=events.Count;player.SecondaryHit(target,5,Vector2.zero);Check(events.Count==count,"secondary damage does not emit recursive original/growth contact");

        foreach(var id in new[]{"recall_spool","cadence_clasp","prism_cage","trail_spindle","focus_prism","mana_aegis"})
        {
            Fresh("rust_sword",id);yield return Frame();int before=player.ItemEffects.ExtendedItemProcs;
            switch(id)
            {
                case "recall_spool":player.OnDirectCombatHit(target,100,30,true,true);player.ItemEffects.OnManaSpent(10,Vector2.right);break;
                case "cadence_clasp":player.OnDirectCombatHit(target,100,30,true);player.ItemEffects.OnDashStarted(Vector2.zero);player.ItemEffects.OnManaSpent(10,Vector2.right);break;
                case "prism_cage":player.ItemEffects.OnManaSpent(10,Vector2.right);Check(player.ItemEffects.TryCatchProjectile(new Vector2(.5f,0),Vector2.left,10),"supplied prism projectile captured");player.OnDirectCombatHit(target,100,30,true);Check(player.ItemEffects.CapturedItemBullets==0,"prism packet consumed once");break;
                case "trail_spindle":StageValidationInput.SetHeld(GameAction.MoveUp,true);yield return Wait(.6f);StageValidationInput.SetHeld(GameAction.MoveUp,false);player.OnDirectCombatHit(target,100,30,true);break;
                case "focus_prism":player.OnDirectCombatHit(target,100,30,true);player.OnDirectCombatHit(target,101,30,true);player.OnDirectCombatHit(target,102,30,true);other=Target(new Vector2(2.2f,0));player.OnDirectCombatHit(other,103,30,true);break;
                case "mana_aegis":var mana=player.Mana;float health=player.Health;player.TakeDamage(20,Vector2.left);Check(Mathf.Abs(mana-player.Mana-12)<.01f&&Mathf.Abs(health-player.Health-10)<.01f,"aegis pays mana and blocks only its bounded amount");break;
            }
            Check(player.ItemEffects.ExtendedItemProcs==before+1,"explicit supplied item callback mechanic: "+id);
            // Removing the actual owned accessory invokes production reconcile.
            var owned=bag.OwnedAccessories[0];Check(bag.TryRemoveConfirmed(owned.Identity),"remove actual owned accessory: "+id);Check(player.ItemEffects.CapturedItemBullets==0,"removed item leaves no captured projectile state: "+id);
        }
        Fresh("rust_sword","arcane_lens");yield return Frame();player.ItemEffects.OnManaSpent(10,Vector2.right);yield return Wait(.3f);
        Check(player.ItemEffects.EchoContactsResolved==0,"lens waits for a direct attack instead of automatic mana echo");player.OnDirectCombatHit(target,1,30,true);yield return Wait(.25f);Check(player.ItemEffects.EchoContactsResolved==1,"direct attack recalls armed lens from its original point");

        Fresh("rust_sword");yield return Frame();player.ConfigureCombatGrowth(t=>t==TalentId.ComboMemory,e=>events.Add(e));player.OnDirectCombatHit(target,100,10,true);Set(player,"comboIndex",2);Set(player,"comboResetTimer",.05f);yield return Press(GameAction.Dash);yield return Wait(.2f);Check(player.ComboStep==2,"hit-dash talent preserves the next combo stage");
        Fresh("rust_sword");yield return Frame();player.ConfigureCombatGrowth(t=>t==TalentId.TurningDash,null);StageValidationInput.SetHeld(GameAction.MoveRight,true);StageValidationInput.SetHeld(GameAction.Dash,true);yield return Wait(.055f);StageValidationInput.SetHeld(GameAction.MoveRight,false);StageValidationInput.SetHeld(GameAction.MoveUp,true);yield return Wait(.15f);StageValidationInput.ReleaseAll();Check(player.transform.position.x>.1f&&player.transform.position.y>.1f&&player.transform.position.magnitude<2.8f,"turning dash bends once within original travel budget");
        Fresh("rust_sword");yield return Frame();player.ConfigureCombatGrowth(t=>t==TalentId.BrakeDash,null);yield return Press(GameAction.Dash,.035f);yield return Wait(.2f);Check(player.transform.position.magnitude<2.3f&&player.DashCharges==1,"brake dash stops travel without refunding its charge");
        Fresh("rust_sword");yield return Frame();player.ConfigureCombatGrowth(t=>t==TalentId.ReturnDash,null);yield return Press(GameAction.Dash);yield return Wait(.22f);yield return Press(GameAction.Dash);yield return Wait(.22f);Check(player.transform.position.magnitude<.3f&&player.DashCharges==0,"return dash spends a second charge and retraces the origin");
        Fresh("rust_sword");yield return Frame();player.ConfigureCombatGrowth(t=>t==TalentId.StoredGuard,null);yield return Press(GameAction.Guard);Check(player.TryCatchHostileProjectile(new Vector2(.4f,0),Vector2.left,10),"stored guard accepts a supplied frontal projectile");Check(Read<int>(player,"storedGuardCharges")==1,"stored guard capacity one");yield return Wait(.6f);float storedMana=player.Mana;yield return Press(GameAction.Guard);Check(Read<int>(player,"storedGuardCharges")==0&&player.Mana>=storedMana,"re-input spends only stored projectile, not another mana action");
        Fresh("rust_sword");yield return Frame();bag.AddWeapon(WeaponLootTable.Find("scrap_pistol"));bag.SelectQuickSlot(1);player.EquipWeapon(bag.EquippedWeapon);var empty=bag.ActiveSlot.Item;while(empty.Ammo>0)empty.TryConsumeAmmo();bag.SelectQuickSlot(0);player.EquipWeapon(bag.EquippedWeapon);player.ConfigureCombatGrowth(t=>t==TalentId.CrossChamber,null);player.OnDirectCombatHit(target,100,10,true,true);Check(empty.Ammo==1&&empty.IsReloading,"cross chamber feeds only one empty owned copy and keeps reload running");
        float chamberReload=empty.ReloadRemaining;Check(empty.TryConsumeAmmo()&&empty.Ammo==0&&empty.ReloadRemaining==chamberReload&&!empty.TryConsumeAmmo(),"cross chamber grants exactly one usable shot during the ongoing reload");

        Fresh("scrap_pistol");yield return Frame();yield return Press(GameAction.Attack);yield return Wait(.35f);
        Check(events.Exists(e=>e.Kind==CombatGrowthKind.DirectHit&&e.IsMagazineHit),"real normal projectile is a magazine contact");
        events.Clear();yield return Press(GameAction.Guard);yield return Wait(.4f);
        Check(events.Exists(e=>e.Kind==CombatGrowthKind.DirectHit)&&!events.Exists(e=>e.IsMagazineHit),"free-ammo special hits are excluded from magazine oath progress");
        Fresh("scrap_pistol");yield return Frame();var rangedCopy=bag.ActiveSlot.Item;
        typeof(OwnedWeapon).GetProperty("Mutation").SetValue(rangedCopy,EquipmentMutation.Ricochet);
        var bounceWall=new GameObject("Supplied reflection wall");bounceWall.layer=GameLayers.Ground;bounceWall.transform.position=new Vector2(2.2f,0);bounceWall.AddComponent<BoxCollider2D>().size=new Vector2(.2f,4);
        other=Target(new Vector2(-1,0));Physics2D.SyncTransforms();float otherHp=other.Health,behindWallHp=target.Health;
        yield return Press(GameAction.Attack);yield return Wait(.65f);
        Check(other.Health<otherHp&&target.Health==behindWallHp,"real mutated bullet reflects once into rear target and never damages through its wall");Remove(bounceWall);yield return Frame();
        Fresh("scrap_pistol");yield return Frame();rangedCopy=bag.ActiveSlot.Item;
        typeof(OwnedWeapon).GetProperty("Mutation").SetValue(rangedCopy,EquipmentMutation.AnchorRound);
        yield return Press(GameAction.Attack);yield return Wait(.3f);Check(Read<float>(player,"plantedUntil")>Read<float>(player,"combatClock"),"real anchor-round shot leaves one bounded contact mark");
        player.OnDirectCombatHit(target,999,10,false);Check(Read<float>(player,"plantedUntil")==0,"next original attack consumes its planted mark exactly once");
        Fresh("scrap_pistol");yield return Frame();rangedCopy=bag.ActiveSlot.Item;
        typeof(OwnedWeapon).GetProperty("Oath").SetValue(rangedCopy,EquipmentOath.SteadyVolley);typeof(OwnedWeapon).GetProperty("Awakened").SetValue(rangedCopy,true);
        while(rangedCopy.Ammo>0)rangedCopy.TryConsumeAmmo();bag.AddWeapon(WeaponLootTable.Find("rust_sword"));bag.SelectQuickSlot(1);player.EquipWeapon(bag.EquippedWeapon);bag.Tick(10);
        Check(events.Exists(e=>e.Kind==CombatGrowthKind.ReloadCompleted&&e.ReloadStartedEmpty&&e.WeaponIdentity==rangedCopy.Identity),"off-slot empty reload retains the owned copy in growth event");
        bag.SelectQuickSlot(0);player.EquipWeapon(bag.EquippedWeapon);yield return Press(GameAction.Attack);yield return Wait(.3f);
        Check(Read<float>(player,"plantedUntil")>Read<float>(player,"combatClock"),"off-slot awakened reload survives equip and marks the next actual round");
        player.CancelCombatForPresentation();bag.TryConsumeAmmo();bag.StartReload();events.Clear();bag.Tick(10);
        Check(events.Exists(e=>e.Kind==CombatGrowthKind.ReloadCompleted&&!e.ReloadStartedEmpty),"partial reload emits clearing event without empty-magazine qualification");

        Fresh("rust_sword");yield return Frame();var copy=bag.ActiveSlot.Item;var profile=MeleeComboCatalog.Get(player.EquippedWeapon);var original=profile.LightSteps[profile.LightSteps.Length-1];
        typeof(OwnedWeapon).GetProperty("Mutation").SetValue(copy,EquipmentMutation.LongFinisher);var changed=(MeleeAttackStep)typeof(PlayerController).GetMethod("ApplyFinisherMutation",All).Invoke(player,new object[]{original,true});Check(changed.HitboxSize.x>original.HitboxSize.x&&changed.HitboxSize.y<original.HitboxSize.y,"long finisher changes reach and breadth instead of damage multiplier");
        typeof(OwnedWeapon).GetProperty("Mutation").SetValue(copy,EquipmentMutation.WideFinisher);changed=(MeleeAttackStep)typeof(PlayerController).GetMethod("ApplyFinisherMutation",All).Invoke(player,new object[]{original,true});Check(changed.HitboxSize.x<original.HitboxSize.x&&changed.HitboxSize.y>original.HitboxSize.y,"wide finisher trades reach for breadth");
        foreach(var oath in new[]{EquipmentOath.CounterDiscipline,EquipmentOath.FinisherHunt,EquipmentOath.CrossWeaponChain})
        {
            typeof(OwnedWeapon).GetProperty("Oath").SetValue(copy,oath);typeof(OwnedWeapon).GetProperty("Awakened").SetValue(copy,true);Set(player,"awakenedAction",-1);float hp=target.Health;
            if(oath==EquipmentOath.CrossWeaponChain){Set(player,"crossAwakeReady",true);player.OnSpecialCommitted();}
            else player.OnDirectCombatHit(target,200+(int)oath,10,true,oath==EquipmentOath.FinisherHunt,oath==EquipmentOath.CounterDiscipline);
            Check(target.Health<hp,"supplied awakened copy runs extra bounded action: "+oath);
        }
        Check(WeaponLootTable.Count==46&&AccessoryLootTable.Count==30,"four new unique weapons and six new accessory mechanics registered in normal tables");
        foreach(var w in WeaponLootTable.All)if(w.Rarity>=WeaponRarity.Epic)Check(WeaponActionCatalog.Get(w).Kind!=UniqueWeaponAction.None,"high rarity has individual action ID: "+w.Id);
    }
    static void Watchdog(){if(running&&EditorApplication.timeSinceStartup-began>120)Finish("FAIL 120 second watchdog");}
    static void PlayState(PlayModeStateChange s){if(s==PlayModeStateChange.ExitingPlayMode&&running)Finish("INTERRUPTED Play ended");}
    static void Finish(string result)
    {
        if(!running)return;running=false;EditorApplication.update-=Watchdog;EditorApplication.playModeStateChanged-=PlayState;
        try
        {
            if(scene.IsValid()&&scene.isLoaded)foreach(var root in scene.GetRootGameObjects())root.SetActive(false);
            if(originalScene.IsValid())SceneManager.SetActiveScene(originalScene);foreach(var root in roots)if(root.Key!=null)root.Key.SetActive(root.Value);
            foreach(var pair in inputs){var set=(HashSet<GameAction>)Input(pair.Key).GetValue(null);set.Clear();set.UnionWith(pair.Value);}typeof(StageValidationInput).GetProperty("AimDirection",All).SetValue(null,oldAim);Input("enabled").SetValue(null,oldEnabled);Input("initialized").SetValue(null,oldInitialized);
            CombatAudio.Configure(oldAudio);Application.targetFrameRate=oldRate;Application.runInBackground=oldBackground;Time.timeScale=oldScale;UnityEngine.Random.state=oldRandom;
            autoTick.GetMethod("SetAutoTick",All).Invoke(null,new object[]{oldTick,oldInterval,false});if(oldFocus!=null)oldFocus.Focus();
            checks.Add("PASS restored original active scene, roots, input, audio, clock and AutoTick");
            if(scene.IsValid()&&scene.isLoaded)
            {
                var unload=SceneManager.UnloadSceneAsync(scene);var pendingResult=result;
                unload.completed+=_=>{checks.Add("PASS temporary fixture scene unloaded");WriteResult(pendingResult);};
                return;
            }
        }catch(Exception error){result+="\nRESTORE FAIL "+error;}
        WriteResult(result);
    }
    static void WriteResult(string result)
    {
        File.WriteAllText(folder+"/result.txt",result+"\n"+string.Join("\n",checks)+"\nSupplied-state functional inputs/callbacks only; no campaign, visual-motion or listening claim.\n",new UTF8Encoding(false));Debug.Log("[Combat expansion] "+result+" "+folder);
    }
}
