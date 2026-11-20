using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

// Generates all styled Menu & Flow content using Assets/_Project/UI and docs/style-guide:
// - MainMenu.unity (with Settings & Credit pop-ups)
// - LevelSelection.unity (05 - Level Selection.pdf)
// - ResultScreen.prefab (09 - Win.pdf, 10 - Lose.pdf) installed into Hakim_playground.unity
// - Build Settings scene registration
public static class MenuFlowBuilder
{
    private const string ScenesDir = "Assets/_Project/Scenes";
    private const string MainMenuScenePath = ScenesDir + "/MainMenu.unity";
    private const string LevelSelectionScenePath = ScenesDir + "/LevelSelection.unity";
    private const string GameplayScenePath = ScenesDir + "/" + SceneFlow.GameplayScene + ".unity";
    private const string PrefabDir = "Assets/_Project/Prefabs/UI";
    private const string ResultPrefabPath = PrefabDir + "/ResultScreen.prefab";
    private const string UIRoot = "Assets/_Project/UI";
    private const string MaterialDir = UIRoot + "/Materials";
    private const string HeadingMaterialPath = MaterialDir + "/TMP_Heading_Outline.mat";
    private const string TmpFontsOutDir = "Assets/TextMesh Pro/Resources/Fonts & Materials";
    private const string HeadingTtfPath = "Assets/TextMesh Pro/Fonts/LuckiestGuy-Regular.ttf";
    private const string BodyTtfPath = "Assets/TextMesh Pro/Fonts/Fredoka-Bold.ttf";
    private const string HeadingFontAssetPath = TmpFontsOutDir + "/LuckiestGuy-Regular SDF.asset";
    private const string BodyFontAssetPath = TmpFontsOutDir + "/Fredoka-Bold SDF.asset";
    private const string MenuRoot = "Tools/Gimersia/Menu & Flow/";

    // Style Guide palette (docs/style-guide/Style Guide - Katalog UI.pdf)
    private static readonly Color OutlineColor = Hex(0x2B1608);
    private static readonly Color HeadingText = Color.white;
    private static readonly Color BodyDarkText = Hex(0x4A2810);
    private static readonly Color BodyMutedText = Hex(0x7A4E24);
    private static readonly Color ParchmentCream = Hex(0xFFF7DE);
    private static readonly Color ParchmentDark = Hex(0xB8894A);
    private static readonly Color DividerColor = Hex(0xD4B584);
    private static readonly Color DimOverlay = new Color(0f, 0f, 0f, 0.58f);
    private static readonly Color CameraFallback = new Color(0.10f, 0.20f, 0.14f);

    private static Sprite _knobSprite;
    private static TMP_FontAsset _headingFont;
    private static TMP_FontAsset _bodyFont;
    private static Material _headingMaterial;

    [MenuItem(MenuRoot + "Build All", priority = 0)]
    public static void BuildAll()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            PrepareSharedAssets();
            BuildResultScreenPrefab();
            BuildMainMenuScene();
            BuildLevelSelectionScene();
            InstallResultScreen();
            RegisterBuildScenes();
            Debug.Log("[MenuFlowBuilder] All Menu & Flow scenes and prefabs are ready.");
        }
        finally
        {
            if (!Application.isBatchMode && CanRestore(setup)) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }

    [MenuItem(MenuRoot + "Build Main Menu", priority = 10)]
    public static void BuildMainMenuOnly()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        PrepareSharedAssets();
        BuildMainMenuScene();
        RegisterBuildScenes();
        EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
    }

    [MenuItem(MenuRoot + "Build Level Selection", priority = 11)]
    public static void BuildLevelSelectionOnly()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        PrepareSharedAssets();
        BuildLevelSelectionScene();
        RegisterBuildScenes();
        EditorSceneManager.OpenScene(LevelSelectionScenePath, OpenSceneMode.Single);
    }

    [MenuItem(MenuRoot + "Build Result Screen", priority = 12)]
    public static void BuildResultScreenOnly()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            PrepareSharedAssets();
            BuildResultScreenPrefab();
            InstallResultScreen();
            RegisterBuildScenes();
        }
        finally
        {
            if (!Application.isBatchMode && CanRestore(setup)) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }

    [MenuItem(MenuRoot + "Register Build Scenes", priority = 20)]
    public static void RegisterBuildScenes()
    {
        var ordered = new[] { MainMenuScenePath, LevelSelectionScenePath, GameplayScenePath };
        var scenes = new List<EditorBuildSettingsScene>();
        foreach (string path in ordered)
        {
            if (File.Exists(path))
                scenes.Add(new EditorBuildSettingsScene(path, true));
        }

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (System.Array.IndexOf(ordered, scene.path) >= 0) continue;
            scenes.Add(scene);
        }

        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log($"[MenuFlowBuilder] Registered {scenes.Count} build scenes (0 = MainMenu, 1 = LevelSelection, 2 = {SceneFlow.GameplayScene}).");
    }

    // ---------------------------------------------------------------- 1. Main Menu

    private static void BuildMainMenuScene()
    {
        if (!ConfirmOverwrite(MainMenuScenePath)) return;

        EnsureSpriteImport(UIRoot + "/Buttons/Slider_Track.png");
        EnsureSpriteImport(UIRoot + "/Buttons/Slider_Fill.png");
        EnsureSpriteImport(UIRoot + "/Buttons/Slider_Handle_Default.png");
        EnsureSpriteImport(UIRoot + "/Buttons/Slider_Handle_Hover.png");
        EnsureSpriteImport(UIRoot + "/Buttons/Slider_Handle_Dragging.png");

        Scene scene = CreateUIScene();
        Canvas canvas = CreateCanvas("MainMenu", 0);
        canvas.gameObject.AddComponent<UIFocusOnNavigate>();
        MainMenu menu = canvas.gameObject.AddComponent<MainMenu>();

        CreateMenuBackground(canvas.transform, dimAlpha: 0f);

        // Decorative Forest Spirits in bottom corners (02 - Main Menu.pdf)
        GameObject decor = CreateUI("CornerSpirits", canvas.transform);
        Stretch(decor);
        CreateDecorSprite(decor.transform, "SpiritLeft_Happy", "Mascot/ForestSpirit_1_Happy.png",
            new Vector2(148f, 190f), new Vector2(-545f, -345f));
        CreateDecorSprite(decor.transform, "SpiritRight_Cool", "Mascot/ForestSpirit_3_Cool.png",
            new Vector2(152f, 190f), new Vector2(732f, -308f));
        CreateDecorSprite(decor.transform, "SpiritRight_Idle", "Mascot/ForestSpirit_2_Idle.png",
            new Vector2(138f, 174f), new Vector2(588f, -364f));

        // Game Logo
        GameObject logo = CreateUI("GameLogo", canvas.transform);
        SetTop(logo, new Vector2(780f, 364f), -198f);
        Image logoImg = AddSpriteImage(logo, LoadSprite("Panels/GameLogo.png"), preserveAspect: true);
        logoImg.raycastTarget = false;

        // 4 Main Menu 3-State Buttons
        GameObject mainPanel = CreateUI("MainButtons", canvas.transform);
        SetCenter(mainPanel, new Vector2(460f, 456f), new Vector2(14f, -152f));
        CanvasGroup mainGroup = mainPanel.AddComponent<CanvasGroup>();
        VerticalLayoutGroup buttonStack = AddVertical(mainPanel, 22f, new RectOffset(0, 0, 0, 0));
        buttonStack.childAlignment = TextAnchor.MiddleCenter;

        Sprite menuDef = LoadSprite("Buttons/MenuButton_Default_Blank.png");
        Sprite menuHover = LoadSprite("Buttons/MenuButton_Hover_Blank.png");
        Sprite menuPressed = LoadSprite("Buttons/MenuButton_Pressed_Blank.png");
        var menuSize = new Vector2(390f, 95f);

        Button start = CreateMenuButton(mainPanel.transform, "StartButton", "START GAME", menuSize,
            menuDef, menuHover, menuPressed, LoadSprite("Icons/Icon_Play.png"),
            new Vector2(76f, 92f), new Vector2(-180f, 3f));
        Button settings = CreateMenuButton(mainPanel.transform, "SettingsButton", "SETTINGS", menuSize,
            menuDef, menuHover, menuPressed, LoadSprite("Icons/Icon_Gear.png"),
            new Vector2(90f, 94f), new Vector2(-170f, 3f));
        Button credit = CreateMenuButton(mainPanel.transform, "CreditButton", "CREDIT", menuSize,
            menuDef, menuHover, menuPressed, LoadSprite("Icons/Icon_Scroll.png"),
            new Vector2(82f, 88f), new Vector2(-175f, 3f));
        Button exit = CreateMenuButton(mainPanel.transform, "ExitButton", "EXIT", menuSize,
            menuDef, menuHover, menuPressed, LoadSprite("Icons/Icon_Door.png"),
            new Vector2(72f, 102f), new Vector2(-162f, 6f));

        // Version stamp
        TextMeshProUGUI version = CreateText(canvas.transform, "Version", "v0.1 · jam build", 22f,
            ParchmentCream, _bodyFont);
        version.alignment = TextAlignmentOptions.BottomRight;
        RectTransform versionRect = (RectTransform)version.transform;
        versionRect.anchorMin = versionRect.anchorMax = versionRect.pivot = new Vector2(1f, 0f);
        versionRect.sizeDelta = new Vector2(360f, 36f);
        versionRect.anchoredPosition = new Vector2(-24f, 16f);

        GameObject settingsPopup = CreateSettingsPopup(canvas.transform,
            out Slider musicSlider, out Slider sfxSlider, out Button settingsClose);
        GameObject creditPopup = CreateCreditPopup(canvas.transform, out Button creditClose);

        Wire(menu, new Dictionary<string, Object>
        {
            { "_mainPanel", mainGroup },
            { "_startButton", start },
            { "_settingsButton", settings },
            { "_creditButton", credit },
            { "_exitButton", exit },
            { "_settingsPopup", settingsPopup },
            { "_musicSlider", musicSlider },
            { "_sfxSlider", sfxSlider },
            { "_settingsCloseButton", settingsClose },
            { "_creditPopup", creditPopup },
            { "_creditCloseButton", creditClose },
        });
        WireString(menu, "_gameplayScene", SceneFlow.LevelSelectionScene);

        settingsPopup.SetActive(false);
        creditPopup.SetActive(false);

        EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        Debug.Log($"[MenuFlowBuilder] Saved {MainMenuScenePath}");
    }

    private static GameObject CreateSettingsPopup(Transform parent,
        out Slider musicSlider, out Slider sfxSlider, out Button closeButton)
    {
        GameObject popup = CreateModalBoard(parent, "SettingsPopup",
            boardSprite: LoadSprite("Panels/Board_M.png"),
            boardSize: new Vector2(840f, 566f),
            boardOffset: new Vector2(0f, -24f),
            bannerSprite: LoadSprite("Panels/Banner_Wood_Blank.png"),
            bannerTitle: "SETTINGS",
            closeOffset: new Vector2(-58f, -24f),
            out Transform board, out closeButton);

        GameObject content = CreateUI("Content", board);
        Stretch(content);
        RectTransform contentRect = (RectTransform)content.transform;
        contentRect.offsetMin = new Vector2(118f, 72f);
        contentRect.offsetMax = new Vector2(-92f, -108f);

        VerticalLayoutGroup vLayout = AddVertical(content, 44f, new RectOffset(0, 0, 12, 0));
        vLayout.childAlignment = TextAnchor.MiddleCenter;

        musicSlider = CreateStyledSliderSection(content.transform, "MusicSection", "MUSIK", "Volume musik latar");
        sfxSlider = CreateStyledSliderSection(content.transform, "SfxSection", "EFEK SUARA", "Volume SFX");
        return popup;
    }

    private static GameObject CreateCreditPopup(Transform parent, out Button closeButton)
    {
        GameObject popup = CreateModalBoard(parent, "CreditPopup",
            boardSprite: LoadSprite("Panels/Board_L.png"),
            boardSize: new Vector2(940f, 665f),
            boardOffset: new Vector2(0f, -24f),
            bannerSprite: LoadSprite("Panels/Banner_Leaf_Blank.png"),
            bannerTitle: "CREDIT",
            closeOffset: new Vector2(-52f, -22f),
            out Transform board, out closeButton);

        GameObject content = CreateUI("Content", board);
        Stretch(content);
        RectTransform contentRect = (RectTransform)content.transform;
        contentRect.offsetMin = new Vector2(112f, 52f);
        contentRect.offsetMax = new Vector2(-92f, -96f);

        VerticalLayoutGroup vLayout = AddVertical(content, 10f, new RectOffset(0, 0, 8, 0));
        vLayout.childAlignment = TextAnchor.UpperCenter;

        Sprite tagSprite = LoadSprite("Panels/Tag_Blank.png");
        (string name, string role)[] members =
        {
            ("NAMA ANGGOTA 1", "Game Designer"),
            ("NAMA ANGGOTA 2", "Programmer"),
            ("NAMA ANGGOTA 3", "3D Artist"),
            ("NAMA ANGGOTA 4", "UI / 2D Artist"),
            ("NAMA ANGGOTA 5", "Sound & Music"),
        };

        for (int i = 0; i < members.Length; i++)
        {
            CreateCreditRow(content.transform, $"Member_{i + 1}", members[i].name, members[i].role, tagSprite);
            if (i < members.Length - 1) CreateDivider(content.transform, 700f);
        }

        GameObject spacer = CreateUI("Spacer", content.transform);
        AddLayout(spacer, 10f, 10f).flexibleHeight = 1f;

        TextMeshProUGUI footer = CreateText(content.transform, "Footer",
            "Salt Studio · Gimersia GameJam 2026", 22f, BodyMutedText, _bodyFont);
        AddLayout(footer.gameObject, 700f, 34f);

        CreateAnchoredDecorSprite(board, "CreditSpirit_1", "Mascot/ForestSpirit_1_Happy.png",
            new Vector2(0f, 0f), new Vector2(88f, 113f), new Vector2(82f, 52f));
        CreateAnchoredDecorSprite(board, "CreditSpirit_2", "Mascot/ForestSpirit_2_Idle.png",
            new Vector2(0f, 0f), new Vector2(76f, 96f), new Vector2(150f, 46f));
        CreateAnchoredDecorSprite(board, "CreditSpirit_3", "Mascot/ForestSpirit_3_Cool.png",
            new Vector2(1f, 0f), new Vector2(88f, 110f), new Vector2(-84f, 52f));

        return popup;
    }

    // ---------------------------------------------------------------- 2. Level Selection (05 - Level Selection.pdf)

    private static void BuildLevelSelectionScene()
    {
        if (!ConfirmOverwrite(LevelSelectionScenePath)) return;

        Scene scene = CreateUIScene();
        Canvas canvas = CreateCanvas("LevelSelection", 0);
        canvas.gameObject.AddComponent<UIFocusOnNavigate>();
        LevelSelection controller = canvas.gameObject.AddComponent<LevelSelection>();

        CreateMenuBackground(canvas.transform, dimAlpha: 0.16f);

        // Top-left Back button (3-state Wood IconButton + ArrowLeft)
        Button backButton = CreateIconButton(canvas.transform, "BackButton",
            new Vector2(78f, 86f), new Vector2(0f, 1f), new Vector2(76f, -68f),
            LoadSprite("Buttons/IconButton_Wood_Default_Blank.png"),
            LoadSprite("Buttons/IconButton_Wood_Hover_Blank.png"),
            LoadSprite("Buttons/IconButton_Wood_Pressed_Blank.png"),
            LoadSprite("Icons/Icon_ArrowLeft.png"), new Vector2(46f, 42f));

        // Top-center title banner ("PILIH LEVEL")
        CreateBanner(canvas.transform, "HeaderBanner", LoadSprite("Panels/Banner_Wood_Blank.png"),
            "PILIH LEVEL", new Vector2(430f, 106f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), 44f);

        // ---- Left Panel: Selected Level Info Board + START button below ----
        GameObject leftBoard = CreateUI("LevelInfoBoard", canvas.transform);
        SetCenter(leftBoard, new Vector2(836f, 626f), new Vector2(-478f, 16f));
        Image leftBoardImg = AddSpriteImage(leftBoard, LoadSprite("Panels/Board_M.png"), preserveAspect: false);
        leftBoardImg.raycastTarget = true;

        TMP_Text levelBannerText = CreateBanner(leftBoard.transform, "LevelBanner",
            LoadSprite("Panels/Banner_Leaf_Blank.png"), "LEVEL 1",
            new Vector2(400f, 98f), new Vector2(0.5f, 1f), new Vector2(10f, 0f), 40f);

        GameObject leftContent = CreateUI("Content", leftBoard.transform);
        Stretch(leftContent);
        RectTransform lcRect = (RectTransform)leftContent.transform;
        lcRect.offsetMin = new Vector2(86f, 64f);
        lcRect.offsetMax = new Vector2(-64f, -86f);
        VerticalLayoutGroup lcLayout = AddVertical(leftContent, 16f, new RectOffset(0, 0, 0, 0));
        lcLayout.childAlignment = TextAnchor.UpperCenter;

        // Map preview frame (dark brown outline + masked BG_Gameplay)
        GameObject previewFrame = CreateUI("PreviewFrame", leftContent.transform);
        AddLayout(previewFrame, 686f, 246f);
        Image frameBorder = AddImage(previewFrame, OutlineColor);
        frameBorder.raycastTarget = false;

        GameObject previewMask = CreateUI("PreviewMask", previewFrame.transform);
        Stretch(previewMask);
        RectTransform pmRect = (RectTransform)previewMask.transform;
        pmRect.offsetMin = new Vector2(5f, 5f);
        pmRect.offsetMax = new Vector2(-5f, -5f);
        previewMask.AddComponent<RectMask2D>();

        GameObject previewGo = CreateUI("PreviewImage", previewMask.transform);
        SetCenter(previewGo, new Vector2(920f, 518f), new Vector2(0f, 6f));
        Sprite level1Preview = LoadSprite("Backgrounds/BG_Gameplay.png");
        Image previewImg = AddSpriteImage(previewGo, level1Preview, preserveAspect: true);
        previewImg.raycastTarget = false;

        // Title + "Rekor terbaik" row
        GameObject titleRow = CreateUI("TitleRow", leftContent.transform);
        AddLayout(titleRow, 686f, 68f);
        HorizontalLayoutGroup trLayout = titleRow.AddComponent<HorizontalLayoutGroup>();
        trLayout.spacing = 16f;
        trLayout.childAlignment = TextAnchor.MiddleLeft;
        trLayout.childControlWidth = trLayout.childControlHeight = true;
        trLayout.childForceExpandWidth = trLayout.childForceExpandHeight = false;

        TextMeshProUGUI levelTitle = CreateText(titleRow.transform, "LevelTitle", "HUTAN PINUS TUA", 34f,
            BodyDarkText, _headingFont);
        levelTitle.alignment = TextAlignmentOptions.MidlineLeft;
        AddLayout(levelTitle.gameObject, 440f, 56f).flexibleWidth = 1f;

        GameObject recordCol = CreateUI("BestRecord", titleRow.transform);
        AddLayout(recordCol, 180f, 64f);
        VerticalLayoutGroup rcLayout = AddVertical(recordCol, 4f, new RectOffset(0, 0, 0, 0));
        rcLayout.childAlignment = TextAnchor.UpperCenter;

        TextMeshProUGUI recordLbl = CreateText(recordCol.transform, "RecordLabel", "Rekor terbaik", 16f,
            BodyMutedText, _bodyFont);
        AddLayout(recordLbl.gameObject, 170f, 22f);

        GameObject medalRow = CreateUI("MedalSlots", recordCol.transform);
        AddLayout(medalRow, 150f, 38f);
        HorizontalLayoutGroup medalLayout = medalRow.AddComponent<HorizontalLayoutGroup>();
        medalLayout.spacing = 12f;
        medalLayout.childAlignment = TextAnchor.MiddleCenter;
        medalLayout.childControlWidth = medalLayout.childControlHeight = true;
        medalLayout.childForceExpandWidth = medalLayout.childForceExpandHeight = false;
        for (int m = 0; m < 3; m++)
        {
            GameObject slot = CreateUI($"Slot_{m + 1}", medalRow.transform);
            AddLayout(slot, 36f, 36f);
            Image outer = AddSpriteImage(slot, _knobSprite, preserveAspect: true);
            outer.color = BodyMutedText;
            outer.raycastTarget = false;
            GameObject inner = CreateUI("Fill", slot.transform);
            SetCenter(inner, new Vector2(30f, 30f), Vector2.zero);
            Image innerImg = AddSpriteImage(inner, _knobSprite, preserveAspect: true);
            innerImg.color = ParchmentCream;
            innerImg.raycastTarget = false;
        }

        // Level description
        TextMeshProUGUI desc = CreateText(leftContent.transform, "Description",
            "Bersihkan 5 grid yang tercemar, tumbuhkan pohon elemen, lalu lindungi hutan dari 7 gelombang monster bersama 3 Forest Spirit.",
            21f, BodyDarkText, _bodyFont);
        desc.alignment = TextAlignmentOptions.TopLeft;
        AddLayout(desc.gameObject, 686f, 64f);

        // 4 Info Tags Row ("7 Malam", "± 17 Menit", "5 Grid", "3 Spirit")
        GameObject tagsRow = CreateUI("TagsRow", leftContent.transform);
        AddLayout(tagsRow, 686f, 48f);
        HorizontalLayoutGroup tagsLayout = tagsRow.AddComponent<HorizontalLayoutGroup>();
        tagsLayout.spacing = 14f;
        tagsLayout.childAlignment = TextAnchor.MiddleLeft;
        tagsLayout.childControlWidth = tagsLayout.childControlHeight = true;
        tagsLayout.childForceExpandWidth = tagsLayout.childForceExpandHeight = false;

        Sprite tagSprite = LoadSprite("Panels/Tag_Blank.png");
        string[] defaultTags = { "7 Malam", "± 17 Menit", "5 Grid", "3 Spirit" };
        var tagLabels = new TMP_Text[defaultTags.Length];
        for (int t = 0; t < defaultTags.Length; t++)
            tagLabels[t] = CreateInfoTagPill(tagsRow.transform, $"Tag_{t + 1}", defaultTags[t], tagSprite, 19f, 44f, 18);

        // START Button below the Left Info Board (3-state Leaf Button)
        Button startButton = CreateActionButton(canvas.transform, "StartButton", "START",
            new Vector2(300f, 92f), new Vector2(-470f, -386f),
            LoadSprite("Buttons/Button_Leaf_Default_Blank.png"),
            LoadSprite("Buttons/Button_Leaf_Hover_Blank.png"),
            LoadSprite("Buttons/Button_Leaf_Pressed_Blank.png"),
            LoadSprite("Buttons/Button_Leaf_Disabled_Blank.png"), 42f);

        // ---- Right Panel: DAFTAR MAP Board with 12-node snake path ----
        GameObject rightBoard = CreateUI("MapListBoard", canvas.transform);
        SetCenter(rightBoard, new Vector2(968f, 704f), new Vector2(438f, -22f));
        Image rightBoardImg = AddSpriteImage(rightBoard, LoadSprite("Panels/Board_L.png"), preserveAspect: false);
        rightBoardImg.raycastTarget = true;

        CreateBanner(rightBoard.transform, "MapListBanner", LoadSprite("Panels/Banner_Wood_Blank.png"),
            "DAFTAR MAP", new Vector2(440f, 108f), new Vector2(0.5f, 1f), new Vector2(14f, 2f), 42f);

        GameObject mapArea = CreateUI("MapNodesArea", rightBoard.transform);
        Stretch(mapArea);
        RectTransform maRect = (RectTransform)mapArea.transform;
        maRect.offsetMin = new Vector2(96f, 64f);
        maRect.offsetMax = new Vector2(-72f, -94f);

        // 3 rows x 4 cols snake positions inside MapNodesArea
        float[] colX = { -286f, -95f, 95f, 286f };
        float[] rowY = { 166f, 8f, -150f };

        // Dotted connecting paths (drawn BEHIND the level nodes)
        GameObject pathsGo = CreateUI("PathDots", mapArea.transform);
        Stretch(pathsGo);
        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 3; c++)
                CreateDotLine(pathsGo.transform, new Vector2(colX[c] + 54f, rowY[r]), new Vector2(colX[c + 1] - 54f, rowY[r]), 5);
        }
        // Vertical snake connectors: Row 0 Col 3 -> Row 1 Col 3, and Row 1 Col 0 -> Row 2 Col 0
        CreateDotLine(pathsGo.transform, new Vector2(colX[3], rowY[0] - 54f), new Vector2(colX[3], rowY[1] + 54f), 4);
        CreateDotLine(pathsGo.transform, new Vector2(colX[0], rowY[1] - 54f), new Vector2(colX[0], rowY[2] + 54f), 4);

        Sprite nodeUnlocked = LoadSprite("Buttons/LevelNode_Unlocked_Blank.png");
        Sprite nodeHover = LoadSprite("Buttons/LevelNode_Hover_Blank.png");
        Sprite nodeSelected = LoadSprite("Buttons/LevelNode_Selected_Blank.png");
        Sprite nodeCompleted = LoadSprite("Buttons/LevelNode_Completed_Blank.png");
        Sprite nodeLocked = LoadSprite("Buttons/LevelNode_Locked.png");

        // Snake ordering: Row 0 (1..4 L->R), Row 1 (5..8 R->L), Row 2 (9..12 L->R)
        Vector2[] nodeCoords =
        {
            new Vector2(colX[0], rowY[0]), new Vector2(colX[1], rowY[0]), new Vector2(colX[2], rowY[0]), new Vector2(colX[3], rowY[0]),
            new Vector2(colX[3], rowY[1]), new Vector2(colX[2], rowY[1]), new Vector2(colX[1], rowY[1]), new Vector2(colX[0], rowY[1]),
            new Vector2(colX[0], rowY[2]), new Vector2(colX[1], rowY[2]), new Vector2(colX[2], rowY[2]), new Vector2(colX[3], rowY[2]),
        };

        var nodeComponents = new UILevelNodeButton[nodeCoords.Length];
        for (int i = 0; i < nodeCoords.Length; i++)
        {
            nodeComponents[i] = CreateLevelNode(mapArea.transform, $"LevelNode_{i + 1}", i + 1,
                nodeCoords[i], new Vector2(106f, 97f),
                nodeUnlocked, nodeHover, nodeSelected, nodeCompleted, nodeLocked);
            nodeComponents[i].Configure(i + 1, unlocked: i == 0, selected: i == 0);
        }

        // Bottom note inside MapListBoard
        TextMeshProUGUI mapNote = CreateText(rightBoard.transform, "UpdateNote",
            "Map lain akan terbuka setelah update", 19f, BodyMutedText, _bodyFont);
        RectTransform mnRect = (RectTransform)mapNote.transform;
        mnRect.anchorMin = mnRect.anchorMax = new Vector2(0.5f, 0f);
        mnRect.pivot = new Vector2(0.5f, 0.5f);
        mnRect.sizeDelta = new Vector2(680f, 34f);
        mnRect.anchoredPosition = new Vector2(14f, 64f);

        // Decorative bottom-right spirit peeking over bush (05 - Level Selection.pdf)
        CreateAnchoredDecorSprite(canvas.transform, "SpiritCorner_Happy", "Mascot/ForestSpirit_3_Happy.png",
            new Vector2(1f, 0f), new Vector2(114f, 144f), new Vector2(-106f, 134f));

        // Wire LevelSelection component & 12 LevelEntry configs
        Wire(controller, new Dictionary<string, Object>
        {
            { "_backButton", backButton },
            { "_startButton", startButton },
            { "_levelBannerText", levelBannerText },
            { "_previewImage", previewImg },
            { "_levelTitleText", levelTitle },
            { "_descriptionText", desc },
        });
        WireArray(controller, "_tagLabels", tagLabels);
        WireArray(controller, "_nodes", nodeComponents);
        WireLevelEntries(controller, level1Preview, nodeCoords.Length);

        EditorSceneManager.SaveScene(scene, LevelSelectionScenePath);
        Debug.Log($"[MenuFlowBuilder] Saved {LevelSelectionScenePath}");
    }

    // ---------------------------------------------------------------- 3. Styled Result Screen (09 - Win.pdf / 10 - Lose.pdf)

    private static void BuildResultScreenPrefab()
    {
        if (!ConfirmOverwrite(ResultPrefabPath)) return;
        Directory.CreateDirectory(PrefabDir);

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject root = CreateCanvas("ResultScreen", 100).gameObject;
        root.AddComponent<UIFocusOnNavigate>();
        ResultHud hud = root.AddComponent<ResultHud>();

        GameObject panel = CreateUI("ResultPanel", root.transform);
        Stretch(panel);
        AddImage(panel, new Color(0f, 0f, 0f, 0.64f)); // blocks world clicks behind modal

        // Central wooden parchment board (Board_M)
        GameObject board = CreateUI("Board", panel.transform);
        SetCenter(board, new Vector2(860f, 594f), new Vector2(0f, -10f));
        Image boardImg = AddSpriteImage(board, LoadSprite("Panels/Board_M.png"), preserveAspect: false);
        boardImg.raycastTarget = true;

        // Top ribbon banner (swaps between Leaf "MENANG!" and Berry "KALAH...")
        Sprite winBannerSprite = LoadSprite("Panels/Banner_Leaf_Blank.png");
        Sprite loseBannerSprite = LoadSprite("Panels/Banner_Berry_Blank.png");
        GameObject bannerGo = CreateUI("TitleBanner", board.transform);
        RectTransform bannerRect = (RectTransform)bannerGo.transform;
        bannerRect.anchorMin = bannerRect.anchorMax = new Vector2(0.5f, 1f);
        bannerRect.pivot = new Vector2(0.5f, 0.5f);
        bannerRect.sizeDelta = new Vector2(440f, 108f);
        bannerRect.anchoredPosition = new Vector2(10f, 4f);
        Image bannerImg = AddSpriteImage(bannerGo, winBannerSprite, preserveAspect: true);
        bannerImg.raycastTarget = false;

        TextMeshProUGUI bannerLabel = CreateText(bannerGo.transform, "BannerTitle", "MENANG!", 46f,
            HeadingText, _headingFont, useHeadingOutline: true);
        Stretch(bannerLabel.gameObject);
        RectTransform blRect = (RectTransform)bannerLabel.transform;
        blRect.offsetMin = new Vector2(60f, 10f);
        blRect.offsetMax = new Vector2(-60f, -14f);

        // Interior stack
        GameObject content = CreateUI("Content", board.transform);
        Stretch(content);
        RectTransform cRect = (RectTransform)content.transform;
        cRect.offsetMin = new Vector2(90f, 54f);
        cRect.offsetMax = new Vector2(-70f, -68f);
        VerticalLayoutGroup vLayout = AddVertical(content, 12f, new RectOffset(0, 0, 0, 0));
        vLayout.childAlignment = TextAnchor.MiddleCenter;

        // Mascot container (toggles Win 3-spirit celebration vs Lose sad spirit + fallen tree)
        GameObject mascotArea = CreateUI("MascotArea", content.transform);
        AddLayout(mascotArea, 680f, 168f);

        GameObject winMascots = CreateUI("WinMascots", mascotArea.transform);
        Stretch(winMascots);
        CreateDecorSprite(winMascots.transform, "Spirit1_Happy", "Mascot/ForestSpirit_1_Happy.png",
            new Vector2(130f, 166f), new Vector2(-176f, -4f));
        GameObject leftThumb = CreateUI("ThumbLeft", winMascots.transform);
        SetCenter(leftThumb, new Vector2(46f, 52f), new Vector2(-254f, -16f));
        leftThumb.transform.localScale = new Vector3(-1f, 1f, 1f);
        AddSpriteImage(leftThumb, LoadSprite("Icons/Icon_ThumbsUp.png"), preserveAspect: true).raycastTarget = false;

        CreateDecorSprite(winMascots.transform, "Spirit2_Cool", "Mascot/ForestSpirit_2_Cool.png",
            new Vector2(144f, 180f), new Vector2(0f, 2f));
        CreateDecorSprite(winMascots.transform, "Spirit3_Happy", "Mascot/ForestSpirit_3_Happy.png",
            new Vector2(130f, 166f), new Vector2(176f, -4f));
        GameObject rightThumb = CreateUI("ThumbRight", winMascots.transform);
        SetCenter(rightThumb, new Vector2(46f, 52f), new Vector2(254f, -16f));
        AddSpriteImage(rightThumb, LoadSprite("Icons/Icon_ThumbsUp.png"), preserveAspect: true).raycastTarget = false;

        GameObject loseMascot = CreateUI("LoseMascot", mascotArea.transform);
        Stretch(loseMascot);
        CreateDecorSprite(loseMascot.transform, "Spirit2_Sad", "Mascot/ForestSpirit_2_Sad.png",
            new Vector2(142f, 178f), new Vector2(-14f, 0f));
        GameObject fallenTree = CreateUI("FallenTree", loseMascot.transform);
        SetCenter(fallenTree, new Vector2(60f, 76f), new Vector2(74f, -42f));
        fallenTree.transform.localRotation = Quaternion.Euler(0f, 0f, -68f);
        AddSpriteImage(fallenTree, LoadSprite("Icons/Icon_Tree.png"), preserveAspect: true).raycastTarget = false;
        loseMascot.SetActive(false);

        // Headline & encouraging subtitle
        TextMeshProUGUI title = CreateText(content.transform, "Headline",
            "HUTAN BERHASIL BERTAHAN 7 MALAM!", 32f, BodyDarkText, _headingFont);
        AddLayout(title.gameObject, 680f, 44f);

        TextMeshProUGUI subtitle = CreateText(content.transform, "Subtitle",
            "Kalian bekerja sama dengan hebat. Gotong royong menyelamatkan hutan!", 20f, BodyMutedText, _bodyFont);
        AddLayout(subtitle.gameObject, 620f, 52f);

        // Stat tags row (auto-sized Tag_Blank pills)
        GameObject statsRow = CreateUI("StatTags", content.transform);
        AddLayout(statsRow, 680f, 52f);
        HorizontalLayoutGroup statsLayout = statsRow.AddComponent<HorizontalLayoutGroup>();
        statsLayout.spacing = 18f;
        statsLayout.childAlignment = TextAnchor.MiddleCenter;
        statsLayout.childControlWidth = statsLayout.childControlHeight = true;
        statsLayout.childForceExpandWidth = statsLayout.childForceExpandHeight = false;

        Sprite tagSprite = LoadSprite("Panels/Tag_Blank.png");
        TMP_Text tag1Text = CreateInfoTagPill(statsRow.transform, "Tag_Trees", "Pohon tersisa: 12", tagSprite, 19f, 44f, 20);
        TMP_Text tag2Text = CreateInfoTagPill(statsRow.transform, "Tag_TimeOrWave", "Waktu: 00:00", tagSprite, 19f, 44f, 20);
        TMP_Text tag3Text = CreateInfoTagPill(statsRow.transform, "Tag_Wave", "Wave 7/7", tagSprite, 19f, 44f, 20);

        // Action buttons row (3-state Wood RESTART + 3-state Leaf PILIH LEVEL)
        GameObject buttonsRow = CreateUI("Buttons", content.transform);
        AddLayout(buttonsRow, 640f, 92f);
        HorizontalLayoutGroup btnLayout = buttonsRow.AddComponent<HorizontalLayoutGroup>();
        btnLayout.spacing = 36f;
        btnLayout.childAlignment = TextAnchor.MiddleCenter;
        btnLayout.childControlWidth = btnLayout.childControlHeight = true;
        btnLayout.childForceExpandWidth = btnLayout.childForceExpandHeight = false;

        Button restart = CreateActionButton(buttonsRow.transform, "RestartButton", "RESTART",
            new Vector2(268f, 84f), Vector2.zero,
            LoadSprite("Buttons/Button_Wood_Default_Blank.png"),
            LoadSprite("Buttons/Button_Wood_Hover_Blank.png"),
            LoadSprite("Buttons/Button_Wood_Pressed_Blank.png"),
            LoadSprite("Buttons/Button_Wood_Disabled_Blank.png"), 34f, useLayoutElement: true);

        Button levelSelect = CreateActionButton(buttonsRow.transform, "LevelSelectButton", "PILIH LEVEL",
            new Vector2(268f, 84f), Vector2.zero,
            LoadSprite("Buttons/Button_Leaf_Default_Blank.png"),
            LoadSprite("Buttons/Button_Leaf_Hover_Blank.png"),
            LoadSprite("Buttons/Button_Leaf_Pressed_Blank.png"),
            LoadSprite("Buttons/Button_Leaf_Disabled_Blank.png"), 34f, useLayoutElement: true);

        Wire(hud, new Dictionary<string, Object>
        {
            { "_panel", panel },
            { "_bannerImage", bannerImg },
            { "_winBannerSprite", winBannerSprite },
            { "_loseBannerSprite", loseBannerSprite },
            { "_bannerLabel", bannerLabel },
            { "_winMascots", winMascots },
            { "_loseMascot", loseMascot },
            { "_label", title },
            { "_subtitle", subtitle },
            { "_tag1Root", tag1Text.transform.parent.gameObject },
            { "_tag1Text", tag1Text },
            { "_tag2Root", tag2Text.transform.parent.gameObject },
            { "_tag2Text", tag2Text },
            { "_tag3Root", tag3Text.transform.parent.gameObject },
            { "_tag3Text", tag3Text },
            { "_restartButton", restart },
            { "_levelSelectButton", levelSelect },
        });

        panel.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(root, ResultPrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log($"[MenuFlowBuilder] Saved styled {ResultPrefabPath}");
    }

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
                    installed = true;
                    continue;
                }

                var panel = new SerializedObject(hud).FindProperty("_panel").objectReferenceValue as GameObject;
                if (panel != null) Object.DestroyImmediate(panel);
                if (hud != null) Object.DestroyImmediate(hud);
            }
        }

        if (!installed)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "ResultScreen";
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[MenuFlowBuilder] ResultScreen verified in {GameplayScenePath}");
    }

    // ---------------------------------------------------------------- Reusable UI Builders

    private static Scene CreateUIScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = CameraFallback;
        cameraObject.AddComponent<AudioListener>();

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        return scene;
    }

    private static void CreateMenuBackground(Transform parent, float dimAlpha)
    {
        GameObject background = CreateUI("Background", parent);
        Stretch(background);
        Image bgImg = AddSpriteImage(background, LoadSprite("Backgrounds/BG_Menu.png"), preserveAspect: false);
        bgImg.raycastTarget = false;
        AspectRatioFitter bgFitter = background.AddComponent<AspectRatioFitter>();
        bgFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        bgFitter.aspectRatio = 1920f / 1080f;

        if (dimAlpha > 0f)
        {
            GameObject tint = CreateUI("BackgroundTint", parent);
            Stretch(tint);
            Image tintImg = AddImage(tint, new Color(0f, 0f, 0f, dimAlpha));
            tintImg.raycastTarget = false;
        }
    }

    private static TextMeshProUGUI CreateBanner(Transform parent, string name, Sprite bannerSprite,
        string title, Vector2 size, Vector2 anchor, Vector2 anchoredPosition, float fontSize)
    {
        GameObject banner = CreateUI(name, parent);
        RectTransform bannerRect = (RectTransform)banner.transform;
        bannerRect.anchorMin = bannerRect.anchorMax = anchor;
        bannerRect.pivot = new Vector2(0.5f, 0.5f);
        bannerRect.sizeDelta = size;
        bannerRect.anchoredPosition = anchoredPosition;
        Image bannerImg = AddSpriteImage(banner, bannerSprite, preserveAspect: true);
        bannerImg.raycastTarget = false;

        TextMeshProUGUI bannerText = CreateText(banner.transform, "Title", title, fontSize,
            HeadingText, _headingFont, useHeadingOutline: true);
        Stretch(bannerText.gameObject);
        RectTransform btRect = (RectTransform)bannerText.transform;
        btRect.offsetMin = new Vector2(56f, 10f);
        btRect.offsetMax = new Vector2(-56f, -12f);
        return bannerText;
    }

    private static GameObject CreateModalBoard(Transform parent, string name,
        Sprite boardSprite, Vector2 boardSize, Vector2 boardOffset,
        Sprite bannerSprite, string bannerTitle, Vector2 closeOffset,
        out Transform boardTransform, out Button closeButton)
    {
        GameObject popup = CreateUI(name, parent);
        Stretch(popup);
        AddImage(popup, DimOverlay);

        GameObject board = CreateUI("Board", popup.transform);
        SetCenter(board, boardSize, boardOffset);
        Image boardImg = AddSpriteImage(board, boardSprite, preserveAspect: true);
        boardImg.raycastTarget = true;
        boardTransform = board.transform;

        CreateBanner(board.transform, "TitleBanner", bannerSprite, bannerTitle,
            new Vector2(450f, 110f), new Vector2(0.5f, 1f), new Vector2(8f, 2f), 46f);

        closeButton = CreateIconButton(board.transform, "CloseButton",
            new Vector2(82f, 92f), new Vector2(1f, 1f), closeOffset,
            LoadSprite("Buttons/IconButton_Berry_Default_Blank.png"),
            LoadSprite("Buttons/IconButton_Berry_Hover_Blank.png"),
            LoadSprite("Buttons/IconButton_Berry_Pressed_Blank.png"),
            LoadSprite("Icons/Icon_Close.png"), new Vector2(52f, 54f));

        return popup;
    }

    private static Button CreateMenuButton(Transform parent, string name, string label, Vector2 size,
        Sprite defaultSprite, Sprite hoverSprite, Sprite pressedSprite,
        Sprite iconSprite, Vector2 iconSize, Vector2 iconOffset)
    {
        GameObject root = CreateUI(name, parent);
        ((RectTransform)root.transform).sizeDelta = size;
        AddLayout(root, size.x, size.y);

        Image hitTarget = root.AddComponent<Image>();
        hitTarget.color = new Color(1f, 1f, 1f, 0f);
        hitTarget.raycastTarget = true;

        Button button = root.AddComponent<Button>();
        button.targetGraphic = hitTarget;
        button.transition = Selectable.Transition.None;

        GameObject visualGo = CreateUI("Visual", root.transform);
        SetCenter(visualGo, size, Vector2.zero);
        Image visualImg = AddSpriteImage(visualGo, defaultSprite, preserveAspect: false);
        visualImg.raycastTarget = false;

        GameObject contentGo = CreateUI("Content", root.transform);
        Stretch(contentGo);

        if (iconSprite != null)
        {
            GameObject iconGo = CreateUI("Icon", contentGo.transform);
            SetCenter(iconGo, iconSize, iconOffset);
            AddSpriteImage(iconGo, iconSprite, preserveAspect: true).raycastTarget = false;
        }

        TextMeshProUGUI text = CreateText(contentGo.transform, "Label", label, 42f,
            HeadingText, _headingFont, useHeadingOutline: true);
        Stretch(text.gameObject);
        RectTransform textRect = (RectTransform)text.transform;
        textRect.offsetMin = new Vector2(36f, 6f);
        textRect.offsetMax = new Vector2(-18f, -2f);

        UIStateButton stateBtn = root.AddComponent<UIStateButton>();
        Wire(stateBtn, new Dictionary<string, Object>
        {
            { "_visual", visualImg },
            { "_content", contentGo.transform },
            { "_defaultSprite", defaultSprite },
            { "_hoverSprite", hoverSprite },
            { "_pressedSprite", pressedSprite },
        });
        return button;
    }

    private static Button CreateActionButton(Transform parent, string name, string label,
        Vector2 size, Vector2 anchoredPosition,
        Sprite defaultSprite, Sprite hoverSprite, Sprite pressedSprite, Sprite disabledSprite,
        float fontSize, bool useLayoutElement = false)
    {
        GameObject root = CreateUI(name, parent);
        SetCenter(root, size, anchoredPosition);
        if (useLayoutElement) AddLayout(root, size.x, size.y);

        Image hitTarget = root.AddComponent<Image>();
        hitTarget.color = new Color(1f, 1f, 1f, 0f);
        hitTarget.raycastTarget = true;

        Button button = root.AddComponent<Button>();
        button.targetGraphic = hitTarget;
        button.transition = Selectable.Transition.None;

        GameObject visualGo = CreateUI("Visual", root.transform);
        SetCenter(visualGo, size, Vector2.zero);
        Image visualImg = AddSpriteImage(visualGo, defaultSprite, preserveAspect: false);
        visualImg.raycastTarget = false;

        GameObject contentGo = CreateUI("Content", root.transform);
        Stretch(contentGo);

        TextMeshProUGUI text = CreateText(contentGo.transform, "Label", label, fontSize,
            HeadingText, _headingFont, useHeadingOutline: true);
        Stretch(text.gameObject);
        RectTransform textRect = (RectTransform)text.transform;
        textRect.offsetMin = new Vector2(16f, 8f);
        textRect.offsetMax = new Vector2(-16f, -4f);

        UIStateButton stateBtn = root.AddComponent<UIStateButton>();
        Wire(stateBtn, new Dictionary<string, Object>
        {
            { "_visual", visualImg },
            { "_content", contentGo.transform },
            { "_defaultSprite", defaultSprite },
            { "_hoverSprite", hoverSprite },
            { "_pressedSprite", pressedSprite },
            { "_disabledSprite", disabledSprite },
        });
        return button;
    }

    private static Button CreateIconButton(Transform parent, string name, Vector2 size,
        Vector2 anchor, Vector2 anchoredPosition,
        Sprite defaultSprite, Sprite hoverSprite, Sprite pressedSprite,
        Sprite iconSprite, Vector2 iconSize)
    {
        GameObject root = CreateUI(name, parent);
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = rootRect.anchorMax = anchor;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = size;
        rootRect.anchoredPosition = anchoredPosition;

        Image hitTarget = root.AddComponent<Image>();
        hitTarget.color = new Color(1f, 1f, 1f, 0f);
        hitTarget.raycastTarget = true;

        Button button = root.AddComponent<Button>();
        button.targetGraphic = hitTarget;
        button.transition = Selectable.Transition.None;

        GameObject visualGo = CreateUI("Visual", root.transform);
        SetCenter(visualGo, size, Vector2.zero);
        Image visualImg = AddSpriteImage(visualGo, defaultSprite, preserveAspect: false);
        visualImg.raycastTarget = false;

        GameObject contentGo = CreateUI("Content", root.transform);
        Stretch(contentGo);

        if (iconSprite != null)
        {
            GameObject iconGo = CreateUI("Icon", contentGo.transform);
            SetCenter(iconGo, iconSize, new Vector2(0f, 3f));
            AddSpriteImage(iconGo, iconSprite, preserveAspect: true).raycastTarget = false;
        }

        UIStateButton stateBtn = root.AddComponent<UIStateButton>();
        Wire(stateBtn, new Dictionary<string, Object>
        {
            { "_visual", visualImg },
            { "_content", contentGo.transform },
            { "_defaultSprite", defaultSprite },
            { "_hoverSprite", hoverSprite },
            { "_pressedSprite", pressedSprite },
        });
        return button;
    }

    private static UILevelNodeButton CreateLevelNode(Transform parent, string name, int number,
        Vector2 position, Vector2 size,
        Sprite unlockedSprite, Sprite hoverSprite, Sprite selectedSprite, Sprite completedSprite, Sprite lockedSprite)
    {
        GameObject root = CreateUI(name, parent);
        SetCenter(root, size, position);

        Image hitTarget = root.AddComponent<Image>();
        hitTarget.color = new Color(1f, 1f, 1f, 0f);
        hitTarget.raycastTarget = true;

        Button button = root.AddComponent<Button>();
        button.targetGraphic = hitTarget;
        button.transition = Selectable.Transition.None;

        GameObject visualGo = CreateUI("Visual", root.transform);
        SetCenter(visualGo, size, Vector2.zero);
        Image visualImg = AddSpriteImage(visualGo, unlockedSprite, preserveAspect: true);
        visualImg.raycastTarget = false;

        TextMeshProUGUI numTmp = CreateText(root.transform, "Number", number.ToString(), 40f,
            HeadingText, _headingFont, useHeadingOutline: true);
        SetCenter(numTmp.gameObject, new Vector2(80f, 52f), new Vector2(0f, 18f));

        UILevelNodeButton node = root.AddComponent<UILevelNodeButton>();
        Wire(node, new Dictionary<string, Object>
        {
            { "_visual", visualImg },
            { "_numberLabel", numTmp },
            { "_unlockedSprite", unlockedSprite },
            { "_hoverSprite", hoverSprite },
            { "_selectedSprite", selectedSprite },
            { "_completedSprite", completedSprite },
            { "_lockedSprite", lockedSprite },
        });
        return node;
    }

    private static TMP_Text CreateInfoTagPill(Transform parent, string name, string text,
        Sprite tagSprite, float fontSize, float height, int horizontalPadding)
    {
        GameObject tagGo = CreateUI(name, parent);
        Image img = AddSpriteImage(tagGo, tagSprite, preserveAspect: false);
        img.raycastTarget = false;

        HorizontalLayoutGroup hLayout = tagGo.AddComponent<HorizontalLayoutGroup>();
        hLayout.padding = new RectOffset(horizontalPadding, horizontalPadding, 6, 10);
        hLayout.childAlignment = TextAnchor.MiddleCenter;
        hLayout.childControlWidth = hLayout.childControlHeight = true;
        hLayout.childForceExpandWidth = hLayout.childForceExpandHeight = false;

        LayoutElement le = tagGo.AddComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;

        TextMeshProUGUI tmp = CreateText(tagGo.transform, "Label", text, fontSize, OutlineColor, _bodyFont);
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    private static void CreateDotLine(Transform parent, Vector2 from, Vector2 to, int dotCount)
    {
        for (int i = 0; i < dotCount; i++)
        {
            float t = dotCount == 1 ? 0.5f : i / (float)(dotCount - 1);
            Vector2 pos = Vector2.Lerp(from, to, t);
            GameObject dot = CreateUI("Dot", parent);
            SetCenter(dot, new Vector2(8f, 8f), pos);
            Image img = AddSpriteImage(dot, _knobSprite, preserveAspect: true);
            img.color = ParchmentDark;
            img.raycastTarget = false;
        }
    }

    private static Slider CreateStyledSliderSection(Transform parent, string name, string title, string subtitle)
    {
        GameObject section = CreateUI(name, parent);
        AddLayout(section, 620f, 126f);
        VerticalLayoutGroup vLayout = AddVertical(section, 14f, new RectOffset(0, 0, 0, 0));
        vLayout.childAlignment = TextAnchor.MiddleLeft;

        GameObject headerRow = CreateUI("Header", section.transform);
        AddLayout(headerRow, 620f, 40f);
        HorizontalLayoutGroup hHeader = headerRow.AddComponent<HorizontalLayoutGroup>();
        hHeader.spacing = 16f;
        hHeader.childAlignment = TextAnchor.LowerLeft;
        hHeader.childControlWidth = hHeader.childControlHeight = true;
        hHeader.childForceExpandWidth = hHeader.childForceExpandHeight = false;

        TextMeshProUGUI titleTmp = CreateText(headerRow.transform, "Title", title, 36f,
            BodyDarkText, _headingFont);
        titleTmp.alignment = TextAlignmentOptions.BottomLeft;
        titleTmp.textWrappingMode = TextWrappingModes.NoWrap;
        AddLayout(titleTmp.gameObject, -1f, 42f);

        TextMeshProUGUI subTmp = CreateText(headerRow.transform, "Subtitle", subtitle, 21f,
            BodyMutedText, _bodyFont);
        subTmp.alignment = TextAlignmentOptions.BottomLeft;
        subTmp.textWrappingMode = TextWrappingModes.NoWrap;
        AddLayout(subTmp.gameObject, -1f, 32f);

        GameObject controlRow = CreateUI("ControlRow", section.transform);
        AddLayout(controlRow, 620f, 70f);
        HorizontalLayoutGroup hCtrl = controlRow.AddComponent<HorizontalLayoutGroup>();
        hCtrl.spacing = 24f;
        hCtrl.childAlignment = TextAnchor.MiddleLeft;
        hCtrl.childControlWidth = hCtrl.childControlHeight = true;
        hCtrl.childForceExpandWidth = hCtrl.childForceExpandHeight = false;

        Sprite trackSprite = LoadSprite("Buttons/Slider_Track.png");
        Sprite fillSprite = LoadSprite("Buttons/Slider_Fill.png");
        Sprite handleDefault = LoadSprite("Buttons/Slider_Handle_Default.png");
        Sprite handleHover = LoadSprite("Buttons/Slider_Handle_Hover.png");
        Sprite handleDragging = LoadSprite("Buttons/Slider_Handle_Dragging.png");

        GameObject sliderGo = CreateUI("Slider", controlRow.transform);
        AddLayout(sliderGo, 496f, 64f);

        GameObject trackGo = CreateUI("Track", sliderGo.transform);
        RectTransform trackRect = (RectTransform)trackGo.transform;
        trackRect.anchorMin = new Vector2(0f, 0.5f);
        trackRect.anchorMax = new Vector2(1f, 0.5f);
        trackRect.pivot = new Vector2(0.5f, 0.5f);
        trackRect.sizeDelta = new Vector2(0f, 42f);
        trackRect.anchoredPosition = Vector2.zero;
        Image trackImg = AddSpriteImage(trackGo, trackSprite, preserveAspect: false);
        trackImg.raycastTarget = true;

        GameObject fillGo = CreateUI("Fill", sliderGo.transform);
        RectTransform fillRect = (RectTransform)fillGo.transform;
        fillRect.anchorMin = new Vector2(0f, 0.5f);
        fillRect.anchorMax = new Vector2(1f, 0.5f);
        fillRect.pivot = new Vector2(0.5f, 0.5f);
        fillRect.sizeDelta = new Vector2(0f, 42f);
        fillRect.anchoredPosition = Vector2.zero;
        Image fillImg = AddSpriteImage(fillGo, fillSprite, preserveAspect: false);
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImg.fillAmount = 0.8f;
        fillImg.raycastTarget = false;

        GameObject slideArea = CreateUI("HandleSlideArea", sliderGo.transform);
        Stretch(slideArea);
        RectTransform slideRect = (RectTransform)slideArea.transform;
        slideRect.offsetMin = new Vector2(22f, 0f);
        slideRect.offsetMax = new Vector2(-22f, 0f);

        GameObject handleGo = CreateUI("Handle", slideArea.transform);
        RectTransform handleRect = (RectTransform)handleGo.transform;
        handleRect.sizeDelta = new Vector2(64f, 0f);
        handleRect.anchoredPosition = Vector2.zero;
        Image handleHit = handleGo.AddComponent<Image>();
        handleHit.color = new Color(1f, 1f, 1f, 0f);
        handleHit.raycastTarget = true;

        GameObject handleVisualGo = CreateUI("HandleVisual", handleGo.transform);
        SetCenter(handleVisualGo, new Vector2(60f, 74f), new Vector2(0f, -3f));
        Image handleImg = AddSpriteImage(handleVisualGo, handleDefault, preserveAspect: true);
        handleImg.raycastTarget = false;

        Slider slider = sliderGo.AddComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0.8f;
        slider.fillRect = null;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleHit;
        slider.transition = Selectable.Transition.None;

        TextMeshProUGUI percentTmp = CreateText(controlRow.transform, "Percent", "80%", 32f,
            BodyDarkText, _bodyFont);
        percentTmp.alignment = TextAlignmentOptions.MidlineLeft;
        AddLayout(percentTmp.gameObject, 96f, 44f);

        UIStateSlider stateSlider = sliderGo.AddComponent<UIStateSlider>();
        Wire(stateSlider, new Dictionary<string, Object>
        {
            { "_fillImage", fillImg },
            { "_handleImage", handleImg },
            { "_percentText", percentTmp },
            { "_defaultHandle", handleDefault },
            { "_hoverHandle", handleHover },
            { "_draggingHandle", handleDragging },
        });
        return slider;
    }

    private static void CreateCreditRow(Transform parent, string name, string memberName, string role, Sprite tagSprite)
    {
        GameObject row = CreateUI(name, parent);
        AddLayout(row, 700f, 64f);
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 24f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        TextMeshProUGUI nameTmp = CreateText(row.transform, "MemberName", memberName, 30f,
            BodyDarkText, _headingFont);
        nameTmp.alignment = TextAlignmentOptions.MidlineLeft;
        AddLayout(nameTmp.gameObject, 430f, 52f).flexibleWidth = 1f;

        GameObject tagGo = CreateUI("RoleTag", row.transform);
        AddLayout(tagGo, 236f, 58f);
        AddSpriteImage(tagGo, tagSprite, preserveAspect: false).raycastTarget = false;

        TextMeshProUGUI roleTmp = CreateText(tagGo.transform, "RoleLabel", role, 22f,
            OutlineColor, _bodyFont);
        Stretch(roleTmp.gameObject);
        RectTransform roleRect = (RectTransform)roleTmp.transform;
        roleRect.offsetMin = new Vector2(14f, 6f);
        roleRect.offsetMax = new Vector2(-14f, -4f);
    }

    private static void CreateDivider(Transform parent, float width)
    {
        GameObject line = CreateUI("Divider", parent);
        AddLayout(line, width, 2f);
        AddImage(line, DividerColor).raycastTarget = false;
    }

    // ---------------------------------------------------------------- Asset & Serialization Helpers

    private static void PrepareSharedAssets()
    {
        _knobSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        _headingFont = GetOrCreateFontAsset(HeadingTtfPath, HeadingFontAssetPath);
        _bodyFont = GetOrCreateFontAsset(BodyTtfPath, BodyFontAssetPath);
        _headingMaterial = GetOrCreateHeadingMaterial(_headingFont);
    }

    private static TMP_FontAsset GetOrCreateFontAsset(string ttfPath, string assetPath)
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null && existing.material != null) return existing;

        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (sourceFont == null)
        {
            Debug.LogWarning($"[MenuFlowBuilder] Font not found at '{ttfPath}', falling back to TMP default.");
            return TMP_Settings.defaultFontAsset;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
        fontAsset.name = Path.GetFileNameWithoutExtension(assetPath);
        fontAsset.TryAddCharacters(
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~·•±");

        AssetDatabase.CreateAsset(fontAsset, assetPath);
        if (fontAsset.atlasTexture != null)
        {
            fontAsset.atlasTexture.name = fontAsset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
        }
        if (fontAsset.material != null)
        {
            fontAsset.material.name = fontAsset.name + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return fontAsset;
    }

    private static Material GetOrCreateHeadingMaterial(TMP_FontAsset font)
    {
        if (font == null) font = TMP_Settings.defaultFontAsset;
        if (font == null || font.material == null) return null;

        Directory.CreateDirectory(MaterialDir);
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(HeadingMaterialPath);
        if (mat == null)
        {
            mat = new Material(font.material);
            AssetDatabase.CreateAsset(mat, HeadingMaterialPath);
        }
        else
        {
            mat.shader = font.material.shader;
            mat.CopyPropertiesFromMaterial(font.material);
        }

        mat.EnableKeyword("OUTLINE_ON");
        mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.10f);
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.24f);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, OutlineColor);

        mat.EnableKeyword("UNDERLAY_ON");
        mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(OutlineColor.r, OutlineColor.g, OutlineColor.b, 0.95f));
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.75f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.28f);
        mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.05f);

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    private static void EnsureSpriteImport(string assetPath)
    {
        if (!File.Exists(assetPath)) return;
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;

        bool dirty = false;
        if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
        if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; dirty = true; }
        if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; dirty = true; }
        if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
        if (dirty) importer.SaveAndReimport();
    }

    private static Sprite LoadSprite(string relativePath)
    {
        string path = $"{UIRoot}/{relativePath}";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            Debug.LogError($"[MenuFlowBuilder] Missing Sprite at '{path}'. Check Texture Type = Sprite (2D and UI).");
        return sprite;
    }

    private static void CreateDecorSprite(Transform parent, string name, string relativePath, Vector2 size, Vector2 position)
    {
        GameObject go = CreateUI(name, parent);
        SetCenter(go, size, position);
        AddSpriteImage(go, LoadSprite(relativePath), preserveAspect: true).raycastTarget = false;
    }

    private static void CreateAnchoredDecorSprite(Transform parent, string name, string relativePath,
        Vector2 anchor, Vector2 size, Vector2 anchoredPosition)
    {
        GameObject go = CreateUI(name, parent);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        AddSpriteImage(go, LoadSprite(relativePath), preserveAspect: true).raycastTarget = false;
    }

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
            $"{path} already exists. Overwrite it?", "Overwrite", "Keep");
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

    private static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size,
        Color color, TMP_FontAsset font = null, bool useHeadingOutline = false, FontStyles style = FontStyles.Normal)
    {
        GameObject go = CreateUI(name, parent);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset resolvedFont = font != null ? font : TMP_Settings.defaultFontAsset;
        if (resolvedFont != null) tmp.font = resolvedFont;
        if (useHeadingOutline && _headingMaterial != null) tmp.fontSharedMaterial = _headingMaterial;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Image AddImage(GameObject go, Color color)
    {
        Image image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Image AddSpriteImage(GameObject go, Sprite sprite, bool preserveAspect)
    {
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = preserveAspect;
        return image;
    }

    private static LayoutElement AddLayout(GameObject go, float width, float height)
    {
        LayoutElement element = go.AddComponent<LayoutElement>();
        if (width >= 0f) element.preferredWidth = width;
        if (height >= 0f) element.preferredHeight = height;
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

    private static Color Hex(int rgb)
    {
        return new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f,
            1f);
    }

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

    private static void WireString(Object target, string fieldName, string value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(fieldName);
        if (property != null)
        {
            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void WireArray<T>(Object target, string fieldName, T[] items) where T : Object
    {
        var serialized = new SerializedObject(target);
        SerializedProperty array = serialized.FindProperty(fieldName);
        if (array == null) return;
        array.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireLevelEntries(LevelSelection target, Sprite level1Preview, int totalLevels)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty levels = serialized.FindProperty("_levels");
        if (levels == null) return;

        levels.arraySize = totalLevels;
        for (int i = 0; i < totalLevels; i++)
        {
            SerializedProperty elem = levels.GetArrayElementAtIndex(i);
            bool isFirst = i == 0;
            elem.FindPropertyRelative("BannerLabel").stringValue = $"LEVEL {i + 1}";
            elem.FindPropertyRelative("LevelTitle").stringValue = isFirst ? "HUTAN PINUS TUA" : $"MAP {i + 1} (TERKUNCI)";
            elem.FindPropertyRelative("Description").stringValue = isFirst
                ? "Bersihkan 5 grid yang tercemar, tumbuhkan pohon elemen, lalu lindungi hutan dari 7 gelombang monster bersama 3 Forest Spirit."
                : "Map ini akan terbuka setelah update berikutnya.";
            elem.FindPropertyRelative("PreviewSprite").objectReferenceValue = level1Preview;
            elem.FindPropertyRelative("SceneName").stringValue = isFirst ? SceneFlow.GameplayScene : "";
            elem.FindPropertyRelative("Unlocked").boolValue = isFirst;

            SerializedProperty tags = elem.FindPropertyRelative("Tags");
            string[] values = isFirst
                ? new[] { "7 Malam", "± 17 Menit", "5 Grid", "3 Spirit" }
                : new[] { "Terkunci" };
            tags.arraySize = values.Length;
            for (int t = 0; t < values.Length; t++)
                tags.GetArrayElementAtIndex(t).stringValue = values[t];
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
