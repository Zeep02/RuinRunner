using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using RuinRunner;

// Supplied transient inventory / frozen settled drops. Not normal acquisition or gameplay evidence.
// Entry point requires Play Mode at the main menu. Never starts/ends a run or opens/saves a slot.
public static class AccessoryStyleReview20261004
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
    const string Review = "ArtDirection/Review-20261004/item-style/";
    static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
    static GameController game;
    static PrototypeHud hud;
    static AccessoryStylePaint20261004 paint;
    static GameObject host;
    static Camera dropCamera;
    static string folder;
    static bool running, cleaning, oldGameEnabled, oldHudEnabled, oldBackground;
    static float oldTime;
    static EditorWindow view;
    static object sizes;
    static int oldSize, oldCustomCount;
    static Type autoTick;
    static bool oldTick;
    static bool viewSizeCaptured, customCountCaptured, autoTickCaptured;
    static long oldInterval;
    static readonly List<int> addedSizes = new List<int>();
    static readonly Dictionary<Camera, bool> cameraStates = new Dictionary<Camera, bool>();
    static readonly List<GameObject> dropRoots = new List<GameObject>();
    static readonly List<Probe> probes = new List<Probe>();
    static readonly JArray records = new JArray(), captures = new JArray(), drawChecks = new JArray();
    static readonly JArray weaponSources = new JArray(), dropChecks = new JArray();
    static readonly string[] QuickSamples = { "iron_greatsword", "scrap_pistol", "void_revolver" };
    static Dictionary<string, string> saveBefore;
    static object oldTooltipKey, oldTooltipScroll;
    static readonly Dictionary<string, object> gamePropertiesBefore = new Dictionary<string, object>();
    static object equipmentSelectionBefore;
    static double deadline;
    static InventorySystem bag;
    static string mode, tooltipId, frameError;
    static int page;
    static float probeBox;
    static Texture2D flat;
    sealed class Probe { public string Id; public Rect Area, Expected; public float Scale; }
    static object Get(object obj, string name) => obj.GetType().GetProperty(name, All).GetValue(obj);
    static void Set(object obj, string name, object value) => obj.GetType().GetProperty(name, All).SetValue(obj, value);
    static object Field(object obj, string name) => obj.GetType().GetField(name, All).GetValue(obj);
    static void Field(object obj, string name, object value) => obj.GetType().GetField(name, All).SetValue(obj, value);
    static object Invoke(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, All).Invoke(obj, args);
    static void Require(bool good, string error) { if (!good) throw new Exception(error); }
    static string Hash(string path) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }

    static Dictionary<string, string> SaveHashes()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            Path.Combine(Application.persistentDataPath, "RuinRunner"),
            Path.GetDirectoryName(game.SaveSlots.SaveDirectory)
        };
        foreach (var dir in dirs)
            if (Directory.Exists(dir))
                foreach (var path in Directory.GetFiles(dir, "*", SearchOption.AllDirectories)) result[Path.GetFullPath(path)] = Hash(path);
        return result;
    }

    public static string Main()
    {
        Require(!running, "Accessory review already running; call Cancel to restore it first.");
        Require(Application.isPlaying, "Play Mode at main menu is required.");
        game = UnityEngine.Object.FindAnyObjectByType<GameController>();
        Require(game != null && !game.IsRunActive, "Run this fixture at the main menu; it never ends an active run.");
        hud = game.GetComponent<PrototypeHud>(); Require(hud != null, "Production PrototypeHud is missing.");
        Require(File.Exists(Review + "accepted-selection.json") && File.Exists(Review + "import-result.json"), "Accepted selection and completed import required.");
        Require((string)JObject.Parse(File.ReadAllText(Review + "import-result.json", Encoding.UTF8))["result"] == "PASS", "Accessory import must pass before visual review.");
        records.Clear(); captures.Clear(); drawChecks.Clear(); weaponSources.Clear(); dropChecks.Clear(); addedSizes.Clear(); cameraStates.Clear(); dropRoots.Clear();
        autoTick = null; view = null; sizes = null; host = null; paint = null; flat = null; frameError = null;
        viewSizeCaptured = false; customCountCaptured = false; autoTickCaptured = false;
        folder = Path.GetFullPath(Review + "unity-review-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
        Directory.CreateDirectory(folder);
        saveBefore = SaveHashes();
        AuditSources();
        oldGameEnabled = game.enabled; oldHudEnabled = hud.enabled; oldTime = Time.timeScale; oldBackground = Application.runInBackground;
        oldTooltipKey = Field(hud, "tooltipScrollKey"); oldTooltipScroll = Field(hud, "tooltipScroll");
        gamePropertiesBefore.Clear();
        foreach (var property in new[] { "Inventory", "IsInventoryOpen", "IsRunActive", "InventoryFocusCell", "InventoryFocusQuickSlot" })
            gamePropertiesBefore[property] = Get(game, property);
        equipmentSelectionBefore = Field(game, "equipmentSelection");
        running = true; deadline = EditorApplication.timeSinceStartup + 90;
        EditorApplication.playModeStateChanged += PlayChanged;
        EditorApplication.update += Watchdog;
        try
        {
            game.enabled = false; hud.enabled = false; Time.timeScale = 0; Application.runInBackground = true;
            SetupView();
            host = new GameObject("AccessoryStyleReview20261004_TEMP") { hideFlags = HideFlags.DontSave };
            paint = host.AddComponent<AccessoryStylePaint20261004>();
            flat = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            flat.SetPixel(0, 0, new Color32(52, 55, 70, 255)); flat.Apply();
            paint.StartCoroutine(Guard(Run()));
            return folder + " (running; supplied state, no player saves written)";
        }
        catch (Exception error) { Finish("FAIL", error.ToString()); throw; }
    }

    static void AuditSources()
    {
        var manifest = JArray.Parse(File.ReadAllText(Review + "accepted-selection.json", Encoding.UTF8));
        var prior = (JArray)JObject.Parse(File.ReadAllText(Review + "active-item-inventory.json", Encoding.UTF8))["accessories"];
        var seen = new HashSet<string>();
        foreach (JObject row in manifest)
        {
            string id = (string)row["id"], path = (string)row["target"];
            var def = AccessoryLootTable.Find(id);
            Require(def != null && seen.Add(id), "Unknown/duplicate selection " + id);
            var icon = InventoryIconFactory.Get(id, def.Color, InventoryIconKind.Accessory);
            var direct = ItemIconLibrary.Get(id, InventoryIconKind.Accessory);
            var world = InventoryIconFactory.GetWorldSprite(id, def.Color, InventoryIconKind.Accessory);
            var bounds = ItemIconLibrary.ContentBounds(icon);
            Require(icon != null && icon == direct && icon == PixelUiSkin.GetAccessoryIcon(id) && world != null && world.texture == icon, "Production icon source disagreement " + id);
            Require(AssetDatabase.GetAssetPath(icon).Replace('\\', '/') == path.Replace('\\', '/'), "Wrong canonical PNG " + id);
            Require(icon.width == (int)row["nativeWidth"] && icon.height == (int)row["nativeHeight"], "Stale/native texture dimensions " + id);
            Require(Hash(path) == ((string)row["sourceSha256"]).ToLowerInvariant(), "Selected original bytes changed " + id);
            var previous = prior.First(x => (string)x["id"] == id);
            var guid = AssetDatabase.AssetPathToGUID(path);
            Require(guid == (string)previous["guid"], "Existing GUID changed " + id);
            Require(icon.filterMode == FilterMode.Point && icon.mipmapCount == 1, "Pixel import mismatch " + id);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            var nativeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Require(importer != null && importer.isReadable && importer.textureCompression == TextureImporterCompression.Uncompressed &&
                nativeSprite != null && nativeSprite.pixelsPerUnit == 32 && Vector2.Distance(nativeSprite.pivot, new Vector2(icon.width, icon.height) * .5f) < .001f,
                "Native importer PPU/pivot/readability/compression mismatch " + id);
            Require(world.rect == new Rect(bounds.x, bounds.y, bounds.width, bounds.height) && Mathf.Approximately(world.pixelsPerUnit, nativeSprite.pixelsPerUnit), "World sprite must preserve imported PPU/crop " + id);
            Require(Vector2.Distance(world.pivot, new Vector2(bounds.width, bounds.height) * .5f) < .001f, "Drop pivot off center " + id);
            records.Add(new JObject { ["id"] = id, ["name"] = def.Name, ["path"] = path, ["guid"] = guid,
                ["sha256"] = Hash(path), ["nativeWidth"] = icon.width, ["nativeHeight"] = icon.height,
                ["visibleWidth"] = bounds.width, ["visibleHeight"] = bounds.height, ["importerPPU"] = nativeSprite.pixelsPerUnit, ["worldPPU"] = world.pixelsPerUnit,
                ["selectionStatus"] = row["status"], ["allProductionIconSourcesAgree"] = true,
                ["priorGuidPreserved"] = true, ["worldCentered"] = true, ["worldPpuPreserved"] = true });
        }
        Require(seen.Count == 24 && seen.SetEquals(AccessoryLootTable.All.Select(x => x.Id)), "Review must cover all 24 active accessories.");
        var priorWeapons = (JArray)JObject.Parse(File.ReadAllText(Review + "active-item-inventory.json", Encoding.UTF8))["weapons"];
        foreach (var id in QuickSamples)
        {
            var texture = InventoryIconFactory.Get(id, Color.white, InventoryIconKind.Weapon);
            var world = InventoryIconFactory.GetWorldSprite(id, Color.white, InventoryIconKind.Weapon);
            var path = AssetDatabase.GetAssetPath(texture);
            var nativeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            var bounds = ItemIconLibrary.ContentBounds(texture);
            var previous = priorWeapons.First(x => (string)x["id"] == id);
            float originalPpu = (float)previous["importerPPU"];
            // Explicit parent-approved 48 -> 32 correction; keep 48 as historical evidence.
            float expectedPpu = id == "void_revolver" ? 32f : originalPpu;
            Require(nativeSprite != null && importer != null && world != null && world.texture == texture &&
                Mathf.Approximately(nativeSprite.pixelsPerUnit, expectedPpu) && Mathf.Approximately(importer.spritePixelsPerUnit, expectedPpu) &&
                Mathf.Approximately(world.pixelsPerUnit, expectedPpu), "Weapon current approved PPU/cached world PPU mismatch " + id);
            Require(world.rect == new Rect(bounds.x, bounds.y, bounds.width, bounds.height) &&
                Vector2.Distance(world.pivot, new Vector2(bounds.width, bounds.height) * .5f) < .001f, "Weapon crop/center mismatch " + id);
            weaponSources.Add(new JObject { ["id"] = id, ["path"] = path, ["nativeWidth"] = texture.width, ["nativeHeight"] = texture.height,
                ["visibleWidth"] = bounds.width, ["visibleHeight"] = bounds.height, ["priorImporterPPU"] = originalPpu,
                ["expectedImporterPPU"] = expectedPpu, ["approvedPpuCorrection"] = id == "void_revolver",
                ["ppuCorrectionEvidence"] = id == "void_revolver" ? "ArtDirection/Review-20261004/item-pixel-presentation/void-revolver-before/result.txt" : null,
                ["importerPPU"] = nativeSprite.pixelsPerUnit, ["worldPPU"] = world.pixelsPerUnit,
                ["worldWidth"] = bounds.width / world.pixelsPerUnit, ["worldHeight"] = bounds.height / world.pixelsPerUnit,
                ["worldPpuPreserved"] = true });
        }
        File.WriteAllText(folder + "/sources.json", records.ToString(), Utf8);
        File.WriteAllText(folder + "/weapon-source-samples.json", weaponSources.ToString(), Utf8);
    }

    static void SetupView()
    {
        view = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")); view.Focus();
        oldSize = (int)view.GetType().GetProperty("selectedSizeIndex", All).GetValue(view);
        viewSizeCaptured = true;
        var t = Type.GetType("UnityEditor.GameViewSizes,UnityEditor");
        Require(t != null, "UnityEditor.GameViewSizes type not found.");
        // Unity 6000.6 inherits instance from ScriptableSingleton<GameViewSizes>.
        var instanceProperty = t.GetProperty("instance", All);
        Require(instanceProperty != null, "Inherited GameViewSizes.instance property not found.");
        var singleton = instanceProperty.GetValue(null);
        Require(singleton != null, "GameViewSizes singleton is unavailable.");
        sizes = t.GetMethod("GetGroup", All).Invoke(singleton, new[] { Enum.Parse(Type.GetType("UnityEditor.GameViewSizeGroupType,UnityEditor"), "Standalone") });
        oldCustomCount = (int)Invoke(sizes, "GetCustomCount");
        customCountCaptured = true;
        autoTick = AppDomain.CurrentDomain.GetAssemblies().Select(x => x.GetType("Unity.Pipeline.Editor.Commands.AutoTickCommand")).FirstOrDefault(x => x != null);
        Require(autoTick != null, "Installed pipeline auto-tick command not found.");
        oldTick = (bool)autoTick.GetProperty("IsEnabled", All).GetValue(null);
        oldInterval = (long)autoTick.GetProperty("CurrentIntervalMs", All).GetValue(null);
        autoTickCaptured = true;
        autoTick.GetMethod("SetAutoTick", All).Invoke(null, new object[] { true, 0, false });
    }

    static void Resolution(int width, int height)
    {
        var t = Type.GetType("UnityEditor.GameViewSize,UnityEditor");
        var size = Activator.CreateInstance(t, new object[] { Enum.Parse(Type.GetType("UnityEditor.GameViewSizeType,UnityEditor"), "FixedResolution"), width, height, "Accessory review " + width + "x" + height });
        var index = (int)Invoke(sizes, "GetTotalCount"); Invoke(sizes, "AddCustomSize", size); addedSizes.Add(index);
        view.GetType().GetProperty("selectedSizeIndex", All).SetValue(view, index); view.Repaint();
    }

    static InventorySystem Bag(int start, int count)
    {
        var inventory = new InventorySystem();
        foreach (var id in QuickSamples) Require(inventory.AddWeapon(WeaponLootTable.Find(id)), "Cannot supply quick slot " + id);
        for (int i = start; i < start + count; i++) Require(inventory.AddAccessory(AccessoryLootTable.All[i]), "Cannot supply accessory " + i);
        return inventory;
    }

    static IEnumerator Run()
    {
        foreach (var resolution in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080) })
        {
            Resolution(resolution.x, resolution.y);
            for (int i = 0; i < 3; i++) yield return null;
            Require(Screen.width == resolution.x && Screen.height == resolution.y, "Game View resolution did not settle.");
            string prefix = resolution.x + "x" + resolution.y;
            for (page = 0; page < 2; page++)
            {
                bag = page == 0 ? Bag(0, 20) : Bag(20, 4); mode = "inventory"; tooltipId = null;
                yield return new WaitForEndOfFrame(); Capture(prefix + "-bag-page" + (page + 1));
            }
            // Actual production tooltips for short, long, and multiline semantic-risk examples.
            foreach (var id in new[] { "ruby_heart", "mana_thread", "powder_satchel", "last_light_pendant" })
            {
                int index = AccessoryLootTable.All.ToList().FindIndex(x => x.Id == id);
                page = index < 20 ? 0 : 1; bag = page == 0 ? Bag(0, 20) : Bag(20, 4);
                mode = "inventory"; tooltipId = id;
                yield return new WaitForEndOfFrame(); Capture(prefix + "-tooltip-" + id);
            }
            tooltipId = null;
            foreach (var box in new[] { 54f, 48f, 72f })
            {
                mode = "probe"; probeBox = box;
                yield return new WaitForEndOfFrame(); Capture(prefix + "-all24-icon-box" + box, true);
            }
            mode = "quick"; bag = Bag(0, 0);
            yield return new WaitForEndOfFrame(); Capture(prefix + "-production-quick-slots-native-icons", true);
            BuildDrops(); mode = "drops";
            yield return new WaitForEndOfFrame(); Capture(prefix + "-all24-production-frozen-drops");
            ClearDrops();
            deadline = EditorApplication.timeSinceStartup + 90;
        }
    }

    static IEnumerator Guard(IEnumerator run)
    {
        while (true)
        {
            bool next; object current = null;
            try { next = run.MoveNext(); if (next) current = run.Current; }
            catch (Exception error) { Finish("FAIL", error.ToString()); yield break; }
            if (!next) break;
            yield return current;
        }
        Finish("PASS", null);
    }

    public static void Paint()
    {
        if (!running || cleaning || Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout) return;
        var matrix = GUI.matrix; var color = GUI.color; var depth = GUI.depth; var mouse = Event.current.mousePosition;
        var originalBag = game.Inventory; var originalOpen = game.IsInventoryOpen; var originalActive = game.IsRunActive;
        var originalSelection = Field(game, "equipmentSelection"); var oldFocus = game.InventoryFocusCell; var oldQuick = game.InventoryFocusQuickSlot;
        var oldSetTab = Field(hud, "inventorySetTab"); var oldDragKey = Field(hud, "inventoryDragKey"); var oldDragIcon = Field(hud, "inventoryDragIcon");
        try
        {
            GUI.depth = -1200; GUI.color = Color.white;
            float scale = GameTypography.LayoutScale, width = Screen.width / scale, height = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            Event.current.mousePosition = new Vector2(-1000, -1000);
            if (mode != "drops") GUI.DrawTexture(new Rect(0, 0, width, height), flat);
            Set(game, "Inventory", bag); Set(game, "IsInventoryOpen", true); Set(game, "IsRunActive", true);
            Field(hud, "inventorySetTab", false); Field(hud, "inventoryDragKey", ""); Field(hud, "inventoryDragIcon", null);
            Set(game, "InventoryFocusCell", 0); Set(game, "InventoryFocusQuickSlot", -1);
            Field(game, "equipmentSelection", bag == null || bag.OwnedAccessories.Count == 0 ? 0L : bag.OwnedAccessories[0].Identity);
            Invoke(hud, "PrepareStyles");
            if (mode == "inventory")
            {
                Invoke(hud, "DrawInventory", width, height);
                if (tooltipId != null)
                {
                    var index = bag.Accessories.ToList().FindIndex(x => x.Id == tooltipId);
                    Require(index >= 0, "Tooltip fixture entry missing " + tooltipId);
                    Event.current.mousePosition = new Vector2(width - 465, height * .40f);
                    Invoke(hud, "DrawItemTooltip", width, height, "A:" + index);
                }
            }
            else if (mode == "quick")
            {
                Invoke(hud, "DrawQuickSlots", width, height);
                Label(new Rect(60, 120, width - 120, 35), "현재 퀵슬롯 80×92 · 아이콘 52×54 · 원본 PPU 유지", 23);
                probes.Clear();
                for (int i = 0; i < QuickSamples.Length; i++)
                {
                    var id = QuickSamples[i]; var icon = InventoryIconFactory.Get(id, Color.white, InventoryIconKind.Weapon);
                    var area = new Rect(width * .5f - 320 + i * 320 - 26, 285, 52, 54);
                    ItemIconLibrary.DrawGUI(area, icon); AddProbe(id, area, icon, scale);
                    Label(new Rect(area.center.x - 150, 235, 300, 28), id + " / " + (float)weaponSources[i]["importerPPU"] + " PPU", 17);
                }
            }
            else if (mode == "probe") DrawProbes(width, scale);
            else if (mode == "drops")
                for (int i = 0; i < 24; i++)
                {
                    var labelPoint = dropCamera.WorldToScreenPoint(dropRoots[i].transform.position + Vector3.down * .48f);
                    Label(new Rect(labelPoint.x / scale - 90, (Screen.height - labelPoint.y) / scale, 180, 28), AccessoryLootTable.All[i].Name, 14);
                }
            Label(new Rect(12, 6, width - 24, 28), "공급 상태 검수 · " + mode + " · 원본 게임 UI/아이콘 경로 · 저장/정상 획득 검증 아님", 15);
        }
        catch (Exception error) { frameError = error.ToString(); }
        finally
        {
            Set(game, "Inventory", originalBag); Set(game, "IsInventoryOpen", originalOpen); Set(game, "IsRunActive", originalActive);
            Set(game, "InventoryFocusCell", oldFocus); Set(game, "InventoryFocusQuickSlot", oldQuick); Field(game, "equipmentSelection", originalSelection);
            Field(hud, "inventorySetTab", oldSetTab); Field(hud, "inventoryDragKey", oldDragKey); Field(hud, "inventoryDragIcon", oldDragIcon);
            GUI.matrix = matrix; GUI.color = color; GUI.depth = depth; Event.current.mousePosition = mouse;
        }
    }

    static void Label(Rect rect, string text, int size)
    {
        var style = new GUIStyle(GUI.skin.label) { font = GameTypography.Regular, fontSize = size, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = Color.white; GUI.Label(rect, text, style);
    }

    static void DrawProbes(float width, float scale)
    {
        probes.Clear();
        for (int i = 0; i < AccessoryLootTable.All.Count; i++)
        {
            var def = AccessoryLootTable.All[i]; var icon = InventoryIconFactory.Get(def.Id, def.Color, InventoryIconKind.Accessory);
            float cx = width * .5f - 500 + i % 6 * 200, y = 100 + i / 6 * 145;
            var area = new Rect(cx - probeBox * .5f, y, probeBox, probeBox);
            ItemIconLibrary.DrawGUI(area, icon);
            AddProbe(def.Id, area, icon, scale);
            Label(new Rect(cx - 98, y + 84, 196, 24), def.Name, 14);
            Label(new Rect(cx - 98, y + 106, 196, 20), def.Id, 11);
        }
    }

    static void AddProbe(string id, Rect area, Texture2D icon, float scale)
    {
        var bounds = ItemIconLibrary.ContentBounds(icon);
        var fit = Mathf.Min(area.width * scale / bounds.width, area.height * scale / bounds.height);
        var pixels = fit >= 1 ? Mathf.Floor(fit) : fit;
        var expected = new Rect(Mathf.Round(area.center.x * scale - bounds.width * pixels * .5f),
            Mathf.Round(area.center.y * scale - bounds.height * pixels * .5f), bounds.width * pixels, bounds.height * pixels);
        probes.Add(new Probe { Id = id, Area = new Rect(area.x * scale, area.y * scale, area.width * scale, area.height * scale), Expected = expected, Scale = pixels });
    }

    static void BuildDrops()
    {
        foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>()) { cameraStates[camera] = camera.enabled; camera.enabled = false; }
        var cameraObject = new GameObject("AccessoryReviewDropCamera_TEMP") { hideFlags = HideFlags.DontSave };
        dropCamera = cameraObject.AddComponent<Camera>();
        dropCamera.orthographic = true; dropCamera.orthographicSize = 5.625f;
        dropCamera.transform.position = new Vector3(1000, 1000, -10);
        dropCamera.clearFlags = CameraClearFlags.SolidColor; dropCamera.backgroundColor = new Color32(52, 55, 70, 255);
        dropCamera.cullingMask = 1 << 30; dropCamera.allowHDR = false; dropCamera.allowMSAA = false;
        for (int i = 0; i < 24; i++)
        {
            var def = AccessoryLootTable.All[i]; var position = new Vector2(1000 - 7.75f + i % 6 * 3.1f, 1000 + 3.35f - i / 6 * 2.1f);
            var sprite = InventoryIconFactory.GetWorldSprite(def.Id, def.Color, InventoryIconKind.Accessory);
            var drop = FieldLootPickup.Create(position, def.Name, def.Color, null, game.Settings, () => false, i, sprite, def.Rarity);
            drop.enabled = false; drop.gameObject.hideFlags = HideFlags.DontSave; drop.transform.position = position;
            Field(drop, "age", 5f); Field(drop, "delay", 0f); Field(drop, "duration", .01f);
            Set(drop, "LandingPosition", position);
            var spin = (Transform)Field(drop, "itemSpin"); spin.localPosition = Vector3.up * .20f;
            var renderer = (SpriteRenderer)Field(drop, "itemRenderer"); renderer.enabled = true;
            ((SpriteRenderer)Field(drop, "groundMarker")).enabled = true;
            foreach (var transform in drop.GetComponentsInChildren<Transform>()) transform.gameObject.layer = 30;
            dropRoots.Add(drop.gameObject);
            Require(renderer.sprite == sprite && Vector3.Distance(renderer.bounds.center, spin.position) < .001f, "Field drop visual not centered " + def.Id);
            var expectedSize = new Vector2(sprite.rect.width, sprite.rect.height) / sprite.pixelsPerUnit;
            Require(Vector3.Distance(renderer.transform.lossyScale, Vector3.one) < .001f &&
                Vector2.Distance(renderer.bounds.size, expectedSize) < .001f, "Field drop must use scale 1 and imported PPU size " + def.Id);
            float screenPixelsPerUnit = dropCamera.pixelHeight / (dropCamera.orthographicSize * 2);
            float pixelScale = screenPixelsPerUnit / sprite.pixelsPerUnit;
            dropChecks.Add(new JObject { ["id"] = def.Id, ["screenWidth"] = Screen.width, ["screenHeight"] = Screen.height,
                ["worldPPU"] = sprite.pixelsPerUnit, ["lossyScale"] = new JArray(renderer.transform.lossyScale.x, renderer.transform.lossyScale.y),
                ["worldSize"] = new JArray(renderer.bounds.size.x, renderer.bounds.size.y),
                ["expectedWorldSize"] = new JArray(expectedSize.x, expectedSize.y), ["screenPixelsPerSourcePixel"] = pixelScale,
                ["projectedPixelSize"] = new JArray(renderer.bounds.size.x * screenPixelsPerUnit, renderer.bounds.size.y * screenPixelsPerUnit),
                ["integerProjectedScale"] = Mathf.Approximately(pixelScale, Mathf.Round(pixelScale)), ["centered"] = true });
        }
    }

    static void ClearDrops()
    {
        // OnGUI continues during the next resolution's settle frames; leave this mode before destroying its data.
        if (mode == "drops") mode = "transition";
        foreach (var root in dropRoots) if (root != null) UnityEngine.Object.DestroyImmediate(root);
        dropRoots.Clear();
        if (dropCamera != null) UnityEngine.Object.DestroyImmediate(dropCamera.gameObject);
        dropCamera = null;
        foreach (var pair in cameraStates) if (pair.Key != null) pair.Key.enabled = pair.Value;
        cameraStates.Clear();
    }

    static void Capture(string name, bool verifyPixels = false)
    {
        Require(string.IsNullOrEmpty(frameError), frameError);
        if (mode == "drops") Require(dropCamera != null && dropRoots.Count == 24, "Drop capture must contain all 24 production drops.");
        var image = ScreenCapture.CaptureScreenshotAsTexture();
        try
        {
            File.WriteAllBytes(folder + "/" + name + ".png", image.EncodeToPNG());
            if (verifyPixels)
            {
                Require(probes.Count == (mode == "quick" ? QuickSamples.Length : 24), "Probe board rendered the wrong item count.");
                foreach (var p in probes) CheckRenderedPixels(image, p, name);
            }
            captures.Add(new JObject { ["file"] = name + ".png", ["width"] = image.width, ["height"] = image.height,
                ["mode"] = mode, ["guiScale"] = GameTypography.LayoutScale, ["suppliedState"] = true });
        }
        finally { UnityEngine.Object.Destroy(image); }
    }

    static void CheckRenderedPixels(Texture2D image, Probe p, string capture)
    {
        int left = Mathf.FloorToInt(p.Area.xMin) - 3, top = Mathf.FloorToInt(p.Area.yMin) - 3;
        int right = Mathf.CeilToInt(p.Area.xMax) + 3, bottom = Mathf.CeilToInt(p.Area.yMax) + 3;
        var colors = image.GetPixels32();
        var background = colors[(image.height - 1 - top) * image.width + left];
        int minX = right, maxX = -1, minY = bottom, maxY = -1;
        for (int y = top; y < bottom; y++) for (int x = left; x < right; x++)
        {
            var c = colors[(image.height - 1 - y) * image.width + x];
            if (Math.Max(Math.Abs(c.r - background.r), Math.Max(Math.Abs(c.g - background.g), Math.Abs(c.b - background.b))) <= 7) continue;
            minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
        }
        Require(maxX >= minX, "Rendered icon missing " + capture + "/" + p.Id);
        var actual = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        float geometryError = Mathf.Max(Mathf.Abs(actual.xMin - p.Expected.xMin), Mathf.Abs(actual.xMax - p.Expected.xMax),
            Mathf.Abs(actual.yMin - p.Expected.yMin), Mathf.Abs(actual.yMax - p.Expected.yMax));
        float centerError = Mathf.Max(Mathf.Abs(actual.center.x - p.Area.center.x), Mathf.Abs(actual.center.y - p.Area.center.y));
        drawChecks.Add(new JObject { ["capture"] = capture, ["id"] = p.Id, ["sourcePixelScale"] = p.Scale,
            ["fractionalReduction"] = p.Scale < 1, ["actualBounds"] = new JArray(actual.x, actual.y, actual.width, actual.height),
            ["expectedBounds"] = new JArray(p.Expected.x, p.Expected.y, p.Expected.width, p.Expected.height),
            ["geometryErrorPixels"] = geometryError, ["centerErrorPixels"] = centerError });
        Require(geometryError <= 1 && centerError <= 1, "Actual rendered icon bounds/centering mismatch " + capture + "/" + p.Id);
    }

    static void Watchdog() { if (running && (EditorApplication.timeSinceStartup > deadline || !Application.isPlaying)) Finish("CANCELLED", "Timed out or Play Mode ended; fixture restored."); }
    static void PlayChanged(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingPlayMode) Finish("CANCELLED", "Play Mode exit; fixture restored."); }
    public static string Cancel() { if (running) Finish("CANCELLED", "Cancelled explicitly; fixture restored."); return "Accessory fixture inactive/restored."; }
    public static void HostDestroyed() { if (running && !cleaning) Finish("CANCELLED", "Fixture host destroyed; fixture restored."); }
    static void Finish(string result, string error)
    {
        if (!running || cleaning) return;
        cleaning = true;
        var restoreErrors = new JArray();
        void Restore(Action action) { try { action(); } catch (Exception ex) { restoreErrors.Add(ex.ToString()); } }
        Restore(() => { if (paint != null) { paint.enabled = false; paint.StopAllCoroutines(); } });
        Restore(ClearDrops);
        Restore(() => {
            if (game != null)
            {
                foreach (var pair in gamePropertiesBefore) Set(game, pair.Key, pair.Value);
                Field(game, "equipmentSelection", equipmentSelectionBefore); game.enabled = oldGameEnabled;
            }
            if (hud != null) { hud.enabled = oldHudEnabled; Field(hud, "tooltipScrollKey", oldTooltipKey); Field(hud, "tooltipScroll", oldTooltipScroll); }
        });
        Restore(() => { Time.timeScale = oldTime; Application.runInBackground = oldBackground; });
        Restore(() => {
            if (view != null && viewSizeCaptured) view.GetType().GetProperty("selectedSizeIndex", All).SetValue(view, oldSize);
            if (sizes != null && customCountCaptured)
            {
                for (int i = addedSizes.Count - 1; i >= 0; i--) Invoke(sizes, "RemoveCustomSize", addedSizes[i]);
                Require((int)Invoke(sizes, "GetCustomCount") == oldCustomCount, "Custom Game View presets not restored.");
            }
            if (view != null) view.Repaint();
        });
        Restore(() => { if (autoTick != null && autoTickCaptured) autoTick.GetMethod("SetAutoTick", All).Invoke(null, new object[] { oldTick, (int)oldInterval, false }); });
        Restore(() => { if (host != null) UnityEngine.Object.DestroyImmediate(host); if (flat != null) UnityEngine.Object.DestroyImmediate(flat); });
        EditorApplication.playModeStateChanged -= PlayChanged; EditorApplication.update -= Watchdog;
        bool unchanged = false;
        Restore(() => { var after = SaveHashes(); unchanged = saveBefore.Count == after.Count && saveBefore.All(x => after.TryGetValue(x.Key, out var hash) && hash == x.Value); Require(unchanged, "A user/active-profile file changed during review; never overwrite it automatically."); });
        if (restoreErrors.Count > 0) result = "FAIL";
        File.WriteAllText(folder + "/result.json", new JObject { ["result"] = result, ["utc"] = DateTime.UtcNow.ToString("O"),
            ["suppliedState"] = true, ["normalGameplayValidated"] = false, ["sourceItems"] = records,
            ["captures"] = captures, ["renderedPixelChecks"] = drawChecks, ["weaponSourceSamples"] = weaponSources,
            ["dropGeometryChecks"] = dropChecks, ["savesUnchanged"] = unchanged,
            ["stateRestored"] = restoreErrors.Count == 0, ["restoreErrors"] = restoreErrors, ["error"] = error,
            ["dropNote"] = "Actual FieldLootPickup constructor and inventory world Sprite; supplied settled pose, reveal/collection skipped.",
            ["quickSlotNote"] = "Actual production 80x92 slots / 52x54 icon area plus same-size DrawGUI pixel probes. World sprites preserve current importer PPU. void_revolver explicitly changed from 48 to 32 PPU; both baseline and approved current values are recorded." }.ToString(), Utf8);
        running = false; cleaning = false; mode = null; frameError = null; host = null; paint = null; flat = null;
        Debug.Log("[Accessory style review] " + result + " " + folder);
    }
}

public sealed class AccessoryStylePaint20261004 : MonoBehaviour
{
    void OnGUI() { AccessoryStyleReview20261004.Paint(); }
    void OnDestroy() { AccessoryStyleReview20261004.HostDestroyed(); }
}
