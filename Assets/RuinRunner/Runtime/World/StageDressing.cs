using System.Collections;
using System.Collections.Generic;
using RuinRunner.Generation;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Makes a room look used. Things that belong together are placed together: a few jars in a corner, rocks and reeds along a wall,
    /// a broken post with rubble out in the open. Spawn points, anchors and doorways stay free.</summary>
    public static class StageDressing
    {
        // Sprites live in Campaign/Dressing/<Theme>/ as flat-<name> (walked over) and stand-<name> (upright and solid).
        // Nothing here looks like a box or a chest: those mean a reward in this game.
        static readonly string[][][] WallGroups =
        {
            new[] { new[] { "jar", "pot" }, new[] { "pot", "pot" }, new[] { "rocks", "grass", "mushrooms" }, new[] { "bush", "grass" }, new[] { "stump", "grass" }, new[] { "mushrooms", "grass" } },
            new[] { new[] { "barrel", "pot" }, new[] { "pot", "pot" }, new[] { "rocks", "reeds" }, new[] { "coralrock", "rocks" }, new[] { "stump", "reeds" }, new[] { "pipe", "reeds" } },
            new[] { new[] { "pot", "pot" }, new[] { "candles", "pot" }, new[] { "rocks", "crystal" }, new[] { "stump", "chains" }, new[] { "skull", "pot" }, new[] { "crystal", "rocks" }, new[] { "brazier", "rocks" } },
        };
        // Pieces that are never a still picture: how the piece itself moves, and what comes off it, measured from its foot.
        const float LivingPieceFramesPerSecond = 10f;
        static readonly Dictionary<string, (string clip, string fx, Vector2 fxAt)> LivingPieces = new Dictionary<string, (string, string, Vector2)>
        {
            ["Outer/mushrooms"] = ("PropMushrooms", "FxPropSpores", new Vector2(0f, .2f)),
            ["Outer/grass"] = ("PropGrass", "FxPropPollen", new Vector2(0f, .25f)),
            ["Outer/bush"] = ("PropBush", "FxPropPollen", new Vector2(0f, .3f)),
            ["Outer/stump"] = ("PropStumpIvy", null, Vector2.zero),
            ["Flooded/reeds"] = ("PropReeds", "FxPropBubbles", new Vector2(0f, .1f)),
            ["Flooded/coralrock"] = ("PropCoral", "FxPropBubbles", new Vector2(0f, .15f)),
            ["Flooded/pipe"] = (null, "FxPropPipeDrip", new Vector2(.22f, 0f)),
            ["Flooded/barrel"] = ("PropBarrel", "FxPropEmbersTeal", new Vector2(0f, .72f)),
            ["Core/brazier"] = ("PropBrazier", "FxPropEmbers", new Vector2(0f, .47f)),
            ["Core/candles"] = ("PropCandles", "FxPropEmbers", new Vector2(0f, .62f)),
            ["Core/crystal"] = ("PropCrystal", "FxPropCrystalGlints", Vector2.zero),
            ["Core/chains"] = ("PropChains", null, Vector2.zero),
        };
        // Out on the floor there is only what can be walked through, so a fight is never snagged on scenery.
        static readonly string[][][] OpenGroups =
        {
            new[] { new[] { "grass", "mushrooms" }, new[] { "bush", "grass" }, new[] { "mushrooms", "grass" } },
            new[] { new[] { "reeds", "reeds" }, new[] { "reeds" } },
            new[] { new[] { "candles", "candles" }, new[] { "candles" } },
        };
        static readonly HashSet<string> Soft = new HashSet<string> { "grass", "bush", "mushrooms", "reeds", "candles" };
        static readonly HashSet<string> Fragile = new HashSet<string> { "pot", "jar", "skull" };
        static DressingKind KindOf(string piece) => Soft.Contains(piece) ? DressingKind.Soft : Fragile.Contains(piece) ? DressingKind.Fragile : piece == "barrel" ? DressingKind.Volatile : DressingKind.Sturdy;
        // The burning skull dishes that keep a room lit, in the stage's own colour of fire.
        static readonly string[] SkullClips = { "AmbSkullGold", "AmbSkullTeal", "AmbSkullPale" };
        const int SkullsPerRoom = 4;
        const float SkullSpacing = 5f;
        // Small things that gather around the foot of a wall or a group.
        static readonly string[][] Patches =
        {
            new[] { "moss", "leaves", "pebbles", "flowers" },
            new[] { "algae", "shells", "lilies", "ripple" },
            new[] { "ash", "shards" },
        };
        // Larger pieces that sit alone on the open floor.
        static readonly string[][] LoneDetails =
        {
            new[] { "crack", "tile", "roots", "bones" },
            new[] { "puddle", "crack", "grate", "coral" },
            new[] { "chain", "plate", "glyph", "wax" },
        };
        const float WallStepsPerGroup = 13f, FloorSquaresPerOpenGroup = 60f, FloorSquaresPerLoneDetail = 26f, WallStepsPerPatch = 6f;
        const float GroupSpacing = 3.5f, PieceSpacing = .72f, ClearOfRoomCenter = 3f, LoneDetailSpacing = 3.5f, WallFaceDrop = .5f;

        internal static IEnumerator Scatter(Transform parent, StageTerrainLayout terrain, GeneratedStage graph, EnvironmentPlacementRules rules, int stage, List<Rect> occupied, TerrainBuildBudget budget)
        {
            var root = new GameObject("Stage dressing"); root.transform.SetParent(parent, false);
            var theme = StageTerrainArt.Theme(stage);
            AmbientMotes.Create(root.transform, stage);
            foreach (var region in graph.Regions)
            {
                var random = new GenerationRandom(graph.Seed, graph.StageIndex, GenerationStream.Decoration, region.Index + 1000);
                var open = new List<Vector2>(); var wallSide = new List<Vector2>(); var corners = new List<Vector2>(); var seen = new HashSet<Vector2Int>();
                foreach (var piece in region.Module.FloorPieces)
                    for (var y = Mathf.FloorToInt(piece.Y + region.Center.Y); y < Mathf.CeilToInt(piece.Top + region.Center.Y); y++)
                    for (var x = Mathf.FloorToInt(piece.X + region.Center.X); x < Mathf.CeilToInt(piece.Right + region.Center.X); x++)
                    {
                        if (!seen.Add(new Vector2Int(x, y))) continue;
                        var point = new Vector2(x + .5f, y + .5f);
                        if (!terrain.IsOpen(point)) continue;
                        var walls = BlockedSides(terrain, point);
                        if (walls >= 2) corners.Add(point);
                        else if (walls == 1) wallSide.Add(point);
                        else open.Add(point);
                    }
                random.Shuffle(open); random.Shuffle(wallSide); random.Shuffle(corners);
                var anchors = new List<Vector2>();
                var center = GeneratedStageUnityAdapter.Vector(region.Center);
                // Corners first: that is where things end up in a real room.
                PlaceSkulls(root.transform, corners, wallSide, anchors, stage, terrain, rules, occupied);
                var wallGroups = Mathf.Max(2, Mathf.RoundToInt((wallSide.Count + corners.Count) / WallStepsPerGroup));
                var placed = PlaceGroups(root.transform, corners, wallGroups, WallGroups[stage], anchors, center, 0f, stage, theme, ref random, terrain, rules, occupied);
                PlaceGroups(root.transform, wallSide, wallGroups - placed, WallGroups[stage], anchors, center, 0f, stage, theme, ref random, terrain, rules, occupied);
                if (budget.Exhausted) { yield return null; budget.Resume(); }
                PlaceGroups(root.transform, open, Mathf.Max(1, Mathf.RoundToInt(open.Count / FloorSquaresPerOpenGroup)), OpenGroups[stage], anchors, center, ClearOfRoomCenter, stage, theme, ref random, terrain, rules, occupied);
                PlaceFloorDetails(root.transform, open, wallSide, anchors, stage, theme, ref random, terrain);
                PlaceLife(root.transform, open, wallSide, stage, ref random, terrain);
                if (budget.Exhausted) { yield return null; budget.Resume(); }
            }
        }

        // A north wall's face hangs down over the floor beneath it, and things stand up from their feet, so the spot
        // that leans on a north wall is a row further out than the one that leans on any other wall.
        static readonly Vector2[] WallReach = { Vector2.up * 2f, Vector2.down, Vector2.left, Vector2.right };

        static int BlockedSides(StageTerrainLayout terrain, Vector2 point)
        {
            var sides = 0;
            foreach (var step in WallReach)
                if (!terrain.IsOpen(point + step)) sides++;
            return sides;
        }

        // Pointing out of the wall this spot leans on, or zero in the open.
        static Vector2 AwayFromWall(StageTerrainLayout terrain, Vector2 point)
        {
            foreach (var step in WallReach)
                if (!terrain.IsOpen(point + step)) return -step.normalized;
            return Vector2.zero;
        }

        /// <summary>Whether all of a picture lies on bare floor: off the walls, off their faces and off the solid islands.</summary>
        public static bool OnBareFloor(StageTerrainLayout terrain, Rect picture)
        {
            var columns = Mathf.Max(1, Mathf.CeilToInt(picture.width / .25f));
            var rows = Mathf.Max(1, Mathf.CeilToInt(picture.height / .25f));
            for (var y = 0; y <= rows; y++) for (var x = 0; x <= columns; x++)
            {
                var point = new Vector2(Mathf.Lerp(picture.xMin, picture.xMax, x / (float)columns), Mathf.Lerp(picture.yMin, picture.yMax, y / (float)rows));
                if (!terrain.IsOpen(point) || !terrain.IsOpen(point + Vector2.up * WallFaceDrop)) return false;
            }
            return true;
        }

        static int PlaceGroups(Transform parent, List<Vector2> spots, int wanted, string[][] choices, List<Vector2> anchors, Vector2 roomCenter, float clearOfCenter, int stage, string theme, ref GenerationRandom random, StageTerrainLayout terrain, EnvironmentPlacementRules rules, List<Rect> occupied)
        {
            var placed = 0;
            foreach (var spot in spots)
            {
                if (placed >= wanted) break;
                if (Vector2.Distance(spot, roomCenter) < clearOfCenter || NearAny(anchors, spot, GroupSpacing)) continue;
                var pieces = choices[random.Next(choices.Length)];
                var away = AwayFromWall(terrain, spot);
                // A row along the wall it leans on; out in the open, a row across the view.
                var along = Mathf.Abs(away.x) > .5f ? Vector2.up : Vector2.right;
                var any = false;
                for (var i = 0; i < pieces.Length; i++)
                {
                    if (pieces[i] == "skull") { PlaceSkull(parent, spot, stage, rules, occupied); any = true; continue; }
                    var sprite = IndividualArt.Sprite("Campaign/Dressing/" + theme + "/stand-" + pieces[i]);
                    var point = spot + along * ((i - (pieces.Length - 1) * .5f) * PieceSpacing) + away * (i % 2 == 0 ? 0f : .18f);
                    point = new Vector2(Mathf.Round(point.x * 32f) / 32f, Mathf.Round(point.y * 32f) / 32f);
                    var foot = new Rect(point - new Vector2(.3f, .14f), new Vector2(.6f, .34f));
                    if (rules.IsReserved(foot) || !rules.HasFloor(foot)) continue;
                    var visual = new Rect(point + (Vector2)sprite.bounds.min, sprite.bounds.size);
                    var blocked = !OnBareFloor(terrain, visual);
                    foreach (var other in occupied) if (visual.Overlaps(other)) { blocked = true; break; }
                    if (blocked) continue;
                    if (LivingPieces.TryGetValue(theme + "/" + pieces[i], out var living))
                    {
                        var prop = EnvironmentProp.CreateDressing(parent, point, sprite, KindOf(pieces[i]), foot.size, living.clip, LivingPieceFramesPerSecond);
                        if (living.fx != null) prop.AttachAmbient(living.fx, living.fxAt);
                    }
                    else EnvironmentProp.CreateDressing(parent, point, sprite, KindOf(pieces[i]), foot.size);
                    occupied.Add(visual);
                    any = true;
                }
                if (!any) continue;
                anchors.Add(spot);
                placed++;
            }
            return placed;
        }

        // Corners first, then along walls, spaced so they light the room instead of bunching.
        static void PlaceSkulls(Transform parent, List<Vector2> corners, List<Vector2> wallSide, List<Vector2> anchors, int stage, StageTerrainLayout terrain, EnvironmentPlacementRules rules, List<Rect> occupied)
        {
            var placed = new List<Vector2>();
            foreach (var spots in new[] { corners, wallSide })
                foreach (var spot in spots)
                {
                    if (placed.Count >= SkullsPerRoom) return;
                    if (NearAny(placed, spot, SkullSpacing) || !PlaceSkull(parent, spot, stage, rules, occupied)) continue;
                    placed.Add(spot);
                    anchors.Add(spot);
                }
        }

        static bool PlaceSkull(Transform parent, Vector2 spot, int stage, EnvironmentPlacementRules rules, List<Rect> occupied)
        {
            var point = new Vector2(Mathf.Round(spot.x * 32f) / 32f, Mathf.Round(spot.y * 32f) / 32f);
            var foot = new Rect(point - new Vector2(.3f, .14f), new Vector2(.6f, .3f));
            // Only the dish and the flame count: the rest of the clip's picture is light falling on whatever is near.
            var visual = new Rect(point - new Vector2(.4f, .1f), new Vector2(.8f, 1.1f));
            if (rules.IsReserved(foot) || !rules.HasFloor(foot)) return false;
            foreach (var other in occupied) if (visual.Overlaps(other)) return false;
            EnvironmentProp.CreateDressing(parent, point, null, DressingKind.Fragile, foot.size, SkullClips[stage]);
            occupied.Add(visual);
            return true;
        }

        // What lives here. None of it touches anything; it is there so a room is never a still picture.
        static void PlaceLife(Transform parent, List<Vector2> open, List<Vector2> wallSide, int stage, ref GenerationRandom random, StageTerrainLayout terrain)
        {
            var share = open.Count / 90 + 1;
            if (stage == 0)
                for (var i = 0; i < share && i < open.Count; i++)
                    AmbientFx.Create(parent, open[random.Next(open.Count)] + Vector2.up * 1.1f, "AmbButterfly", .42f).GetComponent<SpriteRenderer>().sortingOrder = 2001;
            if (stage == 1)
            {
                var drips = 0;
                foreach (var spot in wallSide)
                {
                    if (drips >= share + 1) break;
                    // Water runs off the north wall, so a drip needs one above it.
                    if (terrain.IsOpen(spot + Vector2.up * 2f)) continue;
                    AmbientFx.Create(parent, spot + Vector2.down * .3f, "AmbDrip", .6f);
                    drips++;
                }
                for (var i = 0; i < share * 2 && i < open.Count; i++)
                    AmbientFx.Create(parent, open[random.Next(open.Count)], "AmbRipples", .42f, true);
            }
            if (stage == 2)
            {
                for (var i = 0; i < share && i < wallSide.Count; i++)
                    AmbientFx.Create(parent, wallSide[random.Next(wallSide.Count)], "AmbSpirit", .55f);
                for (var i = 0; i < share && i < open.Count; i++)
                    AmbientFx.Create(parent, open[random.Next(open.Count)] + Vector2.up * 1.6f, "AmbBat", .42f).GetComponent<SpriteRenderer>().sortingOrder = 2001;
            }
        }

        static bool NearAny(List<Vector2> points, Vector2 point, float distance)
        {
            foreach (var other in points) if ((other - point).sqrMagnitude < distance * distance) return true;
            return false;
        }

        static void PlaceFloorDetails(Transform parent, List<Vector2> open, List<Vector2> wallSide, List<Vector2> anchors, int stage, string theme, ref GenerationRandom random, StageTerrainLayout terrain)
        {
            var patches = Patches[stage];
            // Moss, pebbles and the like gather at the foot of groups and creep along walls.
            foreach (var anchor in anchors)
                Patch(parent, anchor + Vector2.down * .35f, patches[random.Next(patches.Length)], patches[random.Next(patches.Length)], theme, ref random, terrain);
            var wallPatches = Mathf.RoundToInt(wallSide.Count / WallStepsPerPatch);
            for (var i = 0; i < wallPatches && i < wallSide.Count; i++)
                Patch(parent, wallSide[wallSide.Count - 1 - i], patches[random.Next(patches.Length)], patches[random.Next(patches.Length)], theme, ref random, terrain);
            // Cracks and stray pieces break up the open floor.
            var lone = LoneDetails[stage];
            var used = new List<Vector2>();
            var wanted = Mathf.RoundToInt(open.Count / FloorSquaresPerLoneDetail);
            var next = random.Next(lone.Length);
            for (var i = 0; i < open.Count && used.Count < wanted; i++)
            {
                var point = open[open.Count - 1 - i];
                if (NearAny(used, point, LoneDetailSpacing) || NearAny(anchors, point, 2f)) continue;
                FloorDetail(parent, point, lone[next++ % lone.Length], theme, ref random, terrain);
                used.Add(point);
            }
        }

        static void Patch(Transform parent, Vector2 center, string first, string second, string theme, ref GenerationRandom random, StageTerrainLayout terrain)
        {
            FloorDetail(parent, center, first, theme, ref random, terrain);
            if (first == second) return;
            var angle = random.Next(360) * Mathf.Deg2Rad;
            FloorDetail(parent, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * .8f, second, theme, ref random, terrain);
        }

        static void FloorDetail(Transform parent, Vector2 point, string kind, string theme, ref GenerationRandom random, StageTerrainLayout terrain)
        {
            // Whole pixels, so the art never lands between the floor's own pixels.
            point = new Vector2(Mathf.Round(point.x * 32f) / 32f, Mathf.Round(point.y * 32f) / 32f);
            var sprite = IndividualArt.Sprite("Campaign/Dressing/" + theme + "/flat-" + kind);
            if (!OnBareFloor(terrain, new Rect(point + (Vector2)sprite.bounds.min, sprite.bounds.size))) return;
            var view = new GameObject("Floor detail").AddComponent<SpriteRenderer>();
            view.transform.SetParent(parent, false); view.transform.position = point;
            view.sprite = sprite;
            view.flipX = random.Next(2) == 0;
            // A little under the floor's own brightness, so details settle into it instead of sitting on top.
            view.color = new Color(.84f, .84f, .84f, 1f);
            view.sortingLayerName = "WorldFloor"; view.sortingOrder = 3;
        }
    }
}
