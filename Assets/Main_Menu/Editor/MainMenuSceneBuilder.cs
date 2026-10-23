using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Membangun ulang scene Main Menu (background parallax + UI) dari layer hasil Tools~/split_layers.py.
// Aman dijalankan berulang kali: root lama dihapus lalu dibuat lagi.
public static class MainMenuSceneBuilder
{
    const string Root = "Assets/Main_Menu";
    const string ScenePath = Root + "/MainMenu.unity";
    // Scene pertama saat New Game. Harus berisi Player dengan PlayerHealth agar save & pause berfungsi.
    const string NewGameScenePath = "Assets/Scenes/SettingInput.unity";
    const string LayerFolder = Root + "/Assets_MainMenu/Layers";
    const string FontPath = Root + "/Fonts/Cinzel.ttf";
    const string FontAssetPath = Root + "/Fonts/Cinzel SDF.asset";
    const string ShadowMaterialPath = Root + "/Fonts/Cinzel SDF - Shadow.mat";
    const string UnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
    const string PaperSheetPath = Root + "/Assets_MainMenu/Paper_UI_SpriteSheet.png";
    const string PaperSlicesPath = Root + "/Assets_MainMenu/Paper_UI_SpriteSheet.slices.json";
    const float PixelsPerUnit = 100f;
    const int SkyTextureWidth = 64;

    static readonly string[] BuiltRoots = { "Parallax", "MainMenuCanvas", "MainMenu", "EventSystem" };
    static readonly Color TextColor = new Color(1f, 0.95f, 0.88f);
    static readonly Color HoverColor = new Color(1f, 0.8f, 0.5f);
    static readonly Color PanelColor = new Color(0.16f, 0.06f, 0.12f, 0.85f);
    // Warna tinta untuk teks di atas panel kertas.
    internal static readonly Color InkColor = new Color(0.30f, 0.19f, 0.13f);
    internal static readonly Color InkAccent = new Color(0.66f, 0.25f, 0.15f);

    [Serializable]
    class PaperSheetMeta
    {
        public int width;
        public int height;
        public PaperSpriteMeta[] sprites;
    }

    [Serializable]
    class PaperSpriteMeta
    {
        public string name;
        public int[] rect;     // x, y, w, h (origin kiri-atas)
        public int[] border;   // L, B, R, T
    }

    [Serializable]
    class LayerMeta
    {
        public int width;
        public int height;
        public int[] sky;
    }

    // Satu awan di dalam lapis kedalaman. Posisi dalam unit lokal art (origin tengah, Y ke atas).
    struct CloudPlacement
    {
        public string sprite;
        public Vector2 position;
        public float scale;
        public bool flip;

        public CloudPlacement(string sprite, float x, float y, float scale, bool flip = false)
        {
            this.sprite = sprite;
            position = new Vector2(x, y);
            this.scale = scale;
            this.flip = flip;
        }
    }

    // Lapis awan: makin dekat makin besar, makin cepat bergulir, dan makin kuat bergeser mengikuti mouse.
    class CloudDepth
    {
        public string name;
        public int order;
        public float alpha;
        public float speed;
        public Vector2 mouseStrength;
        public Vector2 driftAmplitude;
        public float phase;
        public CloudPlacement[] clouds;
    }

    // Awan disebar dari langit atas sampai dekat cakrawala di tengah layar (y = 0 adalah tengah layar, tepi atas art = 10.8).
    // Cloud_02/05 terpotong di bagian atas sumbernya, jadi tepi atasnya ditempel ke tepi atas art.
    // Cloud_01/06 terpotong di sisi kiri/kanan dan potongannya terlihat saat bergulir, jadi tidak dipakai.
    static readonly CloudDepth[] CloudDepths =
    {
        new CloudDepth
        {
            name = "Clouds_Far", order = 10, alpha = 0.6f, speed = 0.12f,
            mouseStrength = new Vector2(0.05f, 0.02f), driftAmplitude = new Vector2(0.03f, 0.015f), phase = 1.4f,
            clouds = new[]
            {
                new CloudPlacement("L1_Cloud_07", -15f, 1.6f, 0.5f, true),
                new CloudPlacement("L1_Cloud_03", -10f, 0.2f, 0.8f, true),
                new CloudPlacement("L1_Cloud_04", -3f, 0.8f, 0.45f),
                new CloudPlacement("L1_Cloud_03", 4f, 2.2f, 1f),
                new CloudPlacement("L1_Cloud_07", 9f, 1.2f, 0.55f),
                new CloudPlacement("L1_Cloud_04", 17f, 0.4f, 0.45f, true),
            },
        },
        new CloudDepth
        {
            name = "Clouds_Mid", order = 11, alpha = 0.85f, speed = 0.25f,
            mouseStrength = new Vector2(0.11f, 0.05f), driftAmplitude = new Vector2(0.05f, 0.025f), phase = 2.1f,
            clouds = new[]
            {
                new CloudPlacement("L1_Cloud_04", -14f, 5.8f, 0.9f),
                new CloudPlacement("L1_Cloud_07", -6f, 3.4f, 0.75f),
                new CloudPlacement("L1_Cloud_07", 0f, 6.4f, 1f, true),
                new CloudPlacement("L1_Cloud_04", 13f, 4.6f, 0.8f, true),
            },
        },
        new CloudDepth
        {
            name = "Clouds_Near", order = 12, alpha = 1f, speed = 0.4f,
            mouseStrength = new Vector2(0.17f, 0.07f), driftAmplitude = new Vector2(0.07f, 0.035f), phase = 2.9f,
            clouds = new[]
            {
                new CloudPlacement("L1_Cloud_02", -9f, 9.475f, 1f),
                new CloudPlacement("L1_Cloud_05", 10f, 9.72f, 1f, true),
            },
        },
    };

    [MenuItem("Lythrum/Build Main Menu")]
    public static void Build()
    {
        if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
        {
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            Debug.Log("[MainMenu] TMP Essential Resources sedang di-import. Jalankan Lythrum/Build Main Menu sekali lagi setelah selesai.");
            return;
        }

        LayerMeta meta = JsonUtility.FromJson<LayerMeta>(File.ReadAllText(LayerFolder + "/layers.json"));
        ConfigureLayerImports(meta);
        ConfigurePaperSheet();
        TMP_FontAsset font = GetOrCreateFontAsset();
        Material shadow = GetOrCreateShadowMaterial(font);

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (GameObject root in scene.GetRootGameObjects())
            if (Array.IndexOf(BuiltRoots, root.name) >= 0)
                Object.DestroyImmediate(root);

        Camera cam = SetupCamera(meta);
        BuildParallax(meta, cam);
        BuildUI(font, shadow);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AddToBuildSettings();
        Debug.Log("[MainMenu] Scene Main Menu selesai dibangun: " + ScenePath);
    }

    // ---------- Assets ----------

    static void ConfigureLayerImports(LayerMeta meta)
    {
        foreach (string file in Directory.GetFiles(LayerFolder, "*.png"))
        {
            string path = file.Replace('\\', '/');
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            bool isSky = Path.GetFileNameWithoutExtension(path) == "L0_Sky";

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            // Sky hanya tekstur kecil warna flat, di-stretch ke ukuran art yang sama.
            importer.spritePixelsPerUnit = isSky ? SkyTextureWidth / (meta.width / PixelsPerUnit) : PixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
    }

    // Potong Paper_UI_SpriteSheet menjadi sprite bernama (hasil Tools~/slice_paper_ui.py), lengkap dengan border 9-slice.
    internal static void ConfigurePaperSheet()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(PaperSheetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 8192;   // sheet 4560 px; 4096 akan memperkecil dan membuat sprite buram
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();

        var sheet = JsonUtility.FromJson<PaperSheetMeta>(File.ReadAllText(PaperSlicesPath));
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        // Pertahankan ID sprite lama berdasarkan nama supaya referensi di scene/prefab tidak putus saat dipotong ulang.
        var existingIds = provider.GetSpriteRects()
            .GroupBy(r => r.name)
            .ToDictionary(g => g.Key, g => g.First().spriteID);

        var rects = sheet.sprites.Select(s => new SpriteRect
        {
            name = s.name,
            rect = new Rect(s.rect[0], sheet.height - s.rect[1] - s.rect[3], s.rect[2], s.rect[3]),
            border = new Vector4(s.border[0], s.border[1], s.border[2], s.border[3]),
            alignment = SpriteAlignment.Center,
            pivot = new Vector2(0.5f, 0.5f),
            spriteID = existingIds.TryGetValue(s.name, out GUID id) ? id : GUID.Generate(),
        }).ToArray();

        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>()
            .SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
    }

    internal static Sprite PaperSprite(string name)
    {
        return AssetDatabase.LoadAllAssetsAtPath(PaperSheetPath).OfType<Sprite>().First(s => s.name == name);
    }

    // Panel kertas (9-slice) dengan pita banner berisi judul di tepi atasnya.
    internal static RectTransform CreatePaperPanel(Transform parent, string name, Vector2 size, string title, TMP_FontAsset font)
    {
        var center = new Vector2(0.5f, 0.5f);
        var rect = CreateRect(parent, name);
        Place(rect, center, center, Vector2.zero, size);
        var paper = rect.gameObject.AddComponent<Image>();
        paper.sprite = PaperSprite("Paper_Large");
        paper.type = Image.Type.Sliced;

        var top = new Vector2(0.5f, 1f);
        var banner = CreateRect(rect, "Banner");
        Place(banner, top, top, new Vector2(0f, 50f), new Vector2(480f, 120f));
        var bannerImage = banner.gameObject.AddComponent<Image>();
        bannerImage.sprite = PaperSprite("Banner_Arc");
        bannerImage.preserveAspect = true;
        bannerImage.raycastTarget = false;

        // Teks diletakkan di bagian pita yang melengkung (sekitar seperempat atas gambar banner).
        var header = CreateText(banner, "Header", title, font, null, 40f, TextAlignmentOptions.Center);
        Place(header.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 29f), new Vector2(-140f, -64f));
        header.color = InkColor;
        // Radius busur teks; lebih kecil dari lengkung pita (~550) supaya lekukannya lebih terasa.
        SetField(header.gameObject.AddComponent<ArcText>(), "radius", 380f);
        return rect;
    }

    // Tombol teks bergaya tinta untuk dipakai di atas kertas.
    internal static void ApplyInkStyle(Button button)
    {
        button.GetComponent<TMP_Text>().color = Color.white;
        var colors = button.colors;
        colors.normalColor = InkColor;
        colors.highlightedColor = InkAccent;
        colors.selectedColor = InkAccent;
        colors.pressedColor = InkAccent * 0.75f;
        colors.disabledColor = new Color(InkColor.r, InkColor.g, InkColor.b, 0.4f);
        button.colors = colors;
    }

    internal static TMP_FontAsset GetOrCreateFontAsset()
    {
        var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (asset == null)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
            asset.name = "Cinzel SDF";
            AssetDatabase.CreateAsset(asset, FontAssetPath);

            asset.atlasTextures[0].name = "Cinzel SDF Atlas";
            asset.material.name = "Cinzel SDF Material";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
        }

        // Semua karakter yang dipakai menu (termasuk info slot save) dimasukkan ke atlas sejak awal.
        asset.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789%:.,-!?'|/() ");
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    static Material GetOrCreateShadowMaterial(TMP_FontAsset font)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(ShadowMaterialPath);
        if (material == null)
        {
            material = new Material(font.material);
            AssetDatabase.CreateAsset(material, ShadowMaterialPath);
        }

        ShaderUtilities.GetShaderPropertyIDs();
        material.shader = font.material.shader;
        material.SetTexture(ShaderUtilities.ID_MainTex, font.atlasTexture);
        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.25f, 0.04f, 0.12f, 0.6f));
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.5f);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
        material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.3f);
        material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.4f);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    // ---------- Background ----------

    static Camera SetupCamera(LayerMeta meta)
    {
        Camera cam = Object.FindAnyObjectByType<Camera>();
        if (cam == null)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }

        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(meta.sky[0] / 255f, meta.sky[1] / 255f, meta.sky[2] / 255f);
        return cam;
    }

    static void BuildParallax(LayerMeta meta, Camera cam)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
        Vector2 artSize = new Vector2(meta.width, meta.height) / PixelsPerUnit;

        var root = new GameObject("Parallax");
        root.AddComponent<ParallaxController>();
        var fitter = root.AddComponent<BackgroundFitter>();
        SetField(fitter, "targetCamera", cam);
        SetField(fitter, "artSize", artSize);

        CreateSprite(root.transform, "Sky", "L0_Sky", 0, material);

        var clouds = new GameObject("Clouds");
        clouds.transform.SetParent(root.transform, false);
        foreach (CloudDepth depth in CloudDepths)
            BuildCloudDepth(clouds.transform, depth, artSize.x, material);

        var ruins = CreateSprite(root.transform, "Ruins", "L2_Ruins", 20, material);
        AddParallax(ruins.gameObject, new Vector2(0.22f, 0.1f), new Vector2(0.08f, 0.04f), 0.2f, 0.7f);

        var foreground = CreateSprite(root.transform, "Foreground", "L3_Foreground", 30, material);
        AddParallax(foreground.gameObject, new Vector2(0.5f, 0.22f), new Vector2(0.14f, 0.06f), 0.22f, 0f);
    }

    static void BuildCloudDepth(Transform parent, CloudDepth depth, float artWidth, Material material)
    {
        var layer = new GameObject(depth.name);
        layer.transform.SetParent(parent, false);
        AddParallax(layer, depth.mouseStrength, depth.driftAmplitude, 0.15f, depth.phase);

        for (int i = 0; i < depth.clouds.Length; i++)
        {
            CloudPlacement cloud = depth.clouds[i];
            var sr = CreateSprite(layer.transform, cloud.sprite + "_" + i, cloud.sprite, depth.order, material);
            sr.transform.localPosition = cloud.position;
            sr.transform.localScale = new Vector3(cloud.scale, cloud.scale, 1f);
            sr.flipX = cloud.flip;
            sr.color = new Color(1f, 1f, 1f, depth.alpha);
        }

        var scroller = layer.AddComponent<CloudScroller>();
        SetField(scroller, "speed", depth.speed);
        SetField(scroller, "artWidth", artWidth);
    }

    static SpriteRenderer CreateSprite(Transform parent, string name, string spriteName, int order, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LayerFolder + "/" + spriteName + ".png");
        sr.sortingOrder = order;
        if (material != null) sr.sharedMaterial = material;
        return sr;
    }

    static void AddParallax(GameObject go, Vector2 mouseStrength, Vector2 driftAmplitude, float driftSpeed, float phase)
    {
        var layer = go.AddComponent<ParallaxLayer>();
        SetField(layer, "mouseStrength", mouseStrength);
        SetField(layer, "driftAmplitude", driftAmplitude);
        SetField(layer, "driftSpeed", driftSpeed);
        SetField(layer, "phase", phase);
    }

    // ---------- UI ----------

    static void BuildUI(TMP_FontAsset font, Material shadow)
    {
        var canvasGo = new GameObject("MainMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;   // ikut tinggi layar supaya posisi vertikal sama di semua aspect ratio
        Transform canvas = canvasGo.transform;

        // Judul di area langit kanan atas.
        var topRight = new Vector2(1f, 1f);
        var title = CreateText(canvas, "Title", "LYTHRUM", font, shadow, 120f, TextAlignmentOptions.Right);
        Place(title.rectTransform, topRight, topRight, new Vector2(-150f, -90f), new Vector2(1100f, 160f));
        title.characterSpacing = 18f;
        AddFader(title.gameObject, 0.4f, 1.1f, new Vector2(0f, -25f));

        // Tombol disusun oleh layout group, jadi Continue/Load Game yang disembunyikan tidak meninggalkan celah.
        var buttons = CreateRect(canvas, "Buttons");
        Place(buttons, topRight, topRight, new Vector2(-150f, -255f), new Vector2(520f, 340f));
        var layout = buttons.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperRight;
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var menuGroup = buttons.gameObject.AddComponent<CanvasGroup>();
        AddFader(buttons.gameObject, 1.1f, 0.8f, new Vector2(30f, 0f));

        Button continueButton = CreateButton(buttons, "ContinueButton", "Continue", font, shadow, 0);
        Button newGame = CreateButton(buttons, "NewGameButton", "New Game", font, shadow, 1);
        Button loadGame = CreateButton(buttons, "LoadGameButton", "Load Game", font, shadow, 2);
        Button settings = CreateButton(buttons, "SettingsButton", "Settings", font, shadow, 3);
        Button quit = CreateButton(buttons, "QuitButton", "Quit", font, shadow, 4);

        SettingsPanel panel = BuildSettingsPanel(canvas, font, out Button back);
        SaveSlotPanel slotPanel = BuildSlotPanel(canvas, font, out Button slotBack);
        ConfirmDialog confirm = BuildConfirmDialog(canvas, font);

        var overlay = CreateRect(canvas, "FadeOverlay");
        Place(overlay, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        overlay.gameObject.AddComponent<Image>().color = Color.black;
        var overlayGroup = overlay.gameObject.AddComponent<CanvasGroup>();
        overlayGroup.blocksRaycasts = false;
        overlayGroup.interactable = false;
        var overlayFader = AddFader(overlay.gameObject, 0f, 0.9f, Vector2.zero);
        SetField(overlayFader, "startAlpha", 1f);
        SetField(overlayFader, "endAlpha", 0f);

        var controllerGo = new GameObject("MainMenu");
        var controller = controllerGo.AddComponent<MainMenuController>();
        SetField(controller, "newGameScene", Path.GetFileNameWithoutExtension(NewGameScenePath));
        SetField(controller, "menuGroup", menuGroup);
        SetField(controller, "continueButton", continueButton.gameObject);
        SetField(controller, "newGameButton", newGame.gameObject);
        SetField(controller, "loadGameButton", loadGame.gameObject);
        SetField(controller, "settingsPanel", panel);
        SetField(controller, "slotPanel", slotPanel);
        SetField(controller, "confirmDialog", confirm);
        SetField(controller, "fadeOverlay", overlayFader);
        SetField(slotPanel, "controller", controller);

        UnityEventTools.AddPersistentListener(continueButton.onClick, controller.Continue);
        UnityEventTools.AddPersistentListener(newGame.onClick, controller.NewGame);
        UnityEventTools.AddPersistentListener(loadGame.onClick, controller.LoadGame);
        UnityEventTools.AddPersistentListener(settings.onClick, controller.OpenSettings);
        UnityEventTools.AddPersistentListener(quit.onClick, controller.Quit);
        UnityEventTools.AddPersistentListener(back.onClick, controller.CloseSettings);
        UnityEventTools.AddPersistentListener(slotBack.onClick, controller.CloseSlots);

        // Tombol awal dipilih oleh MainMenuController (Continue kalau ada save, kalau tidak New Game).
        var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
        eventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    static SaveSlotPanel BuildSlotPanel(Transform canvas, TMP_FontAsset font, out Button back)
    {
        var top = new Vector2(0.5f, 1f);
        RectTransform panelRect = CreatePanel(canvas, "SaveSlotPanel", new Vector2(820f, 620f));

        var header = CreateText(panelRect, "Header", "Load Game", font, null, 64f, TextAlignmentOptions.Center);
        Place(header.rectTransform, top, top, new Vector2(0f, -40f), new Vector2(600f, 90f));

        var slots = new Button[SaveSystem.SlotCount];
        for (int i = 0; i < slots.Length; i++)
            slots[i] = CreateSlotButton(panelRect, i, font, new Vector2(0f, -150f - i * 120f));

        back = CreateButton(panelRect, "BackButton", "Back", font, null, 0);
        Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(300f, 70f));
        back.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;

        var panel = panelRect.gameObject.AddComponent<SaveSlotPanel>();
        SetField(panel, "header", header);
        var so = new SerializedObject(panel);
        SerializedProperty array = so.FindProperty("slotButtons");
        array.arraySize = slots.Length;
        for (int i = 0; i < slots.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        panelRect.gameObject.SetActive(false);
        return panel;
    }

    static Button CreateSlotButton(Transform parent, int index, TMP_FontAsset font, Vector2 position)
    {
        var top = new Vector2(0.5f, 1f);
        var rect = CreateRect(parent, "Slot" + (index + 1));
        Place(rect, top, top, position, new Vector2(700f, 104f));

        var background = rect.gameObject.AddComponent<Image>();
        background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        background.type = Image.Type.Sliced;

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        var colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.08f);
        colors.highlightedColor = new Color(1f, 0.8f, 0.5f, 0.3f);
        colors.selectedColor = new Color(1f, 0.8f, 0.5f, 0.3f);
        colors.pressedColor = new Color(1f, 0.8f, 0.5f, 0.45f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.03f);
        colors.fadeDuration = 0.12f;
        button.colors = colors;

        var label = CreateText(rect, "Label", "Slot " + (index + 1), font, null, 40f, TextAlignmentOptions.Left);
        Place(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-48f, -16f));
        label.lineSpacing = -10f;
        return button;
    }

    internal static ConfirmDialog BuildConfirmDialog(Transform canvas, TMP_FontAsset font)
    {
        // Lapisan gelap penuh layar supaya klik di luar dialog tidak tembus ke menu di belakangnya.
        var blocker = CreateRect(canvas, "ConfirmDialog");
        Place(blocker, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        blocker.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        // Kotak dialog dibuat opaque supaya isi panel di belakangnya tidak ikut terbaca.
        RectTransform box = CreatePanel(blocker, "Box", new Vector2(700f, 320f));
        Color opaque = PanelColor;
        opaque.a = 1f;
        box.GetComponent<Image>().color = opaque;

        var message = CreateText(box, "Message", "Are you sure?", font, null, 40f, TextAlignmentOptions.Center);
        Place(message.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(620f, 150f));
        message.textWrappingMode = TextWrappingModes.Normal;

        var bottom = new Vector2(0.5f, 0f);
        Button yes = CreateButton(box, "YesButton", "Yes", font, null, 0);
        Place((RectTransform)yes.transform, bottom, bottom, new Vector2(-140f, 35f), new Vector2(220f, 70f));
        yes.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;
        Button no = CreateButton(box, "NoButton", "No", font, null, 0);
        Place((RectTransform)no.transform, bottom, bottom, new Vector2(140f, 35f), new Vector2(220f, 70f));
        no.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;

        var dialog = blocker.gameObject.AddComponent<ConfirmDialog>();
        SetField(dialog, "message", message);
        SetField(dialog, "yesButton", yes);
        SetField(dialog, "noButton", no);
        blocker.gameObject.SetActive(false);
        return dialog;
    }

    internal static RectTransform CreatePanel(Transform parent, string name, Vector2 size)
    {
        var center = new Vector2(0.5f, 0.5f);
        var rect = CreateRect(parent, name);
        Place(rect, center, center, Vector2.zero, size);
        var background = rect.gameObject.AddComponent<Image>();
        background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        background.type = Image.Type.Sliced;
        background.color = PanelColor;
        return rect;
    }

    // Panel Settings (volume per kanal, resolusi, fullscreen). Dipakai juga oleh PauseMenuBuilder.
    internal static SettingsPanel BuildSettingsPanel(Transform canvas, TMP_FontAsset font, out Button back)
    {
        var resources = new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
        };

        RectTransform panelRect = CreatePaperPanel(canvas, "SettingsPanel", new Vector2(860f, 700f), "Settings", font);
        var panel = panelRect.gameObject.AddComponent<SettingsPanel>();

        // Baris volume, satu per kanal GameSettings.
        var channels = (GameSettings.Channel[])Enum.GetValues(typeof(GameSettings.Channel));
        var so = new SerializedObject(panel);
        SerializedProperty rows = so.FindProperty("volumes");
        rows.arraySize = channels.Length;
        float y = -150f;
        for (int i = 0; i < channels.Length; i++, y -= 72f)
        {
            string name = channels[i].ToString();
            CreateSettingsLabel(panelRect, name, y, font);
            Slider slider = CreateSettingsSlider(panelRect, name + "Slider", y, resources);

            var value = CreateText(panelRect, name + "Value", "100%", font, null, 32f, TextAlignmentOptions.Right);
            Place(value.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(300f, y), new Vector2(100f, 60f));
            value.color = InkColor;

            SerializedProperty row = rows.GetArrayElementAtIndex(i);
            row.FindPropertyRelative("channel").enumValueIndex = i;
            row.FindPropertyRelative("slider").objectReferenceValue = slider;
            row.FindPropertyRelative("value").objectReferenceValue = value;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        CreateSettingsLabel(panelRect, "Resolution", y, font);
        TMP_Dropdown dropdown = CreateSettingsDropdown(panelRect, y, font);
        y -= 72f;

        CreateSettingsLabel(panelRect, "Fullscreen", y, font);
        var toggle = DefaultControls.CreateToggle(resources).GetComponent<Toggle>();
        toggle.name = "FullscreenToggle";
        toggle.transform.SetParent(panelRect, false);
        Object.DestroyImmediate(toggle.transform.Find("Label").gameObject);   // label legacy diganti TMP di atas
        Place((RectTransform)toggle.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-40f, y), new Vector2(40f, 40f));
        var toggleBackground = (RectTransform)toggle.transform.Find("Background");
        Place(toggleBackground, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        toggleBackground.GetComponent<Image>().color = new Color(InkColor.r, InkColor.g, InkColor.b, 0.25f);
        toggle.graphic.color = InkColor;

        back = CreateButton(panelRect, "BackButton", "Back", font, null, 0);
        Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(300f, 64f));
        back.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;
        ApplyInkStyle(back);

        SetField(panel, "resolutionDropdown", dropdown);
        SetField(panel, "fullscreenToggle", toggle);
        panelRect.gameObject.SetActive(false);
        return panel;
    }

    static void CreateSettingsLabel(Transform panel, string label, float y, TMP_FontAsset font)
    {
        var top = new Vector2(0.5f, 1f);
        var text = CreateText(panel, label + "Label", label, font, null, 36f, TextAlignmentOptions.Left);
        Place(text.rectTransform, top, top, new Vector2(-230f, y), new Vector2(300f, 60f));
        text.color = InkColor;
    }

    static Slider CreateSettingsSlider(Transform panel, string name, float y, DefaultControls.Resources resources)
    {
        var top = new Vector2(0.5f, 1f);
        var slider = DefaultControls.CreateSlider(resources).GetComponent<Slider>();
        slider.name = name;
        slider.transform.SetParent(panel, false);
        Place((RectTransform)slider.transform, top, top, new Vector2(90f, y), new Vector2(300f, 24f));
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.transform.Find("Background").GetComponent<Image>().color = new Color(InkColor.r, InkColor.g, InkColor.b, 0.25f);
        slider.fillRect.GetComponent<Image>().color = InkAccent;
        slider.handleRect.GetComponent<Image>().color = InkColor;
        return slider;
    }

    static TMP_Dropdown CreateSettingsDropdown(Transform panel, float y, TMP_FontAsset font)
    {
        const float itemHeight = 44f;
        var resources = new TMP_DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
            dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
        };

        var top = new Vector2(0.5f, 1f);
        var dropdown = TMP_DefaultControls.CreateDropdown(resources).GetComponent<TMP_Dropdown>();
        dropdown.name = "ResolutionDropdown";
        dropdown.transform.SetParent(panel, false);
        Place((RectTransform)dropdown.transform, top, top, new Vector2(140f, y), new Vector2(400f, 52f));
        dropdown.ClearOptions();

        // Warna kertas & tinta, dan font yang sama dengan menu.
        var paper = new Color(0.94f, 0.88f, 0.76f);
        dropdown.GetComponent<Image>().color = new Color(InkColor.r, InkColor.g, InkColor.b, 0.15f);
        dropdown.template.GetComponent<Image>().color = paper;
        foreach (TMP_Text text in dropdown.GetComponentsInChildren<TMP_Text>(true))
        {
            text.font = font;
            text.fontSize = 28f;
            text.color = InkColor;
            text.alignment = TextAlignmentOptions.Left;
        }
        dropdown.transform.Find("Arrow").GetComponent<Image>().color = InkColor;

        // Baris pilihan lebih tinggi dari default (20 px) supaya teks 28 pt muat, tampil 5 baris sekaligus.
        RectTransform item = (RectTransform)dropdown.template.Find("Viewport/Content/Item");
        item.sizeDelta = new Vector2(item.sizeDelta.x, itemHeight);
        var content = (RectTransform)item.parent;
        content.sizeDelta = new Vector2(content.sizeDelta.x, itemHeight);
        dropdown.template.sizeDelta = new Vector2(dropdown.template.sizeDelta.x, itemHeight * 5f);

        var itemToggle = item.GetComponent<Toggle>();
        var colors = itemToggle.colors;
        colors.normalColor = paper;
        colors.highlightedColor = new Color(InkAccent.r, InkAccent.g, InkAccent.b, 0.35f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(InkAccent.r, InkAccent.g, InkAccent.b, 0.5f);
        itemToggle.colors = colors;
        itemToggle.graphic.color = InkColor;
        return dropdown;
    }

    internal static Button CreateButton(Transform parent, string name, string label, TMP_FontAsset font, Material material, int index)
    {
        var text = CreateText(parent, name, label, font, material, 52f, TextAlignmentOptions.Right);
        text.color = Color.white;   // warna sebenarnya diatur lewat ColorTint tombol
        var topRight = new Vector2(1f, 1f);
        Place(text.rectTransform, topRight, topRight, new Vector2(0f, -index * 68f), new Vector2(520f, 64f));
        text.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;

        var button = text.gameObject.AddComponent<Button>();
        button.targetGraphic = text;
        var colors = button.colors;
        colors.normalColor = TextColor;
        colors.highlightedColor = HoverColor;
        colors.selectedColor = HoverColor;
        colors.pressedColor = HoverColor * 0.8f;
        colors.disabledColor = new Color(TextColor.r, TextColor.g, TextColor.b, 0.4f);
        colors.fadeDuration = 0.12f;
        button.colors = colors;
        return button;
    }

    internal static TextMeshProUGUI CreateText(Transform parent, string name, string value, TMP_FontAsset font, Material material, float size, TextAlignmentOptions alignment)
    {
        var rect = CreateRect(parent, name);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        if (material != null) text.fontSharedMaterial = material;
        text.text = value;
        text.fontSize = size;
        text.color = TextColor;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    internal static RectTransform CreateRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // anchor == pivot untuk elemen ber-anchor titik; anchor (0,0)-(1,1) dengan size nol = stretch penuh.
    internal static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = anchorMin == anchorMax ? anchorMin : new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static UIFader AddFader(GameObject go, float delay, float duration, Vector2 slideFrom)
    {
        if (go.GetComponent<CanvasGroup>() == null) go.AddComponent<CanvasGroup>();
        var fader = go.AddComponent<UIFader>();
        SetField(fader, "delay", delay);
        SetField(fader, "duration", duration);
        SetField(fader, "slideFrom", slideFrom);
        return fader;
    }

    // ---------- Helpers ----------

    internal static void SetField(Object target, string field, object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        switch (value)
        {
            case float f: property.floatValue = f; break;
            case bool b: property.boolValue = b; break;
            case string s: property.stringValue = s; break;
            case Vector2 v: property.vector2Value = v; break;
            case Object o: property.objectReferenceValue = o; break;
            default: throw new ArgumentException("Tipe field tidak didukung: " + field);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void AddToBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == ScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        if (!scenes.Exists(s => s.path == NewGameScenePath))
            scenes.Add(new EditorBuildSettingsScene(NewGameScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
