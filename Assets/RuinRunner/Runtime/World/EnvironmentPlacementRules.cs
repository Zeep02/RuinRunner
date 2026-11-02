using System.Collections;
using System.Collections.Generic;
using RuinRunner.Generation;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Presentation-only placement constraints. The generated graph and walk mask remain authoritative.</summary>
    public sealed class EnvironmentPlacementRules
    {
        readonly StageTerrainLayout terrain;
        readonly GeneratedStage graph;
        readonly List<Rect> reservations = new List<Rect>();

        public EnvironmentPlacementRules(StageTerrainLayout terrain, GeneratedStage graph)
        {
            this.terrain = terrain;
            this.graph = graph;
            if (graph == null) return;
            foreach (var region in graph.Regions)
            {
                Reserve(region.PlayerPosition, 1.4f);
                Reserve(region.RewardPosition, 1.5f);
                Reserve(region.ServicePosition, 1.6f);
                Reserve(region.ExitPosition, 1.5f);
                foreach (var spawn in region.Module.SpawnCandidates) Reserve(region.Center + spawn, 1.2f);
                if (region.Role == RegionRole.Boss) Reserve(region.Center, 4f);
            }
            foreach (var connection in graph.Connections)
            {
                var from = graph.Regions[connection.FromIndex];
                var to = graph.Regions[connection.ToIndex];
                ReserveLane(GeneratedStageUnityAdapter.Vector(from.Center), GeneratedStageUnityAdapter.Vector(connection.Points[0]), connection.Width);
                for (int i = 1; i < connection.Points.Length; i++)
                    ReserveLane(GeneratedStageUnityAdapter.Vector(connection.Points[i - 1]), GeneratedStageUnityAdapter.Vector(connection.Points[i]), connection.Width);
                ReserveLane(GeneratedStageUnityAdapter.Vector(connection.Points[connection.Points.Length - 1]), GeneratedStageUnityAdapter.Vector(to.Center), connection.Width);
            }
        }

        void Reserve(MapPoint point, float radius)
        {
            var center = GeneratedStageUnityAdapter.Vector(point);
            reservations.Add(new Rect(center - Vector2.one * radius, Vector2.one * radius * 2));
        }

        void ReserveLane(Vector2 from, Vector2 to, float width)
        {
            var min = Vector2.Min(from, to) - Vector2.one * (width * .5f + .25f);
            var max = Vector2.Max(from, to) + Vector2.one * (width * .5f + .25f);
            reservations.Add(new Rect(min, max - min));
        }

        public bool IsReserved(Rect footprint)
        {
            foreach (var reserved in reservations) if (footprint.Overlaps(reserved)) return true;
            return false;
        }

        public bool HasFloor(Rect footprint)
        {
            int columns = Mathf.Max(1, Mathf.CeilToInt(footprint.width / .25f));
            int rows = Mathf.Max(1, Mathf.CeilToInt(footprint.height / .25f));
            for (int y = 0; y <= rows; y++) for (int x = 0; x <= columns; x++)
                if (!terrain.IsOpen(new Vector2(Mathf.Lerp(footprint.xMin, footprint.xMax, x / (float)columns),
                    Mathf.Lerp(footprint.yMin, footprint.yMax, y / (float)rows)))) return false;
            return true;
        }

        public bool CanPlaceSupported(int regionIndex, int supportIndex, Rect footprint)
        {
            if (graph == null || regionIndex < 0 || regionIndex >= graph.Regions.Length) return false;
            var region = graph.Regions[regionIndex];
            if (supportIndex < 0 || supportIndex >= region.Module.DecorationSupports.Length) return false;
            var source = region.Module.DecorationSupports[supportIndex].Translated(region.Center);
            var support = new Rect(source.X, source.Y, source.Width, source.Height);
            if (footprint.xMin < support.xMin || footprint.xMax > support.xMax || footprint.yMin < support.yMin || footprint.yMax > support.yMax) return false;
            return !IsReserved(footprint) && HasFloor(footprint);
        }

        public void BuildSupportedProps(Transform parent, int stageIndex, List<Rect> occupied)
        {
            var steps=BuildSupportedPropsSteps(parent,stageIndex,occupied,false);
            while(steps.MoveNext()){}
        }
        public IEnumerator BuildSupportedPropsProgressively(Transform parent,int stageIndex,List<Rect> occupied)
            =>BuildSupportedPropsSteps(parent,stageIndex,occupied,true);
        IEnumerator BuildSupportedPropsSteps(Transform parent,int stageIndex,List<Rect> occupied,bool progressive)
        {
            if (graph == null) yield break;
            var budget=new TerrainBuildBudget(progressive);
            foreach (var region in graph.Regions)
            {
                var random = new GenerationRandom(graph.Seed, graph.StageIndex, GenerationStream.Decoration, region.Index);
                for (int index = 0; index < region.Module.DecorationSupports.Length; index++)
                {
                    var source = region.Module.DecorationSupports[index].Translated(region.Center);
                    var candidates = new List<Vector2>();
                    for (float y = source.Y + .5f; y < source.Top; y += .5f)
                        for (float x = source.X + .5f; x < source.Right; x += .5f) candidates.Add(new Vector2(x, y));
                    random.Shuffle(candidates);
                    var sprite = stageIndex == 0 ? CainosRuinsArt.Sprite("Props/pot-" + (random.Next(2) == 0 ? "a" : "b")) :
                        StageTerrainArt.Sprite(stageIndex, "Props/urn", Vector2.zero);
                    foreach (var point in candidates)
                    {
                        var foot = new Rect(point - new Vector2(.38f, .18f), new Vector2(.76f, .48f));
                        if (!CanPlaceSupported(region.Index, index, foot)) continue;
                        var visual = new Rect(point + (Vector2)sprite.bounds.min - Vector2.one * .18f, (Vector2)sprite.bounds.size + Vector2.one * .36f);
                        bool blocked = false;
                        foreach (var other in occupied) if (visual.Overlaps(other)) { blocked = true; break; }
                        if (blocked) continue;
                        var go = new GameObject("Supported dressing " + region.Id + ":" + index);
                        go.transform.SetParent(parent, false); go.transform.position = point;
                        var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
                        renderer.sortingOrder = 900 - Mathf.RoundToInt(point.y * 100);
                        var placement = go.AddComponent<StageDecorationPlacement>();
                        placement.RegionIndex = region.Index; placement.SupportIndex = index; placement.Footprint = foot;
                        occupied.Add(visual);
                        break;
                    }
                    if(budget.Exhausted){yield return null;budget.Resume();}
                }
            }
        }
    }

    // Retains the accepted footprint so a native-world check can verify the actual rendered props.
    public sealed class StageDecorationPlacement : MonoBehaviour
    {
        public int RegionIndex, SupportIndex;
        public Rect Footprint;
    }
}
