using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Menu: Lythrum > Setup > Shards + Boss
// Builds the memory shard prefab, the skeleton boss prefab and its Animator Controller from the sprites in Assets/Sprites,
// then puts the 5 fixed-position shards, the boss and the arena trigger into the City, Desert and Dungeon scenes.
// Safe to run again: it replaces what it made before ("... (auto)" objects and the two prefabs).
// Note: rebuilding a map with Lythrum > Build Maps recreates the scene, so run this again afterwards.
public static class LythrumBossSetup
{
    const string ShardSprite = "Assets/Sprites/memory-shard.png";
    const string BossSpriteFolder = "Assets/Sprites/Enemy/Skeleton Boss Asset/";
    const string ShockwaveFolder = "Assets/Sprites/shock_wave/frame_by_frame";
    const string FlameSheet = "Assets/Sprites/flames/flame_horizontal.png";

    const string ShardPrefabPath = "Assets/Prefabs/Shards/MemoryShard.prefab";
    const string BossPrefabPath = "Assets/Prefabs/Boss/SkeletonBoss.prefab";
    const string ControllerPath = "Assets/Animation/Boss/SkeletonBoss.controller";

    // The boss art is ~1200px at 100 px/unit, so it is scaled down: the head to about 3 tiles wide, the hands a bit smaller.
    const float HeadScale = 0.35f;
    const float HandScale = 0.27f;

    // Fixed shard positions, in map tiles (x right, y down, like Assets/Maps/Data). Checked against the Collision/Decor layers.
    struct Placement
    {
        public string scene, id;
        public int x, y;
        public Placement(string scene, string id, int x, int y) { this.scene = scene; this.id = id; this.x = x; this.y = y; }
    }

    static readonly Placement[] Shards =
    {
        new Placement("City", "town_01", 21, 11),       // end of the north-west dirt path
        new Placement("City", "town_02", 96, 72),       // end of the south-east dirt path, by the little shrine
        new Placement("Desert", "desert_01", 7, 15),    // on the raised west plateau (reach it by the stairs)
        new Placement("Desert", "desert_02", 12, 56),   // inside the walled shrine in the south-west
        new Placement("Dungeon", "dungeon_01", 83, 13), // the north-east shrine room
    };

    // Map sizes in tiles (from the first line of Assets/Maps/Data/<Map>.txt).
    static Vector2Int MapSize(string scene) => scene == "City" ? new Vector2Int(128, 80) : new Vector2Int(96, 72);

    // Same conversion LythrumMapBuilder uses (tile centre; fractional tiles allowed).
    static Vector3 TileToWorld(string scene, float x, float y)
    {
        Vector2Int size = MapSize(scene);
        return new Vector3(x - size.x / 2f + 0.5f, size.y / 2f - y - 0.5f, 0f);
    }

    // ------------------------------------------------------------------ menu

    [MenuItem("Lythrum/Setup/Shards + Boss (prefabs, controller, scenes)")]
    public static void SetupAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        LythrumWorldSetup.BuildAudio();     // picks up the new boss sounds
        BuildAssets();
        PlaceInScenes();
    }

    // For command line use: Unity -batchmode -projectPath . -executeMethod LythrumBossSetup.SetupAllBatch -quit
    public static void SetupAllBatch()
    {
        LythrumWorldSetup.BuildAudio();
        BuildAssets();
        PlaceInScenes();
    }

    static void BuildAssets()
    {
        MakePixelCrisp(ShardSprite);
        MakePixelCrisp(FlameSheet);
        MakePixelCrisp("Assets/Sprites/flames/flame_exposion.png");

        BuildShardPrefab();
        BuildController();
        BuildBossPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // ------------------------------------------------------------------ shard prefab

    static void BuildShardPrefab()
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ShardSprite);
        if (sprite == null) Debug.LogWarning("[Boss Setup] Missing " + ShardSprite + ", the shard prefab will have no sprite.");

        GameObject go = new GameObject("MemoryShard");
        go.transform.localScale = Vector3.one * 3f;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = "Player";
        sr.sortingOrder = 3;

        CircleCollider2D trigger = go.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = 0.3f;              // x3 scale = about one tile

        // No Light2D here on purpose: City and Desert have no lights, and the first light in a scene turns everything
        // else black. The sprite uses the default unlit material, so it is already visible in the dark dungeon.
        go.AddComponent<MemoryShard>();

        EnsureFolder(Path.GetDirectoryName(ShardPrefabPath).Replace('\\', '/'));
        PrefabUtility.SaveAsPrefabAsset(go, ShardPrefabPath);
        Object.DestroyImmediate(go);
    }

    // ------------------------------------------------------------------ animator controller

    // Empty states (no clips yet) with the 7 triggers. Drop your clips into the states afterwards.
    static void BuildController()
    {
        EnsureFolder(Path.GetDirectoryName(ControllerPath).Replace('\\', '/'));
        AssetDatabase.DeleteAsset(ControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        string[] triggers = { "Rise", "Scream", "Slam", "Breath", "FearCatch", "Phase2Roar", "Die" };
        foreach (string trigger in triggers) controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState hidden = machine.AddState("Hidden");
        AnimatorState idle = machine.AddState("Idle");
        machine.defaultState = hidden;

        foreach (string trigger in triggers)
        {
            AnimatorState state = machine.AddState(trigger);

            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            enter.hasExitTime = false;
            enter.duration = 0f;
            enter.canTransitionToSelf = true;   // Slam fires again and again during the fear catch

            if (trigger == "Die") continue;     // the boss stays dead

            AnimatorStateTransition back = state.AddTransition(idle);
            back.hasExitTime = true;
            back.exitTime = 1f;
            back.duration = 0f;
        }
    }

    // ------------------------------------------------------------------ boss prefab

    static void BuildBossPrefab()
    {
        Sprite headSprite = LoadBossSprite("boss_head_bg.png");
        Sprite leftSprite = LoadBossSprite("boss_hand_left.png");
        Sprite rightSprite = LoadBossSprite("boss_hand_right.png");
        if (headSprite == null || leftSprite == null || rightSprite == null) return;

        // The boss is a head with a hand floating on each side (no body). Positions in world units, from the sprite sizes.
        float handX = headSprite.bounds.extents.x * HeadScale + leftSprite.bounds.extents.x * HandScale + 0.8f;

        GameObject root = new GameObject("SkeletonBoss");
        Animator animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        SkeletonBoss boss = root.AddComponent<SkeletonBoss>();

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);

        // The head is drawn over the player ("WalkBehind" layer), so the player can walk behind it. The hands stay behind the player.
        BossPart head = MakePart(visual.transform, "Head", headSprite, Vector2.zero, BossPartType.Head, HeadScale, "WalkBehind", 6);
        BossPart left = MakePart(visual.transform, "HandLeft", leftSprite, new Vector2(-handX, -1.3f), BossPartType.LeftHand, HandScale, "Collision", 7);
        BossPart right = MakePart(visual.transform, "HandRight", rightSprite, new Vector2(handX, -1.3f), BossPartType.RightHand, HandScale, "Collision", 7);

        GameObject mouth = new GameObject("Mouth");
        mouth.transform.SetParent(root.transform, false);
        mouth.transform.localPosition = new Vector3(0f, -0.9f, 0f);

        // The last shard drops in front of the boss so it is easy to reach.
        GameObject drop = new GameObject("DropPoint");
        drop.transform.SetParent(root.transform, false);
        drop.transform.localPosition = new Vector3(0f, -4f, 0f);

        SerializedObject so = new SerializedObject(boss);
        so.FindProperty("leftHand").objectReferenceValue = left;
        so.FindProperty("rightHand").objectReferenceValue = right;
        so.FindProperty("head").objectReferenceValue = head;
        so.FindProperty("visual").objectReferenceValue = visual.transform;
        so.FindProperty("mouth").objectReferenceValue = mouth.transform;
        so.FindProperty("dropPoint").objectReferenceValue = drop.transform;
        so.FindProperty("fearHandScale").floatValue = HandScale;

        so.FindProperty("fearHandSprite").objectReferenceValue = leftSprite;     // no separate fear hand art: reuse the hand
        so.FindProperty("shardPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(ShardPrefabPath);

        // A built-in circle, tinted at runtime for the slam and fear warnings (nothing is drawn in code).
        Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        if (circle == null) Debug.LogWarning("[Boss Setup] Built-in circle sprite not found, warning zones will be invisible.");
        so.FindProperty("circleSprite").objectReferenceValue = circle;

        SetSprites(so.FindProperty("shockwaveFrames"), LoadShockwaveFrames());
        SetSprites(so.FindProperty("flameFrames"), LoadFlameFrames());
        so.ApplyModifiedPropertiesWithoutUndo();

        EnsureFolder(Path.GetDirectoryName(BossPrefabPath).Replace('\\', '/'));
        PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
        Object.DestroyImmediate(root);
    }

    static BossPart MakePart(Transform parent, string partName, Sprite sprite, Vector2 position, BossPartType type, float scale, string sortingLayer, int order)
    {
        GameObject go = new GameObject(partName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = Vector3.one * scale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = order;

        // A trigger, so the player walks through the boss but swings still hit it.
        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = sprite.bounds.size;
        box.offset = sprite.bounds.center;

        BossPart part = go.AddComponent<BossPart>();
        SerializedObject so = new SerializedObject(part);
        so.FindProperty("type").enumValueIndex = (int)type;
        so.ApplyModifiedPropertiesWithoutUndo();
        return part;
    }

    static Sprite LoadBossSprite(string file)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BossSpriteFolder + file);
        if (sprite == null) Debug.LogWarning("[Boss Setup] Missing boss sprite " + BossSpriteFolder + file + ", the boss prefab was not built.");
        return sprite;
    }

    static Sprite[] LoadShockwaveFrames()
    {
        if (!Directory.Exists(ShockwaveFolder)) { Debug.LogWarning("[Boss Setup] Missing " + ShockwaveFolder); return new Sprite[0]; }

        return Directory.GetFiles(ShockwaveFolder, "*.png")
            .Select(p => p.Replace('\\', '/'))
            .OrderBy(p => TrailingNumber(Path.GetFileNameWithoutExtension(p)))
            .Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p))
            .Where(s => s != null)
            .ToArray();
    }

    static Sprite[] LoadFlameFrames()
    {
        Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(FlameSheet).OfType<Sprite>().OrderBy(s => TrailingNumber(s.name)).ToArray();
        if (frames.Length == 0) Debug.LogWarning("[Boss Setup] No flame sprites in " + FlameSheet);
        return frames;
    }

    static int TrailingNumber(string text)
    {
        int underscore = text.LastIndexOf('_');
        return underscore >= 0 && int.TryParse(text.Substring(underscore + 1), out int n) ? n : 0;
    }

    static void SetSprites(SerializedProperty array, Sprite[] sprites)
    {
        array.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
    }

    // Pixel art: no smoothing, no compression.
    static void MakePixelCrisp(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        if (importer.filterMode == FilterMode.Point && importer.textureCompression == TextureImporterCompression.Uncompressed) return;

        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    // ------------------------------------------------------------------ scenes

    static void PlaceInScenes()
    {
        string previousScene = SceneManager.GetActiveScene().path;
        GameObject shardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShardPrefabPath);
        GameObject bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);

        foreach (string sceneName in new[] { "City", "Desert", "Dungeon" })
        {
            string path = "Assets/Scenes/Maps/" + sceneName + ".unity";
            if (!File.Exists(path)) { Debug.LogWarning("[Boss Setup] Scene not found: " + path + " (run Lythrum > Build Maps first)."); continue; }

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            RemoveOld(scene, "Memory Shards (auto)", "Skeleton Boss (auto)", "Boss Arena (auto)", "Shard Manager (auto)");

            GameObject manager = new GameObject("Shard Manager (auto)");
            manager.AddComponent<ShardManager>();

            GameObject shardRoot = new GameObject("Memory Shards (auto)");
            foreach (Placement p in Shards.Where(p => p.scene == sceneName))
            {
                GameObject shard = (GameObject)PrefabUtility.InstantiatePrefab(shardPrefab, scene);
                shard.name = "Shard " + p.id;
                shard.transform.SetParent(shardRoot.transform);
                shard.transform.position = TileToWorld(sceneName, p.x, p.y);

                SerializedObject so = new SerializedObject(shard.GetComponent<MemoryShard>());
                so.FindProperty("id").stringValue = p.id;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (sceneName == "Dungeon" && bossPrefab != null) PlaceBoss(scene, bossPrefab);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Boss Setup] " + sceneName + " updated.");
        }

        if (!string.IsNullOrEmpty(previousScene) && File.Exists(previousScene)) EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
    }

    // The boss floats against the north wall of the throne room (tiles 30-65 x 4-19), asleep (inactive) until the player
    // steps on the arena trigger in the room's south doorway.
    static void PlaceBoss(Scene scene, GameObject bossPrefab)
    {
        GameObject boss = (GameObject)PrefabUtility.InstantiatePrefab(bossPrefab, scene);
        boss.name = "Skeleton Boss (auto)";
        boss.transform.position = TileToWorld("Dungeon", 47.5f, 8f);     // centre of the head
        boss.SetActive(false);

        // The whole throne room (tiles 30-65 x 4-19, a bit of margin): no ordinary enemies in here.
        Bounds room = new Bounds(TileToWorld("Dungeon", 47.5f, 11.5f), new Vector3(38f, 18f, 2f));

        // One trigger per entrance of the throne room: south (from the great hall), west (library) and east (shrine).
        SkeletonBoss bossComponent = boss.GetComponent<SkeletonBoss>();
        MakeArenaTrigger("Boss Arena (auto)", TileToWorld("Dungeon", 47.5f, 18.5f), new Vector2(10f, 2f), bossComponent, room);
        MakeArenaTrigger("Boss Arena (auto) west", TileToWorld("Dungeon", 31f, 9.5f), new Vector2(2f, 5f), bossComponent, room);
        MakeArenaTrigger("Boss Arena (auto) east", TileToWorld("Dungeon", 64f, 10.5f), new Vector2(2f, 5f), bossComponent, room);

        // Enemies that the map setup already placed in the room are removed.
        int removed = 0;
        foreach (EnemyBrain enemy in Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include))
        {
            if (!room.Contains(enemy.transform.position)) continue;
            Object.DestroyImmediate(enemy.gameObject);
            removed++;
        }
        Debug.Log("[Boss Setup] Removed " + removed + " enemies from the boss room.");
    }

    static void MakeArenaTrigger(string objectName, Vector3 position, Vector2 size, SkeletonBoss boss, Bounds room)
    {
        GameObject go = new GameObject(objectName);
        go.transform.position = position;
        BoxCollider2D trigger = go.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = size;

        BossArena arena = go.AddComponent<BossArena>();
        SerializedObject so = new SerializedObject(arena);
        so.FindProperty("boss").objectReferenceValue = boss;
        so.FindProperty("clearArea").boundsValue = room;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void RemoveOld(Scene scene, params string[] names)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (names.Any(n => root.name.StartsWith(n))) Object.DestroyImmediate(root);   // StartsWith: also removes copies like "Boss Arena (auto) west"
        }
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
