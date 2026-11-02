"""Reuse exact native source/screen head comparison, with the actual physical-stab peak keys."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
t=(root/'Tools/Art/review_sweep_fx_20261001.py').read_text(encoding='utf-8')
t=t.replace('actual sweep screens','actual thrust screens').replace('melee-fx-binding','thrust-fx')
t=t.replace("peak=1 if selected[0]['fxClip']=='Katana' else 2","peak=1 if selected[0]['fxClip']=='DaggerStab' else 2")
(root/'Tools/Art/review_thrust_screen_20261001.py').write_text(t,encoding='utf-8')
print('Prepared unchanged original opaque-head/camera-position QA for native stab screens.')
