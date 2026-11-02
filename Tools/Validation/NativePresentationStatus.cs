using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class NativePresentationStatus
{
    public static object Main()
    {
        var pipeline = GraphicsSettings.currentRenderPipeline;
        return new
        {
            pipeline = pipeline == null ? "BuiltIn" : pipeline.GetType().FullName,
            playing = Application.isPlaying,
            compiling = EditorApplication.isCompiling,
            scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
            actors = Object.FindObjectsByType<RuinRunner.EnemyController>().Length,
            players = Object.FindObjectsByType<RuinRunner.PlayerController>().Length
        };
    }
}
