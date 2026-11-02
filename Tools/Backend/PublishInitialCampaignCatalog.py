"""One-time migration from reviewed Unity definitions, not a second editable runtime catalog.
After publication, edit a NEW immutable Backend/Content/catalog.<version>.json directly.
"""
import json,pathlib,re
ROOT=pathlib.Path(__file__).resolve().parents[2]
source=ROOT/'ArtDirection/Review-20261004/combat-expansion/catalog-items.initial-migration.json'
items=json.loads(source.read_text(encoding='utf-8-sig'))
assert len(items)==76 and len({i['id'] for i in items})==76
talentSource=(ROOT/'Assets/RuinRunner/Runtime/Progression/TalentLoadout.cs').read_text(encoding='utf-8-sig')
talents=[]
free={'combo_memory','survey_next','chest_intent'}
costs={'stored_guard':5,'cross_chamber':5,'turning_dash':3,'brake_dash':3,'return_dash':5,'supply_choice':3,'return_trail':3,'merchant_barter':5,'reserve_offer':5}
requires={'stored_guard','cross_chamber','return_dash','merchant_barter','reserve_offer'}
for id,name,family,description in re.findall(r'new TalentDefinition\(TalentId\.\w+,"([^"]+)","([^"]+)",TalentFamily\.(\w+),"([^"]+)"\)',talentSource):
    talents.append(dict(id=id,branch=family.lower(),unlockCost=0 if id in free else costs[id],requiredRuns=1 if id in requires else 0,name=name,description=description))
assert len(talents)==12
starters=[('rust_sword',0),('worn_dagger',3),('scrap_pistol',3),('iron_mace',5),('training_katana',5),('scrap_shotgun',6)]
for item in items:
    # This is an individual-item weight. The generated Unity loot table uses the same weight and stage range.
    item['dropWeight']=[100,62,28,9,3][item['rarity']]
catalog=dict(version='campaign-v1',status='published',generatorVersions=['modules-v1.0.0'],startingLegacy=3,maxActiveTalents=3,
    starterWeapons=[dict(id=id,unlockCost=cost,name=next(x['name'] for x in items if x['id']==id)) for id,cost in starters],
    defaultUnlockedTalentIds=['combo_memory','survey_next','chest_intent'],talents=talents,
    settlement=dict(minimumReward=1,killsPerShard=4,rewardPerRoom=1,victoryBonus=6,campaignStages=3,maxKills=10000,maxRooms=1000),
    economy=dict(startingGold=0,enemyGold=2,combatGold=6,chestGold=8,bossGold=22,healPrice=16,healAmount=35,shopOffers=4,maxOwnedItems=23,maxAccessories=20),
    exchangeRecipes=[dict(id='mana_aegis',inputIds=['mana_thread','sapphire_flask'],outputId='mana_aegis',name='마나 방벽'),
        dict(id='prism_cage',inputIds=['guard_bell','guardian_seal'],outputId='prism_cage',name='굴절 포획함'),
        dict(id='trail_spindle',inputIds=['rush_spur','storm_feather'],outputId='trail_spindle',name='길짜는 물레')],items=items)
output=ROOT/'Backend/Content/catalog.campaign-v1.json'
encoded=json.dumps(catalog,ensure_ascii=False,indent=2)+'\n'
if output.exists() and output.read_text(encoding='utf-8')!=encoded: raise RuntimeError('Refusing to replace an existing version. Publish a new version instead.')
output.write_text(encoded,encoding='utf-8')
print(json.dumps(dict(path=str(output),items=len(items),talents=len(talents)),ensure_ascii=False))
