using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Generates the placeholder Menu & Flow content: MainMenu scene, ResultScreen prefab,
// installs the prefab in the gameplay scene and registers both scenes in the build list.
// Everything it makes is plain scene/prefab data, so artists can restyle it by hand afterwards.
public static class MenuFlowBuilder
{
    private const string ScenesDir = "Assets/_Project/Scenes";
    private const string MainMenuScenePath = ScenesDir + "/MainMenu.unity";
    private const string GameplayScenePath = ScenesDir + "/" + SceneFlow.GameplayScene + ".unity";
    private const string PrefabDir = "Assets/_Project/Prefabs/UI";
    private const string ResultPrefabPath = PrefabDir + "/ResultScreen.prefab";
    private const string MenuRoot = "Tools/Gimersia/Menu & Flow/";

    // Placeholder palette (forest / parchment) until the UI art lands.
    private static readonly Color Background = new Color(0.10f, 0.20f, 0.14f);
    private static readonly Color Dim = new Color(0f, 0f, 0f, 0.65f);
    private static readonly Color Board = new Color(0.95f, 0.91f, 0.80f);
    private static readonly Color BoardText = new Color(0.24f, 0.17f, 0.10f);
    private static readonly Color ButtonColor = new Color(0.30f, 0.55f, 0.30f);
    private static readonly Color LightText = new Color(0.96f, 0.95f, 0.88f);

    private static Sprite _uiSprite;
    private static Sprite _knobSprite;

    [MenuItem(MenuRoot + "Build All", priority = 0)]
    public static void BuildAll()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            LoadBuiltinSprites();
            BuildResultScreenPrefab();
            BuildMainMenuScene();
            InstallResultScreen();
            RegisterBuildScenes();
            Debug.Log("[MenuFlowBuilder] Menu & Flow content is ready.");
        }
        finally
        {
            // Put the user back where they were.
            if (!Application.isBatchMode && CanRestore(setup)) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }

    [MenuItem(MenuRoot + "Register Build Scenes", priority = 20)]
    public static void RegisterBuildScenes()
    {
        var scenes = new List<EditorBuildSettingsScene>();
        scenes.Add(new EditorBuildSettingsScene(MainMenuScenePath, true));
        scenes.Add(new EditorBuildSettingsScene(GameplayScenePath, true));
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.path == MainMenuScenePath || scene.path == GameplayScenePath) continue;
            scenes.Add(scene);
        }
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log($"[MenuFlowBuilder] Build scenes: 0 = {MainMenuScenePath}, 1 = {GameplayScenePath}");
    }

    // ---------------------------------------------------------------- Result screen

    private static void BuildResultScreenPrefab()
    {
        if (!ConfirmOverwrite(ResultPrefabPath)) return;
        Directory.CreateDirectory(PrefabDir);

        // Built in a throwaway scene so no user scene gets dirty.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject root = CreateCanvas("ResultScreen", 100).gameObject;
        ResultHud hud = root.AddComponent<ResultHud>();

        GameObject panel = CreateUI("ResultPanel", root.transform);
        Stretch(panel);
        AddImage(panel, Dim); // blocks clicks to the game behind it

        GameObject board = CreateUI("Board", panel.transform);
        SetCenter(board, new Vector2(760f, 600f), Vector2.zero);
        AddImage(board, Board, _uiSprite);
        VerticalLayoutGroup layout = AddVertical(board, 24f, new RectOffset(48, 48, 48, 48));
        layout.childAlignment = TextAnchor.MiddleCenter;

        GameObject icon = CreateUI("Icon_Placeholder", board.transform);
        Image iconImage = AddImage(icon, ButtonColor, _knobSprite);
        iconImage.type = Image.Type.Simple;
        iconImage.preserveAspect = true;
        AddLayout(icon, 160f, 160f);
        TextMeshProUGUI iconLabel = CreateText(icon.transform, "IconLabel", "WIN", 34f, LightText, FontStyles.Bold);
        Stretch(iconLabel.gameObject);

        TextMeshProUGUI title = CreateText(board.transform, "Title", "YOU WIN", 72f, BoardText, FontStyles.Bold);
        AddLayout(title.gameObject, 660f, 90f);
        TextMeshProUGUI subtitle = CreateText(board.transform, "Subtitle", "The forest survived!", 32f, BoardText);
        AddLayout(subtitle.gameObject, 660f, 80f);

        GameObject row = CreateUI("Buttons", board.transform);
        AddLayout(row, 660f, 84f);
        HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 32f;
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childControlWidth = rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = false;
        Button restart = CreateButton(row.transform, "RestartButton", "Restart", new Vector2(280f, 80f));
        Button mainMenu = CreateButton(row.transform, "MainMenuButton", "Main Menu", new Vector2(280f, 80f));

        Wire(hud, new Dictionary<string, Object>
        {
            { "_panel", panel },
            { "_label", title },
            { "_subtitle", subtitle },
            { "_icon", iconImage },
            { "_iconLabel", iconLabel },
            { "_restartButton", restart },
            { "_mainMenuButton", mainMenu },
        });

        panel.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(root, ResultPrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log($"[MenuFlowBuilder] Saved {ResultPrefabPath}");
    }

    // Replaces the old text-only result panel in the gameplay scene with the prefab.
    private static void InstallResultScreen()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ResultPrefabPath);
        if (prefab == null || !File.Exists(GameplayScenePath))
        {
            Debug.LogError($"[MenuFlowBuilder] Need {ResultPrefabPath} and {GameplayScenePath}.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        bool installed = false;

        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            foreach (ResultHud hud in rootObject.GetComponentsInChildren<ResultHud>(true))
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(hud.gameObject) != null)
                {
                    installed = true; // already a ResultScreen instance
                    continue;
                }

                // Legacy scene-only HUD: remove its panel and the component.
                var panel = new SerializedObject(hud).FindProperty("_panel").objectReferenceValue as GameObject;
                if (panel != null) Object.DestroyImmediate(panel);
                if (hud != null) Object.DestroyImmediate(hud); // may have lived under the panel
            }
        }

        if (!installed)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "ResultScreen";
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[MenuFlowBuilder] ResultScreen installed in {GameplayScenePath}");
    }

    // ---------------------------------------------------------------- Main menu

    private static void BuildMainMenuScene()
    {
        if (!ConfirmOverwrite(MainMenuScenePath)) return;

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Background;
        cameraObject.AddComponent<AudioListener>();

        // The project uses the Input System only, so UI needs its module (not StandaloneInputModule).
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        Canvas canvas = CreateCanvas("MainMenu", 0);
        MainMenu menu = canvas.gameObject.AddComponent<MainMenu>();

        GameObject background = CreateUI("Background", canvas.transform);
        Stretch(background);
        AddImage(background, Background);

        TextMeshProUGUI title = CreateText(canvas.transform, "Title", "GIMERSIA 2026", 110f, LightText, FontStyles.Bold);
        SetTop(title.gameObject, new Vector2(1400f, 150f), -200f);
        TextMeshProUGUI tagline = CreateText(canvas.transform, "Tagline", "Melestarikan & Gotong Royong", 40f, LightText, FontStyles.Italic);
        SetTop(tagline.gameObject, new Vector2(1400f, 60f), -320f);

        GameObject mainPanel = CreateUI("MainButtons", canvas.transform);
        SetCenter(mainPanel, new Vector2(420f, 420f), new Vector2(0f, -140f));
        CanvasGroup mainGroup = mainPanel.AddComponent<CanvasGroup>();
        AddVertical(mainPanel, 20f, new RectOffset(0, 0, 0, 0)).childAlignment = TextAnchor.MiddleCenter;
        var buttonSize = new Vector2(380f, 84f);
        Button start = CreateButton(mainPanel.transform, "StartButton", "Start Game", buttonSize);
        Button settings = CreateButton(mainPanel.transform, "SettingsButton", "Settings", buttonSize);
        Button credit = CreateButton(mainPanel.transform, "CreditButton", "Credit", buttonSize);
        Button exit = CreateButton(mainPanel.transform, "ExitButton", "Exit", buttonSize);

        TextMeshProUGUI version = CreateText(canvas.transform, "Version", "v0.1 - placeholder UI", 22f, LightText);
        version.alignment = TextAlignmentOptions.BottomRight;
        RectTransform versionRect = (RectTransform)version.transform;
        versionRect.anchorMin = versionRect.anchorMax = versionRect.pivot = new Vector2(1f, 0f);
        versionRect.sizeDelta = new Vector2(500f, 40f);
        versionRect.anchoredPosition = new Vector2(-24f, 16f);

        // Settings pop-up
        GameObject settingsPopup = CreatePopup(canvas.transform, "SettingsPopup", "Settings",
            new Vector2(820f, 520f), out Transform settingsContent, out Button settingsClose);
        Slider music = CreateSliderRow(settingsContent, "Music Volume");
        Slider sfx = CreateSliderRow(settingsContent, "SFX Volume");

        // Credit pop-up
        GameObject creditPopup = CreatePopup(canvas.transform, "CreditPopup", "Credit",
            new Vector2(1100f, 760f), out Transform creditContent, out Button creditClose);
        TextMeshProUGUI credits = CreateText(creditContent, "CreditList",
            "<b>Salt Studio</b>\n\n" +
            "Nama Anggota  -  Game Designer\n" +
            "Nama Anggota  -  Programmer\n" +
            "Nama Anggota  -  Programmer\n" +
            "Nama Anggota  -  3D Artist\n" +
            "Nama Anggota  -  2D / UI Artist\n\n" +
            "<size=24><i>Placeholder: ganti dengan nama dan role tim.</i></size>",
            34f, BoardText);
        AddLayout(credits.gameObject, 900f, 440f).flexibleHeight = 1f;

        Wire(menu, new Dictionary<string, Object>
        {
            { "_mainPanel", mainGroup },
            { "_startButton", start },
            { "_settingsButton", settings },
            { "_creditButton", credit },
            { "_exitButton", exit },
            { "_settingsPopup", settingsPopup },
            { "_musicSlider", music },
            { "_sfxSlider", sfx },
            { "_settingsCloseButton", settingsClose },
            { "_creditPopup", creditPopup },
            { "_creditCloseButton", creditClose },
        });

        settingsPopup.SetActive(false);
        creditPopup.SetActive(false);

        EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        Debug.Log($"[MenuFlowBuilder] Saved {MainMenuScenePath}");
    }

    private static GameObject CreatePopup(Transform parent, string name, string title, Vector2 size,
        out Transform content, out Button closeButton)
    {
        GameObject popup = CreateUI(name, parent);
        Stretch(popup);
        AddImage(popup, Dim); // modal: swallows clicks meant for the menu behind

        GameObject board = CreateUI("Board", popup.transform);
        SetCenter(board, size, Vector2.zero);
        AddImage(board, Board, _uiSprite);
        AddVertical(board, 28f, new RectOffset(56, 56, 48, 48)).childAlignment = TextAnchor.UpperCenter;

        TextMeshProUGUI titleText = CreateText(board.transform, "Title", title, 60f, BoardText, FontStyles.Bold);
        AddLayout(titleText.gameObject, size.x - 112f, 80f);

        GameObject body = CreateUI("Content", board.transform);
        AddLayout(body, size.x - 112f, 100f).flexibleHeight = 1f;
        AddVertical(body, 28f, new RectOffset(0, 0, 0, 0)).childAlignment = TextAnchor.MiddleCenter;
        content = body.transform;

        closeButton = CreateButton(board.transform, "CloseButton", "Close", new Vector2(260f, 76f));
        return popup;
    }

    private static Slider CreateSliderRow(Transform parent, string label)
    {
        GameObject row = CreateUI(label.Replace(" ", "") + "Row", parent);
        AddLayout(row, 700f, 60f);
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 32f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        TextMeshProUGUI text = CreateText(row.transform, "Label", label, 34f, BoardText);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        AddLayout(text.gameObject, 260f, 60f);

        var resources = new DefaultControls.Resources
        {
            standard = _uiSprite,
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            knob = _knobSprite,
        };
        GameObject sliderObject = DefaultControls.CreateSlider(resources);
        sliderObject.name = "Slider";
        sliderObject.transform.SetParent(row.transform, false);
        SetLayerRecursive(sliderObject, row.layer);
        AddLayout(sliderObject, 360f, 32f).flexibleWidth = 1f;

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0.8f;

        Transform bg = sliderObject.transform.Find("Background");
        if (bg != null && bg.TryGetComponent(out Image bgImg)) bgImg.color = new Color(0.76f, 0.71f, 0.60f);
        if (slider.fillRect != null && slider.fillRect.TryGetComponent(out Image fillImg)) fillImg.color = ButtonColor;
        if (slider.handleRect != null && slider.handleRect.TryGetComponent(out Image handleImg)) handleImg.color = BoardText;
        return slider;
    }

    // ---------------------------------------------------------------- UI helpers

    private static void LoadBuiltinSprites()
    {
        _uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        _knobSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
    }

    // An untitled scene has no path and cannot be reopened.
    private static bool CanRestore(SceneSetup[] setup)
    {
        if (setup.Length == 0) return false;
        foreach (SceneSetup entry in setup)
            if (string.IsNullOrEmpty(entry.path)) return false;
        return true;
    }

    private static bool ConfirmOverwrite(string path)
    {
        if (!File.Exists(path) || Application.isBatchMode) return true;
        return EditorUtility.DisplayDialog("Menu & Flow",
            $"{path} already exists. Overwrite it with a fresh placeholder?", "Overwrite", "Keep");
    }

    private static Canvas CreateCanvas(string name, int sortingOrder)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = LayerMask.NameToLayer("UI") };
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static GameObject CreateUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = LayerMask.NameToLayer("UI") };
        go.transform.SetParent(parent, false);
        return go;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 size)
    {
        GameObject go = CreateUI(name, parent);
        Image image = AddImage(go, Color.white, _uiSprite);
        AddLayout(go, size.x, size.y);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = ButtonColor;
        colors.highlightedColor = ButtonColor * 1.2f;
        colors.selectedColor = ButtonColor * 1.25f;
        colors.pressedColor = ButtonColor * 0.75f;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        button.colors = colors;

        TextMeshProUGUI text = CreateText(go.transform, "Label", label, 36f, LightText, FontStyles.Bold);
        Stretch(text.gameObject);
        return button;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size,
        Color color, FontStyles style = FontStyles.Normal)
    {
        GameObject go = CreateUI(name, parent);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Image AddImage(GameObject go, Color color, Sprite sprite = null)
    {
        Image image = go.AddComponent<Image>();
        image.color = color;
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
        }
        return image;
    }

    private static LayoutElement AddLayout(GameObject go, float width, float height)
    {
        LayoutElement element = go.AddComponent<LayoutElement>();
        element.preferredWidth = width;
        element.preferredHeight = height;
        return element;
    }

    private static VerticalLayoutGroup AddVertical(GameObject go, float spacing, RectOffset padding)
    {
        VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = padding;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        return layout;
    }

    private static void Stretch(GameObject go)
    {
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void SetCenter(GameObject go, Vector2 size, Vector2 position)
    {
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void SetTop(GameObject go, Vector2 size, float y)
    {
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(0f, y);
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
    }

    // Assigns private [SerializeField] references the same way the Inspector does.
    private static void Wire(Object target, Dictionary<string, Object> references)
    {
        var serialized = new SerializedObject(target);
        foreach (KeyValuePair<string, Object> pair in references)
        {
            SerializedProperty property = serialized.FindProperty(pair.Key);
            if (property == null)
            {
                Debug.LogError($"[MenuFlowBuilder] {target.GetType().Name} has no field '{pair.Key}'.");
                continue;
            }
            property.objectReferenceValue = pair.Value;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
