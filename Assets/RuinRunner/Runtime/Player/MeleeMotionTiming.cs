using UnityEngine;

namespace RuinRunner
{
    /// <summary>Shared phase boundaries for the body, held blade and damage contact.</summary>
    public static class MeleeMotionTiming
    {
        public const float Contact = .3f;
        public const float Recovery = .58f;
        public const float ThrustStart = .20f;
        public const float ThrustContact = .34f;
        public const float ThrustEnd = .56f;
        public const float CrossFirstContact=.22f;
        public const float CrossSwitch=.34f;
        public const float CrossSecondContact=.52f;
        public static int CrossStroke(float progress)=>progress<CrossSwitch?0:1;
        public static float CrossStrikeOffset(int stroke)=>stroke==0?-35f:35f;
        public static float CrossPoseProgress(float progress)
        {
            if(progress<CrossSwitch)
                return progress<CrossFirstContact?progress/CrossFirstContact*Contact:
                    Mathf.Lerp(Contact,1,(progress-CrossFirstContact)/(CrossSwitch-CrossFirstContact));
            return progress<CrossSecondContact?(progress-CrossSwitch)/(CrossSecondContact-CrossSwitch)*Contact:
                Mathf.Lerp(Contact,1,(progress-CrossSecondContact)/(1-CrossSecondContact));
        }
        public static float CrossBladeAngle(float progress)
        {
            var stroke=CrossStroke(progress);var phase=CrossPoseProgress(progress);
            // Both authored wrist clips meet at their identical neutral pose.
            // The diagonal offset also returns to zero at that seam and at rest.
            var weight=phase<Contact?Mathf.Clamp01(phase/.18f):1-Mathf.InverseLerp(Contact,1,phase);
            return BladeAngle(phase,false,stroke)+CrossStrikeOffset(stroke)*weight;
        }



        public static float BladeAngle(float progress, bool heavy, int comboStep)
        {
            var start = heavy ? -135f : -92f;
            var end = heavy ? 100f : 68f;
            float angle;
            if (progress < .18f) angle = Mathf.Lerp(0f, start, Ease(progress / .18f));
            else if (progress < Contact) angle = Mathf.Lerp(start, 0f, Ease((progress - .18f) / (Contact - .18f)));
            else if (progress < Recovery) angle = Mathf.Lerp(0f, end, Ease((progress - Contact) / (Recovery - Contact)));
            else angle = Mathf.Lerp(end, 0f, Ease((progress - Recovery) / (1f - Recovery)));
            return angle * (!heavy && (comboStep & 1) == 1 ? -1f : 1f);
        }

        public static float ActionAngle(float progress,bool heavy,int step,MeleeAttackMotion motion)
        {
            if(motion==MeleeAttackMotion.CrossCut)return CrossBladeAngle(progress);
            if(motion==MeleeAttackMotion.Thrust)return 0;
            if(motion==MeleeAttackMotion.Cleave||motion==MeleeAttackMotion.Impact)
            {
                if(progress<.18f)return Mathf.Lerp(0,120,Ease(progress/.18f));
                if(progress<Contact)return Mathf.Lerp(120,0,Ease((progress-.18f)/(Contact-.18f)));
                if(progress<Recovery)return Mathf.Lerp(0,-28,Ease((progress-Contact)/(Recovery-Contact)));
                return Mathf.Lerp(-28,0,Ease((progress-Recovery)/(1-Recovery)));
            }
            return BladeAngle(progress,heavy,step);
        }

        private static float Ease(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
