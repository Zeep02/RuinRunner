using UnityEngine;

namespace RuinRunner
{
    public static class ArenaBuilder
    {
        public static bool HasBackgroundAsset() => EnvironmentArt.Available;
        // Isolated room previews and diagnostics use the same terrain builder as the complete stage.
        public static void BuildRoom(Transform dungeonRoot, RoomTemplate template, int roomIndex, Vector2 center, RoomTravelDirection? exitDirection)
        {
            var layout = new StageTerrainLayout(new[] { template }, new[] { center });
            ContinuousTerrainRenderer.Build(dungeonRoot, layout);
            if (template.decorations != null)
                foreach (var item in template.decorations)
                    if (layout.IsOpen(center + item.position))
                        EnvironmentProp.Create(dungeonRoot, center + item.position, item.kind, Mathf.Max(.5f, item.scale));
        }
    }
}
