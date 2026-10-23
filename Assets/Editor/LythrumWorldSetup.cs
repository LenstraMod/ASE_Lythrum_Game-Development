using System;
using System.Collections.Generic;
using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Menu: Lythrum > Setup > ...
// Builds the sound library from Assets/Audio, builds the four enemy prefabs from Assets/Sprites/Enemy,
// and puts camera limits + enemies into the map scenes (Dungeon: slimes + Soultrusts, Desert: bugs + dwarves).
public static class LythrumWorldSetup
{
    const string AudioFolder = "Assets/Audio";
    const string ResourcesFolder = "Assets/Resources";
    const string LibraryPath = ResourcesFolder + "/SfxLibrary.asset";

    const string SpriteFolder = "Assets/Sprites/Enemy";
    const string PrefabFolder = "Assets/Prefabs/Enemies";
    const string MapSceneFolder = "Assets/Scenes/Maps";
    const string EnemyRootName = "Enemies (auto)";

    // ------------------------------------------------------------------ menu

    [MenuItem("Lythrum/Setup/Everything (Audio + Enemies + Map Scenes)")]
    public static void SetupEverything()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        BuildAudio();
        BuildEnemyPrefabs();
        ApplyToAllMapScenes();
    }

    [MenuItem("Lythrum/Setup/Audio (build SFX library)")]
    static void BuildAudioMenu() => BuildAudio();

    [MenuItem("Lythrum/Setup/Enemy Prefabs (rebuild)")]
    static void BuildEnemyPrefabsMenu() => BuildEnemyPrefabs();

    [MenuItem("Lythrum/Setup/Camera Limits + Enemies In All Map Scenes")]
    static void ApplyToAllMapScenesMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ApplyToAllMapScenes();
    }

    [MenuItem("Lythrum/Setup/Camera Limits + Enemies In Open Scene")]
    static void ApplyToOpenSceneMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        ApplyToScene(scene);
        Debug.Log("[World Setup] " + scene.name + " updated. Save the scene.");
    }

    // For command line use: Unity -batchmode -projectPath . -executeMethod LythrumWorldSetup.SetupEverythingBatch -quit
    public static void SetupEverythingBatch()
    {
        BuildAudio();
        BuildEnemyPrefabs();
        ApplyToAllMapScenes();
    }

    static void ApplyToAllMapScenes()
    {
        string previousScene = SceneManager.GetActiveScene().path;

        List<string> done = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { MapSceneFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            ApplyToScene(scene);
            EditorSceneManager.SaveScene(scene);
            done.Add(scene.name);
        }

        if (!string.IsNullOrEmpty(previousScene) && File.Exists(previousScene))
        {
            EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
        }
        Debug.Log("[World Setup] Camera limits + enemies applied to: " + string.Join(", ", done));
    }

    // ------------------------------------------------------------------ audio

    static readonly KeyValuePair<Sfx, string[]>[] SoundClips =
    {
        new KeyValuePair<Sfx, string[]>(Sfx.PlayerFootstep,  new[] { "footstep_concrete_000", "footstep_concrete_001", "footstep_concrete_002", "footstep_concrete_003" }),
        new KeyValuePair<Sfx, string[]>(Sfx.PlayerSlash,     new[] { "pixel_slash_1", "pixel_slash_2", "pixel_slash_3" }),
        new KeyValuePair<Sfx, string[]>(Sfx.PlayerSlashHeavy, new[] { "pixel_slash_heavy" }),
        new KeyValuePair<Sfx, string[]>(Sfx.PlayerAreaSlash, new[] { "pixel_fire_slash" }),
        new KeyValuePair<Sfx, string[]>(Sfx.PlayerHurt,      new[] { "impactSoft_heavy_000", "impactSoft_heavy_001" }),
        new KeyValuePair<Sfx, string[]>(Sfx.PlayerDeath,     new[] { "impactPunch_heavy_001" }),
        new KeyValuePair<Sfx, string[]>(Sfx.HitImpact,       new[] { "pixel_hit_1", "pixel_hit_2", "pixel_hit_3" }),
        new KeyValuePair<Sfx, string[]>(Sfx.HitImpactHeavy,  new[] { "pixel_hit_heavy" }),
        new KeyValuePair<Sfx, string[]>(Sfx.EnemyAttack,     new[] { "impactSoft_medium_001", "impactSoft_medium_000" }),
        new KeyValuePair<Sfx, string[]>(Sfx.EnemyDeath,      new[] { "impactSoft_heavy_002" }),
        new KeyValuePair<Sfx, string[]>(Sfx.TimeStopStart,   new[] { "impactBell_heavy_000" }),
        new KeyValuePair<Sfx, string[]>(Sfx.TimeStopEnd,     new[] { "impactBell_heavy_000" }),
        new KeyValuePair<Sfx, string[]>(Sfx.DoorOpen,        new[] { "doorOpen_1", "doorOpen_2" }),
        new KeyValuePair<Sfx, string[]>(Sfx.DoorClose,       new[] { "doorClose_1" }),
        new KeyValuePair<Sfx, string[]>(Sfx.PlatePress,      new[] { "impactPlate_light_000" }),
        new KeyValuePair<Sfx, string[]>(Sfx.PlateRelease,    new[] { "metalClick" }),
        new KeyValuePair<Sfx, string[]>(Sfx.SpikeHit,        new[] { "impactMetal_heavy_000" }),
        new KeyValuePair<Sfx, string[]>(Sfx.Checkpoint,      new[] { "metalLatch", "handleCoins" }),
        new KeyValuePair<Sfx, string[]>(Sfx.RecordStart,     new[] { "impactGlass_light_000" }),
        new KeyValuePair<Sfx, string[]>(Sfx.ShadowSpawn,     new[] { "impactGlass_medium_001" }),
        new KeyValuePair<Sfx, string[]>(Sfx.ShadowEnd,       new[] { "impactGlass_light_000" }),
        // Boss sounds (Assets/Audio/Enemy). The earthquake rumble is still a placeholder: a heavy impact pitched way down.
        new KeyValuePair<Sfx, string[]>(Sfx.BossSlam,        new[] { "slam-sound" }),
        new KeyValuePair<Sfx, string[]>(Sfx.BossQuake,       new[] { "impactSoft_heavy_002" }),
        new KeyValuePair<Sfx, string[]>(Sfx.BossRoar,        new[] { "Boss_roar" }),
        new KeyValuePair<Sfx, string[]>(Sfx.BossFire,        new[] { "fire_sound" }),
        new KeyValuePair<Sfx, string[]>(Sfx.BossFearCatch,   new[] { "fear_catch_sound" }),
    };

    // volume, pitchMin, pitchMax per sound
    static Vector3 Tuning(Sfx id)
    {
        switch (id)
        {
            case Sfx.PlayerFootstep: return new Vector3(0.45f, 0.9f, 1.1f);
            case Sfx.PlayerSlash: return new Vector3(0.7f, 0.94f, 1.08f);
            case Sfx.PlayerSlashHeavy: return new Vector3(0.8f, 0.95f, 1.05f);
            case Sfx.PlayerAreaSlash: return new Vector3(0.9f, 0.97f, 1.03f);
            case Sfx.PlayerHurt: return new Vector3(0.9f, 0.95f, 1.05f);
            case Sfx.PlayerDeath: return new Vector3(1f, 0.75f, 0.8f);
            case Sfx.HitImpact: return new Vector3(0.75f, 0.94f, 1.08f);
            case Sfx.HitImpactHeavy: return new Vector3(0.9f, 0.95f, 1.05f);
            case Sfx.EnemyAttack: return new Vector3(0.7f, 0.9f, 1.1f);
            case Sfx.EnemyDeath: return new Vector3(0.9f, 0.85f, 1f);
            case Sfx.TimeStopStart: return new Vector3(0.8f, 0.6f, 0.6f);   // same bell, pitched down
            case Sfx.TimeStopEnd: return new Vector3(0.5f, 1.2f, 1.2f);     // ... and pitched up
            case Sfx.DoorOpen: return new Vector3(0.8f, 0.95f, 1.05f);
            case Sfx.DoorClose: return new Vector3(0.8f, 0.95f, 1.05f);
            case Sfx.PlatePress: return new Vector3(0.8f, 0.95f, 1.05f);
            case Sfx.PlateRelease: return new Vector3(0.6f, 0.95f, 1.05f);
            case Sfx.SpikeHit: return new Vector3(0.8f, 0.95f, 1.05f);
            case Sfx.Checkpoint: return new Vector3(0.8f, 1f, 1f);
            case Sfx.RecordStart: return new Vector3(0.7f, 0.7f, 0.7f);
            case Sfx.ShadowSpawn: return new Vector3(0.8f, 0.7f, 0.75f);
            case Sfx.ShadowEnd: return new Vector3(0.6f, 0.5f, 0.5f);
            case Sfx.BossSlam: return new Vector3(1f, 0.97f, 1.03f);     // the real slam sound, a touch of variation
            case Sfx.BossQuake: return new Vector3(1f, 0.25f, 0.3f);     // slowed down into a rumble
            case Sfx.BossRoar: return new Vector3(1f, 1f, 1f);          // the real roar, played as it is
            case Sfx.BossFire: return new Vector3(0.8f, 1f, 1f);
            case Sfx.BossFearCatch: return new Vector3(1f, 1f, 1f);
            default: return new Vector3(1f, 0.95f, 1.05f);
        }
    }

    public static void BuildAudio()
    {
        AssetDatabase.Refresh();
        EnsureFolder(ResourcesFolder);

        SfxLibrary library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(LibraryPath);
        bool isNew = library == null;
        if (isNew) library = ScriptableObject.CreateInstance<SfxLibrary>();

        List<SfxLibrary.Entry> entries = new List<SfxLibrary.Entry>();
        int missing = 0;
        foreach (KeyValuePair<Sfx, string[]> pair in SoundClips)
        {
            List<AudioClip> clips = new List<AudioClip>();
            foreach (string clipName in pair.Value)
            {
                AudioClip clip = FindClip(clipName);
                if (clip == null)
                {
                    Debug.LogWarning("[World Setup] Missing audio clip '" + clipName + "' for " + pair.Key);
                    missing++;
                }
                else clips.Add(clip);
            }

            Vector3 tune = Tuning(pair.Key);
            entries.Add(new SfxLibrary.Entry
            {
                id = pair.Key,
                clips = clips.ToArray(),
                volume = tune.x,
                pitchRange = new Vector2(tune.y, tune.z),
            });
        }
        library.entries = entries.ToArray();

        if (isNew) AssetDatabase.CreateAsset(library, LibraryPath);
        else EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();

        Debug.Log("[World Setup] Sound library ready (" + entries.Count + " sounds" + (missing > 0 ? ", " + missing + " clips missing, see warnings" : "") + "): " + LibraryPath);

        BuildMusic();
        BuildCombatVfx();
    }

    // ------------------------------------------------------------ combat effects

    const string EffectsFolder = "Assets/Sprites/Effects";
    const string VfxLibraryPath = ResourcesFolder + "/CombatVfxLibrary.asset";

    [MenuItem("Lythrum/Setup/Combat Effects (pixel slash + fire)")]
    static void BuildCombatVfxMenu() => BuildCombatVfx();

    static void BuildCombatVfx()
    {
        Texture2D slash = LoadEffectSheet("slash_pixel");
        Texture2D fire = LoadEffectSheet("fire_pixel");
        if (slash == null || fire == null)
        {
            Debug.LogWarning("[World Setup] Effect sheets missing in " + EffectsFolder + " (run Tools/Fx/pack_combat_fx.py). The old arc effect stays.");
            return;
        }

        EnsureFolder(ResourcesFolder);
        CombatVfxLibrary library = AssetDatabase.LoadAssetAtPath<CombatVfxLibrary>(VfxLibraryPath);
        bool isNew = library == null;
        if (isNew) library = ScriptableObject.CreateInstance<CombatVfxLibrary>();

        library.slash = new CombatVfxLibrary.Sheet { texture = slash, cellSize = new Vector2Int(64, 47), frameCount = 9, pixelsPerUnit = 16f };
        library.fire = new CombatVfxLibrary.Sheet { texture = fire, cellSize = new Vector2Int(8, 10), frameCount = 6, pixelsPerUnit = 16f };

        if (isNew) AssetDatabase.CreateAsset(library, VfxLibraryPath);
        else EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        Debug.Log("[World Setup] Combat effects ready: " + VfxLibraryPath);
    }

    // Pixel art: no smoothing, no compression.
    static Texture2D LoadEffectSheet(string fileName)
    {
        string path = EffectsFolder + "/" + fileName + ".png";
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;

        if (importer.filterMode != FilterMode.Point || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.mipmapEnabled)
        {
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ------------------------------------------------------------------ music

    const string MusicFolder = "Assets/Audio/Music";
    const string MusicLibraryPath = ResourcesFolder + "/MusicLibrary.asset";

    // Scene name -> layers (file name without extension, volume). Dungeon = a sparse horror drone plus wind and dripping.
    static readonly KeyValuePair<string, KeyValuePair<string, float>[]>[] SceneMusic =
    {
        new KeyValuePair<string, KeyValuePair<string, float>[]>("Desert", new[] { new KeyValuePair<string, float>("desert_loop", 0.5f) }),
        new KeyValuePair<string, KeyValuePair<string, float>[]>("City", new[] { new KeyValuePair<string, float>("market_day_loop", 0.45f) }),
        // Not a scene: the boss fight song, switched on by the boss (MusicManager.PlayOverride("BossFight")). Lives in Assets/Audio/Enemy.
        new KeyValuePair<string, KeyValuePair<string, float>[]>("BossFight", new[] { new KeyValuePair<string, float>("Boss_Fight_Song", 0.7f) }),
        new KeyValuePair<string, KeyValuePair<string, float>[]>("Dungeon", new[]
        {
            new KeyValuePair<string, float>("horror_atmosphere", 0.55f),
            new KeyValuePair<string, float>("dungeon_ambience", 0.6f),
        }),
    };

    static void BuildMusic()
    {
        if (!AssetDatabase.IsValidFolder(MusicFolder)) return;

        MusicLibrary library = AssetDatabase.LoadAssetAtPath<MusicLibrary>(MusicLibraryPath);
        bool isNew = library == null;
        if (isNew) library = ScriptableObject.CreateInstance<MusicLibrary>();

        List<MusicLibrary.SceneMusic> scenes = new List<MusicLibrary.SceneMusic>();
        foreach (KeyValuePair<string, KeyValuePair<string, float>[]> scene in SceneMusic)
        {
            List<MusicLibrary.Layer> layers = new List<MusicLibrary.Layer>();
            foreach (KeyValuePair<string, float> layer in scene.Value)
            {
                string path = FindMusicFile(layer.Key);
                if (path == null)
                {
                    Debug.LogWarning("[World Setup] Missing music file '" + layer.Key + "' for " + scene.Key);
                    continue;
                }
                MakeStreaming(path);
                layers.Add(new MusicLibrary.Layer { clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path), volume = layer.Value });
            }
            scenes.Add(new MusicLibrary.SceneMusic { sceneName = scene.Key, layers = layers.ToArray(), fadeSeconds = scene.Key == "Dungeon" ? 3f : (scene.Key == "BossFight" ? 1.5f : 2f) });
        }
        library.scenes = scenes.ToArray();

        if (isNew) AssetDatabase.CreateAsset(library, MusicLibraryPath);
        else EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        Debug.Log("[World Setup] Music library ready: " + MusicLibraryPath);
    }

    static string FindMusicFile(string fileName)
    {
        foreach (string guid in AssetDatabase.FindAssets(fileName + " t:AudioClip", new[] { AudioFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == fileName) return path;
        }
        return null;
    }

    // Long loops are streamed from disk instead of being kept decompressed in memory.
    static void MakeStreaming(string path)
    {
        AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer == null) return;

        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        if (settings.loadType == AudioClipLoadType.Streaming && importer.loadInBackground) return;

        settings.loadType = AudioClipLoadType.Streaming;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = 0.6f;
        importer.defaultSampleSettings = settings;
        importer.loadInBackground = true;
        importer.SaveAndReimport();
    }

    static AudioClip FindClip(string clipName)
    {
        foreach (string guid in AssetDatabase.FindAssets(clipName + " t:AudioClip", new[] { AudioFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == clipName) return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        foreach (string file in Directory.GetFiles(AudioFolder, clipName + ".*", SearchOption.AllDirectories))
        {
            if (file.EndsWith(".meta")) continue;
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(file.Replace('\\', '/'));
            if (clip != null) return clip;
        }
        return null;
    }

    // ---------------------------------------------------------------- enemies

    class ClipSpec
    {
        public string name, file;
        public int cellW, cellH, frames;
        public float fps;
        public bool loop;
    }

    class EnemySpec
    {
        public string name;
        public float scale;
        public Vector2 colliderSize, colliderOffset;
        public int health;
        public ClipSpec[] clips;
        public Action<EnemyBrain> tune;
    }

    static ClipSpec C(string name, string file, int cellW, int cellH, int frames, float fps, bool loop)
    {
        return new ClipSpec { name = name, file = file, cellW = cellW, cellH = cellH, frames = frames, fps = fps, loop = loop };
    }

    // Frame counts and grid sizes come from looking at the sheets: slime/bug are one row of 64px cells,
    // the dwarf is a 2x2 grid, the Soultrust a 3x2 grid (unused cells are empty).
    static EnemySpec[] BuildSpecs()
    {
        return new[]
        {
            new EnemySpec
            {
                name = "Slime", scale = 1.3f, health = 30,
                colliderSize = new Vector2(0.55f, 0.45f), colliderOffset = new Vector2(0f, -0.1f),
                clips = new[]
                {
                    C("Idle", "slime_idle", 64, 64, 4, 6f, true),
                    C("Walk", "slime_walk", 64, 64, 4, 8f, true),
                    C("Attack", "slime_attack", 64, 64, 8, 10f, false),
                    C("Death", "slime_death", 64, 64, 8, 10f, false),
                },
                tune = b =>
                {
                    b.detectionRadius = 5f; b.wanderSpeed = 0.6f; b.chaseSpeed = 2f;
                    b.attackRange = 0.85f; b.hitRange = 1.1f; b.windupTime = 0.5f; b.recoverTime = 1f;
                    b.lungeSpeed = 4f; b.healthBarWidth = 0.7f;
                },
            },
            new EnemySpec
            {
                name = "Soultrust", scale = 0.6f, health = 70,
                colliderSize = new Vector2(0.45f, 0.8f), colliderOffset = new Vector2(0f, -0.05f),
                clips = new[]
                {
                    C("Idle", "Soultrust(Idle)", 128, 182, 5, 6f, true),
                    C("Walk", "Soultrust(Walking)", 128, 182, 5, 7f, true),
                    C("Attack", "Soultrust(Attack)", 128, 182, 3, 8f, false),
                    C("Death", "Soultrust(Death)", 128, 182, 4, 6f, false),
                },
                tune = b =>
                {
                    b.detectionRadius = 6f; b.wanderSpeed = 0.6f; b.chaseSpeed = 1.6f;
                    b.attackRange = 1.1f; b.hitRange = 1.4f; b.windupTime = 0.7f; b.recoverTime = 1.3f;
                    b.lungeSpeed = 3f; b.interruptWindup = false; b.healthBarWidth = 0.9f;
                },
            },
            new EnemySpec
            {
                name = "Bug", scale = 1.3f, health = 20,
                colliderSize = new Vector2(0.55f, 0.4f), colliderOffset = new Vector2(0f, -0.05f),
                clips = new[]
                {
                    C("Idle", "bug_idle", 64, 64, 4, 8f, true),
                    C("Walk", "bug_idle", 64, 64, 4, 14f, true), // no walk sheet: idle sped up
                    C("Attack", "bug_attackOnly", 64, 64, 4, 10f, false),
                    C("Death", "bug_death", 64, 64, 4, 8f, false),
                },
                tune = b =>
                {
                    b.detectionRadius = 6f; b.wanderSpeed = 1.2f; b.chaseSpeed = 3.4f;
                    b.attackRange = 0.8f; b.hitRange = 1f; b.windupTime = 0.3f; b.recoverTime = 0.7f;
                    b.lungeSpeed = 6f; b.lungeTime = 0.15f; b.healthBarWidth = 0.7f;
                },
            },
            new EnemySpec
            {
                name = "Dwarf", scale = 1.3f, health = 50,
                colliderSize = new Vector2(0.5f, 0.7f), colliderOffset = new Vector2(0f, -0.03f),
                clips = new[]
                {
                    C("Idle", "Dwarf(Idle)", 74, 74, 4, 5f, true),
                    C("Walk", "Dwarf(walking)", 74, 74, 4, 7f, true),
                    C("Attack", "Dwarf(Attack)", 94, 94, 3, 8f, false),
                    C("Death", "Dwarf(Death)", 74, 74, 4, 6f, false),
                },
                tune = b =>
                {
                    b.detectionRadius = 5.5f; b.wanderSpeed = 0.7f; b.chaseSpeed = 2f;
                    b.attackRange = 1f; b.hitRange = 1.3f; b.windupTime = 0.6f; b.recoverTime = 1.1f;
                    b.lungeSpeed = 3.5f; b.healthBarWidth = 0.8f;
                },
            },
        };
    }

    static void BuildEnemyPrefabs()
    {
        EnsureFolder(PrefabFolder);
        foreach (EnemySpec spec in BuildSpecs()) BuildPrefab(spec);
        AssetDatabase.SaveAssets();
        Debug.Log("[World Setup] Enemy prefabs built in " + PrefabFolder);
    }

    static string PrefabPath(EnemySpec spec) => PrefabFolder + "/" + spec.name + ".prefab";

    static GameObject BuildPrefab(EnemySpec spec)
    {
        GameObject root = new GameObject(spec.name);
        root.tag = "Enemy";

        Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.linearDamping = 8f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        BoxCollider2D col = root.AddComponent<BoxCollider2D>();
        col.size = spec.colliderSize;
        col.offset = spec.colliderOffset;

        Health health = root.AddComponent<Health>();
        health.maxHealth = spec.health;
        health.destroyOnDeath = false; // EnemyBrain plays the death animation, then removes the enemy

        TimeStoppable stoppable = root.AddComponent<TimeStoppable>();

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = new Vector3(spec.scale, spec.scale, 1f);

        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        int playerLayer = SortingLayer.NameToID("Player");
        if (SortingLayer.IsValid(playerLayer)) sr.sortingLayerID = playerLayer;

        SpriteSheetAnimator anim = visual.AddComponent<SpriteSheetAnimator>();
        anim.target = sr;
        anim.pixelsPerUnit = 100f;
        List<SpriteSheetAnimator.Clip> clips = new List<SpriteSheetAnimator.Clip>();
        foreach (ClipSpec c in spec.clips)
        {
            clips.Add(new SpriteSheetAnimator.Clip
            {
                name = c.name,
                sheet = LoadSheet(c.file),
                cellSize = new Vector2Int(c.cellW, c.cellH),
                frameCount = c.frames,
                fps = c.fps,
                loop = c.loop,
            });
        }
        anim.clips = clips.ToArray();

        EnemyBrain brain = root.AddComponent<EnemyBrain>();
        brain.body = sr;
        brain.animator = anim;
        spec.tune?.Invoke(brain);

        stoppable.disableWhileFrozen = new Behaviour[] { anim };

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(spec));
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    // Pixel art needs Point filtering and no compression, otherwise it looks blurry next to the player.
    static Texture2D LoadSheet(string file)
    {
        string path = SpriteFolder + "/" + file + ".png";
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && (importer.filterMode != FilterMode.Point || importer.textureCompression != TextureImporterCompression.Uncompressed))
        {
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null) Debug.LogWarning("[World Setup] Missing sprite sheet: " + path);
        return tex;
    }

    // ----------------------------------------------------------------- scenes

    struct Spawn
    {
        public string enemy;
        public int count;
        public Spawn(string enemy, int count) { this.enemy = enemy; this.count = count; }
    }

    static Spawn[] SpawnsFor(string sceneName)
    {
        switch (sceneName)
        {
            case "Dungeon": return new[] { new Spawn("Slime", 14), new Spawn("Soultrust", 7) };   // big and dark: plenty lurking
            case "City": return new[] { new Spawn("Slime", 16) };   // only in the meadow: the City map defines enemy zones
            case "Desert": return new[] { new Spawn("Bug", 14), new Spawn("Dwarf", 7) };   // big map: more to fight
            default: return new Spawn[0];
        }
    }

    // Camera limits for every scene, enemies for the scenes listed in SpawnsFor.
    // Also called by LythrumMapBuilder so rebuilt maps keep both.
    public static void ApplyToScene(Scene scene)
    {
        // The map scenes were built before PlayerHealth existed, so their player can't take damage
        // (enemy hits, spikes) and the heart bar has nothing to show.
        PlayerMovement player = FindInScene<PlayerMovement>(scene);
        if (player != null && player.GetComponent<PlayerHealth>() == null)
        {
            Undo.AddComponent<PlayerHealth>(player.gameObject);
            Debug.Log("[World Setup] " + scene.name + ": added PlayerHealth to the player.");
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (CinemachineCamera cam in root.GetComponentsInChildren<CinemachineCamera>(true))
            {
                if (cam.GetComponent<CameraMapBounds>() == null) Undo.AddComponent<CameraMapBounds>(cam.gameObject);
            }
        }

        PlaceEnemies(scene);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    static void PlaceEnemies(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == EnemyRootName)
            {
                UnityEngine.Object.DestroyImmediate(root);
                break;
            }
        }

        Spawn[] spawns = SpawnsFor(scene.name);
        if (spawns.Length == 0) return;

        PlayerMovement player = FindInScene<PlayerMovement>(scene);
        Tilemap ground = FindTilemap(scene, "Ground");
        if (player == null || ground == null)
        {
            Debug.LogWarning("[World Setup] " + scene.name + ": no player or Ground tilemap found, enemies skipped.");
            return;
        }

        Physics2D.SyncTransforms();
        List<Vector3> spots = FindReachableSpots(ground, player, scene.name);

        // Maps with enemy zones (the City) only get enemies inside them.
        List<Bounds> zones = new List<Bounds>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (EnemyZone zone in root.GetComponentsInChildren<EnemyZone>(true)) zones.Add(zone.WorldBounds);
        }
        if (zones.Count > 0) spots = spots.FindAll(spot => zones.Exists(z => z.Contains(spot)));

        // ... and never inside a boss room (see BossArena.clearArea).
        List<Bounds> bossRooms = new List<Bounds>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (BossArena arena in root.GetComponentsInChildren<BossArena>(true)) bossRooms.Add(arena.ClearArea);
        }
        if (bossRooms.Count > 0) spots = spots.FindAll(spot => !bossRooms.Exists(b => b.Contains(spot)));

        GameObject container = new GameObject(EnemyRootName);
        SceneManager.MoveGameObjectToScene(container, scene);

        Material litMaterial = FindTileMaterial(scene);
        EnemySpec[] specs = BuildSpecs();
        List<Vector3> used = new List<Vector3>();
        int placed = 0;

        foreach (Spawn spawn in spawns)
        {
            EnemySpec spec = Array.Find(specs, s => s.name == spawn.enemy);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(spec));
            if (prefab == null) prefab = BuildPrefab(spec);

            int made = 0;
            foreach (Vector3 spot in spots)
            {
                if (made >= spawn.count) break;
                if (Vector3.Distance(spot, player.transform.position) < 8f) continue;
                if (used.Exists(u => Vector3.Distance(u, spot) < 4f)) continue;

                GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                enemy.transform.SetParent(container.transform);
                enemy.transform.position = spot;
                // Same lit material as the map, so enemies in a dark scene are only seen where the light reaches.
                if (litMaterial != null)
                {
                    foreach (SpriteRenderer sr in enemy.GetComponentsInChildren<SpriteRenderer>(true)) sr.sharedMaterial = litMaterial;
                }
                used.Add(spot);
                made++;
                placed++;
            }
            if (made < spawn.count) Debug.LogWarning("[World Setup] " + scene.name + ": only found room for " + made + "/" + spawn.count + " " + spawn.enemy + ".");
        }
        Debug.Log("[World Setup] " + scene.name + ": placed " + placed + " enemies.");
    }

    // Flood-fills outward from the player over Ground tiles that have nothing solid on them,
    // so enemies only appear where the player can actually walk. Result is shuffled (same order every run).
    static List<Vector3> FindReachableSpots(Tilemap ground, PlayerMovement player, string seedText)
    {
        Vector3Int start = ground.WorldToCell(player.transform.position);
        BoundsInt bounds = ground.cellBounds;

        HashSet<Vector3Int> seen = new HashSet<Vector3Int> { start };
        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        queue.Enqueue(start);
        List<Vector3> spots = new List<Vector3>();
        Vector3Int[] steps = { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down };

        while (queue.Count > 0)
        {
            Vector3Int cell = queue.Dequeue();
            spots.Add(ground.GetCellCenterWorld(cell));

            foreach (Vector3Int step in steps)
            {
                Vector3Int next = cell + step;
                if (!bounds.Contains(next) || !seen.Add(next)) continue;
                if (!ground.HasTile(next)) continue;
                if (!IsCellFree(ground.GetCellCenterWorld(next), player.gameObject)) continue;
                queue.Enqueue(next);
            }
        }

        int seed = 17;
        foreach (char ch in seedText) seed = seed * 31 + ch;
        System.Random random = new System.Random(seed);
        for (int i = spots.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            Vector3 tmp = spots[i];
            spots[i] = spots[j];
            spots[j] = tmp;
        }
        return spots;
    }

    static bool IsCellFree(Vector3 center, GameObject player)
    {
        foreach (Collider2D col in Physics2D.OverlapBoxAll(center, new Vector2(0.8f, 0.8f), 0f))
        {
            if (col.isTrigger || col.transform.IsChildOf(player.transform)) continue;
            return false;
        }
        return true;
    }

    static Material FindTileMaterial(Scene scene)
    {
        Tilemap ground = FindTilemap(scene, "Ground");
        TilemapRenderer renderer = ground != null ? ground.GetComponent<TilemapRenderer>() : null;
        return renderer != null ? renderer.sharedMaterial : null;
    }

    static Tilemap FindTilemap(Scene scene, string tilemapName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Tilemap map in root.GetComponentsInChildren<Tilemap>(true))
            {
                if (map.name == tilemapName) return map;
            }
        }
        return null;
    }

    static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
