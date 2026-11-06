using System;
using System.Collections.Generic;

namespace RuinRunner.Generation
{
    /// <summary>Composes each room from the run's seed. Floor reaches all four doorways through two bands; each corner of the room is
    /// kept, cut back or stepped, so the outline differs. Combat rooms then get an interior plan (pillar rows, a central block, a ring,
    /// staggered blocks, offset walls or a scatter), and the anchors and enemy spawn points are found on whatever floor is left.</summary>
    public static class SpatialModuleCatalog
    {
        // Largest room is 28 by 20. The generator's room spacing leaves six units of solid ground beyond that.
        const int BossHalfWidth = 14, BossHalfHeight = 10;
        // Rooms without a plan keep their middle open: rewards, services and exits sit there. Combat rooms find those spots afterwards.
        const int ClearHalfWidth = 4, ClearHalfHeight = 6;
        // The floor bands that reach the doorways.
        const int BandHalfWidth = 4;
        // A corridor is 6 wide. Nothing stands in front of a doorway for this depth.
        const int LaneHalfWidth = 3, DoorDepth = 5;
        const int CornerWhole = 0, CornerCut = 1, CornerStepped = 2;
        // Corners in counterclockwise order, so turning a shape is a change of starting corner.
        static readonly int[][] RoomShapes =
        {
            new[] { CornerWhole, CornerWhole, CornerWhole, CornerWhole },
            new[] { CornerCut, CornerCut, CornerCut, CornerCut },
            new[] { CornerWhole, CornerCut, CornerWhole, CornerCut },
            new[] { CornerWhole, CornerWhole, CornerCut, CornerCut },
            new[] { CornerWhole, CornerWhole, CornerWhole, CornerCut },
            new[] { CornerStepped, CornerStepped, CornerStepped, CornerStepped },
            new[] { CornerWhole, CornerStepped, CornerCut, CornerStepped },
            new[] { CornerStepped, CornerCut, CornerStepped, CornerCut },
        };
        static readonly int[][] Corners = { new[] { 1, 1 }, new[] { -1, 1 }, new[] { -1, -1 }, new[] { 1, -1 } };
        // Every size here has drawn landmarks to match: StageLandmarks picks by footprint.
        static readonly int[][] ScatterSizes = { new[] { 2, 2 }, new[] { 2, 2 }, new[] { 3, 3 }, new[] { 3, 3 }, new[] { 5, 2 }, new[] { 2, 4 } };
        static readonly int[][] BlockSizes = { new[] { 4, 4 }, new[] { 5, 2 }, new[] { 2, 4 }, new[] { 3, 3 } };

        static readonly string[][] Names =
        {
            new[] { "무너진 교차 마당", "엇갈린 정원", "붕괴 광장", "행렬의 옛길", "갈라진 기단", "세 단의 정원", "끊긴 회랑", "날개 성문" },
            new[] { "저수조 선착장", "쌍둑 수로", "계단 물받이", "굽은 부두", "수문 열주실", "가지 저수로", "가라앉은 뜰", "펌프실" },
            new[] { "봉인 십자실", "어긋난 금고", "기둥 왕관실", "긴 성소", "갈래 기록실", "삼중 봉인대", "쌍 유물실", "갈라진 성채" },
        };
        static readonly string[] Themes = { "Ruins", "Flooded", "Core" };

        static MapRect R(float x, float y, float w, float h) => new MapRect(x, y, w, h);

        sealed class Plan
        {
            public HashSet<long> Cells;
            public List<MapRect> Placed = new List<MapRect>();
            public int HalfWidth, HalfHeight, WallMargin;
            public bool KeepMiddle;
        }

        public static ModuleDefinition Compose(int stage, RegionRole role, ref GenerationRandom random)
        {
            if (stage < 0 || stage >= Themes.Length)
                throw new ArgumentOutOfRangeException(nameof(stage));
            bool boss = role == RegionRole.Boss, combat = role == RegionRole.Combat;
            int halfWidth = boss ? BossHalfWidth : combat ? 11 + random.Next(4) : 9 + random.Next(3);
            int halfHeight = boss ? BossHalfHeight : combat ? 8 + random.Next(3) : 6 + random.Next(2);

            // Bands along both axes reach all four room edges so any doorway has floor behind it.
            var pieces = new List<MapRect> { R(-halfWidth, -BandHalfWidth, halfWidth * 2, BandHalfWidth * 2), R(-BandHalfWidth, -halfHeight, BandHalfWidth * 2, halfHeight * 2) };
            var shape = boss ? RoomShapes[0] : RoomShapes[random.Next(RoomShapes.Length)];
            int turn = random.Next(4);
            for (int i = 0; i < 4; i++)
                AddCorner(pieces, Corners[i], shape[(i + turn) % 4], halfWidth, halfHeight, ref random);
            var cells = new HashSet<long>();
            foreach (var r in pieces)
                for (int y = (int)r.Y; y < r.Top; y++)
                    for (int x = (int)r.X; x < r.Right; x++)
                        cells.Add(Key(x, y));

            var plan = new Plan { Cells = cells, HalfWidth = halfWidth, HalfHeight = halfHeight, WallMargin = combat ? 2 : 1, KeepMiddle = !combat };
            if (combat)
            {
                PlaceInterior(plan, ref random);
                Scatter(plan, 4 + random.Next(3), ref random);
            }
            else if (boss)
                PlaceBossPillars(plan, ref random);
            else
                Scatter(plan, 1 + random.Next(2), ref random);

            var taken = new List<MapPoint>();
            var player = FindSpot(plan, new MapPoint(-2, 0), taken);
            var reward = FindSpot(plan, new MapPoint(0, -2.75f), taken);
            var service = FindSpot(plan, new MapPoint(0, 2.5f), taken);
            var exit = FindSpot(plan, new MapPoint(0, 5), taken);
            var spawns = PlaceSpawns(plan, player, taken, ref random);
            var names = Names[stage];
            var signature = halfWidth + "x" + halfHeight + "-" + cells.Count + "-" + plan.Placed.Count;
            foreach (var o in plan.Placed)
                signature += "-" + (int)o.X + "," + (int)o.Y;
            return new ModuleDefinition
            {
                Id = Themes[stage] + "-" + signature,
                DisplayName = names[random.Next(names.Length)],
                Theme = Themes[stage],
                FloorPieces = pieces.ToArray(),
                Obstacles = plan.Placed.ToArray(),
                Bounds = new MapRect(-halfWidth, -halfHeight, halfWidth * 2, halfHeight * 2),
                Outline = TraceExterior(cells),
                PlayerAnchor = player,
                RewardAnchor = reward,
                ServiceAnchor = service,
                ExitAnchor = exit,
                SpawnCandidates = spawns,
                DecorationSupports = Array.Empty<MapRect>()
            };
        }

        // Every piece has a corner on the room's centre, so the union is a staircase in each quarter and can never leave a hole.
        static void AddCorner(List<MapRect> pieces, int[] corner, int kind, int halfWidth, int halfHeight, ref GenerationRandom random)
        {
            int wideX = halfWidth - random.Next(2), wideY = halfHeight - random.Next(2);
            int shortX = Math.Min(halfWidth, 6 + random.Next(2)), shortY = Math.Min(halfHeight, 5 + random.Next(2));
            if (kind == CornerWhole)
                pieces.Add(Corner(corner, wideX, wideY));
            else if (kind == CornerCut)
                pieces.Add(Corner(corner, shortX, shortY));
            else
            {
                pieces.Add(Corner(corner, wideX, shortY));
                pieces.Add(Corner(corner, shortX, wideY));
            }
        }
        static MapRect Corner(int[] corner, int w, int h) => R(corner[0] > 0 ? 0 : -w, corner[1] > 0 ? 0 : -h, w, h);

        static void PlaceInterior(Plan plan, ref GenerationRandom random)
        {
            int hw = plan.HalfWidth, hh = plan.HalfHeight;
            switch (random.Next(6))
            {
                case 0: // Two rows of pillars along the room, a few of them fallen.
                    {
                        int spacing = 4 + random.Next(2), row = 3 + random.Next(2);
                        for (int i = -3; i <= 3; i++)
                            foreach (int side in new[] { row, -row - 2 })
                                if (random.Next(5) > 0)
                                    Try(plan, i * spacing - 1, side, 2, 2);
                        break;
                    }
                case 1: // Two rows across the room.
                    {
                        int spacing = 4 + random.Next(2), column = 4 + random.Next(2);
                        for (int i = -2; i <= 2; i++)
                            foreach (int side in new[] { column, -column - 2 })
                                if (random.Next(5) > 0)
                                    Try(plan, side, i * spacing - 1, 2, 2);
                        break;
                    }
                case 2: // One large structure in the middle; the fight circles it.
                    {
                        var size = BlockSizes[random.Next(BlockSizes.Length)];
                        Try(plan, -size[0] / 2 + random.Next(3) - 1, -size[1] / 2 + random.Next(3) - 1, size[0], size[1]);
                        break;
                    }
                case 3: // A broken ring around an open centre.
                    {
                        int across = hw - 5, up = hh - 4;
                        foreach (var step in new[] { new[] { 2, 0 }, new[] { 1, 1 }, new[] { 0, 2 }, new[] { -1, 1 }, new[] { -2, 0 }, new[] { -1, -1 }, new[] { 0, -2 }, new[] { 1, -1 } })
                            if (random.Next(4) > 0)
                                Try(plan, step[0] * across / 2 - 1, step[1] * up / 2 - 1, 2, 2);
                        break;
                    }
                case 4: // Staggered blocks to weave through.
                    {
                        int shift = random.Next(2);
                        for (int row = -1; row <= 1; row++)
                            for (int column = -2; column <= 2; column++)
                                if ((row + column + shift) % 2 == 0)
                                    Try(plan, column * 5 - 1, row * 5 - 1, 3, 3);
                        break;
                    }
                default: // Two long structures offset from each other, so the way through bends.
                    {
                        int flip = random.Next(2) == 0 ? 1 : -1;
                        Try(plan, flip > 0 ? -hw + 3 : hw - 8, 2 + random.Next(2), 5, 2);
                        Try(plan, flip > 0 ? hw - 8 : -hw + 3, -4 - random.Next(2), 5, 2);
                        Try(plan, -1 + (flip > 0 ? 3 : -3) - 1, -2 + random.Next(2), 3, 3);
                        break;
                    }
            }
        }

        static void PlaceBossPillars(Plan plan, ref GenerationRandom random)
        {
            // The arena stays readable: the same pillar in each corner, set a little differently each time.
            int size = 2 + random.Next(2), across = 7 + random.Next(3), up = 4 + random.Next(2);
            foreach (var corner in Corners)
                Try(plan, corner[0] > 0 ? across : -across - size, corner[1] > 0 ? up : -up - size, size, size);
        }

        static void Scatter(Plan plan, int wanted, ref GenerationRandom random)
        {
            for (int attempt = 0; attempt < 200 && plan.Placed.Count < wanted; attempt++)
            {
                var size = ScatterSizes[random.Next(ScatterSizes.Length)];
                Try(plan, -plan.HalfWidth + random.Next(plan.HalfWidth * 2 - size[0] + 1), -plan.HalfHeight + random.Next(plan.HalfHeight * 2 - size[1] + 1), size[0], size[1]);
            }
        }

        static bool Try(Plan plan, int x, int y, int w, int h)
        {
            var candidate = R(x, y, w, h);
            if (!Allowed(plan, candidate))
                return false;
            plan.Placed.Add(candidate);
            return true;
        }

        static bool Allowed(Plan plan, MapRect r)
        {
            if (plan.KeepMiddle)
            {
                if (r.X < ClearHalfWidth && r.Right > -ClearHalfWidth && r.Y < ClearHalfHeight && r.Top > -ClearHalfHeight)
                    return false;
                if (r.X < LaneHalfWidth && r.Right > -LaneHalfWidth)
                    return false;
                if (r.Y < LaneHalfWidth && r.Top > -LaneHalfWidth)
                    return false;
            }
            // In front of each doorway.
            if (r.Y < LaneHalfWidth && r.Top > -LaneHalfWidth && (r.Right > plan.HalfWidth - DoorDepth || r.X < -plan.HalfWidth + DoorDepth))
                return false;
            if (r.X < LaneHalfWidth && r.Right > -LaneHalfWidth && (r.Top > plan.HalfHeight - DoorDepth || r.Y < -plan.HalfHeight + DoorDepth))
                return false;
            // Room to walk all the way around it. Separate islands can never cut the floor in two.
            int margin = plan.WallMargin;
            for (int y = (int)r.Y - margin; y < r.Top + margin; y++)
                for (int x = (int)r.X - margin; x < r.Right + margin; x++)
                    if (!plan.Cells.Contains(Key(x, y)))
                        return false;
            foreach (var other in plan.Placed)
                if (r.X - 2 < other.Right && r.Right + 2 > other.X && r.Y - 2 < other.Top && r.Top + 2 > other.Y)
                    return false;
            return true;
        }

        // The nearest free floor to where this anchor would like to be.
        static MapPoint FindSpot(Plan plan, MapPoint wanted, List<MapPoint> taken)
        {
            bool found = false;
            var best = wanted;
            float bestDistance = float.MaxValue;
            if (SpotIsFree(plan, wanted, taken))
            {
                taken.Add(wanted);
                return wanted;
            }
            for (float y = -plan.HalfHeight; y <= plan.HalfHeight; y += .5f)
                for (float x = -plan.HalfWidth; x <= plan.HalfWidth; x += .5f)
                {
                    var point = new MapPoint(x, y);
                    var distance = Distance(point, wanted);
                    if (distance >= bestDistance || !SpotIsFree(plan, point, taken))
                        continue;
                    best = point;
                    bestDistance = distance;
                    found = true;
                }
            if (!found)
                throw new InvalidOperationException("No free floor for a room anchor");
            taken.Add(best);
            return best;
        }
        static bool SpotIsFree(Plan plan, MapPoint point, List<MapPoint> taken)
        {
            foreach (var other in taken)
                if (Distance(point, other) < 2f)
                    return false;
            if (InsideAny(plan.Placed, point))
                return false;
            for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                    if (!plan.Cells.Contains(Key((int)Math.Floor(point.X + x * 1f), (int)Math.Floor(point.Y + y * .8f))))
                        return false;
            return true;
        }

        static MapPoint[] PlaceSpawns(Plan plan, MapPoint player, List<MapPoint> anchors, ref GenerationRandom random)
        {
            int hw = plan.HalfWidth, hh = plan.HalfHeight;
            var doors = new[] { new MapPoint(hw, 0), new MapPoint(-hw, 0), new MapPoint(0, hh), new MapPoint(0, -hh) };
            var candidates = new List<MapPoint>();
            for (int y = -hh + 2; y <= hh - 2; y++)
                for (int x = -hw + 2; x <= hw - 2; x++)
                {
                    var point = new MapPoint(x, y);
                    if (Distance(point, player) < 4f || !HasMargin(plan.Cells, point) || InsideAny(plan.Placed, point))
                        continue;
                    bool clear = true;
                    foreach (var anchor in anchors)
                        if (Distance(point, anchor) < 2f)
                            clear = false;
                    // Nothing appears in the player's face as they step through a doorway.
                    foreach (var door in doors)
                        if (Distance(point, door) < 5f)
                            clear = false;
                    if (clear)
                        candidates.Add(point);
                }
            if (candidates.Count == 0)
                throw new InvalidOperationException("No floor for enemy spawns");
            random.Shuffle(candidates);
            var spots = new List<MapPoint>();
            foreach (float spacing in new[] { 2.5f, 1.5f })
                foreach (var point in candidates)
                {
                    if (spots.Count >= 8)
                        break;
                    bool clear = true;
                    foreach (var other in spots)
                        if (Distance(point, other) < spacing)
                            clear = false;
                    if (clear)
                        spots.Add(point);
                }
            return spots.ToArray();
        }

        static bool HasMargin(HashSet<long> cells, MapPoint point)
        {
            for (int y = -2; y <= 1; y++)
                for (int x = -2; x <= 1; x++)
                    if (!cells.Contains(Key((int)Math.Floor(point.X) + x, (int)Math.Floor(point.Y) + y)))
                        return false;
            return true;
        }
        static bool InsideAny(List<MapRect> rects, MapPoint point)
        {
            foreach (var r in rects)
                if (point.X > r.X - 1.5f && point.X < r.Right + 1.5f && point.Y > r.Y - 1.5f && point.Y < r.Top + 1.5f)
                    return true;
            return false;
        }
        static float Distance(MapPoint a, MapPoint b) => (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        static MapPoint[] TraceExterior(HashSet<long> cells)
        {
            var edges = new Dictionary<long, long>();
            foreach (var key in cells)
            {
                int x = (int)(key >> 32), y = (int)key;
                if (!cells.Contains(Key(x, y - 1)))
                    edges.Add(Key(x, y), Key(x + 1, y));
                if (!cells.Contains(Key(x + 1, y)))
                    edges.Add(Key(x + 1, y), Key(x + 1, y + 1));
                if (!cells.Contains(Key(x, y + 1)))
                    edges.Add(Key(x + 1, y + 1), Key(x, y + 1));
                if (!cells.Contains(Key(x - 1, y)))
                    edges.Add(Key(x, y + 1), Key(x, y));
            }
            long start = long.MaxValue;
            foreach (var key in edges.Keys)
                if (key < start)
                    start = key;
            long current = start;
            var points = new List<MapPoint>();
            do
            {
                points.Add(new MapPoint((int)(current >> 32), (int)current));
                current = edges[current];
                if (points.Count > edges.Count)
                    throw new InvalidOperationException("Unclosed module boundary");
            } while (current != start);
            if (points.Count != edges.Count)
                throw new InvalidOperationException("Module floor has a hole or disconnected component; express holes as obstacles");
            for (int i = points.Count - 1; i >= 0; i--)
            {
                var a = points[(i + points.Count - 1) % points.Count];
                var b = points[i];
                var c = points[(i + 1) % points.Count];
                if ((b.X - a.X) * (c.Y - b.Y) == (b.Y - a.Y) * (c.X - b.X))
                    points.RemoveAt(i);
            }
            return points.ToArray();
        }
        internal static long Key(int x, int y) => ((long)x << 32) | (uint)y;
    }
}
