using System;
using UnityEditor;
using UnityEngine;

// Existing Game View only; no scene, asset, player settings or game rules modified.
public static class ShowStandingReviewGameView20261002
{
    public static object Main()
    {
        var type = Type.GetType("UnityEditor.GameView,UnityEditor");
        if (type == null) throw new Exception("Game View unavailable");
        // A hidden main window does not become visible merely by focusing a dock.
        // A separate existing Unity Game View exposes the actual rendered game.
        foreach (var item in Resources.FindObjectsOfTypeAll(type))
            ((EditorWindow)item).Close();
        var view = (EditorWindow)ScriptableObject.CreateInstance(type);
        view.titleContent = new GUIContent("Game · stance review");
        view.ShowUtility();
        view.position = new Rect(40, 40, 1320, 790);
        view.Focus();
        view.Repaint();
        return new { window = view.GetType().Name, width = view.position.width,
            height = view.position.height,
            active = UnityEditorInternal.InternalEditorUtility.isApplicationActive };
    }
}
