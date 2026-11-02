"""Revise resting source instead of preserving the old fully drawn bow's upper arm."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
t=(root/'Tools/Validation/AuthorWeaponStanding20261001.cs').read_text(encoding='utf-8').replace('AuthorWeaponStanding20261001','AuthorWeaponStandingV220261002').replace('weapon-standing-v1/','weapon-standing-v2/')
t=t.replace('var pixels=Pose(source,idle0,profile,direction,key,hand,out grip);','var pixels=Pose(source,idle0,profile,direction,key,hand,out grip);') if False else t
old='Vector2 grip;var pixels=Pose(source,idle0,profile,direction,key,hand,out grip);string hash=Hash(pixels);int index;'
new=r'''Vector2 cleanHand;bool cleanBehind;
                var clean=key==0?sprite:SscaryArmedFrames.Carry(profile==2,direction,variant,out cleanHand,out cleanBehind);
                string cleanPath=AssetDatabase.GetAssetPath(clean);var cleanPixels=Read(cleanPath);
                Vector2 grip;var pixels=Pose(cleanPixels,idle0,profile,direction,key,hand,out grip);string hash=Hash(pixels);int index;'''
assert old in t;t=t.replace(old,new)
t=t.replace('{"source",sourcePath},{"sourceSha256",Hash(source)}','{"source",cleanPath},{"readySource",sourcePath},{"sourceSha256",Hash(cleanPixels)}')
t=t.replace('Bow off-hand relaxes instead of holding permanent draw.','Bow off-hand relaxes from a clean carry source instead of preserving old drawn upper arms. Bow held vertically beside the torso while resting.')
(root/'Tools/Validation/AuthorWeaponStandingV220261002.cs').write_text(t,encoding='utf-8')
t=(root/'Tools/Validation/ImportWeaponStanding20261001.cs').read_text(encoding='utf-8')
t=t.replace('ImportWeaponStanding20261001','ImportWeaponStandingV220261002').replace('WeaponStandingV1','WeaponStandingV2').replace('weapon-standing-v1','weapon-standing-v2').replace('standing-import-v1','standing-import-v2')
# Existing class file overwritten only after a protected backup; all v1 assets remain.
needle='public static float Angle(int profile,float progress,Vector2 aim)=>'
# Generated string is a C# code.Append line; replace only the exact current expression.
before='(profile==0?-10:profile==1?20:profile==2?38:0)*(aim.x<0?-1:1)*Mathf.SmoothStep(0,1,progress)'
after='(profile==3?Mathf.DeltaAngle(Mathf.Atan2(aim.y,aim.x)*Mathf.Rad2Deg,aim.x<-.1f||aim.y<-.9f?0:180):(profile==0?-10:profile==1?20:profile==2?38:0)*(aim.x<0?-1:1))*Mathf.SmoothStep(0,1,progress)'
assert before in t;t=t.replace(before,after)
(root/'Tools/Validation/ImportWeaponStandingV220261002.cs').write_text(t,encoding='utf-8')
print('Generated v2: original clean carry source for relaxed arms, ready source kept only at transition key0, upright bow resting angle.')

