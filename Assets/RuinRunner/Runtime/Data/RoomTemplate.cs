using System;
using UnityEngine;

namespace RuinRunner
{
    public enum RoomTravelDirection
    {
        Right,
        Up,
        Left,
        Down
    }

    [Serializable]
    public struct RoomPlatformData
    {
        public Vector2 position;
        public Vector2 size;
        [Range(0, 15)] public int tileIndex;
        [HideInInspector] public bool solid; // Legacy serialization only; all top-down obstacles are solid.

        public RoomPlatformData(float x, float y, float width, float height)
        {
            position = new Vector2(x, y);
            size = new Vector2(width, height);
            tileIndex = 6;
            solid = true;
        }
    }

    [Serializable]
    public struct EnemyPlacementData
    {
        public EnemyType type;
        public Vector2 position;
        [Min(0)] public int wave;

        public EnemyPlacementData(EnemyType enemyType, float x, float y)
        {
            type = enemyType;
            position = new Vector2(x, y);
            wave = 0;
        }
    }

    [CreateAssetMenu(menuName = "Ruin Runner/Room Template", fileName = "RoomTemplate")]
    public sealed class RoomTemplate : ScriptableObject
    {
        public string roomId = "room_new";
        public string displayName = "새 방";
        [Min(.01f)] public float selectionWeight = 1f;
        public bool bossRoom;
        public RoomTravelDirection preferredExitDirection = RoomTravelDirection.Right;
        [Min(1)] public int automaticWaveSize = 2;
        public Color accentColor = new Color(.25f, .5f, .9f, .35f);
        public Vector2 playerSpawn = new Vector2(-8.5f, 0f);
        [Tooltip("이동 가능한 내부 영역의 월드 크기. 큰 방은 카메라가 플레이어를 따라갑니다.")]
        public Vector2 roomSize = new Vector2(22.6f, 11.8f);
        [Tooltip("전투 구역의 로컬 윤곽. 3점 이상이면 이 다각형을 사용하며, 비어 있으면 크기에 맞는 자연형 윤곽을 사용합니다.")]
        public Vector2[] walkableOutline = Array.Empty<Vector2>();
        public Vector2 Size => new Vector2(Mathf.Clamp(roomSize.x, 18f, 48f), Mathf.Clamp(roomSize.y, 10f, 30f));
        public Rect LocalBounds => new Rect(-Size * .5f, Size);
        public Vector2 Entrance(RoomTravelDirection outward)
        {
            var half = Size * .5f;
            switch (outward)
            {
                case RoomTravelDirection.Left: return new Vector2(-half.x + 1.8f, 0);
                case RoomTravelDirection.Up: return new Vector2(0, half.y - 1.4f);
                case RoomTravelDirection.Down: return new Vector2(0, -half.y + 1.4f);
                default: return new Vector2(half.x - 1.8f, 0);
            }
        }
        public Vector2 ExitPoint(RoomTravelDirection outward)
        {
            var half = Size * .5f - Vector2.one * .48f;
            switch (outward)
            {
                case RoomTravelDirection.Left: return new Vector2(-half.x, 0);
                case RoomTravelDirection.Up: return new Vector2(0, half.y);
                case RoomTravelDirection.Down: return new Vector2(0, -half.y);
                default: return new Vector2(half.x, 0);
            }
        }
        [Tooltip("탑다운 XY 평면의 고형 장애물. 기존 저장 호환을 위해 필드명 platforms를 유지합니다.")]
        public RoomPlatformData[] platforms = Array.Empty<RoomPlatformData>();
        public EnemyPlacementData[] enemies = Array.Empty<EnemyPlacementData>();
        public RoomDecorationData[] decorations = RoomTemplateFactory.CreateDefaultDecorations();

        public bool UsesExplicitWaves
        {
            get
            {
                if (enemies == null) return false;
                foreach (var enemy in enemies)
                    if (enemy.wave > 0) return true;
                return false;
            }
        }

        public int WaveCount
        {
            get
            {
                if (enemies == null || enemies.Length == 0) return 0;
                if (UsesExplicitWaves)
                {
                    var highest = 0;
                    foreach (var enemy in enemies) highest = Mathf.Max(highest, enemy.wave);
                    return highest + 1;
                }
                if (bossRoom)
                {
                    var hasBoss = false;
                    var hasMinion = false;
                    foreach (var enemy in enemies)
                    {
                        hasBoss |= enemy.type == EnemyType.Boss;
                        hasMinion |= enemy.type != EnemyType.Boss;
                    }
                    if (hasBoss && hasMinion) return 2;
                }
                return Mathf.CeilToInt(enemies.Length / (float)Mathf.Max(1, automaticWaveSize));
            }
        }

        public int GetWaveIndex(int enemyIndex)
        {
            if (enemies == null || enemyIndex < 0 || enemyIndex >= enemies.Length) return -1;
            if (UsesExplicitWaves) return Mathf.Max(0, enemies[enemyIndex].wave);
            if (bossRoom && WaveCount > 1) return enemies[enemyIndex].type == EnemyType.Boss ? 1 : 0;
            return enemyIndex / Mathf.Max(1, automaticWaveSize);
        }
    }
}
