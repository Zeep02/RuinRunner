using UnityEngine;

namespace RuinRunner
{
    /// <summary>Catches what per-clip levels cannot: many sounds landing on the same instant. Sits on the AudioListener, so it sees the final mix.
    /// Pulls down at once and lets go slowly, so a pile-up comes out as one firm hit instead of a jump in volume.</summary>
    public sealed class MasterLimiter : MonoBehaviour
    {
        const float Ceiling = .6f, ReleaseSeconds = .18f;
        // Tests and tours are all started with --flags and must not be heard. The sandbox on its own is someone playing.
        public static readonly bool SilentRun = System.Array.Exists(System.Environment.GetCommandLineArgs(),
            argument => argument.StartsWith("--") && argument != CombatSandbox.CommandLineArgument);
        // A recorder after this filter needs the mix, and silences the output itself once it has it.
        public static bool OutputHandedOn;
        float gain = 1f, release;

        void Awake() { release = Mathf.Exp(-1f / (AudioSettings.outputSampleRate * ReleaseSeconds)); }

        void OnAudioFilterRead(float[] data, int channels)
        {
            for (var i = 0; i < data.Length; i += channels)
            {
                var peak = 0f;
                for (var c = 0; c < channels; c++) { var level = Mathf.Abs(data[i + c]); if (level > peak) peak = level; }
                var wanted = peak > Ceiling ? Ceiling / peak : 1f;
                gain = wanted < gain ? wanted : 1f - (1f - gain) * release;
                for (var c = 0; c < channels; c++) data[i + c] *= gain;
            }
            if (SilentRun && !OutputHandedOn) System.Array.Clear(data, 0, data.Length);
        }
    }
}
