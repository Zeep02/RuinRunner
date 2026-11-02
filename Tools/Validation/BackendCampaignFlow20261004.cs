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

// Root executes Main on Unity main thread after BackendClientProxy.ps1 -Campaign.
// Uses the production normal-flow coordinator, isolated save + CurrentUser DPAPI directories.
public static class BackendCampaignFlow20261004
{
    public static string Main()
    {
        string root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        string id=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6);
        string report=Path.Combine(root,"Backend/Validation/unity-campaign-flow-"+id+".json");
        _=Run(root,id,report);return "Async production flow fixture started: "+report;
    }
    static async Task Run(string root,string id,string report)
    {
        var checks=new List<string>();
        void Check(string message,bool pass){if(!pass)throw new InvalidOperationException(message);checks.Add(message);}
        var directory=Path.Combine(root,"Backend/.local/private/unity-campaign-"+id);
        var saves=Path.Combine(directory,"Saves");Directory.CreateDirectory(saves);
        var original=Path.Combine(saves,"save1.json");
        var legacy=JsonConvert.SerializeObject(new {version=1,slotNumber=1,createdUtc="",lastPlayedUtc="",totalRuns=2,victories=1,campaignVictories=1,bestCampaignStage=3,deepestRoom=12,totalKills=60,legacyShards=20,upgrades=new[]{new{type=0,rank=1}}});
        File.WriteAllText(original,legacy,new UTF8Encoding(true));File.WriteAllText(original+".bak",legacy,new UTF8Encoding(false));
        var originalBytes=File.ReadAllBytes(original);var backupBytes=File.ReadAllBytes(original+".bak");
        var options=new BackendClientOptions {ApiBaseUri="http://127.0.0.1:5187/",StorageDirectory=Path.Combine(directory,"Recovery"),TimeoutSeconds=3};
        BackendFlowCoordinator flow=null;
        bool unchanged=false;
        try
        {
            var slots=new SaveSlotService(saves);flow=new BackendFlowCoordinator(slots,options);
            Check("production coordinator connects",await flow.ConnectAsync());
            Check("lost committed profile-create response reported pending",!await flow.SelectSlotAsync(1) && flow.HasPending);
            flow.Dispose();slots=new SaveSlotService(saves);flow=new BackendFlowCoordinator(slots,options);
            Check("protected guest and pending profile command survive coordinator restart",await flow.ConnectAsync());
            Check("normal slot selection imports legacy with one refund",await flow.SelectSlotAsync(1) && flow.Profile.legacyShards==23 && flow.Profile.state.legacyRefundVersion==1);
            var profileId=flow.Profile.profileId;
            Check("save service is server projection with zero old ranks",slots.ServerBound && slots.ActiveData.serverProfileId==profileId && slots.ActiveData.upgrades.Count==0);
            Check("new server slot cannot overwrite or delete legacy file",!slots.DeleteSlot(1) && !slots.OpenSlot(1));
            Check("free defaults and published full catalog loaded",flow.Profile.state.activeTalentIds.Length==3 && flow.Catalog.items.Length==76);
            Check("unlock uses server cost",await flow.UnlockAsync("weapon","worn_dagger") && flow.Profile.legacyShards==20);
            Check("town configuration awaits committed revision",await flow.ConfigureAsync("worn_dagger",new[]{"combo_memory","survey_next","chest_intent"}));
            Check("normal run start freezes seed version and loadout",await flow.StartAsync(20261004,"modules-v1.0.0","unity-campaign-fixture") && flow.Run.seed==20261004 && flow.Run.loadout.startingWeaponId=="worn_dagger");
            var runId=flow.Run.runId;int events=0;flow.EconomyCommitted+=_=>events++;
            var reward=new RewardRequest {operationId=Guid.NewGuid().ToString(),eventId="fixture-enemy-1",source="enemy",stageIndex=0,roomId="s0-r01"};
            Check("lost reward response never reports local success",await flow.RewardAsync(reward)==null && flow.HasPending && flow.Economy.gold==0);
            flow.Dispose();slots=new SaveSlotService(saves);flow=new BackendFlowCoordinator(slots,options);flow.EconomyCommitted+=_=>events++;
            Check("outbox replay after coordinator restart",await flow.ConnectAsync() && await flow.SelectSlotAsync(1));
            Check("same run recovered and reward applied once",flow.Profile.profileId==profileId && flow.Run.runId==runId && flow.Economy.gold==2 && events==1);
            Check("same reward UUID replay does not duplicate callback or currency",await flow.RewardAsync(reward)!=null && events==1 && flow.Economy.gold==2);
            for(int n=0;n<4;n++)Check("funding reward "+n,await flow.RewardAsync(new RewardRequest {operationId=Guid.NewGuid().ToString(),eventId="fixture-boss-"+n,source="boss",stageIndex=0,roomId="s0-r02"})!=null);
            var offer=flow.Economy.shops[0].offers.First(x=>x.kind!="heal");long before=flow.Economy.gold;int eventsBefore=events;
            var trade=new TradeRequest {operationId=Guid.NewGuid().ToString(),action="buy",shopId=flow.Economy.shops[0].shopId,offerId=offer.offerId};
            Check("lost purchase response leaves durable pending intent",await flow.TradeAsync(trade)==null && flow.HasPending);
            Check("purchase retry succeeds via real transaction receipt",await flow.RetryAsync());
            Check("purchase charge item and callback applied once",flow.Economy.gold==before-offer.price && flow.Economy.items.Length==2 && events==eventsBefore+1);
            var replay=await flow.TradeAsync(trade);Check("confirmed purchase replay has same stable instance",replay!=null && replay.grantedItems.Length==1 && flow.Economy.items.Any(x=>x.instanceId==replay.grantedItems[0].instanceId) && events==eventsBefore+1);
            long legacyBefore=flow.Profile.legacyShards;
            Check("lost settlement response remains pending",!await flow.SettleAsync("victory",60,12,3) && flow.HasPending);
            Check("settlement command can be retried safely",await flow.RetryAsync() && await flow.SettleAsync("victory",60,12,3));
            Check("single permanent reward and settled state",flow.Profile.legacyShards==legacyBefore+33 && flow.LastReward==33 && flow.Run.status=="victory");
            unchanged=File.ReadAllBytes(original).SequenceEqual(originalBytes)&&File.ReadAllBytes(original+".bak").SequenceEqual(backupBytes);
            Check("original legacy and backup byte-identical after all normal flow",unchanged);
            await Save(report,new{status="PASS",checks,originalSavesUnchanged=unchanged,realUnityWebRequest=true,productionFlowCoordinator=true,gameSceneInteraction=false});
            Debug.Log("[Backend Campaign] PASS "+checks.Count+" checks; "+report);
        }
        catch(Exception e)
        {
            unchanged=File.ReadAllBytes(original).SequenceEqual(originalBytes)&&File.ReadAllBytes(original+".bak").SequenceEqual(backupBytes);
            await Save(report,new{status="FAIL",checks,code=flow?.ErrorCode,message=e.Message,originalSavesUnchanged=unchanged});
            Debug.LogError("[Backend Campaign] FAIL "+e.Message+"; "+report);
        }
        finally {flow?.Dispose();}
    }
    static Task Save(string path,object report)=>Task.Run(()=>{Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonConvert.SerializeObject(report,Formatting.Indented),new UTF8Encoding(false));});
}
