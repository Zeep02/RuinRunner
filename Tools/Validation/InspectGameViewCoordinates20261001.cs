using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
public static class InspectGameViewCoordinates20261001
{
    public static string Main()
    {
        var view = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
        var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy;
        var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        var result = new JObject { { "pipeline", pipeline == null ? "BuiltIn" : pipeline.GetType().FullName }, { "playing", EditorApplication.isPlaying }, { "paused", EditorApplication.isPaused }, { "position", view.position.ToString() }, { "screen", Screen.width + "x" + Screen.height } };
        foreach (var p in view.GetType().GetProperties(flags))
            if (p.GetIndexParameters().Length == 0 && (p.Name.IndexOf("mouse", StringComparison.OrdinalIgnoreCase) >= 0 || p.Name.IndexOf("view", StringComparison.OrdinalIgnoreCase) >= 0 || p.Name.IndexOf("target", StringComparison.OrdinalIgnoreCase) >= 0 || p.Name.IndexOf("zoom", StringComparison.OrdinalIgnoreCase) >= 0))
                try { result[p.Name] = p.GetValue(view)?.ToString(); } catch {}
        return result.ToString();
    }
}
