using UnityEngine;

namespace RuinRunner
{
    public sealed class EnemyPixelAnimator : MonoBehaviour
    {
        private EnemyController owner;
        private SpriteRenderer target;
        private int state = -1;
        private float stateTime;
        private float phaseTime;
        private int attackPhase=-1;
        private float hurtTime=-1;
        public int ArtDirection {get;private set;}
        public void Hit(){hurtTime=0;}

        public void Initialize(EnemyController enemy, SpriteRenderer renderer)
        {
            owner = enemy;
            target = renderer;
        }

        private void LateUpdate()
        {
            if (owner == null || target == null || owner.IsDead || owner.AnimationPaused) return;
            var nextState = owner.IsActing ? 2 : owner.Velocity.sqrMagnitude > .0225f ? 1 : 0;
            if (nextState != state)
            {
                state = nextState;
                stateTime = 0f;
            }
            else stateTime += Time.deltaTime;

            if(owner.Type==EnemyType.SealHeart)
            {
                if(nextState!=2 || stateTime==0 || attackPhase!=owner.AttackVisualPhase)
                {phaseTime=0;attackPhase=owner.AttackVisualPhase;}
                else phaseTime+=Time.deltaTime;
                if(nextState!=2 || stateTime==0)
                    ArtDirection=ExternalCreatureArt.Direction(nextState==2?owner.CampaignActionDirection:owner.VisualAimDirection);
                target.sprite=state==2?SealHeartArt.Action(owner.CurrentBossAction,ArtDirection,attackPhase,phaseTime,owner.CampaignPhaseDuration):
                    SealHeartArt.Loop(state==1,ArtDirection,stateTime);
                if(hurtTime>=0)
                {
                    // Dedicated physical recoil may interrupt idle or locomotion,
                    // but never conceal a committed anticipation or contact pose.
                    if(state!=2 && hurtTime<SealHeartArt.RecoilDuration)target.sprite=SealHeartArt.Recoil(ArtDirection,hurtTime);
                    hurtTime+=Time.deltaTime;
                    if(hurtTime>=SealHeartArt.RecoilDuration)hurtTime=-1;
                }
                target.flipX=false;
                return;
            }

            if(owner.Type==EnemyType.Tidekeeper)
            {
                if(nextState!=2 || stateTime==0 || attackPhase!=owner.AttackVisualPhase)
                {phaseTime=0;attackPhase=owner.AttackVisualPhase;}
                else phaseTime+=Time.deltaTime;
                target.sprite=state==2?TidekeeperArt.Action(owner.CurrentBossAction,attackPhase,phaseTime,owner.CampaignPhaseDuration):TidekeeperArt.Idle(stateTime);
                if(hurtTime>=0)
                {
                    if(state!=2 && hurtTime<TidekeeperArt.RecoilDuration)target.sprite=TidekeeperArt.Recoil(hurtTime);
                    hurtTime+=Time.deltaTime;
                    if(hurtTime>=TidekeeperArt.RecoilDuration)hurtTime=-1;
                }
                target.flipX=false;
                return;
            }

            if(ExternalCreatureArt.Supports(owner.Type))
            {
                if(nextState!=2 || attackPhase!=owner.AttackVisualPhase)
                {phaseTime=0;attackPhase=owner.AttackVisualPhase;}
                else phaseTime+=Time.deltaTime;
                // Lock an action's direction at anticipation; walking faces its actual velocity.
                if(nextState!=2 || stateTime==0)ArtDirection=ExternalCreatureArt.Direction(owner.VisualAimDirection);
                var authored=state==2?ExternalCreatureArt.AttackFrames(owner.Type,ArtDirection,attackPhase):ExternalCreatureArt.Frames(owner.Type,state,ArtDirection);
                var index=state==2?ExternalCreatureArt.AttackFrame(owner.Type,attackPhase,phaseTime):
                    Mathf.FloorToInt(stateTime*(state==0?ExternalCreatureArt.IdleFramesPerSecond(owner.Type):12f))%authored.Length;
                target.sprite=authored[Mathf.Clamp(index,0,authored.Length-1)];
                if(hurtTime>=0)
                {
                    hurtTime+=Time.deltaTime;
                    var hurt=ExternalCreatureArt.HurtFrames(owner.Type,ArtDirection);
                    // Never hide the dangerous attack pose behind a hurt animation.
                    if(hurt!=null && state!=2 && hurtTime<.18f)
                        target.sprite=hurt[Mathf.Min(hurt.Length-1,(int)(hurtTime/.18f*hurt.Length))];
                    if(hurtTime>=.18f)hurtTime=-1;
                }
                target.flipX=false;
            }
        }
    }
}
