using UnityEngine;

namespace RuinRunner
{
    /// <summary>Window defaults, without fighting user resizing or validation resolutions.</summary>
    public sealed class GameDisplayPolicy : MonoBehaviour
    {
        public const int Width = 1280;
        public const int Height = 720;
        private static Vector2Int windowSize = new Vector2Int(Width, Height);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            var obj = new GameObject("Window presentation policy");
            DontDestroyOnLoad(obj); obj.AddComponent<GameDisplayPolicy>();
            if (Screen.width >= 640 && Screen.height >= 360)
                windowSize = new Vector2Int(Screen.width, Screen.height);
        }
        public static void Apply(bool fullscreen)
        {
            if (fullscreen)
            {
                if (!Screen.fullScreen) windowSize = new Vector2Int(Screen.width, Screen.height);
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            }
            else if (Screen.fullScreen)
                Screen.SetResolution(Mathf.Max(640, windowSize.x), Mathf.Max(360, windowSize.y), FullScreenMode.Windowed);
        }
        private void Update()
        {
            if (!Screen.fullScreen) windowSize = new Vector2Int(Screen.width, Screen.height);
        }
    }
}
