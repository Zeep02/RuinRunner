using System;
using UnityEngine;

namespace RuinRunner
{
    public sealed class WaveSpawnResult
    {
        public int EnemyCount { get; }
        public EnemyController Boss { get; }

        public WaveSpawnResult(int enemyCount, EnemyController boss)
        {
            EnemyCount = enemyCount;
            Boss = boss;
        }
    }

    public static class WaveDirector
    {
        public static WaveSpawnResult SpawnWave(
            RoomTemplate room,
            int waveIndex,
            PlayerController player,
            Func<bool> isPaused,
            Action<EnemyController> onEnemyDied,
            CameraFollow cameraFollow,
            Vector2 roomCenter,
            StageTerrainLayout navigationTerrain = null)
        {
            EnemyController boss = null;
            if (room.enemies == null)
                return new WaveSpawnResult(0, null);
            var count = 0;
            for (var index = 0; index < room.enemies.Length; index++)
            {
                if (room.GetWaveIndex(index) != waveIndex)
                    continue;
                var placement = room.enemies[index];
                var enemy = EnemyController.Create(
                    placement.type,
                    placement.position + roomCenter,
                    player,
                    isPaused,
                    onEnemyDied,
                    cameraFollow);
                enemy.ConfigureArena(new Rect(roomCenter - room.Size * .5f, room.Size));
                enemy.ConfigureNavigation(navigationTerrain);

                if (EnemyRoles.IsBoss(placement.type))
                    boss = enemy;
                count++;
            }

            return new WaveSpawnResult(count, boss);
        }

        public static EnemySpawnTelegraph[] CreateTelegraphs(RoomTemplate room, int waveIndex, Vector2 roomCenter)
        {
            if (room.enemies == null)
                return new EnemySpawnTelegraph[0];
            var telegraphs = new System.Collections.Generic.List<EnemySpawnTelegraph>();
            for (var index = 0; index < room.enemies.Length; index++)
            {
                if (room.GetWaveIndex(index) != waveIndex)
                    continue;
                var placement = room.enemies[index];
                telegraphs.Add(EnemySpawnTelegraph.Create(placement.position + roomCenter, placement.type));
            }
            return telegraphs.ToArray();
        }
    }
}
