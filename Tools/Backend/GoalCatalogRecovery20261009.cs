using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RuinRunner;
using RuinRunner.Backend;
using UnityEngine;
using UnityEngine.SceneManagement;

// Execute Main on the Unity main thread in Play Mode at the main menu.
// This uses supplied items and isolated API accounts; it is not normal campaign evidence.
public static class GoalCatalogRecovery20261009
{
    public static string Main()
    {
        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();
        if(!Application.isPlaying||game==null||game.IsRunActive)throw new InvalidOperationException("Play Mode at main menu required; a running campaign must not be replaced.");
        var root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        var id=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6);
        var report=Path.Combine(root,"Backend/Validation/unity-catalog-recovery-"+id+".json");
        _=Run(root,id,report);return "Started isolated catalog/recovery fixture: "+report;
    }
    static async Task Run(string root,string id,string report)
    {
        var checks=new List<string>();BackendFlowCoordinator flow=null;PlayerController oldPlayer=null,newPlayer=null;
        var previousScene=SceneManager.GetActiveScene();var scene=SceneManager.CreateScene("CatalogRecovery-"+id);
        var previousScale=Time.timeScale;
        var previousVersion=AccessoryLootTable.PinnedCatalogVersion??"campaign-v1";
        var restore=JsonConvert.DeserializeObject<ContentCatalog>(File.ReadAllText(Path.Combine(root,"Backend/Content/catalog."+previousVersion+".json"),Encoding.UTF8));
        void Check(string name,bool pass){if(!pass)throw new InvalidOperationException(name);checks.Add(name);}
        var directory=Path.Combine(root,"Backend/.local/isolated/goal-catalog-20261009/client-fixtures",id);
        Directory.CreateDirectory(directory);
        void Bind(ContentCatalog catalog){WeaponLootTable.BindPinnedCatalog(catalog);AccessoryLootTable.BindPinnedCatalog(catalog);}
        try
        {
            var v1=JsonConvert.DeserializeObject<ContentCatalog>(File.ReadAllText(Path.Combine(root,"Backend/Content/catalog.campaign-v1.json"),Encoding.UTF8));
            var v2=JsonConvert.DeserializeObject<ContentCatalog>(File.ReadAllText(Path.Combine(root,"Backend/Content/catalog.campaign-v2.json"),Encoding.UTF8));
            Bind(v1);
            var oldRuby=AccessoryLootTable.Find("ruby_heart");var oldTrail=AccessoryLootTable.Find("trail_spindle");
            var oldBag=new InventorySystem();oldBag.AddWeapon(WeaponLootTable.Find("rust_sword"));oldBag.AddAccessory(AccessoryLootTable.Find("guardian_plate"));oldBag.AddAccessory(AccessoryLootTable.Find("guardian_seal"));
            SceneManager.SetActiveScene(scene);Time.timeScale=1;
            var settings=new GameSettingsService(Path.Combine(directory,"settings.json"),false);
            oldPlayer=PlayerController.Create(new Vector2(1000,1000),()=>false,()=>false,()=>{},null,oldBag,settings);
            oldPlayer.enabled=false;oldPlayer.EquipWeapon(oldBag.EquippedWeapon);
            var oldArmor=oldPlayer.Stats.Armor;
            Bind(v2);
            Check("all 30 accessories and eight sets bind from real v2",AccessoryLootTable.All.Count==30&&ItemSetCatalog.Bonuses.Length==8&&AccessoryLootTable.PinnedCatalogVersion=="campaign-v2");
            Check("v2 descriptions resolve without placeholders",AccessoryLootTable.All.All(x=>!x.Description.Contains("{"))&&ItemSetCatalog.Bonuses.All(x=>!x.Description.Contains("{")));
            Check("v1 owned trail remains two marks while v2 uses stated three",oldTrail.Tuning.EffectCount("requiredMarkCount")==2&&AccessoryLootTable.Find("trail_spindle").Tuning.EffectCount("requiredMarkCount")==3);
            var synthetic=JsonConvert.DeserializeObject<ContentCatalog>(JsonConvert.SerializeObject(v2));synthetic.version="isolated-numeric-fixture";
            synthetic.items.First(x=>x.id=="ruby_heart").stats["maxHealth"]=27;
            synthetic.items.First(x=>x.id=="mana_aegis").stats["effect"]["manaCost"]=19;
            synthetic.items.First(x=>x.id=="mana_aegis").stats["effect"]["absorbDamage"]=13;
            synthetic.itemSets.First(x=>x.id=="guardian"&&x.pieces==2).stats["armor"]=13;
            synthetic.itemSets.First(x=>x.id=="guardian"&&x.pieces==2).stats["effect"]["shield"]=17;
            Bind(synthetic);
            Check("description and definition use identical synthetic value",AccessoryLootTable.Find("ruby_heart").Description.Contains("27")&&oldRuby.Tuning.Number("maxHealth")==18);
            oldPlayer.Stats.Recalculate();oldPlayer.ItemEffects.OnDefenseSuccess(Vector2.right);
            Check("existing player retains pinned set stat and proc after catalog rebind",Math.Abs(oldPlayer.Stats.Armor-oldArmor)<.001f&&Math.Abs(oldPlayer.ItemEffects.Shield-8)<.001f);
            var newBag=new InventorySystem();newBag.AddWeapon(WeaponLootTable.Find("rust_sword"));newBag.AddAccessory(AccessoryLootTable.Find("guardian_plate"));newBag.AddAccessory(AccessoryLootTable.Find("guardian_seal"));newBag.AddAccessory(AccessoryLootTable.Find("mana_aegis"));
            newPlayer=PlayerController.Create(new Vector2(1100,1000),()=>false,()=>false,()=>{},null,newBag,settings);newPlayer.enabled=false;newPlayer.EquipWeapon(newBag.EquippedWeapon);
            newPlayer.ItemEffects.OnDefenseSuccess(Vector2.right);
            Check("new player consumes changed set stat and shield",Math.Abs(newPlayer.Stats.Armor-oldArmor-5)<.001f&&Math.Abs(newPlayer.ItemEffects.Shield-17)<.001f);
            var beforeMana=newPlayer.Mana;var damage=newPlayer.ItemEffects.AbsorbManaAegis(20);
            Check("real mana aegis action consumes bound cost and absorption",Math.Abs(beforeMana-newPlayer.Mana-19)<.001f&&Math.Abs(damage-7)<.001f);
            var aegis=newBag.OwnedAccessories.First(x=>x.Definition.Id=="mana_aegis");Check("owned aegis removal allowed",newBag.TryRemoveConfirmed(aegis.Identity));newBag.AddAccessory(AccessoryLootTable.Find("mana_aegis"));
            Check("remove and reacquire does not reset shared proc cooldown",Math.Abs(newPlayer.ItemEffects.AbsorbManaAegis(20)-20)<.001f&&Math.Abs(beforeMana-newPlayer.Mana-19)<.001f);
            Bind(v1);Check("rebinding v1 restores exact frozen defaults",AccessoryLootTable.Find("ruby_heart").Tuning.Number("maxHealth")==18&&AccessoryLootTable.Find("trail_spindle").Tuning.EffectCount("requiredMarkCount")==2&&ItemSetCatalog.Bonuses.First(x=>x.SetType==ItemSetType.Guardian&&x.RequiredPieces==2).Tuning.Effect("shield")==8);
            UnityEngine.Object.DestroyImmediate(oldPlayer.gameObject);oldPlayer=null;UnityEngine.Object.DestroyImmediate(newPlayer.gameObject);newPlayer=null;
            Time.timeScale=previousScale;SceneManager.SetActiveScene(previousScene);
            var options=new BackendClientOptions{ApiBaseUri="http://127.0.0.1:5197/",StorageDirectory=Path.Combine(directory,"Recovery"),TimeoutSeconds=5};
            var saves=Path.Combine(directory,"Saves");var slots=new SaveSlotService(saves);flow=new BackendFlowCoordinator(slots,options);
            Check("isolated v2 API connects and selects new account slot",await flow.ConnectAsync()&&await flow.SelectSlotAsync(1));
            Check("new real server expedition pins v2",await flow.StartAsync(20261009,"modules-v1.0.0","unity-catalog-recovery-fixture")&&flow.Run.contentVersion=="campaign-v2");
            Bind(flow.Catalog);
            var inventory=new InventorySystem();inventory.AddWeapon(WeaponLootTable.Find("rust_sword"));int callbacks=0;bool failProjection=true;
            Action<EconomyReceipt> project=receipt=>
            {
                callbacks++;
                foreach(var item in receipt.grantedItems??Array.Empty<OwnedItem>())
                    if(!inventory.TryAddConfirmed(item.contentId,item.kind=="weapon"?InventoryEntryKind.Weapon:InventoryEntryKind.Accessory,item.instanceId))throw new InvalidOperationException("fixture inventory full");
                if(failProjection){failProjection=false;throw new InvalidOperationException("intentional isolated projection failure after idempotent add");}
            };
            flow.EconomyCommitted+=project;
            var request=new RewardRequest{operationId=Guid.NewGuid().ToString(),eventId="isolated-projection-item",source="pickup",stageIndex=0,roomId="s0-r01",contentId="ruby_heart"};
            Check("committed receipt remains pending when local projection throws",await flow.RewardAsync(request)==null&&flow.HasPending&&flow.ErrorCode=="projection_failed");
            var committedRevision=flow.Economy.revision;var runId=flow.Run.runId;
            Check("first attempt partially applies only one instance",inventory.OwnedAccessories.Count==1&&callbacks==1);
            flow.Dispose();flow=new BackendFlowCoordinator(new SaveSlotService(saves),options);flow.EconomyCommitted+=project;
            Check("unprojected receipt survives coordinator recreation",await flow.ConnectAsync()&&await flow.SelectSlotAsync(1));
            Check("retry uses same receipt without duplicate server or local item",flow.Run.runId==runId&&flow.Economy.revision==committedRevision&&inventory.OwnedAccessories.Count==1&&callbacks==2&&!flow.HasPending);
            Check("explicit repeat returns receipt without a third callback",await flow.RewardAsync(request)!=null&&callbacks==2&&flow.Economy.revision==committedRevision);
            var originalProjection=project;flow.EconomyCommitted-=originalProjection;
            int appliedHeals=0;bool failAfterHeal=true;var seen=new HashSet<string>();
            var healingStats=new PlayerStats();healingStats.ReceiveDamage(30);
            flow.EconomyCommitted+=receipt=>
            {
                if(!seen.Add(receipt.operationId))return;
                if(receipt.healAmount>0){healingStats.Heal(receipt.healAmount);appliedHeals++;if(failAfterHeal){failAfterHeal=false;throw new InvalidOperationException("intentional view failure after heal acknowledgement");}}
            };
            for(int i=0;i<3;i++)Check("isolated heal funding "+i,await flow.RewardAsync(new RewardRequest{operationId=Guid.NewGuid().ToString(),eventId="isolated-funding-"+i,source="boss",stageIndex=0,roomId="s0-r01"})!=null);
            var shop=flow.Economy.shops[0];var offer=shop.offers.First(x=>x.kind=="heal");
            var trade=new TradeRequest{operationId=Guid.NewGuid().ToString(),action="buy",shopId=shop.shopId,offerId=offer.offerId};
            Check("post-heal feedback failure leaves projection pending",await flow.TradeAsync(trade)==null&&flow.HasPending&&appliedHeals==1);
            var healed=healingStats.Health;var healRevision=flow.Economy.revision;
            Check("healing receipt retry preserves one-time resource mutation",await flow.RetryAsync()&&healingStats.Health==healed&&appliedHeals==1&&flow.Economy.revision==healRevision&&!flow.HasPending);
            healingStats.Dispose();
            Check("isolated expedition safely settles",await flow.SettleAsync("abandoned",0,0,0));
            Write(report,new{status="PASS",checks,normalGameplay=false,realUnity=true,realHttp=true,scope="supplied numeric binding and failed local receipt projection recovery"});
        }
        catch(Exception e){Write(report,new{status="FAIL",checks,message=e.ToString(),code=flow?.ErrorCode,normalGameplay=false});Debug.LogError("[Goal Catalog Recovery] "+e.Message);}
        finally
        {
            flow?.Dispose();if(oldPlayer!=null)UnityEngine.Object.DestroyImmediate(oldPlayer.gameObject);if(newPlayer!=null)UnityEngine.Object.DestroyImmediate(newPlayer.gameObject);
            Bind(restore);Time.timeScale=previousScale;if(previousScene.IsValid())SceneManager.SetActiveScene(previousScene);if(scene.IsValid())_ = SceneManager.UnloadSceneAsync(scene);
        }
    }
    static void Write(string path,object value){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonConvert.SerializeObject(value,Formatting.Indented),new UTF8Encoding(false));}
}
