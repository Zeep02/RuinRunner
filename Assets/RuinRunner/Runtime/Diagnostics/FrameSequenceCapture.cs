using System;
using System.IO;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Saves the screen at a steady rate, for checking how a transition or effect moves: --capture-frames &lt;folder&gt; &lt;seconds between&gt; &lt;seconds in&gt; &lt;count&gt;.</summary>
    public sealed class FrameSequenceCapture : MonoBehaviour
    {
        string folder;
        float every, next;
        int left, index;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            var arguments = Environment.GetCommandLineArgs();
            var at = Array.IndexOf(arguments, "--capture-frames");
            if (at < 0 || at + 4 >= arguments.Length) return;
            var root = new GameObject("FrameSequenceCapture");
            DontDestroyOnLoad(root);
            var capture = root.AddComponent<FrameSequenceCapture>();
            capture.folder = arguments[at + 1];
            capture.every = float.Parse(arguments[at + 2], System.Globalization.CultureInfo.InvariantCulture);
            capture.next = float.Parse(arguments[at + 3], System.Globalization.CultureInfo.InvariantCulture);
            capture.left = int.Parse(arguments[at + 4]);
            Directory.CreateDirectory(capture.folder);
        }

        void Update()
        {
            if (left <= 0 || Time.unscaledTime < next) return;
            next += every;
            left--;
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, (index++).ToString("0000") + ".png"));
        }
    }
}
