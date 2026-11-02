"""Summarize actual process/log/timestamp evidence; never reclassify a failure."""
from pathlib import Path
import csv
import json
import re

root = Path(__file__).resolve().parents[2]
evidence = root / 'Logs/Validation/2026-10-04-normal-growth'
rows = []
for folder in sorted(evidence.glob('normal-*')):
    launch = json.loads((folder / 'launch.json').read_text(encoding='utf-8-sig'))
    text = (folder / 'player.log').read_text(encoding='utf-8-sig')
    args = launch['args']
    value = lambda flag: args[args.index(flag) + 1]
    finish = re.search(r'\[Stage E2E\] (PASS|FAIL) — (.*), elapsed=([\d.]+)s, rooms=(\d+), kills=(\d+)', text)
    growth = re.search(r'GROWTH upgrades=(\d+) disassemblies=(\d+) firstStageUpgrades=(\d+) firstStageDisassemblies=(\d+)', text)
    record = list(csv.DictReader((folder / 'recording.csv').open(encoding='utf-8-sig')))
    exit_file = folder / 'exit.json'
    row = dict(folder=folder.relative_to(root).as_posix(), launch=launch,
               buildDirection=value('--validation-build'), seed=int(value('--validation-seed')),
               result=finish[1] if finish else 'RUNNING', reason=finish[2] if finish else None,
               elapsedSeconds=float(finish[3]) if finish else None, kills=int(finish[5]) if finish else None,
               growth=list(map(int, growth.groups())) if growth else None,
               observedStages=sorted({int(r['stage']) for r in record}),
               weapons=sorted({r['weapon'] for r in record}), frames=len(list((folder / 'frames').glob('*.png'))),
               exit=json.loads(exit_file.read_text(encoding='utf-8-sig')) if exit_file.exists() else None,
               postVictoryFixture=re.findall(r'\[Save Lifecycle\] PASS phase=post-victory checks=(\d+)', text),
               scope='Normal GameAction campaign. Post-victory save/retry/forced-defeat fixture is separate. No claim of human duration, balanced combat, audio or performance.')
    rows.append(row)
out = root / 'ArtDirection/Review-20261004/normal-growth/normal-results.json'
out.write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps([{k: r[k] for k in ('folder','result','elapsedSeconds','growth','observedStages','frames')} for r in rows], ensure_ascii=False))
