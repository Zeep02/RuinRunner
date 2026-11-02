"""Guarded source-only adoption after Sprite Editor import completed."""
from pathlib import Path
import shutil
root=Path(__file__).resolve().parents[2]
assert (root/'ArtDirection/Review-20261001/weapon-carry/native-import-v3.json').is_file(),'Wait for actual importer completion.'
backup=root/'ArtDirection/Review-20261001/weapon-carry/before';backup.mkdir(parents=True,exist_ok=True)
paths=['Assets/RuinRunner/Runtime/Player/PlayerAnimationController.cs','Assets/RuinRunner/Runtime/Player/HeldWeaponView.cs','Assets/RuinRunner/Runtime/Diagnostics/RuntimeSmokeTest.cs','Assets/RuinRunner/Runtime/Player/SscaryArmedFrames.cs']
for p in paths:
    dest=backup/Path(p).name
    if not dest.exists():shutil.copy2(root/p,dest)
p=root/paths[0];t=p.read_text(encoding='utf-8');assert 'SscaryArmedFrames.Carry' not in t
for old,new in [('SscaryMeleeFrames.Sample','SscaryArmedFrames.Melee'),('SscaryShortBladeFrames.Sample','SscaryArmedFrames.Short'),('SscaryThrustFrames.Sample','SscaryArmedFrames.Thrust'),('SscaryDefenseFrames.Sample','SscaryArmedFrames.Defense'),('SscaryRangedFrames.Sample','SscaryArmedFrames.Ranged')]:t=t.replace(old,new)
for name in ['SscaryThrustFrames','SscaryMeleeFrames','SscaryShortBladeFrames','SscaryDefenseFrames','SscaryRangedFrames']:t=t.replace('            '+name+'.Preload();\n','')
t=t.replace('            MeleeThrustEffect.Preload();','            MeleeThrustEffect.Preload();\n            SscaryArmedFrames.Preload();')
t=t.replace('action || aiming?player.WeaponAimDirection','action || player.EquippedWeapon!=null?player.WeaponAimDirection')
t=t.replace('player.EquippedWeapon!=null && player.EquippedWeapon.AttackType==WeaponAttackType.Ranged &&\n                    Vector2.Dot','player.EquippedWeapon!=null &&\n                    Vector2.Dot')
needle='            else if(moving)\n            {\n                // Preserve gait phase'
assert needle in t
t=t.replace(needle,'''            else if(player.EquippedWeapon!=null)
            {
                var variant=moving?8+Mathf.FloorToInt(stride*8):IdleFrame(idleTime);
                bool behind;var family=player.EquippedWeapon.Family;
                selected=SscaryArmedFrames.Carry(family==WeaponFamily.Greatsword||family==WeaponFamily.Mace,FacingDirection,variant,out thrustHand,out behind);
                FrameIndex=variant%8;authoredThrust=true;HandBehindBody=behind;
                PoseName=moving?"armed-carry-move":"armed-carry";
            }
            else if(moving)
            {
                // Preserve gait phase''')
p.write_text(t,encoding='utf-8')
p=root/paths[2];t=p.read_text(encoding='utf-8');assert 'SscaryArmedFrames.Thrust' not in t
for old,new in [('SscaryMeleeFrames.Sample','SscaryArmedFrames.Melee'),('SscaryShortBladeFrames.Sample','SscaryArmedFrames.Short'),('SscaryThrustFrames.Sample','SscaryArmedFrames.Thrust'),('SscaryDefenseFrames.Sample','SscaryArmedFrames.Defense'),('SscaryRangedFrames.Sample','SscaryArmedFrames.Ranged')]:t=t.replace(old,new)
for name in ['SscaryThrustFrames','SscaryMeleeFrames','SscaryShortBladeFrames','SscaryDefenseFrames','SscaryRangedFrames']:t=t.replace(name+'.Preload();','SscaryArmedFrames.Preload();')
p.write_text(t,encoding='utf-8')
p=root/paths[3];t=p.read_text(encoding='utf-8')
old='=>Pick(4,c,d,v,k,out h,out b);';new='=>c==3&&k==0?Pick(4,2,d,v,15,out h,out b):Pick(4,c,d,v,k,out h,out b);'
assert old in t or new in t
if old in t:t=t.replace(old,new);p.write_text(t,encoding='utf-8')
print('Adopted native raised hands in carry/melee/thrust/defense/ranged and current native smoke. Aim-facing gait retained; charge seam shares identical native end/start.')
