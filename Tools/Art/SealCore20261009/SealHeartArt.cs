using UnityEngine;

namespace RuinRunner
{
    /// <summary>Omnidirectional seal core. Separate crown, keystones and heart articulate in authored poses.</summary>
    public static class SealHeartArt
    {
        public const string Root="Campaign/SealCoreV4/";
        public static Sprite[] Clip(string clip,int direction=0)
        {
            // Preserve old diagnostic entry points; the nonhuman core has no
            // anatomical front/back face, and locked ground tells show its aim.
            if(clip=="walk")clip="move";
            else if(clip=="swipe")clip="sweep";
            else if(clip=="stomp"||clip=="attack02")clip="cast";
            else if(clip=="attack03")clip="charge";
            return IndividualArt.Frames(Root+clip,clip=="idle"?4:8);
        }
        public static Sprite[] Frames(int state,int direction=0)=>Clip(state==0?"idle":state==1?"move":state==2?"cast":"death",direction);
        public static void Preload()
        {foreach(string clip in new[]{"idle","move","cast","sweep","charge","hurt","death","entrance"})Clip(clip);}
        public static Sprite Loop(bool moving,int direction,float time)
        {
            var frames=Clip(moving?"move":"idle");
            return frames[Mathf.FloorToInt(Mathf.Max(0,time)*(moving?10f:5f))%frames.Length];
        }
        public const float RecoilDuration=.32f;
        public static Sprite Recoil(int direction,float time)=>Clip("hurt")[Mathf.Clamp((int)(time/RecoilDuration*8),0,7)];
        public static Sprite Action(CampaignBossAction action,int direction,int phase,float time,float phaseDuration)
        {
            bool charge=action==CampaignBossAction.SealCharge;
            var frames=Clip(charge?"charge":action==CampaignBossAction.SealSweep?"sweep":"cast");
            if(phase==0)return frames[Mathf.Clamp((int)(time/Mathf.Max(.01f,phaseDuration)*4),0,3)];
            // The compressed release pose stays committed throughout the rush;
            // braking is an authored recovery, after contact is disabled.
            if(phase==1)return charge?frames[4]:frames[4+Mathf.Clamp((int)(time/Mathf.Max(.01f,phaseDuration)*2),0,1)];
            const float recovery=.3f;
            if(time>=recovery)return Loop(false,direction,time-recovery);
            int first=charge?5:6;
            return frames[Mathf.Clamp(first+(int)(Mathf.Clamp01(time/recovery)*(8-first)),first,7)];
        }
        public static Sprite Action(bool cross,int direction,int phase,float time)=>
            Action(cross?CampaignBossAction.SealCross:CampaignBossAction.SealCharge,direction,phase,time,
                phase==0?(cross?1.05f:.85f):.44f);
        public static Sprite Entrance(float time)
        {
            const float assemble=1.05f;
            return time<assemble?Clip("entrance")[Mathf.Clamp((int)(time/assemble*8),0,7)]:Loop(false,0,time-assemble);
        }
        public static Sprite Death(int direction,float progress)=>Clip("death")[Mathf.Clamp((int)(Mathf.Clamp01(progress)*8),0,7)];
    }
}
