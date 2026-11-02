using UnityEngine;

namespace RuinRunner
{
    public enum CampaignBossAction { None, SealSweep, SealCharge, SealCross, SealVolley, TideLanes, TidePools, TideFan, TideSurge }

    /// <summary>Action history and spacing, independent of loot/layout RNG.</summary>
    public sealed class CampaignBossBrain
    {
        readonly int[] ages = { 0, 0, 0, 0 };
        int last = -1, selections;
        public int Select(float distance, bool secondPhase, bool tide)
        {
            float best = float.NegativeInfinity; int selected = 0;
            for (int i = 0; i < 4; i++)
            {
                if (i == last) continue;
                // An action excluded by distance steadily gains priority. Standing
                // under the body cannot suppress space/ranged attacks indefinitely.
                float score = ages[i] * 2f;
                if (tide) score += i == 2 && distance < 4 ? 3 : i == 3 && distance > 5 ? 3 : 0;
                else score += i == 0 && distance < 3.4f ? 4 : i == 1 && distance > 4 ? 3 : i == 3 && distance > 6 ? 4 : 0;
                if (secondPhase)
                {
                    if (last == 1 && i == 2) score += 4;
                    if (last == 3 && i == 0) score += 3;
                }
                score += ((i + selections) % 4) * .07f;
                if (score > best) { best = score; selected = i; }
            }
            for (int i = 0; i < 4; i++) ages[i]++;
            ages[selected] = 0; last = selected; selections++;
            return selected;
        }
    }
}
