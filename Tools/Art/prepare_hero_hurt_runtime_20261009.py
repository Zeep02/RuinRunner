"""Generate a reviewable hurt-only Runtime patch outside Assets."""
from pathlib import Path
import hashlib
import json

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ROOT / 'ArtDirection/Generated/SscaryMotion-20261009/hurt-native-v5-full/manifest-reviewed.json'
OUT = ROOT / 'Tools/Art/Staging/HeroHurt'
manifest = json.loads(MANIFEST.read_text(encoding='utf-8-sig'))
assert manifest['fullGaitVariants'] and manifest['profiles']==7 and len(manifest['frames']) == 7 * 8 * 16 * 9
records = sorted(manifest['frames'], key=lambda r: (r['profile'], r['direction'], r['variant'], r['key']))
assert [(r['profile'], r['direction'], r['variant'], r['key']) for r in records] == [(p,d,v,k) for p in range(7) for d in range(8) for v in range(16) for k in range(9)]
assert set(r['runtimeIndex'] for r in records) == set(range(manifest['uniqueSprites']))
receipt = {'status': 'PREPARED OUTSIDE ASSETS; import, compile and actual playback not yet accepted', 'manifest': str(MANIFEST.relative_to(ROOT)).replace('\\','/'), 'manifestSha256': hashlib.sha256(MANIFEST.read_bytes()).hexdigest(), 'files': []}

def stage(relative, text):
    target = ROOT / 'Assets/RuinRunner' / relative
    candidate = OUT / relative
    candidate.parent.mkdir(parents=True, exist_ok=True)
    candidate.write_text(text, encoding='utf-8')
    receipt['files'].append({'target': str(target.relative_to(ROOT)).replace('\\','/'), 'sourceSha256': hashlib.sha256(target.read_bytes()).hexdigest() if target.exists() else None, 'candidate': str(candidate.relative_to(ROOT)).replace('\\','/'), 'candidateSha256': hashlib.sha256(candidate.read_bytes()).hexdigest()})

def source(relative):
    return (ROOT / 'Assets/RuinRunner' / relative).read_text(encoding='utf-8-sig')

def once(text, before, after):
    assert text.count(before) == 1, before
    return text.replace(before, after, 1)

mapping = '''// Native recoil joints, authored from repaired WeaponStandingV4 individual PNGs.
// Entry and final key are the exact matching native breathing/gait stance.
using UnityEngine;
namespace RuinRunner
{
    public static class SscaryHurtFrames
    {
        public const string Root="External/SscaryMaleCombat/WeaponHurtV1";
        public const int UniqueSpriteCount=UNIQUE_COUNT;
        public const int Keys=9;
        static Sprite[] frames;
        public static void Preload()
        {
            if(frames!=null)return;frames=new Sprite[UniqueSpriteCount];
            for(int i=0;i<frames.Length;i++)frames[i]=IndividualArt.Sprite(Root+"/"+i.ToString("D5"));
        }
        public static int PhaseIndex(float progress)=>Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(progress)*Keys),0,Keys-1);
        public static Sprite Sample(int profile,int direction,int variant,int key,out Vector2 hand,out bool behind)
        {
            Preload();int slot=((Mathf.Clamp(profile,0,6)*8+Mathf.Clamp(direction,0,7))*16+Mathf.Clamp(variant,0,15))*Keys+Mathf.Clamp(key,0,Keys-1);
            hand=Hands[slot];behind=Behind[slot];return frames[Cells[slot]];
        }
        static readonly int[] Cells={CELLS};
        static readonly Vector2[] Hands={HANDS};
        static readonly bool[] Behind={BEHIND};
    }
}
'''.replace('UNIQUE_COUNT', str(manifest['uniqueSprites']))
mapping = mapping.replace('CELLS', ','.join(str(r['runtimeIndex']) for r in records))
mapping = mapping.replace('HANDS', ','.join(f'new Vector2({r["grip"][0]:.1f}f,{r["grip"][1]:.1f}f)' for r in records))
mapping = mapping.replace('BEHIND', ','.join(str(r['behind']).lower() for r in records))
stage('Runtime/Player/SscaryHurtFrames.cs', mapping)

path = 'Runtime/Player/PlayerController.cs'
text = source(path)
text = once(text, '        public float RangedPresentationProgress =>', '        public const float HurtPresentationSeconds = .18f;\n        public float HurtPresentationProgress => Mathf.Clamp01(1f-Mathf.Max(0f,hurtPoseTimer)/HurtPresentationSeconds);\n        public float RangedPresentationProgress =>')
text = once(text, 'hurtPoseTimer = guarded || shielded ? 0f : .18f;', 'hurtPoseTimer = guarded || shielded ? 0f : HurtPresentationSeconds;')
stage(path, text)

path = 'Runtime/Player/PlayerAnimationController.cs'
text = source(path)
text = once(text, '        public const bool HasAuthoredCombatPoses = false;\n        // Thrust, melee, defense and ranged actions have native joint poses.\n        // Dedicated hurt body clips remain pending; the whole set is not finished.', '        public const bool HasAuthoredCombatPoses = true;\n        // Native joint poses retain the approved hero across combat and recoil.\n        public const bool HasAuthoredHurtPoses = true;')
text = once(text, '            WeaponReloadFrames.Preload();', '            WeaponReloadFrames.Preload();\n            SscaryHurtFrames.Preload();')
text = once(text, '            else if(aiming)\n', '''            else if(currentState==PlayerMotionState.Hurt&&!player.IsRangedPresentationActive&&!reloading)
            {
                // Observe the real damage timer without cancelling attacks or
                // changing movement, ammo, reload, cooldown or commitment clocks.
                var variant=moving?8+Mathf.FloorToInt(stride*8):IdleFrame(idleTime);
                var profile=player.EquippedWeapon==null?6:WeaponStandingFrames.Profile(player.EquippedWeapon.Family);
                FrameIndex=SscaryHurtFrames.PhaseIndex(player.HurtPresentationProgress);
                bool behind;selected=SscaryHurtFrames.Sample(profile,FacingDirection,variant,FrameIndex,out thrustHand,out behind);
                authoredThrust=true;HandBehindBody=behind;AuthoredWeaponRotationActive=player.EquippedWeapon!=null;
                AuthoredWeaponRotation=profile==6?0:WeaponStandingFrames.Angle(profile,1,player.WeaponAimDirection);
                standingTime=WeaponStandingFrames.ReturnSeconds;
                PoseName="hurt-"+(profile==6?"unarmed":player.EquippedWeapon.Family.ToString().ToLowerInvariant())+(moving?"-move":"");
            }
            else if(aiming)
''')
stage(path, text)

path = 'Runtime/Feedback/NativeHeroFrameCatalog.cs'
text = source(path)
text = once(text, '"ArmedGripV3", "WeaponStandingV4", "WeaponReloadV3"', '"ArmedGripV3", "WeaponStandingV4", "WeaponReloadV3", "WeaponHurtV1"')
text = once(text, '                case "WeaponReloadV3":return WeaponReloadFrames.UniqueSpriteCount;', '                case "WeaponReloadV3":return WeaponReloadFrames.UniqueSpriteCount;\n                case "WeaponHurtV1":return SscaryHurtFrames.UniqueSpriteCount;')
stage(path, text)

(OUT / 'prepared.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'stagedFiles':len(receipt['files']), 'records':len(records), 'unique':manifest['uniqueSprites'], 'runtimeFilesModified':0}))
