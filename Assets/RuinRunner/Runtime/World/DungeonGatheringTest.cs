using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Isolated, authored asset-pack playground. No run, loot table or save-slot mutations.</summary>
    public sealed class DungeonGatheringTest : MonoBehaviour
    {
        [Serializable] public sealed class Area
        {
            public string name;
            public int x, y, width, height;
            public bool Contains(Vector2Int p) => p.x >= x && p.x < x + width && p.y >= y && p.y < y + height;
        }
        [Serializable] public sealed class Layout
        {
            public Area[] floorAreas;
            public Area pool;
            public Vector2 spawn;
            public Vector2[] urns;
        }
        public Layout Recipe { get; private set; }
        public readonly HashSet<Vector2Int> Floor = new HashSet<Vector2Int>();
        public PlayerController Player { get; private set; }
        public CameraFollow Follow { get; private set; }
        public int OpenedUrns { get; private set; }
        public bool OverviewVisible => overview;
        private GameSettingsService settings;
        private readonly List<SpriteRenderer> urns = new List<SpriteRenderer>();
        private bool overview;
        private GUIStyle title, small;
        private static readonly Vector2Int[] Neighbors = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        private readonly Color floorTint = new Color(.72f, .75f, .86f);

        private void Start()
        {
            Application.targetFrameRate = 60;
            Time.fixedDeltaTime = GameTick.Duration;
            Physics2D.gravity = Vector2.zero;
            Recipe = JsonUtility.FromJson<Layout>(Resources.Load<TextAsset>("Maps/dungeon_gathering_test").text);
            foreach (var area in Recipe.floorAreas)
                for (var y = area.y; y < area.y + area.height; y++)
                for (var x = area.x; x < area.x + area.width; x++) Floor.Add(new Vector2Int(x, y));
            BuildFloor();
            BuildWalls();
            BuildPool();
            BuildDetails();
            settings = new GameSettingsService();
            Screen.fullScreen = false;
            var inventory = new InventorySystem();
            inventory.AddWeapon(DemoContent.Weapon("rust_sword"));
            Follow = CameraFollow.Create();
            Follow.GetComponent<Camera>().backgroundColor = new Color(.045f, .045f, .075f);
            Player = PlayerController.Create(Recipe.spawn, () => overview, () => true, () => { }, Follow, inventory, settings);
            Player.EquipWeapon(inventory.EquippedWeapon);
            Follow.Target = Player.transform;
            Follow.FocusRoom(new Vector2(16, 11), new Vector2(33, 23), true);
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--dungeon-gathering-validation") >= 0)
                gameObject.AddComponent<DungeonGatheringValidation>().Initialize(this);
            Debug.Log("[Dungeon Gathering] READY " + Floor.Count + " connected floor cells; standalone scene; no save slots loaded.");
        }

        private SpriteRenderer Tile(string id, Vector2 position, int order = 0, bool ground = true)
        {
            var obj = new GameObject(id);
            obj.transform.SetParent(transform, false);
            obj.transform.position = position;
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = IndividualArt.Sprite("DungeonGathering/" + id);
            renderer.sortingLayerName = ground ? "WorldFloor" : "Default";
            renderer.sortingOrder = order;
            return renderer;
        }

        private void BuildFloor()
        {
            foreach (var cell in Floor)
            {
                var hash = (cell.x * 73 + cell.y * 131) % 41;
                var tile = hash == 0 ? "cracks" : hash == 1 ? "bricks" : "plain";
                if (cell.y >= 16) tile = ((cell.x + cell.y) & 1) == 0 ? "checker" : "engraved";
                Tile("Floor/" + tile, cell).color = floorTint;
            }
            // Recessed entry stair, entirely inside the walkable floor.
            for (var x = 4; x <= 7; x++)
            for (var row = 0; row < 3; row++) Tile("Stairs/0", new Vector2(x, 5 - row), 2);
        }

        private void BuildWalls()
        {
            var blocked = new HashSet<Vector2Int>();
            foreach (var cell in Floor)
                foreach (var direction in Neighbors)
                    if (!Floor.Contains(cell + direction)) blocked.Add(cell + direction);
            foreach (var cell in blocked) Block("Masonry collision", cell, Vector2.one);

            // Contours come from the union, so joined modules never gain an internal wall.
            foreach (var cell in Floor)
            {
                if (!Floor.Contains(cell + Vector2Int.up))
                {
                    var face = new Vector2(cell.x, cell.y + 1);
                    Tile("Wall/face_" + (cell.x % 3), face, 40);
                    var left = !Floor.Contains(cell + Vector2Int.left);
                    var right = !Floor.Contains(cell + Vector2Int.right);
                    Tile("Trim/" + (left ? "tl" : right ? "tr" : "t"), face + Vector2.up, 30);
                    FlatShade(cell + new Vector2(0, .40f), new Vector2(1, .2f), .26f, 5);
                    if (cell.x % 5 == 3) Animate("Torch", 8, face, 70, 9);
                }
                if (!Floor.Contains(cell + Vector2Int.down))
                {
                    Tile("Trim/b", cell + Vector2.down, 20);
                    Tile("Wall/base_" + (cell.x % 3), cell + Vector2.down * 2, 10).color = new Color(.7f, .7f, .82f);
                }
                if (!Floor.Contains(cell + Vector2Int.left)) Tile("Trim/l", cell + Vector2Int.left, 25);
                if (!Floor.Contains(cell + Vector2Int.right)) Tile("Trim/r", cell + Vector2Int.right, 25);
            }
            // Fill outside 90-degree corners; no disconnected block objects along room seams.
            foreach (var cell in Floor)
            {
                foreach (var dx in new[] { -1, 1 })
                foreach (var dy in new[] { -1, 1 })
                {
                    var outside = cell + new Vector2Int(dx, dy);
                    if (Floor.Contains(cell + new Vector2Int(dx, 0)) || Floor.Contains(cell + new Vector2Int(0, dy)) || Floor.Contains(outside)) continue;
                    Tile("Trim/" + (dy > 0 ? "t" : "b") + (dx < 0 ? "l" : "r"), outside, 35);
                    Block("Corner collision", outside, Vector2.one);
                }
            }
        }

        private void BuildPool()
        {
            var pool = Recipe.pool;
            for (var y = 0; y < pool.height; y++)
            for (var x = 0; x < pool.width; x++)
            {
                var row = y == 0 ? "b" : y == pool.height - 1 ? "t" : "";
                var col = x == 0 ? "l" : x == pool.width - 1 ? "r" : "";
                var id = row + col; if (id.Length == 0) id = "c";
                var cell = new Vector2(pool.x + x, pool.y + y);
                var tile = Tile("Pool/" + id, cell, 6);
                // Bottom edge uses the pack's matching top lip, flipped once as a complete tile.
                if (y == 0) tile.flipY = true;
                // Ripple art extends toward the bottom of its cell; never overlay a shore tile.
                if (x > 0 && x < pool.width - 1 && y > 0 && y < pool.height - 1)
                    Animate("Ripple", 8, cell, 8, 7);
            }
            Block("Cistern water collision", new Vector2(pool.x + (pool.width - 1) * .5f, pool.y + (pool.height - 1) * .5f), new Vector2(pool.width, pool.height));
        }

        private void BuildDetails()
        {
            foreach (var position in Recipe.urns)
            {
                FlatShade(position + Vector2.down * .27f, new Vector2(.7f, .24f), .4f, 5);
                urns.Add(Animate("Urn", 16, position, 1000 - Mathf.RoundToInt((position.y - .2f) * 100), 7, false));
            }
            foreach (var pos in new[] { new Vector2(3, 4), new Vector2(10, 9), new Vector2(18, 4), new Vector2(29, 10), new Vector2(14, 13) })
                Tile("Props/rubble", pos, 4).color = new Color(.55f, .58f, .70f);
            for (var x = 21; x <= 25; x += 4)
            {
                Tile("Wall/pillar_top", new Vector2(x, 18.7f), -650, false);
                Tile("Wall/pillar_middle", new Vector2(x, 17.7f), -650, false);
                Tile("Wall/face_0", new Vector2(x, 16.7f), -650, false);
                Block("Sanctum pillar collision", new Vector2(x, 16.7f), new Vector2(.8f, .75f));
            }
        }

        private SpriteRenderer Animate(string id, int count, Vector2 pos, int order, float rate, bool ground = true)
        {
            var renderer = Tile(id + "/00", pos, order, ground);
            renderer.gameObject.AddComponent<DungeonGatheringAnimation>().Initialize(id, count, rate, pos.x * .13f + pos.y * .21f);
            return renderer;
        }

        private void FlatShade(Vector2 position, Vector2 size, float alpha, int order)
        {
            var obj = new GameObject("Contact shade"); obj.transform.SetParent(transform);
            obj.transform.position = position; obj.transform.localScale = size;
            var sprite = PrototypeVisuals.AddSprite(obj, new Color(.02f, .025f, .06f, alpha), order);
            sprite.sortingLayerName = "WorldFloor";
        }

        private void Block(string label, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(label); obj.transform.SetParent(transform);
            obj.layer = GameLayers.Ground; obj.transform.position = position;
            obj.AddComponent<BoxCollider2D>().size = size;
        }

        public bool IsWalkable(Vector2Int cell) => Floor.Contains(cell) && !Recipe.pool.Contains(cell);

        private void Update()
        {
            if (Player == null) return;
            if (settings.GetKeyDown(GameAction.Map)) overview = !overview;
            if (settings.GetKeyDown(GameAction.Pause)) { if (overview) overview = false; else Application.Quit(); }
            if (overview) return;
            if (settings.GetKeyDown(GameAction.Interact))
            {
                foreach (var urn in urns)
                {
                    if (urn == null || Vector2.Distance(Player.transform.position, urn.transform.position) > 1.35f) continue;
                    Destroy(urn.gameObject); OpenedUrns++; break;
                }
            }
        }

        private void OnGUI()
        {
            if (Player == null) return;
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { font = GameTypography.Emphasis, fontSize = 24 };
                title.normal.textColor = new Color(.87f, .91f, 1);
                small = new GUIStyle(GUI.skin.label) { font = GameTypography.Regular, fontSize = 18 };
                small.normal.textColor = new Color(.72f, .78f, .86f);
            }
            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 1280f, Screen.height / 720f, 1));
            Panel(new Rect(20, 20, 352, 68), new Color(.04f, .05f, .09f, .93f));
            GUI.Label(new Rect(34, 26, 332, 29), "DUNGEON / TILESET TEST", title);
            var area = "PASSAGE";
            foreach (var candidate in Recipe.floorAreas)
                if (candidate.Contains(Vector2Int.RoundToInt(Player.transform.position))) area = candidate.name;
            GUI.Label(new Rect(34, 57, 330, 24), area, small);
            Panel(new Rect(20, 668, 690, 32), new Color(.04f, .05f, .09f, .90f));
            GUI.Label(new Rect(32, 671, 676, 26), "WASD 이동   Space 대시   클릭 공격   E 항아리   Tab 지도   Esc 종료", small);
            if (overview) DrawOverview();
            GUI.matrix = old;
        }

        private void DrawOverview()
        {
            Panel(new Rect(300, 98, 680, 532), new Color(.025f, .03f, .06f, .98f));
            GUI.Label(new Rect(328, 112, 600, 32), "UNDER THE CASTLE / MAP TEST", title);
            foreach (var cell in Floor)
                Panel(new Rect(342 + cell.x * 18, 588 - cell.y * 18, 18, 18), Recipe.pool.Contains(cell) ? new Color(.06f, .42f, .61f) : new Color(.30f, .35f, .46f));
            var p = Player.transform.position;
            Panel(new Rect(346 + p.x * 18, 592 - p.y * 18, 10, 10), new Color(1, .79f, .28f));
        }

        private static void Panel(Rect rect, Color color)
        {
            var old = GUI.color; GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old;
        }
    }

    public sealed class DungeonGatheringAnimation : MonoBehaviour
    {
        public int FrameIndex { get; private set; }
        private SpriteRenderer target;
        private Sprite[] frames;
        private float rate, phase;
        public void Initialize(string id, int count, float fps, float offset)
        {
            target = GetComponent<SpriteRenderer>();
            frames = IndividualArt.Frames("DungeonGathering/" + id, count);
            rate = fps; phase = offset;
        }
        private void Update()
        {
            // Authored props can sit at negative coordinates, hence a negative phase.
            var step = Mathf.FloorToInt((Time.time + phase) * rate);
            FrameIndex = (step % frames.Length + frames.Length) % frames.Length;
            target.sprite = frames[FrameIndex];
        }
    }
}
