using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RuinRunner;
using RuinRunner.Generation;
using UnityEngine;

// Native support/anchor checks for the rendered decoration path. This is not normal campaign play.
public static class GoalWorld20261009
{
    public static object Main()
    {
        var checks = new List<string>();
        var samples = new List<string>();
        int props = 0;
        for (int stage = 0; stage < 3; stage++)
        for (int seed = 2026100400; seed < 2026100410; seed++)
        {
            var generated = GeneratedStageUnityAdapter.Create(seed, stage);
            var root = new GameObject("World fixture only");
            root.SetActive(false);
            try
            {
                var terrain = generated.BuildTerrain();
                var walk = (bool[])terrain.Walkable.Clone();
                var paths = (bool[])terrain.Path.Clone();
                var signature = generated.Graph.StructureSignature;
                var rules = new EnvironmentPlacementRules(terrain, generated.Graph);
                rules.BuildSupportedProps(root.transform, stage, new List<Rect>());
                var actual = root.GetComponentsInChildren<StageDecorationPlacement>(true);
                Require(actual.Length > 0, "support pads produce real props: " + stage + "/" + seed, checks);
                foreach (var prop in actual)
                {
                    var region = generated.Graph.Regions[prop.RegionIndex];
                    var pad = region.Module.DecorationSupports[prop.SupportIndex].Translated(region.Center);
                    var foot = prop.Footprint;
                    Require(foot.xMin >= pad.X && foot.xMax <= pad.Right && foot.yMin >= pad.Y && foot.yMax <= pad.Top,
                        "rendered base remains inside authored support", checks);
                    for (float y = foot.yMin; y <= foot.yMax + .001f; y += .1f)
                    for (float x = foot.xMin; x <= foot.xMax + .001f; x += .1f)
                        if (!terrain.IsOpen(new Vector2(x, y))) throw new InvalidOperationException("Unsupported rendered base: " + prop.name);
                    Require(!rules.IsReserved(foot), "rendered base avoids gameplay reservations", checks);
                    var renderer = prop.GetComponent<SpriteRenderer>();
                    Require(renderer != null && renderer.sprite != null && renderer.sprite.texture.filterMode == FilterMode.Point,
                        "rendered prop uses existing point-sampled sprite", checks);
                    Require(prop.GetComponent<Collider2D>() == null, "decorative prop does not add a hidden navigation blocker", checks);
                }
                for (int i = 0; i < walk.Length; i++)
                    if (walk[i] != terrain.Walkable[i] || paths[i] != terrain.Path[i]) throw new InvalidOperationException("Dressing mutated terrain");
                Require(ProceduralStageGenerator.Generate(seed, stage).StructureSignature == signature,
                    "dressing leaves seed/version signature unchanged", checks);
                props += actual.Length;
                samples.Add("stage=" + stage + " seed=" + seed + " props=" + actual.Length + " signature=" + signature);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                foreach (var room in generated.Rooms) UnityEngine.Object.DestroyImmediate(room);
            }
        }
        string output = Write("native-placement.txt", "PASS native placement fixture; not normal play.\nchecks=" + checks.Count + " props=" + props +
            "\n" + string.Join("\n", samples));
        return new { passed = true, samples = 30, checks = checks.Count, props, output };
    }

    public static object Objectives()
    {
        DungeonMap map = null;
        var checks = new List<string>();
        try
        {
            map = DungeonMap.BuildStage(_ => { }, 2026100400, 0);
            map.Root.SetActive(false);
            Require(map.GetObjectiveText(map.EntranceRoomIndex) == "길을 따라 유적 탐사", "safe entrance never asks for kills", checks);
            Require(map.GetObjectiveText(-1) == "길을 따라 다음 구역 탐사", "corridor has exploration instruction", checks);
            int combat = -1;
            foreach (var region in map.Graph.Regions)
            {
                if (region.Role == RegionRole.Combat && combat < 0) combat = region.Index;
                if (region.Role == RegionRole.Merchant) Require(map.GetObjectiveText(region.Index).Contains("상인"), "merchant objective", checks);
                if (region.Role == RegionRole.FieldUpgrade) Require(map.GetObjectiveText(region.Index).Contains("시설"), "field service objective", checks);
            }
            map.BeginEncounter(combat);
            Require(map.GetObjectiveText(combat).Contains("적을 처치"), "combat objective appears only in active encounter", checks);
            map.UnlockExit(combat);
            Require(!map.GetObjectiveText(combat).Contains("적을 처치"), "cleared region returns to exploration", checks);
            Require(map.GetObjectiveText(map.BossRoomIndex).Contains("남은 전투"), "boss prerequisite objective", checks);
            for (int i = 0; i < map.RoomCount; i++) if (i != map.BossRoomIndex && map.RequiresCombat(i)) map.UnlockExit(i);
            Require(map.GetObjectiveText(map.BossRoomIndex) == "수호자의 영역으로 진입", "ready boss objective", checks);
            Require(map.GetObjectiveText(map.BossRoomIndex, true) == "봉인석으로 이동", "exit objective", checks);
            Require(map.GetObjectiveText(map.BossRoomIndex, true, true).Contains("보상"), "unclaimed rewards take priority", checks);
            foreach (var kind in new[] { FieldServiceKind.Merchant, FieldServiceKind.ShapeAnvil, FieldServiceKind.OathStone, FieldServiceKind.ExchangeAltar, FieldServiceKind.Expedition })
            {
                var texture = IndividualArt.Texture(DungeonMapView.ServiceMapIcon(kind));
                Require(texture != null && texture.filterMode == FilterMode.Point && ItemIconLibrary.ContentBounds(texture).width > 0,
                    "map symbol source is readable and nonempty: " + kind, checks);
            }
            string output = Write("native-objectives.txt", "PASS supplied-state objective fixture; not normal play.\n" + string.Join("\n", checks));
            return new { passed = true, checks = checks.Count, output };
        }
        finally { if (map?.Root != null) UnityEngine.Object.DestroyImmediate(map.Root); }
    }

    public static object InspectLiveTown()
    {
        var town = UnityEngine.Object.FindAnyObjectByType<TownWorld>();
        if (town == null) throw new InvalidOperationException("Open the real town before this read-only check.");
        Physics2D.SyncTransforms();
        var barriers = new List<Bounds>();
        foreach (var collider in town.GetComponentsInChildren<Collider2D>()) if (!collider.isTrigger) barriers.Add(collider.bounds);
        var checks = new List<string>();
        foreach (var service in town.GetComponentsInChildren<FieldServiceObject>())
        {
            var approach = (Vector2)service.transform.position + Vector2.down;
            Require(StageNavigation.FindPath(town.Terrain, TownWorld.Arrival, approach, barriers.ToArray()) != null,
                "actual town collisions leave an approach to " + service.Label, checks);
        }
        Require(StageNavigation.FindPath(town.Terrain, TownWorld.Arrival, TownWorld.TrainingPosition + Vector2.down * .8f, barriers.ToArray()) != null,
            "actual town collisions leave the training approach open", checks);
        string pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null ? "BuiltIn" :
            UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType().FullName;
        string output = Write("live-town-navigation.txt", "PASS read-only live town reachability; not normal player walking.\npipeline=" + pipeline + "\n" + string.Join("\n", checks));
        return new { passed = true, checks = checks.Count, pipeline, output };
    }

    static string Write(string file, string text)
    {
        string output = Path.GetFullPath("Logs/Validation/2026-10-09-goal/world/" + file);
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        File.WriteAllText(output, text, new UTF8Encoding(false));
        return output;
    }
    static void Require(bool condition, string label, List<string> checks)
    {
        if (!condition) throw new InvalidOperationException(label);
        checks.Add(label);
    }
}
