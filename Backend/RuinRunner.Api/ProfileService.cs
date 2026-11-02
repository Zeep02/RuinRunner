using Npgsql;
using System.Text.Json;

namespace RuinRunner.Api;

public sealed class ProfileService(Database db, CatalogStore catalogs)
{
    public static async Task<ProfileView> Load(NpgsqlConnection c,Guid player,Guid id)
    {
        await using var cmd=Database.Command(c,"SELECT id,player_id,slot,name,revision,legacy_shards,state::text FROM profiles WHERE id=$1 AND player_id=$2",id,player);
        await using var reader=await cmd.ExecuteReaderAsync();
        if(!await reader.ReadAsync()) throw new ApiFailure(404,"profile_missing","프로필을 찾을 수 없습니다.");
        return Read(reader);
    }
    static ProfileView Read(NpgsqlDataReader r)=>new(r.GetGuid(0),r.GetGuid(1),r.GetInt32(2),r.GetString(3),r.GetInt64(4),r.GetInt64(5),Wire.Decode<ProfileState>(r.GetString(6)));
    public async Task<object> List(Guid player)
    {
        await using var c=await db.Source.OpenConnectionAsync();
        await using var cmd=Database.Command(c,"SELECT id,player_id,slot,name,revision,legacy_shards,state::text FROM profiles WHERE player_id=$1 ORDER BY slot",player);
        await using var r=await cmd.ExecuteReaderAsync(); var items=new List<ProfileView>(); while(await r.ReadAsync()) items.Add(Read(r));
        return new { profiles=items };
    }
    public async Task<ProfileView> Get(Guid player,Guid id)
    { await using var c=await db.Source.OpenConnectionAsync(); return await Load(c,player,id); }
    static void Revision(ProfileView p,long expected)=>Wire.Require(p.Revision==expected,"revision_conflict","다른 변경이 저장되었습니다. 새로 불러오세요.",409);
    static async Task InTown(NpgsqlConnection c,Guid profile)=>Wire.Require(await Database.Scalar(c,"SELECT 1 FROM runs WHERE profile_id=$1 AND status='active'",profile)==null,"run_active","원정 중에는 시작 구성을 변경할 수 없습니다.",409);
    public static async Task<ProfileView> Save(NpgsqlConnection c,ProfileView p)
    {
        await Database.Exec(c,"UPDATE profiles SET state=$1::jsonb,legacy_shards=$2,revision=revision+1,updated_utc=now() WHERE id=$3",Wire.Encode(p.State),p.LegacyShards,p.ProfileId);
        return p with { Revision=p.Revision+1 };
    }
    public static Task Ledger(NpgsqlConnection c,ProfileView p,Guid op,string reason,long delta)
        =>Database.Exec(c,"INSERT INTO currency_ledger(id,profile_id,operation_id,reason,delta,balance_after) VALUES($1,$2,$3,$4,$5,$6)",Guid.NewGuid(),p.ProfileId,op,reason,delta,p.LegacyShards);
    static ProfileState InitialState(Catalog catalog)
    {
        var state=new ProfileState();
        state.UnlockedWeaponIds=catalog.StarterWeapons.Where(w=>w.UnlockCost==0).Select(w=>w.Id).ToArray();
        state.UnlockedTalentIds=catalog.DefaultUnlockedTalentIds??[];
        state.ActiveTalentIds=state.UnlockedTalentIds.Take(catalog.MaxActiveTalents).ToArray();
        Wire.Require(state.UnlockedWeaponIds.Length>0,"catalog_invalid","시작 장비가 없습니다.");
        state.StartingWeaponId=state.UnlockedWeaponIds[0];
        return state;
    }
    public Task<string> Create(Guid player,CreateProfileRequest request)=>db.Mutate(player,request.OperationId,"profile.create",request,async c=>
    {
        Wire.Require(request.Slot is >=1 and <=3,"slot_invalid","저장 슬롯은 1~3입니다.");
        var name=request.Name?.Trim()??"";
        Wire.Require(name.Length is >0 and <=32 && !name.Any(char.IsControl),"name_invalid","이름은 1~32자로 입력하세요.");
        Wire.Require(await Database.Scalar(c,"SELECT 1 FROM profiles WHERE player_id=$1 AND slot=$2",player,request.Slot)==null,"slot_exists","이미 사용 중인 슬롯입니다.",409);
        var catalog=await catalogs.Get(c,catalogs.CurrentVersion); var state=InitialState(catalog);
        var p=new ProfileView(Guid.NewGuid(),player,request.Slot,name,0,catalog.StartingLegacy,state);
        await Database.Exec(c,"INSERT INTO profiles(id,player_id,slot,name,legacy_shards,state) VALUES($1,$2,$3,$4,$5,$6::jsonb)",p.ProfileId,player,p.Slot,p.Name,p.LegacyShards,Wire.Encode(state));
        await Ledger(c,p,request.OperationId,"profile.initial",p.LegacyShards); return p;
    });
    public Task<string> BeginReset(Guid player,Guid id,BeginProfileResetRequest request)=>db.Mutate(player,request.OperationId,"profile.reset-challenge",new{id,request},async c=>
    {
        var p=await Load(c,player,id); Revision(p,request.ExpectedRevision); await InTown(c,id);
        var challenge=new ProfileResetChallenge(Guid.NewGuid(),id,p.Slot,p.Name,p.Revision,DateTimeOffset.UtcNow.AddMinutes(5));
        await Database.Exec(c,"INSERT INTO profile_reset_challenges(id,profile_id,player_id,expected_revision,expires_utc) VALUES($1,$2,$3,$4,$5)",challenge.ChallengeId,id,player,p.Revision,challenge.ExpiresUtc);
        return challenge;
    });
    public Task<string> ConfirmReset(Guid player,Guid id,ConfirmProfileResetRequest request)=>db.Mutate(player,request.OperationId,"profile.reset",new{id,request},async c=>
    {
        var p=await Load(c,player,id); Revision(p,request.ExpectedRevision); await InTown(c,id);
        Wire.Require(request.ChallengeId!=Guid.Empty,"reset_confirmation_required","초기화 내용을 먼저 확인하세요.");
        await using(var command=Database.Command(c,"SELECT expected_revision,expires_utc,consumed_utc FROM profile_reset_challenges WHERE id=$1 AND profile_id=$2 AND player_id=$3",request.ChallengeId,id,player))
        await using(var reader=await command.ExecuteReaderAsync())
        {
            Wire.Require(await reader.ReadAsync(),"reset_challenge_missing","초기화 확인을 다시 시작하세요.",409);
            Wire.Require(reader.GetInt64(0)==p.Revision,"revision_conflict","프로필이 변경되었습니다. 초기화 내용을 다시 확인하세요.",409);
            Wire.Require(reader.IsDBNull(2),"reset_challenge_used","이미 사용한 초기화 확인입니다.",409);
            Wire.Require(reader.GetDateTime(1)>DateTime.UtcNow,"reset_challenge_expired","초기화 확인 시간이 지났습니다. 다시 확인하세요.",409);
        }
        var catalog=await catalogs.Get(c,catalogs.CurrentVersion); var state=InitialState(catalog);
        state.LegacyImportBlocked=true; state.ResetCount=checked(p.State.ResetCount+1);
        state.LegacyMigrationVersion=p.State.LegacyMigrationVersion; state.LegacyRefundVersion=p.State.LegacyRefundVersion;
        state.TrustOrigin="server-reset";
        var reset=await Save(c,p with {LegacyShards=catalog.StartingLegacy,State=state});
        await Database.Exec(c,"UPDATE profile_reset_challenges SET consumed_utc=now() WHERE id=$1",request.ChallengeId);
        await Database.Exec(c,"INSERT INTO profile_resets(id,profile_id,player_id,operation_id,previous_revision,resulting_revision,previous_profile) VALUES($1,$2,$3,$4,$5,$6,$7::jsonb)",request.ChallengeId,id,player,request.OperationId,p.Revision,reset.Revision,Wire.Encode(p));
        await Ledger(c,reset,request.OperationId,"profile.reset",reset.LegacyShards-p.LegacyShards);
        return new ProfileResetReceipt(reset,request.ChallengeId,request.OperationId);
    });
    public Task<string> Configure(Guid player,Guid id,ConfigurationRequest request)=>db.Mutate(player,request.OperationId,"profile.configure",new{id,request},async c=>
    {
        var p=await Load(c,player,id); Revision(p,request.ExpectedRevision); await InTown(c,id);
        var catalog=await catalogs.Get(c,catalogs.CurrentVersion);
        Wire.Require(p.State.UnlockedWeaponIds.Contains(request.StartingWeaponId) && catalog.StarterWeapons.Any(x=>x.Id==request.StartingWeaponId),"weapon_locked","아직 해금하지 않은 시작 장비입니다.");
        var talents=request.ActiveTalentIds??[];
        Wire.Require(talents.Length<=catalog.MaxActiveTalents && talents.Distinct().Count()==talents.Length,"talent_limit","선택할 수 있는 재능 수를 확인하세요.");
        Wire.Require(talents.All(t=>p.State.UnlockedTalentIds.Contains(t) && catalog.Talents.Any(x=>x.Id==t)),"talent_locked","아직 해금하지 않은 재능입니다.");
        Wire.Require(talents.Select(t=>catalog.Talents.Single(x=>x.Id==t).Branch).Distinct().Count()==talents.Length,"talent_branch","한 계열에서 재능 하나만 선택하세요.");
        p.State.StartingWeaponId=request.StartingWeaponId; p.State.ActiveTalentIds=talents;
        return await Save(c,p);
    });
    public Task<string> Unlock(Guid player,Guid id,UnlockRequest request)=>db.Mutate(player,request.OperationId,"profile.unlock",new{id,request},async c=>
    {
        var p=await Load(c,player,id); Revision(p,request.ExpectedRevision); await InTown(c,id);
        Wire.Require(request.ContentVersion==catalogs.CurrentVersion,"catalog_stale","새 가격을 확인하세요.",409);
        var catalog=await catalogs.Get(c,request.ContentVersion); int cost;
        if(request.Kind=="weapon")
        {
            var definition=catalog.StarterWeapons.SingleOrDefault(w=>w.Id==request.ContentId)??throw new ApiFailure(404,"content_missing","장비를 찾을 수 없습니다."); cost=definition.UnlockCost;
            Wire.Require(!p.State.UnlockedWeaponIds.Contains(request.ContentId),"already_unlocked","이미 해금되었습니다.",409);
            p.State.UnlockedWeaponIds=[..p.State.UnlockedWeaponIds,request.ContentId];
        }
        else if(request.Kind=="talent")
        {
            var definition=catalog.Talents.SingleOrDefault(t=>t.Id==request.ContentId)??throw new ApiFailure(404,"content_missing","재능을 찾을 수 없습니다."); cost=definition.UnlockCost;
            Wire.Require(p.State.TotalRuns>=definition.RequiredRuns,"achievement_required","먼저 원정을 완료하세요.",409);
            Wire.Require(!p.State.UnlockedTalentIds.Contains(request.ContentId),"already_unlocked","이미 해금되었습니다.",409);
            p.State.UnlockedTalentIds=[..p.State.UnlockedTalentIds,request.ContentId];
        }
        else throw new ApiFailure(400,"unlock_kind","해금 종류를 확인하세요.");
        Wire.Require(cost>=0 && p.LegacyShards>=cost,"insufficient_legacy","유산이 부족합니다.",409);
        p=p with {LegacyShards=p.LegacyShards-cost}; p=await Save(c,p); await Ledger(c,p,request.OperationId,"unlock."+request.Kind,-cost); return p;
    });
    public Task<string> Import(Guid player,Guid id,ImportRequest request)=>db.Mutate(player,request.OperationId,"profile.legacy-import",new{id,request},async c=>
    {
        var p=await Load(c,player,id); Revision(p,request.ExpectedRevision); await InTown(c,id);
        Wire.Require(request.MigrationVersion==1,"migration_version","지원하지 않는 이관 버전입니다.");
        Wire.Require(request.SourceJson!=null && request.SourceJson.Length<=32768 && Database.Hash(request.SourceJson)==request.SourceSha256?.ToLowerInvariant(),"source_hash","원본 저장 파일이 달라졌습니다.");
        Wire.Require(!p.State.LegacyImportBlocked && p.Revision==0 && p.State.TotalRuns==0 && p.State.LegacyMigrationVersion==0,"import_conflict","진행이 있거나 초기화한 서버 슬롯에는 기존 기록을 덮어쓸 수 없습니다.",409);
        Wire.Require(await Database.Scalar(c,"SELECT 1 FROM legacy_imports WHERE player_id=$1 AND source_sha256=$2 AND migration_version=$3",player,request.SourceSha256,request.MigrationVersion)==null,"already_imported","이미 이관한 저장입니다.",409);
        LegacySnapshot old;
        try {old=Wire.Decode<LegacySnapshot>(request.SourceJson!);} catch(JsonException){throw new ApiFailure(400,"legacy_invalid","기존 저장 형식을 확인하세요.");}
        Wire.Require(old.Version==1 && old.SlotNumber==p.Slot,"legacy_slot","기존 저장 슬롯과 버전을 확인하세요.");
        Wire.Require(old.TotalRuns>=0 && old.Victories>=0 && old.Victories<=old.TotalRuns && old.CampaignVictories>=0 && old.CampaignVictories<=old.Victories && old.BestCampaignStage is >=0 and <=3 && old.DeepestRoom>=0 && old.TotalKills>=0 && old.LegacyShards is >=0 and <=1000000000,"legacy_range","기존 저장의 값이 유효하지 않습니다.");
        long refund=0; var ranks=old.Upgrades??[];
        Wire.Require(ranks.All(r=>r!=null),"legacy_rank","비어 있는 기존 재능 기록이 있습니다.");
        Wire.Require(ranks.Select(r=>r.Type).Distinct().Count()==ranks.Length,"legacy_duplicates","중복된 기존 재능 기록이 있습니다.");
        foreach(var rank in ranks)
        {
            Wire.Require(rank.Type is >=0 and <=6 && rank.Rank>=0 && rank.Rank<=(rank.Type==6?3:5),"legacy_rank","기존 재능 기록을 확인하세요.");
            var baseCost=rank.Type<2?3:4; refund+=rank.Rank*baseCost+rank.Rank*(rank.Rank-1);
        }
        p.State.TotalRuns=old.TotalRuns; p.State.Victories=old.Victories; p.State.CampaignVictories=old.CampaignVictories;
        p.State.BestCampaignStage=old.BestCampaignStage; p.State.DeepestRoom=old.DeepestRoom; p.State.TotalKills=old.TotalKills;
        p.State.TrustOrigin="legacy-local-untrusted"; p.State.LegacyMigrationVersion=1; p.State.LegacyRefundVersion=1;
        var previous=p.LegacyShards; p=p with { LegacyShards=old.LegacyShards+refund }; p=await Save(c,p);
        await Database.Exec(c,"INSERT INTO legacy_imports(profile_id,player_id,source_sha256,migration_version,refund,original_snapshot) VALUES($1,$2,$3,$4,$5,$6::jsonb)",id,player,request.SourceSha256!.ToLowerInvariant(),1,refund,request.SourceJson);
        await Ledger(c,p,request.OperationId,"legacy.import-and-refund",p.LegacyShards-previous);
        return new {profile=p,refundedLegacy=refund,migrationVersion=1,sourceSha256=request.SourceSha256,trustOrigin="legacy-local-untrusted"};
    });
}
