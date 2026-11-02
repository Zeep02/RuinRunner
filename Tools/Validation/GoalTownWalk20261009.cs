using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using RuinRunner;
using UnityEngine;

// Movement and opening/closing services use only the normal GameAction path.
// No teleport, health/item/currency modification, time-scale change or service-state assignment.
public static class GoalTownWalk20261009
{
    static GameController game;
    static TownWorld town;
    static GoalTownWalkRunner20261009 runner;
    static bool wasEnabled, wasInitialized;
    static readonly List<string> checks = new List<string>();
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Static;
    static string output;
    public static object Main()
    {
        game = UnityEngine.Object.FindAnyObjectByType<GameController>();
        town = UnityEngine.Object.FindAnyObjectByType<TownWorld>();
        if (!Application.isPlaying || game == null || town == null || !game.IsTown || !game.SaveSlots.IsValidationProfile ||
            game.IsServiceOpen || game.IsPaused || game.SessionOperationPending || game.BackendBusy)
            throw new InvalidOperationException("An idle isolated validation profile in its real town is required.");
        if (runner != null || StageValidationInput.Enabled)
            throw new InvalidOperationException("Another input driver already owns validation input.");
        output = Path.GetFullPath("Logs/Validation/2026-10-09-goal/world/town-walk-" + DateTime.UtcNow.ToString("HHmmss") + ".txt");
        checks.Clear();
        wasEnabled = Read("enabled"); wasInitialized = Read("initialized");
        Set("initialized", true); Set("enabled", true);
        var host = new GameObject("Town GameAction walking check");
        runner = host.AddComponent<GoalTownWalkRunner20261009>();
        runner.Begin(Run(), Finish);
        return new { running = true, output, scope = "Normal input town walking only; no campaign completion claim" };
    }

    static IEnumerator Run()
    {
        foreach (var kind in new[] { FieldServiceKind.StartingGear, FieldServiceKind.Talents, FieldServiceKind.Expedition })
        {
            FieldServiceObject service = null;
            foreach (var candidate in town.GetComponentsInChildren<FieldServiceObject>()) if (candidate.Kind == kind) service = candidate;
            if (service == null) throw new InvalidOperationException("Missing town service " + kind);
            yield return WalkTo((Vector2)service.transform.position + Vector2.down * 1.25f);
            yield return null;
            if (game.NearbyService != service) throw new InvalidOperationException("Walking did not reach expected service " + kind);
            yield return Tap(GameAction.Interact);
            if (game.OpenService != service) throw new InvalidOperationException("Interact did not open " + kind);
            checks.Add("Walked to and opened " + service.Label + " via GameAction.Interact");
            yield return Tap(GameAction.Pause);
            if (game.IsServiceOpen) throw new InvalidOperationException("Pause did not close " + kind);
            for (int i = 0; i < 2; i++) yield return null;
        }
        yield return WalkTo(TownWorld.TrainingPosition + Vector2.down * 1.25f);
        checks.Add("Walked to the real training target approach");
        yield return WalkTo(TownWorld.Arrival);
        checks.Add("Walked back to arrival after all services");
    }

    static IEnumerator WalkTo(Vector2 target)
    {
        Physics2D.SyncTransforms();
        var obstacles = new List<Bounds>();
        foreach (var collider in town.GetComponentsInChildren<Collider2D>()) if (!collider.isTrigger) obstacles.Add(collider.bounds);
        var path = StageNavigation.FindPath(town.Terrain, game.Player.transform.position, target, obstacles.ToArray());
        if (path == null) throw new InvalidOperationException("No collision-clear town approach to " + target);
        float deadline = Time.realtimeSinceStartup + 15f;
        for (int index = 0; index < path.Count; index++)
        {
            while (Vector2.Distance(game.Player.transform.position, path[index]) > .24f)
            {
                if (!game.IsTown || game.IsPaused || game.Player == null) throw new InvalidOperationException("Town state changed during walking");
                if (Time.realtimeSinceStartup > deadline) throw new InvalidOperationException("Actual walking was blocked approaching " + target);
                var delta = path[index] - (Vector2)game.Player.transform.position;
                StageValidationInput.BeginFrame();
                StageValidationInput.SetHeld(GameAction.MoveLeft, delta.x < -.12f);
                StageValidationInput.SetHeld(GameAction.MoveRight, delta.x > .12f);
                StageValidationInput.SetHeld(GameAction.MoveDown, delta.y < -.12f);
                StageValidationInput.SetHeld(GameAction.MoveUp, delta.y > .12f);
                yield return null;
            }
        }
        StageValidationInput.ReleaseAll(); StageValidationInput.BeginFrame();
        yield return null;
        if (Vector2.Distance(game.Player.transform.position, target) > .7f) throw new InvalidOperationException("Path ended too far from requested approach");
    }

    static IEnumerator Tap(GameAction action)
    {
        StageValidationInput.BeginFrame(); StageValidationInput.SetHeld(action, true);
        yield return null;
        StageValidationInput.BeginFrame(); StageValidationInput.SetHeld(action, false);
        yield return null;
        StageValidationInput.BeginFrame();
    }

    static bool Read(string field) => (bool)typeof(StageValidationInput).GetField(field, Flags).GetValue(null);
    static void Set(string field, bool value) => typeof(StageValidationInput).GetField(field, Flags).SetValue(null, value);
    static void Finish(Exception error)
    {
        StageValidationInput.ReleaseAll(); StageValidationInput.BeginFrame();
        Set("enabled", wasEnabled); Set("initialized", wasInitialized);
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        File.WriteAllText(output, (error == null ? "PASS" : "FAIL") + " real GameAction town walking; not campaign play.\n" +
            string.Join("\n", checks) + (error == null ? "" : "\n" + error), new UTF8Encoding(false));
        if (error != null) Debug.LogError("[Town Walk] " + error);
        else Debug.Log("[Town Walk] PASS " + output);
        var completed = runner; runner = null;
        if (completed != null) UnityEngine.Object.Destroy(completed.gameObject);
    }
}

public sealed class GoalTownWalkRunner20261009 : MonoBehaviour
{
    IEnumerator job;
    Action<Exception> finished;
    float deadline;
    public void Begin(IEnumerator routine, Action<Exception> complete)
    {
        job = Flatten(routine); finished = complete; deadline = Time.realtimeSinceStartup + 80f;
        StartCoroutine(Advance());
    }
    IEnumerator Advance()
    {
        while (true)
        {
            bool next = false; Exception error = null; object current = null;
            try
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Town walking exceeded 80 seconds");
                next = job.MoveNext(); if (next) current = job.Current;
            }
            catch (Exception exception) { error = exception; }
            if (error != null || !next) { var callback = finished; finished = null; callback?.Invoke(error); yield break; }
            yield return current;
        }
    }
    static IEnumerator Flatten(IEnumerator source)
    {
        while (source.MoveNext())
        {
            if (source.Current is IEnumerator nested)
            {
                var flat = Flatten(nested); while (flat.MoveNext()) yield return flat.Current;
            }
            else yield return source.Current;
        }
    }
    void OnDestroy()
    {
        var callback = finished; finished = null;
        callback?.Invoke(new OperationCanceledException("Town walk runner was destroyed before completion"));
    }
}
