using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Shared top-down room geometry and authoring checks, in room-local XY units.</summary>
    public static class RoomLayoutRules
    {
        public static readonly Vector2 RewardPosition = new Vector2(0f, -2.75f);
        public static readonly Vector2 FootOffset = new Vector2(0f, -.28f);
        // Match PlayerController's .68 x .46 collider, including its root offset.
        public static readonly Vector2 FootHalfSize = new Vector2(.34f, .23f);
        public const float GridStep = .5f;
        private static int Columns(RoomTemplate room) => Mathf.FloorToInt((room.Size.x - 1f) / GridStep) + 1;
        private static int Rows(RoomTemplate room) => Mathf.FloorToInt((room.Size.y - 1.5f) / GridStep) + 1;

        public static bool IsClear(RoomTemplate room, Vector2 root, Vector2 halfSize)
        {
            var foot = root + FootOffset;
            var bounds = room.LocalBounds;
            if (foot.x - halfSize.x < bounds.xMin || foot.x + halfSize.x > bounds.xMax ||
                foot.y - halfSize.y < bounds.yMin || foot.y + halfSize.y > bounds.yMax)
                return false;
            for (var x = -1; x <= 1; x += 2)
                for (var y = -1; y <= 1; y += 2)
                    if (!StageTerrainLayout.ContainsRoom(room, foot + new Vector2(halfSize.x * x, halfSize.y * y)))
                        return false;
            if (room.platforms == null)
                return true;
            foreach (var obstacle in room.platforms)
            {
                var delta = foot - obstacle.position;
                if (Mathf.Abs(delta.x) < obstacle.size.x * .5f + halfSize.x &&
                    Mathf.Abs(delta.y) < obstacle.size.y * .5f + halfSize.y)
                    return false;
            }
            return true;
        }

        public static bool SegmentIsClear(RoomTemplate room, Vector2 from, Vector2 to)
        {
            var steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / .1f));
            for (var index = 0; index <= steps; index++)
                if (!IsClear(room, Vector2.Lerp(from, to, index / (float)steps), FootHalfSize))
                    return false;
            return true;
        }

        // Used by the diagnostic pilot as well as data validation. Returns actual
        // waypoints; it never moves or teleports actors and does not change physics.
        public static List<Vector2> FindPath(RoomTemplate room, Vector2 from, Vector2 to)
        {
            if (!IsClear(room, from, FootHalfSize) || !IsClear(room, to, FootHalfSize))
                return null;
            // The diagnostic sends digital WASD, not an analog vector. A clear
            // arbitrary-angle line is not the staircase that those inputs follow.
            if ((Mathf.Abs(from.x - to.x) < .06f || Mathf.Abs(from.y - to.y) < .06f) &&
                SegmentIsClear(room, from, to))
                return new List<Vector2> { to };
            var columns = Columns(room);
            var rowCount = Rows(room);
            var visited = new bool[columns * rowCount];
            var previous = new int[visited.Length];
            var queue = new Queue<int>();
            var start = NearestVisibleNode(room, from);
            var goal = NearestVisibleNode(room, to);
            if (start < 0 || goal < 0)
                return null;
            queue.Enqueue(start);
            visited[start] = true;
            previous[start] = -1;
            var offsets = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == goal)
                {
                    var path = new List<Vector2> { to };
                    for (var cursor = goal; cursor >= 0; cursor = previous[cursor])
                        path.Add(NodePosition(room, cursor));
                    path.Reverse();
                    return path;
                }
                foreach (var offset in offsets)
                {
                    var x = current % columns + offset.x;
                    var y = current / columns + offset.y;
                    if (x < 0 || x >= columns || y < 0 || y >= rowCount)
                        continue;
                    var next = y * columns + x;
                    if (visited[next] || !SegmentIsClear(room, NodePosition(room, current), NodePosition(room, next)))
                        continue;
                    visited[next] = true;
                    previous[next] = current;
                    queue.Enqueue(next);
                }
            }
            return null;
        }

        private static Vector2 NodePosition(RoomTemplate room, int index) => room.LocalBounds.min + new Vector2(.5f + index % Columns(room) * GridStep, 1f + index / Columns(room) * GridStep);

        private static int NearestVisibleNode(RoomTemplate room, Vector2 point)
        {
            var best = float.PositiveInfinity;
            var chosen = -1;
            for (var index = 0; index < Columns(room) * Rows(room); index++)
            {
                var node = NodePosition(room, index);
                var distance = (node - point).sqrMagnitude;
                if (distance >= best || !SegmentIsClear(room, point, node))
                    continue;
                best = distance;
                chosen = index;
            }
            return chosen;
        }

        private static Vector2 DirectionVector(RoomTravelDirection direction) =>
            direction == RoomTravelDirection.Left ? Vector2.left : direction == RoomTravelDirection.Up ? Vector2.up :
            direction == RoomTravelDirection.Down ? Vector2.down : Vector2.right;

        public static List<string> Validate(RoomTemplate room)
        {
            var issues = new List<string>();
            if (room == null)
            {
                issues.Add("방 데이터 없음");
                return issues;
            }
            if (room.platforms != null)
                foreach (var obstacle in room.platforms)
                    if (obstacle.size.x <= 0f || obstacle.size.y <= 0f)
                        issues.Add("장애물 크기는 양수여야 합니다.");
            var targets = new List<Vector2> { RewardPosition };
            foreach (RoomTravelDirection direction in System.Enum.GetValues(typeof(RoomTravelDirection)))
            {
                targets.Add(room.Entrance(direction));
                targets.Add(room.ExitPoint(direction) - DirectionVector(direction) * .3f);
            }
            var bosses = 0;
            if (room.enemies != null)
                foreach (var enemy in room.enemies)
                {
                    targets.Add(enemy.position);
                    if (enemy.type == EnemyType.Boss)
                        bosses++;
                }
            if (room.bossRoom && bosses != 1)
                issues.Add("보스 방에는 보스 1명이 필요합니다.");
            if (!room.bossRoom && bosses != 0)
                issues.Add("일반 방에는 보스를 배치하지 마세요.");
            if (!IsClear(room, room.playerSpawn, FootHalfSize))
                issues.Add("플레이어 시작점이 벽/장애물과 겹칩니다.");
            foreach (var target in targets)
                if (FindPath(room, room.playerSpawn, target) == null)
                    issues.Add("접근 불가: " + target);
            return issues;
        }
    }
}
