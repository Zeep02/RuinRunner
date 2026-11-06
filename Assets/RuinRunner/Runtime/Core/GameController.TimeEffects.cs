using System.Collections;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Hit stop and slow motion.</summary>
    public sealed partial class GameController
    {
        private void RefreshTimeScale()
        {
            Time.timeScale = IsPaused ? 0f : timeEffectActive ? timeEffectScale : 1f;
        }

        private void ResetTimeEffect()
        {
            timeEffectRoutine = null;
            timeEffectActive = false;
            timeEffectScale = 1f;
        }

        private void PlayHitStop(bool critical, bool boss)
        {
            if (IsPaused || timeEffectActive)
                return;
            if (critical)
                PlayTimeEffect(.075f, .05f);
            else
                PlayTimeEffect(boss ? .03f : .035f, .12f);
        }

        private void PlayMeleeFinisherHitStop()
        {
            if (IsPaused || IsStageClearPending || timeEffectActive)
                return;
            PlayTimeEffect(.07f, .06f);
        }

        // Boss phase changes and other authored beats take over any short hit stop.
        public void PlayDramaticSlow(float realSeconds, float scale)
        {
            if (IsPaused)
                return;
            PlayTimeEffect(realSeconds, scale);
        }

        private void PlayBossDefeatSlowMotion()
        {
            PlayTimeEffect(1.15f, .18f);
        }

        private void PlayMeleeHitStop(float realSeconds, float scale)
        {
            if (IsPaused || IsStageClearPending)
                return;
            if (timeEffectActive && timeEffectEndsAt - Time.unscaledTime > realSeconds)
                return;
            PlayTimeEffect(realSeconds, scale);
        }

        private float timeEffectEndsAt;
        private void PlayTimeEffect(float realSeconds, float scale)
        {
            timeEffectEndsAt = Time.unscaledTime + realSeconds;
            if (timeEffectRoutine != null)
                StopCoroutine(timeEffectRoutine);
            timeEffectRoutine = StartCoroutine(TimeEffectRoutine(realSeconds, scale));
        }

        private IEnumerator TimeEffectRoutine(float realSeconds, float scale)
        {
            timeEffectActive = true;
            timeEffectScale = Mathf.Clamp(scale, .01f, 1f);
            RefreshTimeScale();
            yield return new WaitForSecondsRealtime(realSeconds);
            timeEffectActive = false;
            timeEffectScale = 1f;
            timeEffectRoutine = null;
            RefreshTimeScale();
        }
    }
}
