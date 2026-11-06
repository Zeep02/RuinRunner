using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    public enum FieldServiceKind { StartingGear, Talents, Expedition, Merchant, ShapeAnvil, OathStone, ExchangeAltar }
    public sealed class FieldServiceObject : MonoBehaviour
    {
        public string StableId { get; private set; }
        public FieldServiceKind Kind { get; private set; }
        public string Label { get; private set; }
        public int RegionIndex { get; private set; }
        public bool Used { get; set; }
        // The same reach the town uses to offer an interaction, so the door is open exactly when it can be used.
        public const float TownServiceReach = 2f;
        SpriteRenderer visual;
        string animation;
        float offset;
        int shownFrame = -1;
        static readonly List<FieldServiceObject> present = new List<FieldServiceObject>();
        public static List<FieldServiceObject> Present => present;
        void OnEnable() => present.Add(this);
        void OnDisable() => present.Remove(this);
        public static FieldServiceObject Create(Transform parent, string id, FieldServiceKind kind, string label, Vector2 feet, int region, string art, string animated = null)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            go.transform.position = feet;
            go.layer = GameLayers.Ground;
            var obj = go.AddComponent<FieldServiceObject>();
            obj.StableId = id;
            obj.Kind = kind;
            obj.Label = label;
            obj.RegionIndex = region;
            obj.animation = animated;
            obj.offset = Mathf.Abs(feet.x * .17f + feet.y * .33f);
            obj.visual = go.AddComponent<SpriteRenderer>();
            obj.visual.sprite = IndividualArt.Sprite(art);
            obj.visual.sortingOrder = 900 - Mathf.RoundToInt(feet.y * 100);
            if (kind == FieldServiceKind.ShapeAnvil)
            {
                var top = new GameObject("Iron anvil on stone footing");
                top.transform.SetParent(go.transform, false);
                top.transform.localPosition = new Vector3(0, .5f, 0);
                var anvil = top.AddComponent<SpriteRenderer>();
                anvil.sprite = IndividualArt.Sprite("External/TownQuesters/anvil");
                anvil.sortingOrder = obj.visual.sortingOrder + 1;
            }
            if (kind == FieldServiceKind.Expedition)
                ProximityGate.Attach(go, obj.visual, TownServiceReach);
            else
            {
                var box = go.AddComponent<BoxCollider2D>();
                box.size = new Vector2(.65f, .36f);
                box.offset = new Vector2(0, .12f);
            }
            return obj;
        }
        void Update()
        {
            if (animation == null || visual == null)
                return;
            var frame = Mathf.FloorToInt((Time.time + offset) * 6) % 5;
            if (frame == shownFrame)
                return;
            shownFrame = frame;
            visual.sprite = IndividualArt.Sprite(animation + "/" + frame.ToString("00"));
        }
    }
    public sealed class TownWorld : MonoBehaviour
    {
        public StageTerrainLayout Terrain { get; private set; }
        public static readonly Vector2 Arrival = new Vector2(0, -1.75f);
        public static readonly Vector2 Size = new Vector2(18, 10);
        public static readonly Vector2 TrainingPosition = new Vector2(3.6f, 1.6f);
        RoomTemplate room;
        public static TownWorld Create()
        {
            var go = new GameObject("WayfarerTown");
            var town = go.AddComponent<TownWorld>();
            town.room = ScriptableObject.CreateInstance<RoomTemplate>();
            town.room.roomId = "town-wayfarers-rest";
            town.room.displayName = "길잡이 쉼터";
            town.room.roomSize = Size;
            town.room.walkableOutline = new[] { new Vector2(-9, -2.5f), new Vector2(-7.5f, -5), new Vector2(5.5f, -5), new Vector2(9, -2.5f), new Vector2(9, 2), new Vector2(7, 5), new Vector2(-6.5f, 5), new Vector2(-9, 2.5f) };
            town.room.platforms = System.Array.Empty<RoomPlatformData>();
            town.room.decorations = System.Array.Empty<RoomDecorationData>();
            town.room.enemies = System.Array.Empty<EnemyPlacementData>();
            var paths = new[]
            {
                new StagePathData{points=new[]{Arrival,new Vector2(-2.1f,-.4f),new Vector2(-4.25f,.4f)},width=2.6f},
                new StagePathData{points=new[]{Arrival,new Vector2(-.25f,.2f),new Vector2(-.25f,1.45f)},width=2.5f},
                new StagePathData{points=new[]{Arrival,new Vector2(3,-1.4f),new Vector2(6.2f,-1.25f)},width=3f},
                new StagePathData{points=new[]{new Vector2(.25f,.8f),TrainingPosition+Vector2.down*.8f},width=2.5f}
            };
            town.Terrain = new StageTerrainLayout(new[] { town.room }, new[] { Vector2.zero }, trails: paths, snapToDungeonTiles: true);
            DungeonTilesetRenderer.Build(go.transform, town.Terrain, 0);
            FieldServiceObject.Create(go.transform, "town-smith", FieldServiceKind.StartingGear, "무기지기 · 로안", new Vector2(-4.25f, 1.15f), -1, "External/TownQuesters/smith/00", "External/TownQuesters/smith");
            FieldServiceObject.Create(go.transform, "town-mentor", FieldServiceKind.Talents, "수련지기 · 미라", new Vector2(-.25f, 2.1f), -1, "External/TownQuesters/mentor/00", "External/TownQuesters/mentor");
            FieldServiceObject.Create(go.transform, "town-expedition", FieldServiceKind.Expedition, "원정 입구", new Vector2(6.2f, -1.25f), -1, ProximityGate.ArtRoot + "/00");
            town.Furnish(go.transform);
            town.BringToLife(go.transform);
            return town;
        }
        // The rest stop is lived in: the gate burns, the forge smoulders, and things drift about.
        void BringToLife(Transform parent)
        {
            AmbientFx.Create(parent, new Vector2(6.2f, -1.25f), "AmbPortal", .62f);
            foreach (var fire in new[] { new Vector2(4.75f, -.35f), new Vector2(7.65f, -.35f), new Vector2(-1.75f, 2.1f), new Vector2(1.25f, 2.1f), new Vector2(-3, 1.45f) })
                AmbientFx.Create(parent, fire, "AmbSkullGold");
            AmbientFx.Create(parent, new Vector2(-5.45f, 3.1f), "AmbForge");
            foreach (var wing in new[] { new Vector2(-6, -1.5f), new Vector2(2.5f, .2f), new Vector2(4, -3.4f) })
                AmbientFx.Create(parent, wing, "AmbButterfly", .42f).GetComponent<SpriteRenderer>().sortingOrder = 2001;
            AmbientMotes.Create(parent, 0);
        }
        void Furnish(Transform parent)
        {
            // Furnished work bays continue the native worn paving of the approach paths.
            // These are render-only courts: floor and navigation remain the shared terrain.
            Court(parent, new Rect(-7, 0, 4, 2), "Smith stone apron");
            Court(parent, new Rect(-2, 1, 7, 2), "Training court");
            Court(parent, new Rect(5, -2, 3, 2), "Departure landing");
            var bench = Decorate(parent, "External/TownRefuge/bench-s", new Vector2(-5.45f, 2.25f), "Smith stone workbench", new Vector2(1.55f, .4f));
            var anvil = new GameObject("Iron anvil on workbench");
            anvil.transform.SetParent(bench.transform, false);
            anvil.transform.localPosition = new Vector3(0, .62f, 0);
            var iron = anvil.AddComponent<SpriteRenderer>();
            iron.sprite = IndividualArt.Sprite("External/TownQuesters/anvil");
            iron.sortingOrder = bench.sortingOrder + 1;
            Decorate(parent, "External/TownRefuge/crate", new Vector2(-6.9f, 1.7f), "Smith supply crates", new Vector2(.8f, .42f));
            Decorate(parent, "External/TownRefuge/barrel", new Vector2(-8f, .6f), "Smith water barrel", new Vector2(.7f, .38f));
            // Who does what reads from across the rest stop: goods by the one who sells, a dummy by the one who trains.
            LivingProp.Create(parent, "PropWagon", new Vector2(-6.6f, -1f), "Merchant wagon");
            LivingProp.Create(parent, "PropBundle", new Vector2(-5.65f, .75f), "Merchant bundle");
            LivingProp.Create(parent, "PropDummy", new Vector2(.95f, 2.85f), "Mentor training dummy");
            Decorate(parent, "External/Cainos/Props/pot-b", new Vector2(-3, 2.8f), "Smith supply vessel", new Vector2(.7f, .4f));
            Decorate(parent, "External/TownRefuge/bench-s", new Vector2(-.65f, 3.35f), "Mentor rest bench", new Vector2(1.55f, .4f));
            Decorate(parent, "External/Cainos/Props/lantern", new Vector2(-2.45f, 3.25f), "Mentor lamp", new Vector2(.7f, .4f));
            Decorate(parent, "External/Cainos/Props/broken-pillar", new Vector2(2.05f, 3.05f), "Training court west post", new Vector2(.7f, .4f));
            Decorate(parent, "External/Cainos/Props/broken-pillar", new Vector2(4.85f, 3.05f), "Training court east post", new Vector2(.7f, .4f));
            Decorate(parent, "External/TownRefuge/sign-e", new Vector2(5.15f, -2.9f), "Expedition direction sign", new Vector2(.45f, .3f));
            Decorate(parent, "External/TownRefuge/barrel", new Vector2(8f, -2.2f), "Departure supplies", new Vector2(.7f, .38f));
            Decorate(parent, "External/Cainos/Props/pot-a", new Vector2(-6, -3.25f), "Garden vessel", new Vector2(.6f, .4f));
            var trees = new[]{new Vector2(-10.4f,-3.5f),new Vector2(-10.4f,.5f),new Vector2(-9.8f,3.9f),new Vector2(-6.7f,5.8f),
                new Vector2(-2.2f,5.8f),new Vector2(3.3f,5.8f),new Vector2(8.8f,4.7f),new Vector2(10.5f,1.2f),new Vector2(10.5f,-3.5f)};
            for (int i = 0; i < trees.Length; i++)
                Woodland(parent, trees[i], i % 3 + 1);
        }
        void Court(Transform parent, Rect bounds, string label)
        {
            // Use the same native half-unit stones and staggered pixel grid as
            // the approach paths. Worn corners and missing stones reveal turf.
            var rules = new EnvironmentPlacementRules(Terrain, null);
            var occupied = new System.Collections.Generic.HashSet<Vector2Int>();
            foreach (var existing in parent.GetComponentsInChildren<SpriteRenderer>())
                if (existing.sortingLayerName == "WorldFloor" && (existing.name == "Worn paving" || existing.name.StartsWith("Refuge paving ", System.StringComparison.Ordinal)))
                    occupied.Add(Vector2Int.RoundToInt((Vector2)existing.transform.position * 32));
            var radius = bounds.size * .5f;
            for (int row = Mathf.FloorToInt(bounds.yMin * 2); row < Mathf.CeilToInt(bounds.yMax * 2); row++)
                for (int column = Mathf.FloorToInt(bounds.xMin * 2) - 1; column < Mathf.CeilToInt(bounds.xMax * 2); column++)
                {
                    var point = new Vector2(column * .5f + .25f + (row & 1) * .25f, row * .5f + .25f);
                    var delta = point - bounds.center;
                    float edge = (1 - Mathf.Pow(Mathf.Pow(Mathf.Abs(delta.x) / radius.x, 4) + Mathf.Pow(Mathf.Abs(delta.y) / radius.y, 4), .25f)) * radius.y;
                    uint hash;
                    unchecked
                    {
                        hash = (uint)(column * 73856093 ^ row * 19349663) * 2654435761u;
                        hash ^= hash >> 16;
                        hash *= 0x7feb352du;
                        hash ^= hash >> 15;
                        hash *= 0x846ca68bu;
                        hash ^= hash >> 16;
                    }
                    if (edge < .04f + (hash % 7) * .025f || (edge < .35f && hash % 3 == 0) || hash % 13 == 0)
                        continue;
                    if (occupied.Contains(Vector2Int.RoundToInt(point * 32)) || !rules.HasFloor(new Rect(point - Vector2.one * .24f, Vector2.one * .48f)))
                        continue;
                    var go = new GameObject("Refuge paving " + label);
                    go.transform.SetParent(parent, false);
                    go.transform.position = point;
                    var view = go.AddComponent<SpriteRenderer>();
                    view.sprite = CainosRuinsArt.Sprite("Paving/" + (hash % 32).ToString("00"));
                    view.sortingLayerName = "WorldFloor";
                    view.sortingOrder = 1;
                }
        }
        SpriteRenderer Decorate(Transform parent, string art, Vector2 feet, string label, Vector2 size)
        {
            var footprint = new Rect(feet - new Vector2(size.x * .5f, .2f), size);
            if (!new EnvironmentPlacementRules(Terrain, null).HasFloor(footprint))
                throw new System.InvalidOperationException("Town furniture lacks floor: " + label);
            // Reserve the walk lane, rather than the broad decorative paving verge.
            foreach (var path in Terrain.Trails)
                if (StageTerrainLayout.PathDistance(feet, path) < .6f + size.x * .5f)
                    throw new System.InvalidOperationException("Town furniture blocks trail: " + label);
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            go.transform.position = feet;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = IndividualArt.Sprite(art);
            r.sortingOrder = 900 - Mathf.RoundToInt(feet.y * 100);
            go.layer = GameLayers.Ground;
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = footprint.size;
            collider.offset = new Vector2(0, footprint.center.y - feet.y);
            return r;
        }
        void Woodland(Transform parent, Vector2 feet, int variant)
        {
            // A compact clearing has no large closed rectangle inside its bounds.
            // Root the forest outside those bounds while retaining the real floor mask.
            for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                    if (Terrain.IsOpen(feet + new Vector2(x * .65f, y * .35f)))
                        return;
            var go = new GameObject("Refuge outer woodland");
            go.transform.SetParent(parent, false);
            go.transform.position = feet;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = CainosRuinsArt.Sprite("Plants/t" + variant);
            r.sortingOrder = 900 - Mathf.RoundToInt(feet.y * 100);
        }
        void OnDestroy()
        {
            if (room != null)
            {
                if (Application.isPlaying)
                    Destroy(room);
                else
                    DestroyImmediate(room);
            }
        }
    }
    public sealed class TownTrainingTarget : MonoBehaviour
    {
        EnemyController target;
        GameController game;
        public static TownTrainingTarget Create(GameController game, Transform parent, Vector2 position)
        {
            var go = new GameObject("수련 석상");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var t = go.AddComponent<TownTrainingTarget>();
            t.game = game;
            t.Spawn();
            return t;
        }
        void Spawn()
        {
            target = EnemyController.Create(EnemyType.Grunt, transform.position, game.Player, () => true, _ => { }, null);
            target.transform.SetParent(transform, true);
            target.ApplyBalance(new EnemyBalanceData(EnemyType.Grunt, 10000, 0, 0, .1f, 0) { movementFootSize = new Vector2(.6f, .4f) });
            foreach (var r in target.GetComponentsInChildren<SpriteRenderer>())
                r.enabled = false;
            var art = new GameObject("Practice stone");
            art.transform.SetParent(target.transform, false);
            art.transform.localScale = Vector3.one;
            var view = art.AddComponent<SpriteRenderer>();
            view.sprite = CainosRuinsArt.Sprite("Props/statue");
            view.sortingOrder = 900 - Mathf.RoundToInt(transform.position.y * 100);
        }
        void Update()
        {
            if (game == null || game.Phase != GameSessionPhase.Town)
                return;
            if (target == null || target.IsDead)
            {
                if (target != null)
                    Destroy(target.gameObject);
                Spawn();
            }
            else
            {
                target.transform.position = transform.position;
                var body = target.GetComponent<Rigidbody2D>();
                if (body != null)
                    body.constraints = RigidbodyConstraints2D.FreezeAll;
            }
        }
    }
}
