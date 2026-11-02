"""Prepare fresh AU tools; preserves all existing AT/AS executables and proof."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
for before,after in [('BuildRangeCandidate20261001.ps1','BuildThrustCandidate20261001.ps1'),('ReadRangeBuild20261001.ps1','ReadThrustBuild20261001.ps1'),('SmokeCandidateRange20261001.ps1','SmokeCandidateThrust20261001.ps1')]:
    t=(root/'Tools/Validation'/before).read_text(encoding='utf-8')
    t=t.replace('melee-fx-range','thrust-fx').replace('MeleeRangeAT','ThrustAU').replace('20261001-AT','20261001-AU').replace('smoke-AT','smoke-AU').replace('build-start-AT','build-start-AU').replace('build-AT','build-AU').replace('existing AT','existing AU').replace('AT executable','AU executable').replace('AT smoke','AU smoke')
    (root/'Tools/Validation'/after).write_text(t,encoding='utf-8')
print('Fresh AU build/readback/isolated smoke tools; no build performed yet.')
