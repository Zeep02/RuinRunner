using UnityEngine;

namespace RuinRunner
{
    public static class RoomTemplateFactory
    {
        public static RoomDecorationData[] CreateDefaultDecorations() => new[]
        {
            new RoomDecorationData(EnvironmentPropKind.Grass, -7f, 3.8f, 1.1f),
            new RoomDecorationData(EnvironmentPropKind.Grass, 7f, 3.8f, 1.2f),
            new RoomDecorationData(EnvironmentPropKind.Grass, -7f, -3.8f, 1.2f),
            new RoomDecorationData(EnvironmentPropKind.Grass, 7f, -3.8f, 1.1f),
            new RoomDecorationData(EnvironmentPropKind.Urn, -7f, -1f, 1.1f),
            new RoomDecorationData(EnvironmentPropKind.Urn, 7f, -1f, 1.1f),
            new RoomDecorationData(EnvironmentPropKind.Banner, -2f, 4.7f, 1.2f),
            new RoomDecorationData(EnvironmentPropKind.Brazier, 2f, 4.7f, 1.2f)
        };

        public static RoomTemplate[] CreateStarterTemplates()
        {
            return new[]
            {
                Create("hall_broken", "무너진 회랑", false, new Color(.2f, .55f, .9f, .4f),
                    new[] { P(-4f, 2f, 2.4f, 1.6f), P(4f, -1.5f, 2f, 2f) },
                    new[] { E(EnemyType.Grunt, -5f, -2f), E(EnemyType.Grunt, 6f, 2.5f), E(EnemyType.Leaper, 1.5f, 3f) }),
                Create("cross_bridge", "교차 다리", false, new Color(.35f, .7f, .55f, .4f),
                    new[] { P(-4f, 2.5f, 1.5f, 1.5f), P(4f, 2.5f, 1.5f, 1.5f), P(-4f, -2.5f, 1.5f, 1.5f), P(4f, -2.5f, 1.5f, 1.5f) },
                    new[] { E(EnemyType.Shooter, -6f, 0f), E(EnemyType.Charger, 3f, 0f), E(EnemyType.Leaper, 0f, 3.5f) }),
                Create("low_pit", "가라앉은 제단", false, new Color(.65f, .35f, .8f, .4f),
                    new[] { P(-4f, 0f, 1.6f, 2.4f), P(4f, 0f, 1.6f, 2.4f), P(0f, 2.8f, 3f, 1.2f) },
                    new[] { E(EnemyType.Grunt, -6f, -3f), E(EnemyType.Shooter, 6f, 3f), E(EnemyType.Charger, 6f, -3f) }),
                Create("twin_towers", "쌍둥이 탑", false, new Color(.85f, .45f, .25f, .4f),
                    new[] { P(-4f, 1.5f, 2f, 2.5f), P(4f, 1.5f, 2f, 2.5f) },
                    new[] { E(EnemyType.Shooter, -6f, -2f), E(EnemyType.Shooter, 6f, -2f), E(EnemyType.Leaper, 0f, 3.5f) }),
                Create("charge_lane", "돌진자의 길", false, new Color(.95f, .65f, .15f, .4f),
                    new[] { P(-5f, 3f, 2.2f, 1.2f), P(5f, -3f, 2.2f, 1.2f) },
                    new[] { E(EnemyType.Charger, 5f, 0f), E(EnemyType.Charger, -3f, 0f), E(EnemyType.Grunt, 2f, 3f) }),
                Create("green_steps", "이끼 계단", false, new Color(.3f, .9f, .45f, .4f),
                    new[] { P(-5f, 2.5f, 2f, 1.5f), P(-2f, -.8f, 1.8f, 1.6f), P(3f, 2.5f, 2f, 1.5f), P(6f, -1.5f, 1.6f, 1.6f) },
                    new[] { E(EnemyType.Leaper, -5f, -2f), E(EnemyType.Leaper, 2f, -1f), E(EnemyType.Shooter, 6f, 2f) }),
                Create("warden_throne", "감시자의 옥좌", true, new Color(1f, .18f, .48f, .48f),
                    new[] { P(-5f, 2.2f, 1.8f, 1.8f), P(5f, 2.2f, 1.8f, 1.8f) },
                    new[] { E(EnemyType.Boss, 0f, 1.5f), E(EnemyType.Leaper, -5f, -2f), E(EnemyType.Shooter, 5f, -2f) })
            };
        }

        private static RoomTemplate Create(string id, string title, bool boss, Color accent, RoomPlatformData[] platforms, EnemyPlacementData[] enemies)
        {
            var template = ScriptableObject.CreateInstance<RoomTemplate>();
            template.name = id;
            template.roomId = id;
            template.displayName = title;
            template.bossRoom = boss;
            template.accentColor = accent;
            template.preferredExitDirection = DirectionFor(id);
            template.automaticWaveSize = 2;
            template.platforms = platforms;
            template.enemies = enemies;
            template.roomSize = SizeFor(id);
            var factor = new Vector2(template.Size.x / 22.6f, template.Size.y / 11.8f);
            template.playerSpawn = template.Entrance(RoomTravelDirection.Left);
            for (var i = 0; i < platforms.Length; i++)
            {
                platforms[i].position = Vector2.Scale(platforms[i].position, factor);
                platforms[i].size = Vector2.Scale(platforms[i].size, factor);
            }
            for (var i = 0; i < enemies.Length; i++) enemies[i].position = Vector2.Scale(enemies[i].position, factor);
            template.decorations = CreateDefaultDecorations();
            for (var i = 0; i < template.decorations.Length; i++)
                template.decorations[i].position = Vector2.Scale(template.decorations[i].position, factor);
            if (!boss && template.Size.x * template.Size.y > 500f)
            {
                var expanded = new System.Collections.Generic.List<EnemyPlacementData>(enemies)
                {
                    E(EnemyType.Shooter, -template.Size.x * .36f, -template.Size.y * .32f),
                    E(EnemyType.Leaper, template.Size.x * .35f, template.Size.y * .34f),
                    E(EnemyType.Grunt, 0f, -template.Size.y * .34f)
                };
                template.enemies = expanded.ToArray();
            }
            return template;
        }

        private static Vector2 SizeFor(string id)
        {
            switch (id)
            {
                case "hall_broken": return new Vector2(20f, 12f);
                case "cross_bridge": return new Vector2(30f, 20f);
                case "low_pit": return new Vector2(24f, 16f);
                case "twin_towers": return new Vector2(26f, 22f);
                case "charge_lane": return new Vector2(38f, 14f);
                case "green_steps": return new Vector2(38f, 26f);
                default: return new Vector2(30f, 20f);
            }
        }

        private static RoomTravelDirection DirectionFor(string id)
        {
            if (id == "cross_bridge" || id == "green_steps") return RoomTravelDirection.Up;
            if (id == "low_pit" || id == "warden_throne") return RoomTravelDirection.Left;
            if (id == "twin_towers") return RoomTravelDirection.Down;
            return RoomTravelDirection.Right;
        }

        private static RoomPlatformData P(float x, float y, float width, float height)
        {
            return new RoomPlatformData(x, y, width, height);
        }

        private static EnemyPlacementData E(EnemyType type, float x, float y)
        {
            return new EnemyPlacementData(type, x, y);
        }
    }
}
