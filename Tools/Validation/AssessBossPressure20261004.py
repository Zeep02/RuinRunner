"""Audit opt-in observations against the current executable and normal pilot result."""
import csv
import hashlib
import json
import pathlib
import re
import sys
import argparse

root = pathlib.Path(__file__).resolve().parents[2]
review = root / 'ArtDirection/Review-20261004/boss-pressure'
parser = argparse.ArgumentParser()
parser.add_argument('--candidate', default='BT')
parser.add_argument('folders', nargs='+')
args = parser.parse_args()
results = []
for raw in args.folders:
    folder = pathlib.Path(raw).resolve()
    launch = json.loads((folder / 'launch.json').read_text(encoding='utf-8-sig'))
    exit_info = json.loads((folder / 'exit.json').read_text(encoding='utf-8-sig'))
    log = (folder / 'player.log').read_text(encoding='utf-8-sig')
    report = json.loads((folder / 'boss-pressure.json').read_text(encoding='utf-8-sig'))
    runtime = pathlib.Path(launch['exe']).parent / 'RuinRunner_Data/Managed/RuinRunner.Runtime.dll'
    assert hashlib.sha256(runtime.read_bytes()).hexdigest() == launch['runtimeSha256'].lower()
    assert exit_info['exitCode'] == 0 and '[Stage E2E] PASS' in log
    assert '[Stage E2E] FAIL' not in log and not re.search(r'(?m)^(?:\w*Exception:|NullReferenceException:)', log)
    with (folder / 'boss-hits.csv').open(encoding='utf-8-sig', newline='') as stream:
        hits = list(csv.DictReader(stream))
    assert len(report['bosses']) == 3
    summaries = []
    for boss in report['bosses']:
        rows = [h for h in hits if h['boss'] == boss['boss']]
        assert boss['outcome'] == 'defeated'
        assert len(rows) == boss['originalHits'] + boss['itemHits']
        for source, field in [('original', 'originalDamage'), ('item', 'itemDamage')]:
            total = sum(float(h['effectiveDamage']) for h in rows if h['source'] == source)
            assert abs(total - boss[field]) < .1, (source, total, boss[field])
        assert abs(boss['originalDamage'] + boss['itemDamage'] - boss['maxHealth']) < .1
        required = {'Boss':{'Charge','Leap','Radial','Volley'}, 'Tidekeeper':{'TideLanes','TidePools'}, 'SealHeart':{'SealCharge','SealCross'}}[boss['boss']]
        summaries.append(dict(boss, allPatternTypesObserved=required.issubset(set(boss['patterns'])),
                              contactGameSeconds=round(boss['endedAt']-boss['firstHitAt'], 3),
                              lifetimeGameSeconds=round(boss['endedAt']-boss['startedAt'], 3),
                              itemDamagePercent=round(100*boss['itemDamage']/boss['maxHealth'], 2)))
    results.append({'folder':str(folder), 'launch':launch, 'exit':exit_info, 'bosses':summaries,
                    'pilotResult':re.findall(r'\[Stage E2E\] PASS[^\n]*', log),
                    'growth':re.findall(r'\[Stage E2E\] GROWTH[^\n]*', log),
                    'observationConsistent':True, 'balanceApproved':False})
output = review / ('normal-pressure-results-'+args.candidate+'.json')
output.write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding='utf-8')
for row in results:
    print(pathlib.Path(row['folder']).name)
    for boss in row['bosses']:
        print(boss['boss'], 'contact game sec', boss['contactGameSeconds'], 'item %',
              boss['itemDamagePercent'], 'patterns', boss['patterns'])
print(output)
