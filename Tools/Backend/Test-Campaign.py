"""Real localhost API/PostgreSQL campaign checks; fresh isolated guests only. No secrets in reports."""
import argparse, base64, concurrent.futures, hashlib, json, os, pathlib, shutil, subprocess, urllib.error, urllib.request, uuid
from datetime import datetime, timezone

ROOT=pathlib.Path(__file__).resolve().parents[2]
BASE='http://127.0.0.1:5186'
checks=[]
def uid(): return str(uuid.uuid4())
def check(name, value=True):
    assert value, name
    checks.append(name)
def call(method,path,body=None,token=None,status=200):
    headers={'Content-Type':'application/json'}
    if token: headers['Authorization']='Bearer '+token
    req=urllib.request.Request(BASE+path,None if body is None else json.dumps(body,ensure_ascii=False).encode(),headers,method=method)
    try:
        with urllib.request.urlopen(req,timeout=20) as response: actual=response.status; data=json.load(response)
    except urllib.error.HTTPError as response:
        actual=response.code
        try: data=json.load(response)
        except ValueError: data={}
    assert actual in (status if isinstance(status,tuple) else (status,)), (method,path,actual,status,data.get('code'))
    return data
def guest():
    identity=dict(installationId=uid(),guestSecret=base64.urlsafe_b64encode(os.urandom(32)).decode().rstrip('='))
    return identity,call('POST','/v1/auth/guest',identity)
def main(restart, expected_version, instance, marker):
    identity,a=guest(); _,b=guest(); at=a['accessToken']; bt=b['accessToken']
    catalog=call('GET','/v1/catalogs/current',token=at); version=catalog['version']
    check('full published campaign catalog',version==expected_version and len(catalog['items'])==76 and len(catalog['talents'])==12)
    if version=='campaign-v2':
        check('v2 accessory and set numeric data available',catalog['accessoryTuningVersion']==1 and len(catalog['itemSets'])==8 and all(x['stats'].get('effect') is not None for x in catalog['items'] if x['kind']=='accessory'))
    check('published catalog version lookup matches current',call('GET','/v1/catalogs/'+version,token=at)==catalog)
    p=call('POST','/v1/profiles',dict(operationId=uid(),slot=1,name='Campaign integration isolated'),at)
    other_slot=call('POST','/v1/profiles',dict(operationId=uid(),slot=2,name='Unmodified second slot'),at)
    foreign_profile=call('POST','/v1/profiles',dict(operationId=uid(),slot=1,name='Unmodified other guest'),bt)
    profile='/v1/profiles/'+p['profileId']; initial=p
    check('three free defaults and one starter',len(p['state']['activeTalentIds'])==3 and p['state']['unlockedWeaponIds']==['rust_sword'])
    call('GET',profile,token=bt,status=404); check('foreign profile denied')
    unlock=lambda id,kind='talent': dict(operationId=uid(),expectedRevision=p['revision'],kind=kind,contentId=id,contentVersion=version)
    failure=call('POST',profile+'/unlocks',unlock('stored_guard'),at,status=409)
    check('achievement requirement enforced',failure.get('code')=='achievement_required')
    failure=call('POST',profile+'/unlocks',unlock('iron_mace','weapon'),at,status=409)
    check('insufficient legacy atomic',failure.get('code')=='insufficient_legacy' and call('GET',profile,token=at)==initial)
    old=dict(version=1,slotNumber=1,createdUtc='',lastPlayedUtc='',totalRuns=3,victories=1,campaignVictories=1,bestCampaignStage=3,deepestRoom=12,totalKills=60,legacyShards=100,upgrades=[dict(type=0,rank=2),dict(type=6,rank=1)])
    source=json.dumps(old,ensure_ascii=False,separators=(',',':'))
    imp=dict(operationId=uid(),expectedRevision=0,migrationVersion=1,sourceSha256=hashlib.sha256(source.encode()).hexdigest(),sourceJson=source)
    call('POST',profile+'/legacy-import',{**imp,'sourceSha256':'0'*64},at,status=400);check('legacy source hash validated')
    receipt=call('POST',profile+'/legacy-import',imp,at);p=receipt['profile']
    check('old ranks refunded exactly and version marked',p['legacyShards']==112 and p['state']['legacyMigrationVersion']==1 and p['state']['legacyRefundVersion']==1)
    check('same import operation replays exact receipt',call('POST',profile+'/legacy-import',imp,at)==receipt)
    call('POST',profile+'/legacy-import',{**imp,'operationId':uid(),'expectedRevision':p['revision']},at,status=409);check('old save cannot overwrite newer server profile')
    for tid in ['merchant_barter','stored_guard','turning_dash']:
        request=unlock(tid); before=p['legacyShards']; result=call('POST',profile+'/unlocks',request,at)
        check('unlock charged once '+tid,call('POST',profile+'/unlocks',request,at)==result and result['legacyShards']<before); p=result
    config=dict(operationId=uid(),expectedRevision=p['revision'],startingWeaponId='rust_sword',activeTalentIds=['combo_memory','stored_guard'])
    call('PUT',profile+'/configuration',config,at,status=400);check('one talent per branch enforced')
    call('PUT',profile+'/configuration',{**config,'activeTalentIds':['combo_memory','survey_next','chest_intent','turning_dash']},at,status=400);check('three active talent maximum enforced')
    config['activeTalentIds']=['combo_memory','survey_next','merchant_barter']; p=call('PUT',profile+'/configuration',config,at)
    call('PUT',profile+'/configuration',{**config,'operationId':uid()},at,status=409);check('stale revision cannot replace loadout')
    start=dict(operationId=uid(),profileId=p['profileId'],expectedProfileRevision=p['revision'],requestedSeed=20261004,contentVersion=version,generatorVersion='modules-v1.0.0',buildId='campaign-integration-isolated')
    run=call('POST','/v1/runs',start,at); path='/v1/runs/'+run['runId']
    check('seed and content pinned on run',run['seed']==20261004 and run['contentVersion']==version and run['loadout']['activeTalentIds']==config['activeTalentIds'])
    check('run start retry is stable',call('POST','/v1/runs',start,at)==run)
    call('POST','/v1/runs',{**start,'operationId':uid()},at,status=409);check('one active run per profile')
    econ=call('GET',path+'/economy',token=at)
    check('starter owned but has no sale value',len(econ['items'])==1 and econ['items'][0]['origin']=='starter' and econ['items'][0]['sellPrice']==0)
    check('seeded shops persisted with service offer',len(econ['shops'])==3 and all(any(o['kind']=='heal' for o in s['offers']) for s in econ['shops']))
    call('GET',path+'/economy',token=bt,status=404);check('foreign economy denied')
    def trade(action,status=200,**kwargs):
        nonlocal econ
        req=dict(operationId=uid(),expectedRevision=econ['revision'],action=action,**kwargs)
        result=call('POST',path+'/trades',req,at,status)
        if status==200:econ=result['economy']
        return req,result
    _,bad=trade('sell',409,shopId='stage-0',instanceId=econ['items'][0]['instanceId'])
    check('starter cannot be sold',bad.get('code')=='starter_not_for_sale')
    _,bad=trade('discard',409,instanceId=econ['items'][0]['instanceId']);check('last weapon cannot be discarded',bad.get('code')=='last_weapon')
    def reward(source='enemy',content=None,event=None,status=200):
        nonlocal econ
        req=dict(operationId=uid(),eventId=event or uid(),source=source,stageIndex=0,roomId='s0-r01',contentId=content)
        result=call('POST',path+'/rewards',req,at,status)
        if status==200:econ=result['economy']
        return req,result
    req,rewarded=reward(event='enemy:0:1');balance=econ['gold']
    check('reward response-loss retry returns original receipt',call('POST',path+'/rewards',req,at)==rewarded)
    call('POST',path+'/rewards',{**req,'operationId':uid()},at,status=409);check('same world event with a new operation cannot mint gold',call('GET',path+'/economy',token=at)['gold']==balance)
    call('POST',path+'/rewards',{**req,'source':'boss'},at,status=409);check('operation payload cannot change')
    call('POST',path+'/rewards',{**req,'operationId':uid(),'eventId':uid(),'stageIndex':3},at,status=400);check('invalid stage rejected')
    call('POST',path+'/rewards',{**req,'operationId':uid(),'eventId':uid(),'contentId':'rust_sword'},at,status=400);check('enemy reward cannot mint inventory')
    for n in range(18): reward('boss',event='funding:'+str(n))
    # Existing item definitions are immutable; these are real server-owned instances.
    for content in ['mana_thread','sapphire_flask','guard_bell','guardian_seal','rush_spur','storm_feather','worn_dagger']:
        reward('pickup',content,event='pickup:'+content)
    check('pickup has independent instance identities',len(set(x['instanceId'] for x in econ['items']))==len(econ['items']))
    before=econ
    _,bad=trade('sell',404,shopId='stage-0',instanceId=uid());check('unowned item cannot be sold',call('GET',path+'/economy',token=at)==before)
    inputs=[x['instanceId'] for x in econ['items'] if x['contentId'] in ['mana_thread','sapphire_flask']]
    exchange,exchanged=trade('exchange',recipeId='mana_aegis',instanceIds=inputs,objectId='s0-r05:exchange-altar')
    check('exact recipe atomically consumes two and grants one',len(exchanged['removedInstanceIds'])==2 and exchanged['grantedItems'][0]['contentId']=='mana_aegis')
    check('exchange operation retry replays same output instance',call('POST',path+'/trades',exchange,at)==exchanged)
    second=[x['instanceId'] for x in econ['items'] if x['contentId'] in ['guard_bell','guardian_seal']]
    _,bad=trade('exchange',409,recipeId='prism_cage',instanceIds=second,objectId='s0-r05:exchange-altar');check('altar can only be used once',bad.get('code')=='object_used')
    _,bad=trade('exchange',400,recipeId='mana_aegis',instanceIds=second,objectId='s0-r06:exchange-altar');check('same-kind wrong recipe input rejected',bad.get('code')=='recipe_inputs')
    offer=next(o for o in econ['shops'][0]['offers'] if o['kind']!='heal')
    req,bought=trade('buy',shopId='stage-0',offerId=offer['offerId'])
    check('purchase price supplied only by server',bought['goldDelta']==-offer['price'] and len(bought['grantedItems'])==1)
    with concurrent.futures.ThreadPoolExecutor(max_workers=5) as pool: repeated=list(pool.map(lambda _:call('POST',path+'/trades',req,at),range(5)))
    check('concurrent purchase retries do not charge twice',all(x==bought for x in repeated))
    # Use a different merchant so the existing stage-0 heal/replay case retains
    # its one-use stock. This concurrent check must not alter its preconditions.
    competing=[dict(operationId=uid(),expectedRevision=econ['revision'],action='buy',shopId='stage-1',offerId='heal') for _ in range(2)]
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        attempts=list(pool.map(lambda request:call('POST',path+'/trades',request,at,status=(200,409)),competing))
    wins=[x for x in attempts if 'economy' in x];losses=[x for x in attempts if 'code' in x]
    check('different concurrent trades at one revision commit exactly one',len(wins)==1 and len(losses)==1 and losses[0]['code']=='revision_conflict' and wins[0]['economy']['gold']==econ['gold']-catalog['economy']['healPrice'])
    econ=wins[0]['economy']
    _,bad=trade('buy',409,shopId='stage-0',offerId=offer['offerId']);check('sold offer cannot be bought with another operation',bad.get('code')=='out_of_stock')
    boughtItem=bought['grantedItems'][0]
    req,sold=trade('sell',shopId='stage-0',instanceId=boughtItem['instanceId'])
    check('sell removes owned instance and credits catalog resale value',sold['goldDelta']==boughtItem['sellPrice'] and boughtItem['instanceId'] not in [x['instanceId'] for x in econ['items']])
    trade('sell',404,shopId='stage-0',instanceId=boughtItem['instanceId']);check('removed item cannot be sold twice')
    req,healed=trade('buy',shopId='stage-0',offerId='heal');check('heal charged once with server amount',healed['healAmount']==catalog['economy']['healAmount'] and healed['goldDelta']==-catalog['economy']['healPrice'])
    call('POST',path+'/trades',{**req,'operationId':uid()},at,status=409);check('stale economy revision rejected')
    healedRequest=req
    reward('pickup','starbreaker',event='barter-input')
    barterInput=next(x for x in econ['items'] if x['contentId']=='starbreaker')
    targets=[(s,o) for s in econ['shops'] for o in s['offers'] if o['stock']>0 and o['kind']!='heal' and o['price']<=barterInput['sellPrice']*2]
    assert targets,'deterministic fixture requires a valid barter offer'
    shop,offer=targets[0]
    _,bartered=trade('barter',shopId=shop['shopId'],offerId=offer['offerId'],instanceId=barterInput['instanceId'])
    check('barter exchanges one item without inventing gold',bartered['goldDelta']==0 and len(bartered['grantedItems'])==1 and bartered['removedInstanceIds']==[barterInput['instanceId']])
    reward('pickup','starbreaker',event='second-barter-input')
    barterInput=next(x for x in econ['items'] if x['contentId']=='starbreaker')
    remaining=next(o for s in econ['shops'] if s['shopId']==shop['shopId'] for o in s['offers'] if o['stock']>0)
    _,bad=trade('barter',409,shopId=shop['shopId'],offerId=remaining['offerId'],instanceId=barterInput['instanceId'])
    check('barter is once per merchant',bad.get('code')=='barter_unavailable')
    while sum(x['kind']=='weapon' for x in econ['items'])>3:
        item=next(x for x in econ['items'] if x['kind']=='weapon' and x['origin']!='starter')
        trade('discard',instanceId=item['instanceId'])
    while sum(x['kind']=='accessory' for x in econ['items'])<20: reward('pickup','lucky_dice')
    before=econ
    _,bad=reward('pickup','lucky_dice',status=409)
    check('server accessory capacity rejects without partial state',bad.get('code')=='inventory_full' and call('GET',path+'/economy',token=at)==before)
    while len(econ['items'])<23:reward('pickup','worn_dagger')
    _,bad=reward('pickup','worn_dagger',status=409)
    check('server total inventory capacity enforced',bad.get('code')=='inventory_full' and len(econ['items'])==23)
    final=call('GET',path+'/economy',token=at)
    if restart:
        pwsh=shutil.which('pwsh.exe')
        assert pwsh,'PowerShell 7 is required for isolated restart'
        for stopping in (True,False):
            command=[pwsh,'-NoProfile','-File',str(ROOT/'Tools/Backend/Start-IsolatedBackend.ps1'),'-Instance',instance,
                     '-ApiPort',str(marker['apiPort']),'-DatabasePort',str(marker['databasePort']),'-ContentVersion',expected_version]
            command+=['-Stop'] if stopping else ['-SkipBuild']
            # Background Windows service handles must not hold a Python PIPE open after pwsh exits.
            log=ROOT/'Backend/.local/isolated'/instance/'logs'/('campaign-test-'+('stop' if stopping else 'start')+'.log')
            with log.open('w',encoding='utf-8') as serviceLog:
                result=subprocess.run(command,stdout=serviceLog,stderr=subprocess.STDOUT,timeout=60)
            assert result.returncode==0,'Isolated restart failed; local service logs retained'
        auth=call('POST','/v1/auth/guest',identity);at=auth['accessToken']
        check('API and PostgreSQL restart preserve guest',auth['playerId']==a['playerId'])
        check('API and PostgreSQL restart preserve economy',call('GET',path+'/economy',token=at)==final)
        check('API and PostgreSQL restart preserve pinned run',call('GET',path,token=at)==run)
        check('receipt survives API and PostgreSQL restart',call('POST',path+'/trades',healedRequest,at)==healed)
    settle=dict(operationId=uid(),expectedRunRevision=run['revision'],outcome='victory',enemiesDefeated=70,roomsReached=12,stagesCleared=3)
    before=call('GET',profile,token=at)
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        settlements=list(pool.map(lambda _:call('POST',path+'/settle',settle,at),range(4)))
    result=settlements[0]
    check('concurrent settlement retries return one receipt and one counter increment',all(x==result for x in settlements) and result['profile']['state']['totalRuns']==before['state']['totalRuns']+1)
    check('settlement changes permanent legacy not run gold',result['profile']['legacyShards']==before['legacyShards']+result['run']['legacyReward'] and call('GET',path+'/economy',token=at)['gold']==final['gold'])
    check('settlement retry grants once',call('POST',path+'/settle',settle,at)==result)
    call('POST',path+'/settle',{**settle,'operationId':uid(),'expectedRunRevision':1},at,status=409);check('cannot settle twice with new operation')
    call('POST',path+'/rewards',dict(operationId=uid(),eventId=uid(),source='enemy',stageIndex=0,roomId='r1'),at,status=409);check('settled run rejects new rewards')
    call('POST',path+'/trades',dict(operationId=uid(),expectedRevision=final['revision'],action='discard',instanceId=final['items'][-1]['instanceId']),at,status=409);check('settled run rejects new trades')
    # A new game cannot spend the previous game's gold. It creates a fresh starter instance.
    p=result['profile']; fresh=call('POST','/v1/runs',{**start,'operationId':uid(),'expectedProfileRevision':p['revision'],'requestedSeed':20261005},at)
    freshEconomy=call('GET','/v1/runs/'+fresh['runId']+'/economy',token=at)
    check('run gold and items are not persistent power',freshEconomy['gold']==catalog['economy']['startingGold'] and len(freshEconomy['items'])==1)
    call('POST','/v1/runs/'+fresh['runId']+'/settle',dict(operationId=uid(),expectedRunRevision=0,outcome='abandoned',enemiesDefeated=0,roomsReached=0,stagesCleared=0),at)
    check('same guest other slot remains unchanged',call('GET','/v1/profiles/'+other_slot['profileId'],token=at)==other_slot)
    check('other guest profile remains unchanged',call('GET','/v1/profiles/'+foreign_profile['profileId'],token=bt)==foreign_profile)
    return version

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--restart',action='store_true')
    parser.add_argument('--instance',required=True,help='An existing isolated fixture; normal/default DB is never used')
    parser.add_argument('--expected-catalog',choices=['campaign-v1','campaign-v2'],default='campaign-v2')
    args=parser.parse_args()
    assert args.instance and all(c.isalnum() or c=='-' for c in args.instance)
    isolated=(ROOT/'Backend/.local/isolated'/args.instance).resolve()
    marker=json.loads((isolated/'instance.json').read_text(encoding='utf-8-sig'))
    assert pathlib.Path(marker['dll']).resolve().is_relative_to(isolated)
    assert int(marker['apiPort']) not in (5186,5196),'This regression must not use the normal or current gameplay API'
    BASE='http://127.0.0.1:'+str(int(marker['apiPort']))
    stamp=datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S');out=ROOT/'Backend/Validation'/('campaign-'+stamp+'.json')
    report={'passed':False,'checks':checks,'serviceRestart':args.restart,'isolatedFreshGuests':True,'combatTrust':'client-reported-singleplayer','normalGameplay':False,'instance':args.instance,'apiPort':int(marker['apiPort'])}
    try: report['contentVersion']=main(args.restart,args.expected_catalog,args.instance,marker);report['passed']=True
    except Exception as e: report['failure']=str(e);raise
    finally:
        out.parent.mkdir(parents=True,exist_ok=True);out.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
        print(json.dumps({'passed':report['passed'],'checks':len(checks),'report':str(out)},ensure_ascii=False))
