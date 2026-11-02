using Npgsql;
using RuinRunner.Generation;

namespace RuinRunner.Api;

public sealed class RunService(Database db,CatalogStore catalogs)
{
    public static async Task<RunView> Load(NpgsqlConnection c,Guid player,Guid id)
    {
        await using var cmd=Database.Command(c,"SELECT r.id,r.profile_id,r.revision,r.status,r.seed,r.content_version,r.generator_version,r.build_id,r.loadout::text,r.legacy_reward,r.trust_origin,g.stage_signatures::text,g.candidate_attempts,g.repeated_recent_structure,g.selection_mode FROM runs r JOIN profiles p ON p.id=r.profile_id LEFT JOIN run_generation g ON g.run_id=r.id WHERE r.id=$1 AND p.player_id=$2",id,player);
        await using var r=await cmd.ExecuteReaderAsync(); if(!await r.ReadAsync()) throw new ApiFailure(404,"run_missing","원정을 찾을 수 없습니다.");
        return new(r.GetGuid(0),r.GetGuid(1),r.GetInt64(2),r.GetString(3),r.GetInt32(4),r.GetString(5),r.GetString(6),r.GetString(7),Wire.Decode<ProfileState>(r.GetString(8)),r.GetInt64(9),r.GetString(10),
            r.IsDBNull(11)?null:new(Wire.Decode<string[]>(r.GetString(11)),r.GetInt32(12),r.GetBoolean(13),r.GetString(14)));
    }
    public async Task<RunView> Get(Guid player,Guid id)
    {await using var c=await db.Source.OpenConnectionAsync();return await Load(c,player,id);}
    public async Task<object> Active(Guid player,Guid profileId)
    {
        await using var c=await db.Source.OpenConnectionAsync(); await ProfileService.Load(c,player,profileId);
        var id=await Database.Scalar(c,"SELECT id FROM runs WHERE profile_id=$1 AND status='active'",profileId);
        return new {run=id is Guid runId ? await Load(c,player,runId) : null};
    }
    public Task<string> Start(Guid player,StartRunRequest request)=>db.Mutate(player,request.OperationId,"run.start",request,async c=>
    {
        var p=await ProfileService.Load(c,player,request.ProfileId);
        Wire.Require(p.Revision==request.ExpectedProfileRevision,"revision_conflict","구성이 변경되었습니다. 새로 불러오세요.",409);
        Wire.Require(await Database.Scalar(c,"SELECT 1 FROM runs WHERE profile_id=$1 AND status='active'",p.ProfileId)==null,"run_active","이미 진행 중인 원정이 있습니다.",409);
        Wire.Require(request.ContentVersion==catalogs.CurrentVersion,"catalog_stale","현재 콘텐츠 버전으로 출정하세요.",409);
        var catalog=await catalogs.Get(c,request.ContentVersion);
        Wire.Require(catalog.StarterWeapons.Any(x=>x.Id==p.State.StartingWeaponId) && p.State.UnlockedWeaponIds.Contains(p.State.StartingWeaponId),"loadout_invalid","시작 장비 구성을 다시 확인하세요.",409);
        Wire.Require(p.State.ActiveTalentIds.Length<=catalog.MaxActiveTalents && p.State.ActiveTalentIds.All(t=>p.State.UnlockedTalentIds.Contains(t) && catalog.Talents.Any(x=>x.Id==t)),"loadout_invalid","재능 구성을 다시 확인하세요.",409);
        Wire.Require(catalog.GeneratorVersions.Contains(request.GeneratorVersion),"generator_unsupported","지원하지 않는 지형 생성 버전입니다.",409);
        Wire.Require(request.GeneratorVersion==ProceduralStageGenerator.Version,"generator_unsupported","서버와 지형 생성 버전이 일치하지 않습니다.",409);
        Wire.Require(!string.IsNullOrWhiteSpace(request.BuildId) && request.BuildId.Length<=128,"build_id","빌드 식별자가 필요합니다.");
        var selected=RunSeedSelection.Choose(request.RequestedSeed,await RunSeedSelection.ReadRecent(c,p.ProfileId));
        var seed=selected.Seed;
        var run=new RunView(Guid.NewGuid(),p.ProfileId,0,"active",seed,request.ContentVersion,request.GeneratorVersion,request.BuildId,p.State,0,"client-reported-singleplayer",selected.Generation);
        await Database.Exec(c,"INSERT INTO runs(id,profile_id,status,seed,content_version,generator_version,build_id,loadout) VALUES($1,$2,'active',$3,$4,$5,$6,$7::jsonb)",run.RunId,p.ProfileId,seed,run.ContentVersion,run.GeneratorVersion,run.BuildId,Wire.Encode(run.Loadout));
        await RunSeedSelection.Persist(c,run.RunId,selected.Generation);
        await EconomyService.Initialize(c,run,catalog);
        return run;
    });
    public Task<string> Settle(Guid player,Guid id,SettleRequest request)=>db.Mutate(player,request.OperationId,"run.settle",new{id,request},async c=>
    {
        var run=await Load(c,player,id);
        Wire.Require(run.Revision==request.ExpectedRunRevision,"revision_conflict","원정 기록이 변경되었습니다.",409);
        Wire.Require(run.Status=="active","run_settled","이미 정산한 원정입니다.",409);
        Wire.Require(request.Outcome is "victory" or "defeat" or "abandoned","outcome_invalid","원정 결과를 확인하세요.");
        var catalog=await catalogs.Get(c,run.ContentVersion); var rules=catalog.Settlement;
        Wire.Require(request.EnemiesDefeated>=0 && request.EnemiesDefeated<=rules.MaxKills && request.RoomsReached>=0 && request.RoomsReached<=rules.MaxRooms && request.StagesCleared>=0 && request.StagesCleared<=rules.CampaignStages,"result_range","원정 기록의 범위를 확인하세요.");
        Wire.Require(request.Outcome!="victory" || request.StagesCleared==rules.CampaignStages,"victory_incomplete","모든 지역을 완료한 뒤 승리 정산할 수 있습니다.");
        // An abandoned zero-progress run never farms the historical minimum reward.
        var reward=request.Outcome=="abandoned" || request.EnemiesDefeated==0 && request.RoomsReached==0 ? 0 : Math.Max(rules.MinimumReward,request.EnemiesDefeated/rules.KillsPerShard+request.RoomsReached*rules.RewardPerRoom+(request.Outcome=="victory"?rules.VictoryBonus:0));
        var p=await ProfileService.Load(c,player,run.ProfileId); p=p with { LegacyShards=p.LegacyShards+reward };
        p.State.TotalRuns=checked(p.State.TotalRuns+1); p.State.TotalKills=checked(p.State.TotalKills+request.EnemiesDefeated);
        p.State.DeepestRoom=Math.Max(p.State.DeepestRoom,request.RoomsReached); p.State.BestCampaignStage=Math.Max(p.State.BestCampaignStage,request.StagesCleared);
        if(request.Outcome=="victory"){p.State.Victories=checked(p.State.Victories+1);p.State.CampaignVictories=checked(p.State.CampaignVictories+1);}
        p=await ProfileService.Save(c,p); await ProfileService.Ledger(c,p,request.OperationId,"run.settlement",reward);
        await Database.Exec(c,"UPDATE runs SET revision=revision+1,status=$1,settled_utc=now(),result=$2::jsonb,legacy_reward=$3 WHERE id=$4",request.Outcome,Wire.Encode(request),(long)reward,id);
        return new {run=run with {Revision=run.Revision+1,Status=request.Outcome,LegacyReward=reward},profile=p};
    });
}
