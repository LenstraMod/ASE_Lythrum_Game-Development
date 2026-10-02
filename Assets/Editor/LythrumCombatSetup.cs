using System.Collections.Generic;
using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Menu: Lythrum > Combat > ...
// Wires the combat scripts onto the Player (one scene or all scenes) and creates a training dummy prefab.
public static class LythrumCombatSetup
{
    const string MaterialFolder = "Assets/Materials";
    const string VfxMaterialPath = MaterialFolder + "/CombatVFX.mat";
    const string PrefabFolder = "Assets/Prefabs/Combat";
    const string DummyPrefabPath = PrefabFolder + "/TrainingDummy.prefab";

    const string SettingInputScene = "Assets/Scenes/SettingInput.unity";
    const string MapSceneFolder = "Assets/Scenes/Maps";

    [MenuItem("Lythrum/Combat/Setup Player In Open Scene")]
    static void SetupPlayer()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!ApplyToScene(scene))
        {
            EditorUtility.DisplayDialog("Combat Setup", "No object with PlayerMovement found in the open scene.", "OK");
            return;
        }
        Debug.Log("[Combat Setup] " + scene.name + ": player ready (PlayerCombat, TimeStopAbility, screen shake, post-processing). Save the scene.");
    }

    // Opens SettingInput and every scene in Assets/Scenes/Maps, gives the player combat,
    // drops a training dummy next to the spawn (if the scene has none) and saves.
    [MenuItem("Lythrum/Combat/Setup All Scenes (SettingInput + Maps)")]
    static void SetupAllScenes()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string previousScene = SceneManager.GetActiveScene().path;

        List<string> paths = new List<string>();
        if (File.Exists(SettingInputScene)) paths.Add(SettingInputScene);
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { MapSceneFolder }))
        {
            paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        }

        List<string> done = new List<string>();
        foreach (string path in paths)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (!ApplyToScene(scene))
            {
                Debug.LogWarning("[Combat Setup] " + path + ": no PlayerMovement found, skipped.");
                continue;
            }
            AddDummyIfMissing(scene);
            EditorSceneManager.SaveScene(scene);
            done.Add(scene.name);
        }

        if (!string.IsNullOrEmpty(previousScene) && File.Exists(previousScene))
        {
            EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
        }
        Debug.Log("[Combat Setup] Combat ready in: " + string.Join(", ", done));
    }

    // Gives the player in "scene" combat. Also used by LythrumMapBuilder so rebuilt maps keep combat.
    // Returns false if the scene has no player.
    public static bool ApplyToScene(Scene scene)
    {
        PlayerMovement player = FindInScene<PlayerMovement>(scene);
        if (player == null) return false;

        GameObject go = player.gameObject;
        Material vfx = GetOrCreateVfxMaterial();

        CinemachineImpulseSource impulse = go.GetComponent<CinemachineImpulseSource>();
        if (impulse == null) impulse = Undo.AddComponent<CinemachineImpulseSource>(go);

        PlayerCombat combat = go.GetComponent<PlayerCombat>();
        if (combat == null) combat = Undo.AddComponent<PlayerCombat>(go);
        Undo.RecordObject(combat, "Setup PlayerCombat");
        combat.impulseSource = impulse;
        combat.effectMaterial = vfx;

        TimeStopAbility timeStop = go.GetComponent<TimeStopAbility>();
        if (timeStop == null) timeStop = Undo.AddComponent<TimeStopAbility>(go);
        Undo.RecordObject(timeStop, "Setup TimeStopAbility");
        timeStop.effectMaterial = vfx;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            // Screen shake needs a listener on the Cinemachine camera(s).
            foreach (CinemachineCamera cam in root.GetComponentsInChildren<CinemachineCamera>(true))
            {
                if (cam.GetComponent<CinemachineImpulseListener>() == null) Undo.AddComponent<CinemachineImpulseListener>(cam.gameObject);
            }

            // Time Stop's grey screen is post-processing.
            foreach (Camera cam in root.GetComponentsInChildren<Camera>(true))
            {
                UniversalAdditionalCameraData camData = cam.GetUniversalAdditionalCameraData();
                Undo.RecordObject(camData, "Enable Post Processing");
                camData.renderPostProcessing = true;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        return true;
    }

    [MenuItem("Lythrum/Combat/Add Training Dummy")]
    static void AddTrainingDummy()
    {
        Scene scene = SceneManager.GetActiveScene();
        Vector3 pos = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;

        PlayerMovement player = FindInScene<PlayerMovement>(scene);
        if (player != null) pos = FindFreeSpotNear(player.transform.position, player.gameObject);

        GameObject instance = SpawnDummy(scene, pos);
        Undo.RegisterCreatedObjectUndo(instance, "Add Training Dummy");
        Selection.activeGameObject = instance;
    }

    public static void AddDummyIfMissing(Scene scene)
    {
        if (FindInScene<CombatDummy>(scene) != null) return;

        PlayerMovement player = FindInScene<PlayerMovement>(scene);
        SpawnDummy(scene, FindFreeSpotNear(player.transform.position, player.gameObject));
    }

    static GameObject SpawnDummy(Scene scene, Vector3 position)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(GetOrCreateDummyPrefab(), scene);
        position.z = 0f;
        instance.transform.position = position;
        EditorSceneManager.MarkSceneDirty(scene);
        return instance;
    }

    // Looks for an empty spot around the player so the dummy doesn't spawn inside a wall.
    // The spot also needs some room left and right for the dummy to walk.
    static Vector3 FindFreeSpotNear(Vector3 playerPos, GameObject player)
    {
        Physics2D.SyncTransforms();

        Vector2[] offsets =
        {
            new Vector2(3f, 0f), new Vector2(-3f, 0f), new Vector2(0f, -3f), new Vector2(0f, 3f),
            new Vector2(3f, -3f), new Vector2(-3f, -3f), new Vector2(3f, 3f), new Vector2(-3f, 3f),
            new Vector2(5f, 0f), new Vector2(-5f, 0f), new Vector2(0f, -5f), new Vector2(0f, 5f),
        };
        Vector2[] areas = { new Vector2(3f, 1.2f), new Vector2(1.2f, 1.2f) }; // with walking room first, then just the body

        foreach (Vector2 area in areas)
        {
            foreach (Vector2 offset in offsets)
            {
                Vector2 candidate = (Vector2)playerPos + offset;
                if (IsAreaFree(candidate, area, player)) return candidate;
            }
        }

        Debug.LogWarning("[Combat Setup] Couldn't find a free spot near the player, dummy placed to the right. Move it if it's inside a wall.");
        return playerPos + new Vector3(3f, 0f, 0f);
    }

    static bool IsAreaFree(Vector2 center, Vector2 size, GameObject player)
    {
        foreach (Collider2D col in Physics2D.OverlapBoxAll(center, size, 0f))
        {
            if (col.isTrigger || col.transform.IsChildOf(player.transform)) continue;
            return false;
        }
        return true;
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

    static GameObject GetOrCreateDummyPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefabPath);
        if (existing != null) return existing;

        EnsureFolder(PrefabFolder);

        GameObject go = new GameObject("TrainingDummy");

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.linearDamping = 8f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.8f, 1f);

        Health health = go.AddComponent<Health>();
        health.maxHealth = 60;
        health.destroyOnDeath = false;

        go.AddComponent<TimeStoppable>();
        go.AddComponent<CombatDummy>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, DummyPrefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    static Material GetOrCreateVfxMaterial()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(VfxMaterialPath);
        if (mat != null) return mat;

        EnsureFolder(MaterialFolder);
        mat = new Material(Shader.Find("Sprites/Default")) { name = "CombatVFX" };
        AssetDatabase.CreateAsset(mat, VfxMaterialPath);
        AssetDatabase.SaveAssets();
        return mat;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
