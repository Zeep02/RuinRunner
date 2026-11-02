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

// Production spawning with supplied projectile-trigger/direct-contact callbacks.
// This is not normal campaign, native input, motion smoothness or audio evidence.
public static class AnchorFractureRegression20261026
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
    static void Remove(UnityEngine.Object obj){if(obj is Component c)c.gameObject.SetActive(false);if(obj!=null)UnityEngine.Object.Destroy(obj is Component cmp?cmp.gameObject:obj);}
    static EnemyController Target(Vector2 point)
    {
        var e=EnemyController.Create(EnemyType.Grunt,point,player,()=>true,_=>{},null);
        e.ApplyBalance(new EnemyBalanceData(EnemyType.Grunt,10000,0,0,0,0));e.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll; var box=(BoxCollider2D)e.DamageCollider;box.size=new Vector2(.18f,.18f);box.offset=Vector2.zero;
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
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-10-25-goal/combat-followup/"+DateTime.Now.ToString("HHmmss");Directory.CreateDirectory(folder);
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
            scene=SceneManager.CreateScene("AnchorFracture-SuppliedFixture");SceneManager.SetActiveScene(scene);camera=CameraFollow.Create();camera.FocusRoom(Vector2.zero,new Vector2(100,100),true);
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
    static object Call(object obj,string name,params object[] arguments)=>obj.GetType().GetMethod(name,All).Invoke(obj,arguments);
    static void Contact(PlayerProjectile shot,EnemyController enemy)=>Call(shot,"OnTriggerEnter2D",enemy.DamageCollider);
    static List<PlayerProjectile> Shots()
    {
        var result=new List<PlayerProjectile>();
        foreach(var shot in UnityEngine.Object.FindObjectsByType<PlayerProjectile>())if(Read<PlayerController>(shot,"owner")==player)result.Add(shot);
        return result;
    }
    static PlayerProjectile MarkingShot(int action,float damage=20,int pierce=1)
        =>PlayerProjectile.Create(new Vector2(.8f,0),Vector2.right,12,damage,false,Color.white,new Vector2(.2f,.1f),pierce,WeaponFamily.Rifle,false,"fixture",player,action,0).WithMutation(false,true);
    static bool HasMark()=>Read<float>(player,"plantedUntil")>Read<float>(player,"combatClock");
    static bool Near(float a,float b)=>Mathf.Abs(a-b)<.02f;
    static void Direct(int action)=>player.OnDirectCombatHit(target,action,10,false,sourceWeaponIdentity:player.CombatWeaponIdentity,magazineHit:true);
    static IEnumerator Run()
    {
        yield return Frame();
        Fresh("scrap_shotgun");yield return Frame();
        var copy=bag.ActiveSlot.Item;typeof(OwnedWeapon).GetProperty("Mutation").SetValue(copy,EquipmentMutation.AnchorRound);
        Call(player,"RangedAttack");var pellets=Shots();Check(pellets.Count==3,"real basic shotgun creates three pellets");
        int action=Read<int>(pellets[0],"sourceAction");float expected=0;foreach(var pellet in pellets)expected+=Read<float>(pellet,"damage");
        float before=target.Health;foreach(var pellet in pellets)Contact(pellet,target);
        Check(Near(before-target.Health,expected),"same-action shotgun pellets cause no premature anchor explosion");
        Check(HasMark()&&Read<int>(player,"plantedAction")==action,"shotgun leaves one mark belonging to its fired action");
        float markDamage=Read<float>(player,"plantedDamage");before=target.Health;Direct(action+1);
        Check(!HasMark()&&Near(before-target.Health,markDamage),"next supplied direct action consumes the mark exactly once");
        before=target.Health;Direct(action+1);Direct(action+1);
        Check(Near(before,target.Health),"other contacts of the consuming action cannot repeat the explosion");
        var lateSame=MarkingShot(action);Contact(lateSame,target);
        Check(!HasMark(),"late pellet cannot resurrect its already consumed mark");
        var older=MarkingShot(action+2,17);var newer=MarkingShot(action+3,20);Contact(newer,target);
        before=target.Health;Contact(older,target);
        Check(HasMark()&&Read<int>(player,"plantedAction")==action+3&&Near(Read<float>(player,"plantedDamage"),9)&&Near(before-target.Health,17),"older delayed shot neither consumes nor overwrites the newer mark");
        before=target.Health;Direct(action+4);
        Check(!HasMark()&&Near(before-target.Health,9),"later action consumes the retained newer mark once");
        var oldCopyShot=MarkingShot(action+5,16);long oldCopy=player.CombatWeaponIdentity;
        bag.AddWeapon(WeaponLootTable.Find("rust_sword"));bag.SelectQuickSlot(1);player.EquipWeapon(bag.EquippedWeapon);
        Check(player.CombatWeaponIdentity!=oldCopy&&!HasMark(),"weapon swap clears the previous copy's mark");
        Call(player,"PlantAwakenedRound",(Vector2)target.transform.position,8f,action+6,player.CombatWeaponIdentity);
        before=target.Health;Contact(oldCopyShot,target);
        Check(HasMark()&&Read<int>(player,"plantedAction")==action+6&&Near(before-target.Health,16),"unequipped delayed projectile cannot consume or replace current-copy mark");

        Fresh("scrap_shotgun");yield return Frame();copy=bag.ActiveSlot.Item;
        typeof(OwnedWeapon).GetProperty("Oath").SetValue(copy,EquipmentOath.SteadyVolley);typeof(OwnedWeapon).GetProperty("Awakened").SetValue(copy,true);
        Call(player,"OnCombatReload",copy,true);Call(player,"RangedAttack");pellets=Shots();
        var marking=pellets.Find(p=>Read<bool>(p,"anchorRound"));Check(marking!=null&&pellets.FindAll(p=>Read<bool>(p,"anchorRound")).Count==1,"awakened empty-reload callback arms one actual shotgun pellet");
        expected=0;foreach(var pellet in pellets)expected+=Read<float>(pellet,"damage");before=target.Health;
        Contact(marking,target);foreach(var pellet in pellets)if(pellet!=marking)Contact(pellet,target);
        Check(HasMark()&&Near(before-target.Health,expected),"same-action pellets do not recall the awakened volley mark");

        Fresh("siege_mortar");yield return Frame();copy=bag.ActiveSlot.Item;
        typeof(OwnedWeapon).GetProperty("Mutation").SetValue(copy,EquipmentMutation.AnchorRound);
        other=Target(new Vector2(3.8f,0));Physics2D.SyncTransforms();Call(player,"RangedAttack");var cannon=Shots()[0];float cannonDamage=Read<float>(cannon,"damage");
        before=target.Health;float otherBefore=other.Health;Contact(cannon,target);
        Check(HasMark()&&Near(before-target.Health,cannonDamage)&&Near(otherBefore-other.Health,cannonDamage*.35f),"same-action cannon splash cannot immediately recall its newly planted mark");

        Fresh("scrap_pistol");yield return Frame();other=Target(new Vector2(3.8f,0));Physics2D.SyncTransforms();
        var piercing=MarkingShot(1,20,2);before=target.Health;otherBefore=other.Health;Contact(piercing,target);Contact(piercing,other);
        Check(HasMark()&&Near(before-target.Health,20)&&Near(otherBefore-other.Health,20),"second piercing contact causes no premature anchor explosion");

        Fresh("blood_reaver");yield return Frame();Set(player,"specialInputBuffer",.16f);
        Check((bool)Call(player,"TryUniqueInput")&&player.ActiveUniqueAction==UniqueWeaponAction.FractureLink,"real unique start places a fracture session");
        var state=Read<object>(player,"uniqueSession");Vector2 origin=Read<Vector2>(state,"Origin"),end=Read<Vector2>(state,"Point");
        Check(Vector2.Distance(origin,Vector2.zero)<.01f&&Vector2.Distance(end,new Vector2(3.1f,0))<.01f,"fracture stores its original marked endpoints");
        player.GetComponent<Rigidbody2D>().position=new Vector2(0,3);target.transform.position=new Vector2(1.1f,0);other=Target(new Vector2(1.55f,1.5f));
        var trigger=Target(new Vector2(1.2f,3));Physics2D.SyncTransforms();before=target.Health;otherBefore=other.Health;
        player.OnDirectCombatHit(trigger,Read<int>(state,"Sequence")+1,10,true);
        Check(target.Health<before&&Near(other.Health,otherBefore),"after walking, fracture damages marked ground and excludes the unmarked diagonal");
        Check(player.UniqueActionsReleased==1&&player.ActiveUniqueAction==UniqueWeaponAction.None,"basic contact consumes the fixed fracture session once");
        checks.Add("SCOPE Production shotgun/cannon spawning and supplied projectile-trigger/direct-contact callbacks; supplied movement/colliders. No normal campaign, native input, continuous motion or audio claim.");
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
        File.WriteAllText(folder+"/result.txt",result+"\n"+string.Join("\n",checks)+"\nSupplied-state functional inputs/callbacks only; no campaign, visual-motion or listening claim.\n",new UTF8Encoding(false));Debug.Log("[Anchor fracture followup] "+result+" "+folder);
    }
}
