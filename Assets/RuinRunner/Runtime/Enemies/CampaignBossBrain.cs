namespace RuinRunner
{
    public enum CampaignBossAction
    {
        None, SealSweep, SealCharge, SealCross, SealVolley, TideLanes, TidePools, TideFan, TideSurge,
        TideMaelstrom, TideWaveWall, TideDive, TideGeysers, SealBeams, SealBlink, SealJudgment, SealBloom
    }

    /// <summary>Action history and spacing, independent of loot/layout RNG.</summary>
    public sealed class CampaignBossBrain
    {
        // The first four are the plain moves; the rest are the set pieces, one of which should never be far off.
        const int Actions = 8, PlainActions = 4;
        readonly int[] ages = new int[Actions];
        int last = -1, selections;
        public int Select(float distance, bool secondPhase, bool tide)
        {
            float best = float.NegativeInfinity;
            int selected = 0;
            for (int i = 0; i < Actions; i++)
            {
                if (i == last)
                    continue;
                // An action excluded by distance steadily gains priority. Standing
                // under the body cannot suppress space/ranged attacks indefinitely.
                float score = ages[i] * 2f;
                if (tide)
                    score += i == 2 && distance < 4 ? 3 : i == 3 && distance > 5 ? 3 : 0;
                else
                    score += i == 0 && distance < 3.4f ? 4 : i == 1 && distance > 4 ? 3 : i == 3 && distance > 6 ? 4 : 0;
                if (last < PlainActions && i >= PlainActions)
                    score += secondPhase ? 6 : 4;
                if (secondPhase)
                {
                    if (last == 1 && i == 2)
                        score += 4;
                    if (last == 3 && i == 0)
                        score += 3;
                }
                score += ((i + selections) % Actions) * .07f;
                if (score > best)
                {
                    best = score;
                    selected = i;
                }
            }
            for (int i = 0; i < Actions; i++)
                ages[i]++;
            ages[selected] = 0;
            last = selected;
            selections++;
            return selected;
        }
    }
}
