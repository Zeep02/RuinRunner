"""Black-box HTTP integration checks. Only fresh generated test accounts are used."""
import argparse, base64, concurrent.futures, hashlib, json, os, pathlib, subprocess, time, urllib.error, urllib.request, uuid

ROOT=pathlib.Path(__file__).resolve().parents[2]
LOCAL=ROOT/'Backend/.local'
STATE=LOCAL/'private/validation-identity.json'
BASE='http://127.0.0.1:5186'
checks=[]
def uid(): return str(uuid.uuid4())
def secret(): return base64.urlsafe_b64encode(os.urandom(32)).decode().rstrip('=')
def call(method,path,body=None,token=None,status=200):
    headers={'Content-Type':'application/json'}
    if token: headers['Authorization']='Bearer '+token
    req=urllib.request.Request(BASE+path,data=None if body is None else json.dumps(body,ensure_ascii=False).encode(),headers=headers,method=method)
    try:
        with urllib.request.urlopen(req,timeout=20) as r: actual=r.status; value=json.load(r)
    except urllib.error.HTTPError as e:
        actual=e.code
        try: value=json.load(e)
        except Exception: value={}
    assert actual in (status if isinstance(status,tuple) else (status,)), (method,path,actual,status,value.get('code'))
    return value
def check(name,condition=True):
    assert condition,name
    checks.append(name)
def login(identity): return call('POST','/v1/auth/guest',identity)
def new_profile(token,slot,name):
    return call('POST','/v1/profiles',dict(operationId=uid(),slot=slot,name=name),token)
def get_profile(token,p): return call('GET','/v1/profiles/'+p['profileId'],token=token)
def main(phase):
    if phase=='create':
        a={'installationId':uid(),'guestSecret':secret()}; b={'installationId':uid(),'guestSecret':secret()}
        au,bu=login(a),login(b); at,bt=au['accessToken'],bu['accessToken']
        check('two guest identities differ',au['playerId']!=bu['playerId'])
        check('repeat guest login preserves identity',login(a)['playerId']==au['playerId'])
        call('POST','/v1/auth/guest',{**a,'guestSecret':secret()},status=401);check('installation UUID alone cannot authenticate')
        call('GET','/v1/profiles',status=401);check('anonymous profile access denied')
        pa,pb=new_profile(at,1,'백엔드 검증 A'),new_profile(bt,1,'백엔드 검증 B')
        check('same slot isolated by player',pa['profileId']!=pb['profileId'])
        check('list is owner scoped',len(call('GET','/v1/profiles',token=at)['profiles'])==1)
        call('GET','/v1/profiles/'+pa['profileId'],token=bt,status=404);check('other player profile read denied')
        config=dict(operationId=uid(),expectedRevision=0,startingWeaponId='rust_sword',activeTalentIds=[])
        call('PUT','/v1/profiles/'+pa['profileId']+'/configuration',config,bt,status=404);check('other player profile write denied')
        legacy=dict(version=1,slotNumber=1,createdUtc='2026-09-01T00:00:00Z',lastPlayedUtc='2026-10-01T00:00:00Z',totalRuns=3,victories=2,campaignVictories=1,bestCampaignStage=3,deepestRoom=12,totalKills=80,legacyShards=20,upgrades=[dict(type=0,rank=2),dict(type=6,rank=1)])
        source=json.dumps(legacy,ensure_ascii=False,separators=(',',':'))
        imp=dict(operationId=uid(),expectedRevision=0,migrationVersion=1,sourceSha256=hashlib.sha256(source.encode()).hexdigest(),sourceJson=source)
        path='/v1/profiles/'+pa['profileId']+'/legacy-import'
        call('POST',path,{**imp,'sourceSha256':'0'*64},at,status=400);check('import source checksum validated')
        imported=call('POST',path,imp,at);pa=imported['profile']
        check('legacy value preserved and old ranks refunded once',pa['legacyShards']==32 and imported['refundedLegacy']==12)
        check('legacy import marked untrusted',pa['state']['trustOrigin']=='legacy-local-untrusted')
        retry=call('POST',path,imp,at);check('lost import response retry returns exact receipt',retry==imported)
        call('POST',path,{**imp,'operationId':uid(),'expectedRevision':pa['revision']},at,status=409);check('new operation cannot duplicate import/refund')
        call('POST',path,{**imp,'sourceJson':source+' '},at,status=409);check('same operation with different payload rejected')
        purchase=dict(operationId=uid(),expectedRevision=pa['revision'],kind='weapon',contentId='worn_dagger',contentVersion='integration-v1')
        purchase_path='/v1/profiles/'+pa['profileId']+'/unlocks'
        with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
            results=list(pool.map(lambda _:call('POST',purchase_path,purchase,at),range(8)))
        pa=results[0];check('eight concurrent purchase retries charge once',all(x==pa for x in results) and pa['legacyShards']==27)
        call('POST',purchase_path,{**purchase,'operationId':uid(),'expectedRevision':pa['revision']},at,status=409);check('already unlocked item cannot charge again')
        stale=dict(operationId=uid(),expectedRevision=0,startingWeaponId='worn_dagger',activeTalentIds=[])
        call('PUT','/v1/profiles/'+pa['profileId']+'/configuration',stale,at,status=409);check('stale revision cannot overwrite profile')
        config={**stale,'operationId':uid(),'expectedRevision':pa['revision']}
        pa=call('PUT','/v1/profiles/'+pa['profileId']+'/configuration',config,at)
        check('unlocked starting weapon configuration persisted',pa['state']['startingWeaponId']=='worn_dagger')
        start=dict(operationId=uid(),profileId=pa['profileId'],expectedProfileRevision=pa['revision'],requestedSeed=20261004,contentVersion='integration-v1',generatorVersion='modules-v1.0.0',buildId='backend-http-integration-20261004')
        run=call('POST','/v1/runs',start,at)
        check('run freezes seed catalog generator and loadout',run['seed']==20261004 and run['loadout']['startingWeaponId']=='worn_dagger' and run['generatorVersion']=='modules-v1.0.0')
        check('run creation retry returns same ID',call('POST','/v1/runs',start,at)==run)
        call('POST','/v1/runs',{**start,'operationId':uid()},at,status=409);check('one active run per profile enforced')
        call('GET','/v1/runs/'+run['runId'],token=bt,status=404);check('other player run read denied')
        call('PUT','/v1/profiles/'+pa['profileId']+'/configuration',{**config,'operationId':uid(),'expectedRevision':pa['revision']},at,status=409);check('town configuration locked during expedition')
        result=dict(operationId=uid(),expectedRunRevision=0,outcome='victory',enemiesDefeated=60,roomsReached=12,stagesCleared=3)
        settle_path='/v1/runs/'+run['runId']+'/settle'
        call('POST',settle_path,result,bt,status=404);check('other player settlement denied')
        call('POST',settle_path,{**result,'stagesCleared':2},at,status=400);check('partial campaign cannot claim final victory')
        with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
            results=list(pool.map(lambda _:call('POST',settle_path,result,at),range(8)))
        settled=results[0];pa=settled['profile'];run=settled['run']
        check('eight concurrent settlements grant once',all(x==settled for x in results) and run['legacyReward']==33 and pa['legacyShards']==60)
        check('settlement preserves historical counters',pa['state']['totalRuns']==4 and pa['state']['campaignVictories']==2 and pa['state']['totalKills']==140)
        call('POST',settle_path,{**result,'operationId':uid(),'expectedRunRevision':run['revision']},at,status=409);check('new operation cannot re-settle completed run')
        check('unrelated player unchanged',get_profile(bt,pb)==pb)
        STATE.write_text(json.dumps(dict(a=a,b=b,pa=pa,pb=pb,run=run,settlement=result,settlementResponse=settled),ensure_ascii=False),encoding='utf-8')
    elif phase=='restart':
        s=json.loads(STATE.read_text(encoding='utf-8')); auth=login(s['a']);at=auth['accessToken']
        check('API and DB restart preserves identity',auth['playerId']==s['pa']['playerId'])
        check('API and DB restart preserves profile progress',get_profile(at,s['pa'])==s['pa'])
        check('API and DB restart preserves frozen expedition',call('GET','/v1/runs/'+s['run']['runId'],token=at)==s['run'])
        retry=call('POST','/v1/runs/'+s['run']['runId']+'/settle',s['settlement'],at)
        check('receipt replay survives service and DB restart',retry==s['settlementResponse'])
    elif phase=='negative':
        s=json.loads(STATE.read_text(encoding='utf-8'));at=login(s['a'])['accessToken'];bt=login(s['b'])['accessToken'];pa=s['pa'];pb=s['pb']
        call('GET','/v1/profiles',token=secret(),status=401);check('unknown bearer denied')
        # Expiration is induced on this test player's sessions only, not through a production test endpoint.
        env=dict(os.environ,PGPASSFILE=str(LOCAL/'private/pgpass'))
        player=str(uuid.UUID(pa['playerId']))
        sql=f"UPDATE sessions SET expires_utc=now()-interval '1 second' WHERE player_id='{player}'::uuid"
        subprocess.run([str(LOCAL/'postgresql/pgsql/bin/psql.exe'),'-X','-h','127.0.0.1','-p','55432','-U','postgres','-d','ruinrunner','-v','ON_ERROR_STOP=1','-c',sql],env=env,check=True,capture_output=True)
        call('GET','/v1/profiles',token=at,status=401);check('expired bearer denied')
        at=login(s['a'])['accessToken'];check('credential renewal restores same progress',get_profile(at,pa)==pa)
        buy=dict(operationId=uid(),expectedRevision=pb['revision'],kind='weapon',contentId='worn_dagger',contentVersion='integration-v1')
        call('POST','/v1/profiles/'+pb['profileId']+'/unlocks',buy,bt,status=409)
        check('insufficient funds rolls back entire purchase',get_profile(bt,pb)==pb)
        config=dict(expectedRevision=pa['revision'],startingWeaponId='worn_dagger',activeTalentIds=[])
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            replies=list(pool.map(lambda _:call('PUT','/v1/profiles/'+pa['profileId']+'/configuration',dict(config,operationId=uid()),at,status=(200,409)),range(2)))
        check('different concurrent commands cannot both overwrite one revision',sum('profileId' in x for x in replies)==1 and sum(x.get('code')=='revision_conflict' for x in replies)==1)
        pa=get_profile(at,pa);s['pa']=pa
        fresh=new_profile(at,2,'잘못된 이관 검증')
        legacy=dict(version=1,slotNumber=2,totalRuns=0,victories=0,campaignVictories=0,bestCampaignStage=0,deepestRoom=0,totalKills=0,legacyShards=15,upgrades=[None])
        source=json.dumps(legacy,separators=(',',':'))
        bad=dict(operationId=uid(),expectedRevision=0,migrationVersion=1,sourceSha256=hashlib.sha256(source.encode()).hexdigest(),sourceJson=source)
        call('POST','/v1/profiles/'+fresh['profileId']+'/legacy-import',bad,at,status=400)
        check('malformed old rank rejected without partial import',get_profile(at,fresh)==fresh)
        start=dict(operationId=uid(),profileId=pb['profileId'],expectedProfileRevision=pb['revision'],requestedSeed=11,contentVersion='integration-v1',generatorVersion='modules-v1.0.0',buildId='negative-integration')
        call('POST','/v1/runs',{**start,'contentVersion':None},bt,status=409);check('missing content version fails safely')
        run=call('POST','/v1/runs',start,bt)
        result=dict(operationId=uid(),expectedRunRevision=0,outcome='abandoned',enemiesDefeated=0,roomsReached=0,stagesCleared=0)
        settled=call('POST','/v1/runs/'+run['runId']+'/settle',result,bt);s['pb']=settled['profile']
        check('start and abandon grants no farmable currency',settled['run']['legacyReward']==0 and s['pb']['legacyShards']==pb['legacyShards'])
        STATE.write_text(json.dumps(s,ensure_ascii=False),encoding='utf-8')
    elif phase=='outage':
        s=json.loads(STATE.read_text(encoding='utf-8'))
        call('POST','/v1/auth/guest',s['a'],status=503);check('DB outage reports failure instead of false local success')
    output=ROOT/'Backend/Validation'/f'{phase}-{time.strftime("%Y%m%d-%H%M%S")}.json'
    output.parent.mkdir(parents=True,exist_ok=True)
    assembly=ROOT/'Backend/RuinRunner.Api/bin/Release/net10.0/RuinRunner.Api.dll'
    output.write_text(json.dumps(dict(status='PASS',phase=phase,checks=checks,api=BASE,apiAssemblySha256=hashlib.sha256(assembly.read_bytes()).hexdigest(),method='real HTTP to ASP.NET Core and PostgreSQL; no Unity integration yet'),ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(dict(status='PASS',phase=phase,checks=len(checks),report=str(output))))
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('phase',choices=['create','restart','negative','outage']);main(parser.parse_args().phase)
