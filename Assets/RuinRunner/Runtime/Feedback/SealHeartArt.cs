using UnityEngine;

namespace RuinRunner
{
    /// <summary>Omnidirectional seal core. Every clip starts from and returns to one shared stance.</summary>
    public static class SealHeartArt
    {
        public const string Root="Pilot/SealHeart/";
        public static Sprite[] Clip(string clip,int direction=0)
        {
            // Preserve old diagnostic entry points; the nonhuman core has no
            // anatomical front/back face, and locked ground tells show its aim.
            if(clip=="walk")clip="move";
            else if(clip=="swipe")clip="sweep";
            else if(clip=="stomp"||clip=="attack02")clip="cast";
            else if(clip=="attack03")clip="charge";
            return IndividualArt.Frames(Root+clip,clip=="idle"||clip=="cast"||clip=="sweep"||clip=="charge"?24:clip=="hurt"?8:16);
        }
        public static Sprite[] Frames(int state,int direction=0)=>Clip(state==0?"idle":state==1?"move":state==2?"cast":"death",direction);
        public static void Preload()
        {foreach(string clip in new[]{"idle","move","cast","sweep","charge","hurt","death"})Clip(clip);}
        public static Sprite Loop(bool moving,int direction,float time)
        {
            var frames=Clip(moving?"move":"idle");
            return frames[Mathf.FloorToInt(Mathf.Max(0,time)*15f)%frames.Length];
        }
        public const float RecoilDuration=.32f;
        public static Sprite Recoil(int direction,float time)=>Clip("hurt")[Mathf.Clamp((int)(time/RecoilDuration*8),0,7)];
        public static Sprite Action(CampaignBossAction action,int direction,int phase,float time,float phaseDuration)
        {
            bool charge=action==CampaignBossAction.SealCharge;
            var frames=Clip(charge?"charge":action==CampaignBossAction.SealSweep?"sweep":"cast");
            if(phase==0)return frames[Mathf.Clamp((int)(time/Mathf.Max(.01f,phaseDuration)*12),0,11)];
            if(phase==1)return frames[12+Mathf.Clamp((int)(time/Mathf.Max(.01f,phaseDuration)*4),0,3)];
            const float recovery=.3f;
            if(time>=recovery)return Loop(false,direction,time-recovery);
            return frames[16+Mathf.Clamp((int)(time/recovery*8),0,7)];
        }
        public static Sprite Action(bool cross,int direction,int phase,float time)=>
            Action(cross?CampaignBossAction.SealCross:CampaignBossAction.SealCharge,direction,phase,time,
                phase==0?(cross?1.05f:.85f):.44f);
        public static Sprite Entrance(float time)
        {
            // The collapse played backward is the seal pulling itself together.
            const float assemble=1.05f;
            if(time>=assemble)return Loop(false,0,time-assemble);
            var frames=Clip("death");
            return frames[frames.Length-1-Mathf.Clamp((int)(time/assemble*frames.Length),0,frames.Length-1)];
        }
        public static Sprite Death(int direction,float progress)=>Clip("death")[Mathf.Clamp((int)(Mathf.Clamp01(progress)*16),0,15)];
    }
}
