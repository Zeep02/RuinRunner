using System;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>The minimap and Tab map sample the same continuous walk mask as world collision.</summary>
    public sealed class DungeonMapView : IDisposable
    {
        private DungeonMap owner;
        private Texture2D map;
        private int revealedThrough = -1;
        private static GUIStyle frameStyle;
        private FieldServiceObject[] services;
        private static readonly Color PlayerInk = new Color(.45f, 1f, .85f);

        public void Dispose()
        {
            if (map != null)
                UnityEngine.Object.Destroy(map);
            map = null;
            owner = null;
            services = null;
            revealedThrough = -1;
        }

        private void Prepare(GameController game)
        {
            if (owner != game.DungeonMap)
            {
                Dispose();
                owner = game.DungeonMap;
            }
            if (services == null || services.Length == 0)
                services = owner.Root.GetComponentsInChildren<FieldServiceObject>();
            if (map != null && revealedThrough == owner.MapRevision)
                return;
            if (map != null)
                UnityEngine.Object.Destroy(map);
            revealedThrough = owner.MapRevision;
            var terrain = owner.Terrain;
            // The display needs two samples per world tile, not the collision mask's eight.
            // Sampling the same terrain avoids allocating multi-megapixel textures on every discovery.
            const float sampleSize = .5f;
            var mapWidth = Mathf.CeilToInt(terrain.Bounds.width / sampleSize);
            var mapHeight = Mathf.CeilToInt(terrain.Bounds.height / sampleSize);
            var pixels = new Color32[mapWidth * mapHeight];
            for (var y = 0; y < mapHeight; y++)
                for (var x = 0; x < mapWidth; x++)
                {
                    var p = terrain.Bounds.min + new Vector2((x + .5f) * sampleSize, (y + .5f) * sampleSize);
                    if (!terrain.IsOpen(p))
                        continue;
                    var visible = false;
                    var current = false;
                    for (var i = 0; i < owner.RoomCount; i++)
                    {
                        if (owner.Regions[i].Phase == EncounterRegionPhase.Undiscovered)
                            continue;
                        var local = p - owner.GetRoomCenter(i);
                        visible |= new Rect(-owner.GetRoom(i).Size * .5f - Vector2.one * 3f, owner.GetRoom(i).Size + Vector2.one * 6f).Contains(local);
                        current |= i == owner.PhysicalRegion && StageTerrainLayout.ContainsRoom(owner.GetRoom(i), local);
                    }
                    for (var i = 0; i < owner.ConnectionCount && i < terrain.Connections.Length; i++)
                    {
                        var connection = owner.GetConnection(i);
                        if (connection == null || !ConnectionExplored(connection))
                            continue;
                        visible |= StageTerrainLayout.PathDistance(p, terrain.Connections[i]) < terrain.Connections[i].width;
                    }
                    if (owner.Definition?.explorationRewards != null && owner.Definition.explorationRewards.Length > 0)
                        for (var branch = 0; branch < terrain.Exploration.Length; branch++)
                            if (owner.Regions[owner.Definition.explorationRewards[Mathf.Min(branch, owner.Definition.explorationRewards.Length - 1)].region].Phase != EncounterRegionPhase.Undiscovered)
                                visible |= StageTerrainLayout.PathDistance(p, terrain.Exploration[branch]) < terrain.Exploration[branch].width;
                    if (!visible)
                        continue;
                    var edge = !terrain.IsOpen(p + Vector2.left * sampleSize) || !terrain.IsOpen(p + Vector2.right * sampleSize) ||
                        !terrain.IsOpen(p + Vector2.down * sampleSize) || !terrain.IsOpen(p + Vector2.up * sampleSize);
                    pixels[y * mapWidth + x] = edge ? new Color32(211, 231, 218, 255) :
                        current ? new Color32(45, 98, 81, 230) : new Color32(26, 47, 43, 225);
                }
            map = new Texture2D(mapWidth, mapHeight, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            map.SetPixels32(pixels);
            map.Apply();
        }

        public void DrawMini(GameController game, float width)
        {
            if (game.DungeonMap == null || game.Player == null)
                return;
            Prepare(game);
            var panel = new Rect(width - 204, 18, 186, 138);
            Panel(panel, .78f);
            var area = new Rect(panel.x + 5, panel.y + 5, panel.width - 10, panel.height - 10);
            var windowSize = new Vector2(34f, 34f * area.height / area.width);
            var world = new Rect((Vector2)game.Player.transform.position - windowSize * .5f, windowSize);
            DrawWorld(game, area, world, false);
        }

        public void DrawExpanded(GameController game, float width, float height)
        {
            if (game.DungeonMap == null || game.Player == null)
                return;
            Prepare(game);
            var panel = new Rect(width * .08f, height * .1f, width * .84f, height * .8f);
            Panel(panel, .95f);
            var region = new Rect(panel.x + 25, panel.y + 25, panel.width - 50, panel.height - 50);
            var world = owner.Terrain.Bounds;
            var scale = Mathf.Min(region.width / world.width, region.height / world.height);
            var area = new Rect(region.center - world.size * scale * .5f, world.size * scale);
            DrawWorld(game, area, world, true);
        }

        private void DrawWorld(GameController game, Rect area, Rect world, bool expanded)
        {
            var bounds = owner.Terrain.Bounds;
            var uv = new Rect((world.min - bounds.min) / bounds.size, world.size / bounds.size);
            GUI.BeginGroup(area);
            GUI.DrawTextureWithTexCoords(new Rect(0, 0, area.width, area.height), map, uv);
            for (var i = 0; i < owner.RoomCount; i++)
            {
                if (owner.Regions[i].Phase == EncounterRegionPhase.Undiscovered)
                    continue;
                var center = ToArea(owner.GetRoomCenter(i), world, area.size);
                var role = owner.GetRegionRole(i);
                // A fight still ahead is the only thing a room needs to say.
                if (owner.Regions[i].Phase != EncounterRegionPhase.Cleared && (role == Generation.RegionRole.Combat || role == Generation.RegionRole.Boss))
                    Icon(center, role == Generation.RegionRole.Boss ? "Map/Boss" : "Map/Combat", expanded ? 28 : 16, false);
                if (i == owner.EntranceRoomIndex)
                    Icon(ToArea(owner.GetSpawnPoint(i), world, area.size), "Map/Portal", expanded ? 26 : 16, false);
                if (owner.Regions[i].Chest != null && !owner.Regions[i].Chest.IsOpened)
                    Icon(ToArea(owner.Regions[i].Chest.transform.position, world, area.size), "Map/Treasure", expanded ? 24 : 14, false);
            }
            for (var i = 0; i < owner.ConnectionCount; i++)
            {
                var connection = owner.GetConnection(i);
                if (connection == null)
                    continue;
                var delta = connection.ArrivalPosition - connection.DeparturePosition;
                var vertical = Mathf.Abs(delta.y) > Mathf.Abs(delta.x);
                var size = vertical ? new Vector2(9, 3) : new Vector2(3, 9);
                var color = connection.IsUnlocked ? new Color(.7f, .85f, .6f) : new Color(1f, .75f, .35f);
                if (RegionDiscovered(connection.SourceRoom))
                    Marker(ToArea(connection.DeparturePosition, world, area.size), color, size);
                if (connection.IsGraphConnection && RegionDiscovered(connection.TargetRoom))
                    Marker(ToArea(connection.ArrivalPosition, world, area.size), color, size);
            }
            foreach (var service in services)
            {
                if (service == null || !RegionDiscovered(service.RegionIndex))
                    continue;
                Icon(ToArea(service.transform.position, world, area.size), ServiceMapIcon(service.Kind), expanded ? 26 : 16, service.Used);
            }
            foreach (var chest in RuntimeSceneObjects.FindAll<RewardChest>())
                if (!chest.IsOpened && chest.ExplorationRegion >= 0 && owner.Regions[chest.ExplorationRegion].Phase == EncounterRegionPhase.Cleared)
                {
                    var point = ToArea(chest.transform.position, world, area.size);
                    Icon(point, "Map/Treasure", expanded ? 24 : 14, false);
                }
            if (RegionDiscovered(owner.BossRoomIndex))
                Icon(ToArea(game.StageExitPosition, world, area.size), game.IsStageResultReady ? "Map/Portal" : "Map/PortalSealed", expanded ? 26 : 16, false);
            if (game.Boss != null)
                Marker(ToArea(game.Boss.transform.position, world, area.size), new Color(1f, .35f, .35f), Vector2.one * 6);
            Marker(ToArea(game.Player.transform.position, world, area.size), PlayerInk, Vector2.one * (expanded ? 7 : 5));
            GUI.EndGroup();
        }

        public static string ServiceMapIcon(FieldServiceKind kind)
        {
            switch (kind)
            {
                case FieldServiceKind.Merchant:
                    return "Map/Merchant";
                case FieldServiceKind.ShapeAnvil:
                    return "Map/Anvil";
                case FieldServiceKind.OathStone:
                    return "Map/OathStone";
                case FieldServiceKind.ExchangeAltar:
                    return "Map/Altar";
                default:
                    return "Map/Portal";
            }
        }

        static void Icon(Vector2 point, string id, float size, bool used)
        {
            var old = GUI.color;
            var rect = new Rect(Mathf.Round(point.x - size * .5f), Mathf.Round(point.y - size * .5f), size, size);
            GUI.color = used ? new Color(.5f, .56f, .53f, .85f) : Color.white;
            ItemIconLibrary.DrawGUI(rect, IndividualArt.Texture(id));
            GUI.color = old;
        }

        private static Vector2 ToArea(Vector2 point, Rect world, Vector2 size) =>
            new Vector2((point.x - world.xMin) / world.width * size.x, (world.yMax - point.y) / world.height * size.y);
        private bool RegionDiscovered(int index) => index >= 0 && index < owner.RoomCount &&
            owner.Regions[index].Phase != EncounterRegionPhase.Undiscovered;
        private bool ConnectionExplored(RoomExit connection) =>
            connection.SourceRoom >= 0 && connection.SourceRoom < owner.RoomCount && owner.Regions[connection.SourceRoom].Phase == EncounterRegionPhase.Cleared ||
            connection.TargetRoom >= 0 && connection.TargetRoom < owner.RoomCount && owner.Regions[connection.TargetRoom].Phase == EncounterRegionPhase.Cleared;
        public static Vector2 ToMap(Vector2 local, RoomTemplate room, Rect area) => area.position + ToArea(local, room.LocalBounds, area.size);

        // Editor/isolated room preview uses the same region silhouette; the in-game map uses the full terrain mask above.
        public static Texture2D CreateFootprint(RoomTemplate room)
        {
            var w = Mathf.CeilToInt(room.Size.x * 4);
            var h = Mathf.CeilToInt(room.Size.y * 4);
            var pixels = new Color32[w * h];
            var terrain = new StageTerrainLayout(new[] { room }, new[] { Vector2.zero });
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    var p = new Vector2((x + .5f) / w * room.Size.x, (y + .5f) / h * room.Size.y) - room.Size * .5f;
                    if (terrain.IsOpen(p))
                        pixels[y * w + x] = new Color32(98, 142, 116, 255);
                }
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static void Panel(Rect rect, float alpha)
        {
            if (frameStyle == null)
            {
                frameStyle = new GUIStyle { border = PixelUiSkin.FrameBorder };
                frameStyle.normal.background = PixelUiSkin.GetInterfaceElement(PixelUiElement.Panel);
            }
            var old = GUI.color;
            GUI.color = new Color(1, 1, 1, alpha);
            GUI.Box(rect, GUIContent.none, frameStyle);
            GUI.color = old;
        }
        private static void Marker(Vector2 point, Color color, Vector2 size)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(new Vector2(Mathf.Round(point.x - size.x / 2), Mathf.Round(point.y - size.y / 2)), size), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
