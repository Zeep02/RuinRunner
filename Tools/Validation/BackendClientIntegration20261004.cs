using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RuinRunner.Backend;
using UnityEngine;

// Run from Unity's main thread using run_script. No existing scene, user save or gameplay is modified.
public static class BackendClientIntegration20261004
{
    public static string Main()
    {
        var root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        var id=DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6);
        var report=Path.Combine(root,"Backend/Validation/unity-client-"+id+".json");
        _=Run(root,id,report);
        return "Started async UnityWebRequest fixture; result: "+report;
    }
    static async Task Run(string root,string id,string report)
    {
        var checks=new List<string>();
        void Check(string name,bool okay){if(!okay)throw new InvalidOperationException(name);checks.Add(name);}
        var storage=Path.Combine(root,"Backend/.local/private/unity-client-"+id);
        var options=new BackendClientOptions{ApiBaseUri="http://127.0.0.1:5187/",StorageDirectory=storage,TimeoutSeconds=3};
        RuinBackendClient client=null;
        try
        {
            client=new RuinBackendClient(options);await client.ConnectAsync();var owner=client.PlayerId;
            Check("real guest connection",!string.IsNullOrEmpty(owner));
            var create=new CreateProfileRequest{operationId=Guid.NewGuid().ToString(),slot=1,name="Unity 연결 검증"};
            bool lost=false;
            try{await client.CreateProfileAsync(create);}catch(BackendClientException e){lost=e.OutcomeUnknown;}
            Check("committed create response was lost",lost);
            Check("pending request durable with same UUID",(await client.GetPendingOperationsOfflineAsync()).Single().operationId==create.operationId);
            client.Dispose();client=new RuinBackendClient(options);await client.ConnectAsync();
            Check("same protected credential restores same account",client.PlayerId==owner);
            var profile=await client.RetryOperationAsync<ProfileView>(create.operationId);
            Check("lost create response retry creates one slot",(await client.GetProfilesAsync()).profiles.Length==1 && profile.slot==1);
            Check("confirmed operation removed from pending",(await client.GetPendingOperationsOfflineAsync()).Length==0);
            // Invalid old session exercises the real HTTP 401 -> credential renewal path without exposing a token.
            var session=(GuestSession)typeof(RuinBackendClient).GetField("session",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(client);
            session.accessToken=new string('A',43);
            Check("HTTP 401 renews guest session automatically",(await client.GetProfileAsync(profile.profileId)).playerId==owner);
            var oldPath=Path.Combine(storage,"isolated-save.json");
            var old=JsonConvert.SerializeObject(new{version=1,slotNumber=1,createdUtc="2026-09-01T00:00:00Z",lastPlayedUtc="2026-10-01T00:00:00Z",totalRuns=2,victories=1,campaignVictories=1,bestCampaignStage=3,deepestRoom=12,totalKills=60,legacyShards=20,upgrades=new[]{new{type=0,rank=1}}});
            await Task.Run(()=>{Directory.CreateDirectory(storage);File.WriteAllText(oldPath,old,new UTF8Encoding(true));File.WriteAllText(oldPath+".bak",old,new UTF8Encoding(false));});
            var backup=await client.BackupLegacyAsync(oldPath);var restored=Path.Combine(storage,"restored-copy.json");await client.RestoreLegacyBackupToNewFileAsync(backup,restored);
            Check("original UTF8/BOM and backup untouched",await Task.Run(()=>File.ReadAllBytes(oldPath).SequenceEqual(File.ReadAllBytes(restored)) && File.Exists(oldPath+".bak")));
            var import=new LegacyImportRequest{expectedRevision=profile.revision,sourceSha256=backup.importSha256,sourceJson=backup.sourceJson};
            var receipt=await client.ImportLegacyAsync(profile.profileId,import);profile=receipt.profile;
            Check("server import and old-rank refund",receipt.refundedLegacy==3 && profile.legacyShards==23 && profile.state.trustOrigin=="legacy-local-untrusted");
            Check("same import receipt replay",(await client.ImportLegacyAsync(profile.profileId,import)).profile.revision==profile.revision);
            var unlock=new UnlockRequest{expectedRevision=profile.revision,kind="weapon",contentId="worn_dagger",contentVersion="integration-v1"};
            profile=await client.UnlockAsync(profile.profileId,unlock);
            Check("server-owned unlock price",profile.legacyShards==18 && profile.state.unlockedWeaponIds.Contains("worn_dagger"));
            var configure=new ConfigurationRequest{expectedRevision=profile.revision,startingWeaponId="worn_dagger"};profile=await client.ConfigureAsync(profile.profileId,configure);
            bool conflict=false;try{await client.ConfigureAsync(profile.profileId,new ConfigurationRequest{expectedRevision=0,startingWeaponId="rust_sword"});}catch(BackendClientException e){conflict=e.StatusCode==409&&!e.OutcomeUnknown;}
            Check("stale revision surfaces without false success",conflict);
            var run=await client.StartRunAsync(new StartRunRequest{profileId=profile.profileId,expectedProfileRevision=profile.revision,requestedSeed=20261004,contentVersion="integration-v1",generatorVersion="modules-v1.0.0",buildId="unity-backend-fixture-"+id});
            Check("active run can be recovered from owned profile",(await client.GetActiveRunAsync(profile.profileId)).run.runId==run.runId);
            var catalog=await client.GetPinnedCatalogAsync(run);Check("pinned catalog downloaded and cached",catalog.version==run.contentVersion && catalog.sha256.Length==64);
            var settlement=new SettleRunRequest{expectedRunRevision=0,outcome="victory",enemiesDefeated=60,roomsReached=12,stagesCleared=3};
            lost=false;try{await client.SettleRunAsync(run.runId,settlement);}catch(BackendClientException e){lost=e.OutcomeUnknown;}
            Check("committed settlement response was lost",lost);
            Check("settlement pending survives local reload",(await client.GetPendingOperationsOfflineAsync()).Single().operationId==settlement.operationId);
            client.Dispose();client=new RuinBackendClient(options);
            Check("cached pinned catalog can load before authentication",(await client.GetPinnedCatalogAsync(run)).sha256==catalog.sha256);
            var final=await client.RetryOperationAsync<SettlementReceipt>(settlement.operationId);
            Check("settlement retry credits once",final.run.legacyReward==33 && final.profile.legacyShards==51 && final.profile.state.totalRuns==3);
            var current=await client.GetProfileAsync(profile.profileId);Check("fresh server read agrees with committed result",current.revision==final.profile.revision && current.legacyShards==51);
            await client.RetryOperationAsync<ProfileView>(create.operationId);
            Check("old operation replay cannot overwrite confirmed newer cache",(await client.ReadLastConfirmedProfileAsync(profile.profileId)).revision==current.revision);
            Check("settled run has no active slot",(await client.GetActiveRunAsync(profile.profileId)).run==null);
            await SaveReport(report,new{status="PASS",checks,method="actual UnityWebRequest + protected local store + response-loss proxy + PostgreSQL",gameplayIntegration=false});
            Debug.Log("[Backend Client] PASS "+checks.Count+" checks: "+report);
        }
        catch(Exception e)
        {
            var backend=e as BackendClientException;
            await SaveReport(report,new{status="FAIL",checks,code=backend?.Code??e.GetType().Name,message=e.Message});
            Debug.LogError("[Backend Client] FAIL "+(backend?.Code??e.GetType().Name)+": "+e.Message+"; report="+report);
        }
        finally{client?.Dispose();}
    }
    static Task SaveReport(string path,object value)=>Task.Run(()=>{Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonConvert.SerializeObject(value,Formatting.Indented),new UTF8Encoding(false));});
}
