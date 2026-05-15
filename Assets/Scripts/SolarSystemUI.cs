using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class SolarSystemUI : MonoBehaviour
{
    const string UiBuildLabel = "MeshRIR v0.5.0";

    static readonly Color PanelColor = new Color(0.015f, 0.024f, 0.05f, 0.9f);
    static readonly Color CardColor = new Color(0.025f, 0.04f, 0.078f, 0.88f);
    static readonly Color CardLineColor = new Color(0.11f, 0.22f, 0.3f, 0.95f);
    static readonly Color TextDim = new Color(0.66f, 0.76f, 0.84f);
    static readonly Color Accent = new Color(0.16f, 0.9f, 1f);
    static readonly Color Warm = new Color(1f, 0.72f, 0.34f);
    static readonly Color ActiveColor = new Color(0.05f, 0.68f, 0.86f, 0.95f);
    static readonly Color InactiveColor = new Color(0.07f, 0.09f, 0.14f, 0.95f);

    [Header("UI 容器設定")]
    public RectTransform content;

    [Header("選單面板")]
    public GameObject menuPanel;

    private SolarSystemManager manager;
    private bool allSelected = true;
    private bool isMenuOpen = true;

    private readonly List<(Image img, TextMeshProUGUI txt,
        PlanetOrbit planet, FMSynthesizer synth)> toggles
        = new List<(Image, TextMeshProUGUI, PlanetOrbit, FMSynthesizer)>();

    // ── Solo state ──────────────────────────────────────────────────────────
    // _soloTarget: the synth currently soloed (null = no solo active).
    // _preSoloActives: each planet's isActive state before solo was pressed,
    //   so that un-soloing restores exactly the prior mute/unmute pattern.
    private FMSynthesizer _soloTarget = null;
    private readonly List<bool> _preSoloActives = new List<bool>();

    private GameObject scrollPanel;
    private GameObject phoneRoot;
    private ScrollRect menuScroll;
    private Sprite circleSprite;
    private Sprite roundedSprite;
    private RectTransform hamburgerRT;
    private RectTransform backBtnRT;
    private RectTransform muteBtnRT;
    private RectTransform originBtnRT;
    private RectTransform zoomSliderRT;
    private Slider        zoomSlider;
    private readonly RectTransform[] crescendoLineRTs = new RectTransform[2];
    private GameObject    muteLine;
    private bool isMuted   = false;
    private float lastVol  = 0.35f;

    // Edit-mode state
    private readonly List<GameObject> planetCards   = new List<GameObject>();
    private readonly List<GameObject> planetDetails = new List<GameObject>();
    private readonly List<GameObject> editButtons   = new List<GameObject>();
    private GameObject listHeaderGO;
    private GameObject editHeaderGO;
    private TextMeshProUGUI editHeaderTitle;
    private int editingIdx = -1;
    private int lastScreenW = -1;
    private int lastScreenH = -1;
    private bool draggingMenuScroll = false;
    private Vector2 lastMenuPointer;

    // Content anchor saved for edit-mode restore
    private Vector2 contentOrigAnchorMin, contentOrigAnchorMax;
    private Vector2 contentOrigOffsetMin, contentOrigOffsetMax;
    private bool contentAnchorSaved = false;

    bool IsPhoneLayout()
    {
        return Application.isMobilePlatform || Screen.height > Screen.width || Screen.width <= 700;
    }

    void EnsurePhoneRuntimeShell()
    {
        var rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (rootCanvas == null)
            return;

        if (phoneRoot != null)
        {
            menuPanel = phoneRoot;
            content = phoneRoot.transform.Find("Content") as RectTransform;
            menuScroll = null;
            scrollPanel = null;
            return;
        }

        if (menuPanel != null)
            menuPanel.SetActive(false);

        phoneRoot = new GameObject("PhoneRuntimeMenu_v026");
        phoneRoot.transform.SetParent(rootCanvas.transform, false);
        phoneRoot.transform.SetAsLastSibling();

        var rootRT = phoneRoot.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        var rootImg = phoneRoot.AddComponent<Image>();
        rootImg.sprite = roundedSprite;
        rootImg.type = Image.Type.Sliced;
        rootImg.color = PanelColor;

        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(phoneRoot.transform, false);
        var contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = Vector2.zero;
        contentRT.anchorMax = Vector2.one;
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;

        menuPanel = phoneRoot;
        content = contentRT;
        menuScroll = null;
        scrollPanel = null;
    }

    void BuildManualPhoneUI()
    {
        EnsurePhoneRuntimeShell();
        DisableContentAutoLayout();

        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = new Vector2(0f, content.offsetMin.y);
        content.offsetMax = new Vector2(0f, content.offsetMax.y);

        float y = 8f;
        float gap = 6f;

        var header = CreateManualCard("PhoneHeader", y, 54f, PanelColor);
        AddManualText(header.transform, "SOLARMIX", 18, Warm, true,
            new Vector2(14f, -6f), new Vector2(220f, 26f), TextAlignmentOptions.Left);
        AddManualText(header.transform, UiBuildLabel, 9, TextDim, false,
            new Vector2(14f, -30f), new Vector2(220f, 16f), TextAlignmentOptions.Left);
        AddManualText(header.transform, "AUDIO8", 14, Accent, true,
            new Vector2(300f, -14f), new Vector2(80f, 24f), TextAlignmentOptions.Right);
        y += 54f + gap;

        var master = CreateManualCard("PhoneMaster", y, 94f, CardColor);
        var masterVLG = master.AddComponent<VerticalLayoutGroup>();
        masterVLG.padding = new RectOffset(12, 12, 7, 7);
        masterVLG.spacing = 3;
        masterVLG.childControlWidth = true;
        masterVLG.childControlHeight = true;
        masterVLG.childForceExpandWidth = true;
        masterVLG.childForceExpandHeight = false;
        var outLabel = MakeTMP(master.transform, "Output", 12, TextDim, true);
        outLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 14;
        AudioListener.volume = Mathf.Max(0.25f, AudioListener.volume);
        MakeLargeSlider(master.transform, 0f, 1f, AudioListener.volume, v => AudioListener.volume = v);
        var roomLabel = MakeTMP(master.transform, "Space", 12, TextDim, true);
        roomLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 14;
        MakeLargeSlider(master.transform, 0f, 1f, 0.42f, v =>
        {
            foreach (var sp in FindObjectsByType<MeshRIRSpatializer>(FindObjectsInactive.Exclude))
                sp.energy = v;
        });
        y += 94f + gap;

        for (int i = 0; i < manager.Planets.Count; i++)
        {
            CreateManualPlanetRow(i, y, 58f);
            y += 58f + gap;
        }

        content.sizeDelta = new Vector2(0f, y + 96f);
        if (menuScroll != null)
            menuScroll.verticalNormalizedPosition = 1f;
    }

    void DisableContentAutoLayout()
    {
        var csf = content.GetComponent<ContentSizeFitter>();
        if (csf != null) csf.enabled = false;

        var vlg = content.GetComponent<VerticalLayoutGroup>();
        if (vlg != null) vlg.enabled = false;
    }

    GameObject CreateManualCard(string name, float y, float h, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(content, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(8f, -y - h);
        rt.offsetMax = new Vector2(-8f, -y);

        var img = go.AddComponent<Image>();
        img.sprite = roundedSprite;
        img.type = Image.Type.Sliced;
        img.color = color;
        return go;
    }

    TextMeshProUGUI AddManualText(Transform parent, string text, int size, Color color, bool bold,
        Vector2 pos, Vector2 sizeDelta, TextAlignmentOptions alignment)
    {
        var tmp = MakeTMP(parent, text, size, color, bold);
        tmp.alignment = alignment;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        var rt = tmp.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        return tmp;
    }

    GameObject AddManualButton(Transform parent, string name, string label, Vector2 rightTop,
        Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = rightTop;
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.sprite = roundedSprite;
        img.type = Image.Type.Sliced;
        img.color = color;

        var btn = go.AddComponent<Button>();
        if (onClick != null)
            btn.onClick.AddListener(onClick);

        var tmp = MakeTMP(go.transform, label, 10, Color.white, true);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        tmp.enableWordWrapping = false;
        var trt = tmp.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        return go;
    }

    void CreateManualPlanetRow(int idx, float y, float h)
    {
        var planet = manager.Planets[idx];
        var synth = manager.Synths[idx];
        var col = manager.GetPlanetColor(idx);

        var card = CreateManualCard("PhoneRow_" + planet.planetName, y, h, CardColor);
        planetCards.Add(card);

        var dot = new GameObject("Dot");
        dot.transform.SetParent(card.transform, false);
        var dotRT = dot.AddComponent<RectTransform>();
        dotRT.anchorMin = dotRT.anchorMax = new Vector2(0f, 0.5f);
        dotRT.pivot = new Vector2(0f, 0.5f);
        dotRT.anchoredPosition = new Vector2(12f, 0f);
        dotRT.sizeDelta = new Vector2(9f, 9f);
        var dotImg = dot.AddComponent<Image>();
        dotImg.sprite = circleSprite;
        dotImg.color = col;
        dotImg.raycastTarget = false;

        AddManualText(card.transform, planet.planetName, 15, col, true,
            new Vector2(28f, -8f), new Vector2(152f, 24f), TextAlignmentOptions.Left);
        AddManualText(card.transform, synth.ModelLabel + "  " + planet.trajectoryType,
            8, TextDim, false, new Vector2(28f, -31f), new Vector2(152f, 14f), TextAlignmentOptions.Left);

        var togGO = AddManualButton(card.transform, "Tog", planet.isActive ? "ON" : "MUTE",
            new Vector2(-126f, -13f), new Vector2(42f, 30f),
            planet.isActive ? ActiveColor : InactiveColor, null);
        var togImg = togGO.GetComponent<Image>();
        var togTxt = togGO.GetComponentInChildren<TextMeshProUGUI>();
        toggles.Add((togImg, togTxt, planet, synth));
        togGO.GetComponent<Button>().onClick.AddListener(() =>
        {
            planet.isActive = !planet.isActive;
            SetPlanetActive(planet, synth, planet.isActive);
            RefreshToggleVisual(togImg, togTxt, planet.isActive);
        });

        AddManualButton(card.transform, "Solo", "SOLO",
            new Vector2(-70f, -13f), new Vector2(48f, 30f), CardLineColor,
            () => SoloPlanet(planet, synth));

        var editBtn = AddManualButton(card.transform, "Tune", "EDIT",
            new Vector2(-12f, -13f), new Vector2(50f, 30f), Warm,
            () => BuildManualPhoneTunePage(idx));
        editButtons.Add(editBtn);

        planetDetails.Add(new GameObject("PhoneDetailPlaceholder"));
        planetDetails[planetDetails.Count - 1].SetActive(false);
    }

    void BuildManualPhoneTunePage(int idx)
    {
        CameraController.Blocked = true;   // tune page always visible = always block
        foreach (Transform child in content) Destroy(child.gameObject);
        toggles.Clear();
        planetCards.Clear();
        planetDetails.Clear();
        editButtons.Clear();
        DisableContentAutoLayout();

        var planet = manager.Planets[idx];
        var synth = manager.Synths[idx];
        var col = manager.GetPlanetColor(idx);

        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);

        float y = 8f;
        var header = CreateManualCard("PhoneTuneHeader", y, 58f, PanelColor);
        AddManualButton(header.transform, "Back", "BACK", new Vector2(-12f, -14f),
            new Vector2(64f, 30f), ActiveColor, BuildUI);
        AddManualText(header.transform, planet.planetName, 18, col, true,
            new Vector2(14f, -8f), new Vector2(220f, 24f), TextAlignmentOptions.Left);
        AddManualText(header.transform, "tuning", 10, TextDim, false,
            new Vector2(14f, -32f), new Vector2(180f, 16f), TextAlignmentOptions.Left);
        y += 64f;

        var detail = CreateManualCard("PhoneTuneDetail", y, 360f, CardColor);
        var vlg = detail.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 8, 8);
        vlg.spacing = 5;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        AddModelSummary(detail.transform, synth);
        AddMeshRirTuneRows(detail.transform, synth);

        content.sizeDelta = new Vector2(0f, y + 460f);
        if (menuScroll != null)
            menuScroll.verticalNormalizedPosition = 1f;
        StartCoroutine(ForceLayoutRebuild());
    }

    void Start()
    {
        manager = SolarSystemManager.Instance;
        if (manager == null) return;

        circleSprite  = CreateCircleSprite();
        roundedSprite = CreateRoundedRectSprite(12);

        // Menu starts open — block camera immediately so first touch doesn't rotate.
        CameraController.Blocked = true;

        // Set output volume before first audio frame — Unity defaults to 1.0f.
        AudioListener.volume = 0.55f;

        StartCoroutine(WaitAndBuild());
    }

    void LateUpdate()
    {
        if (Screen.width != lastScreenW || Screen.height != lastScreenH)
        {
            ApplyResponsiveShell();
            StartCoroutine(ForceLayoutRebuild());
        }
    }

    void Update()
    {
        HandleMenuScrollInput();
    }

    IEnumerator WaitAndBuild()
    {
        yield return new WaitForSeconds(0.5f);
        int attempts = 0;
        while (manager.Planets.Count == 0 && attempts < 50)
        {
            attempts++;
            yield return new WaitForSeconds(0.1f);
        }
        if (content == null || manager.Planets.Count == 0) yield break;
        BuildUI();
    }

    void BuildUI()
    {
        try
        {
            if (manager.Planets.Count == 0) return;

            StyleMenuPanel();

            if (IsPhoneLayout())
                EnsurePhoneRuntimeShell();

            foreach (Transform child in content) Destroy(child.gameObject);
            toggles.Clear();
            planetCards.Clear();
            planetDetails.Clear();
            editButtons.Clear();

            var sr = content.GetComponentInParent<ScrollRect>();
            if (sr != null)
            {
                menuScroll = sr;
                scrollPanel = sr.gameObject;
                sr.scrollSensitivity = 40f;
                sr.horizontal = false;
                sr.vertical = true;
                sr.movementType = ScrollRect.MovementType.Clamped;
                sr.content = content;
            }

            if (IsPhoneLayout())
            {
                BuildManualPhoneUI();
                CreateHamburgerButton();
                StartCoroutine(ForceLayoutRebuild());
                return;
            }

            var contentCSF = content.GetComponent<ContentSizeFitter>()
                ?? content.gameObject.AddComponent<ContentSizeFitter>();
            contentCSF.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            contentCSF.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var vlg = content.GetComponent<VerticalLayoutGroup>()
                   ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing              = IsPhoneLayout() ? 5f : 6f;
            vlg.padding              = IsPhoneLayout()
                ? new RectOffset(8, 8, 8, 96)
                : new RectOffset(9, 9, 9, 10);
            vlg.childAlignment       = TextAnchor.UpperCenter;
            vlg.childControlWidth    = true;
            vlg.childControlHeight   = true;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;

            CreateHeader();
            CreateMasterVolumeRow();
            for (int i = 0; i < manager.Planets.Count; i++)
                MakePlanetRow(i);

            CreateHamburgerButton();
            StartCoroutine(ForceLayoutRebuild());
        }
        catch (System.Exception e)
        {
            Debug.LogError("BuildUI 錯誤：" + e.Message + "\n" + e.StackTrace);
        }
    }

    void StyleMenuPanel()
    {
        if (menuPanel == null)
            return;

        var img = menuPanel.GetComponent<Image>() ?? menuPanel.AddComponent<Image>();
        img.sprite = roundedSprite;
        img.type = Image.Type.Sliced;
        img.color = PanelColor;

        ApplyResponsiveShell();
    }

    void ApplyResponsiveShell()
    {
        lastScreenW = Screen.width;
        lastScreenH = Screen.height;

        var canvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (canvas == null)
            return;

        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            bool mobile = Application.isMobilePlatform;
            bool portraitScreen = Screen.height > Screen.width;
            scaler.referenceResolution = mobile
                ? (portraitScreen ? new Vector2(430f, 932f) : new Vector2(932f, 430f))
                : new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = mobile ? 0.5f : (portraitScreen ? 0f : 1f);
        }

        if (menuPanel != null)
        {
            var canvasRT = canvas.GetComponent<RectTransform>();
            var panelRT = menuPanel.GetComponent<RectTransform>();
            if (canvasRT != null && panelRT != null)
            {
                float canvasW = canvasRT.rect.width;
                float canvasH = canvasRT.rect.height;
                bool portrait = canvasH > canvasW;
                bool mobile = Application.isMobilePlatform;
                float panelW = portrait || mobile ? canvasW : Mathf.Clamp(canvasW * 0.24f, 260f, 320f);

                panelRT.anchorMin = portrait || mobile ? Vector2.zero : new Vector2(1f, 0f);
                panelRT.anchorMax = portrait || mobile ? Vector2.one : new Vector2(1f, 1f);
                panelRT.pivot = portrait || mobile ? new Vector2(0.5f, 0.5f) : new Vector2(1f, 0.5f);
                panelRT.anchoredPosition = Vector2.zero;
                panelRT.sizeDelta = portrait || mobile ? Vector2.zero : new Vector2(panelW, 0f);
            }
        }

        SnapHamburgerToViewport();
    }

    void HandleMenuScrollInput()
    {
        if (!isMenuOpen || menuScroll == null || content == null || menuPanel == null)
            return;

        Vector2 mouse = Input.mousePosition;
        bool insideMenu = IsScreenPointInsideMenu(mouse);

        float wheel = Input.mouseScrollDelta.y;
        if (insideMenu && Mathf.Abs(wheel) > 0.01f)
            ScrollMenu(wheel * 0.08f);

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                draggingMenuScroll = IsScreenPointInsideMenu(touch.position) && !IsPointerOverSlider(touch.position);
                lastMenuPointer = touch.position;
            }
            else if (draggingMenuScroll && touch.phase == TouchPhase.Moved)
            {
                DragMenuScroll(touch.position);
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                draggingMenuScroll = false;
            }
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            draggingMenuScroll = insideMenu && !IsPointerOverSlider(mouse);
            lastMenuPointer = mouse;
        }
        else if (draggingMenuScroll && Input.GetMouseButton(0))
        {
            DragMenuScroll(mouse);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            draggingMenuScroll = false;
        }
    }

    void DragMenuScroll(Vector2 pointer)
    {
        Vector2 delta = pointer - lastMenuPointer;
        lastMenuPointer = pointer;

        float scrollable = GetScrollableHeight();
        if (scrollable <= 1f)
            return;

        ScrollMenu(-delta.y / scrollable);
    }

    void ScrollMenu(float normalizedDelta)
    {
        menuScroll.verticalNormalizedPosition = Mathf.Clamp01(
            menuScroll.verticalNormalizedPosition + normalizedDelta);
    }

    float GetScrollableHeight()
    {
        RectTransform viewport = menuScroll.viewport != null
            ? menuScroll.viewport
            : menuScroll.GetComponent<RectTransform>();
        return Mathf.Max(1f, content.rect.height - viewport.rect.height);
    }

    bool IsScreenPointInsideMenu(Vector2 point)
    {
        var canvas = GetComponentInParent<Canvas>()?.rootCanvas;
        var menuRT = menuPanel.GetComponent<RectTransform>();
        return menuRT != null && RectTransformUtility.RectangleContainsScreenPoint(
            menuRT, point, canvas != null ? canvas.worldCamera : null);
    }

    bool IsPointerOverSlider(Vector2 point)
    {
        if (EventSystem.current == null)
            return false;

        var pointer = new PointerEventData(EventSystem.current) { position = point };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        for (int i = 0; i < hits.Count; i++)
        {
            if (hits[i].gameObject.GetComponentInParent<Slider>() != null)
                return true;
        }

        return false;
    }

    // ─── Header ─────────────────────────────────────────────────────────

    void CreateHeader()
    {
        listHeaderGO = new GameObject("ListHeader");
        listHeaderGO.transform.SetParent(content, false);
        var le = listHeaderGO.AddComponent<LayoutElement>();
        le.preferredHeight = IsPhoneLayout() ? 50 : 60;
        le.minHeight = le.preferredHeight;
        var vlg = listHeaderGO.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleLeft;
        vlg.spacing = 0;
        vlg.padding = new RectOffset(4, 64, 0, 0);
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var title = MakeTMP(listHeaderGO.transform, "SOLARMIX", IsPhoneLayout() ? 22 : 26, Warm, true);
        title.alignment = TextAlignmentOptions.Left;
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;

        var sub = MakeTMP(listHeaderGO.transform, UiBuildLabel, IsPhoneLayout() ? 11 : 13, TextDim);
        sub.alignment = TextAlignmentOptions.Left;
        sub.characterSpacing = 8;
        sub.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;

        // Edit header — "← PlanetName" (hidden until edit mode)
        editHeaderGO = new GameObject("EditHeader");
        editHeaderGO.transform.SetParent(content, false);
        var ele = editHeaderGO.AddComponent<LayoutElement>();
        ele.preferredHeight = 54; ele.minHeight = 54;
        editHeaderGO.SetActive(false);

        var backBtnGO = new GameObject("BackBtn");
        backBtnGO.transform.SetParent(editHeaderGO.transform, false);
        var backRT = backBtnGO.AddComponent<RectTransform>();
        backRT.anchorMin = Vector2.zero; backRT.anchorMax = Vector2.one;
        backRT.offsetMin = Vector2.zero; backRT.offsetMax = Vector2.zero;

        var backImg = backBtnGO.AddComponent<Image>();
        backImg.sprite = roundedSprite; backImg.type = Image.Type.Sliced;
        backImg.color = CardLineColor;

        editHeaderTitle = MakeTMP(backBtnGO.transform, "← ", 26, Warm, true);
        editHeaderTitle.alignment = TextAlignmentOptions.Left;
        editHeaderTitle.verticalAlignment = VerticalAlignmentOptions.Middle;
        var etRT = editHeaderTitle.GetComponent<RectTransform>();
        etRT.anchorMin = Vector2.zero; etRT.anchorMax = Vector2.one;
        etRT.offsetMin = new Vector2(20, 0); etRT.offsetMax = new Vector2(-20, 0);

        var backBtn = backBtnGO.AddComponent<Button>();
        backBtn.onClick.AddListener(ExitEditMode);
    }

    // ─── Master Volume ───────────────────────────────────────────────────

    void CreateMasterVolumeRow()
    {
        var go = CreateCard("MasterVol");
        if (IsPhoneLayout())
        {
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = 132;
            le.minHeight = 132;
        }

        var label = MakeTMP(go.transform, "Output", IsPhoneLayout() ? 14 : 18, TextDim, true);
        label.gameObject.AddComponent<LayoutElement>().preferredHeight = IsPhoneLayout() ? 18 : 24;

        AudioListener.volume = 0.55f;
        MakeLargeSlider(go.transform, 0f, 1f, AudioListener.volume,
            v => AudioListener.volume = v);

        var roomLabel = MakeTMP(go.transform, "Space", IsPhoneLayout() ? 14 : 18, TextDim, true);
        roomLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = IsPhoneLayout() ? 18 : 24;

        MakeLargeSlider(go.transform, 0f, 1f, 0.42f, v =>
        {
            foreach (var sp in FindObjectsByType<MeshRIRSpatializer>(FindObjectsInactive.Exclude))
                sp.energy = v;
        });

        // All / None
        var anGO = new GameObject("Btn_AllNone");
        anGO.transform.SetParent(go.transform, false);
        var anLE = anGO.AddComponent<LayoutElement>();
        anLE.preferredWidth = 116; anLE.preferredHeight = 40; anLE.flexibleWidth = 0;
        var anImg = anGO.AddComponent<Image>();
        anImg.sprite = roundedSprite; anImg.type = Image.Type.Sliced;
        anImg.color = ActiveColor;
        var anBtn = anGO.AddComponent<Button>();
        var anTMP = MakeTMP(anGO.transform, "ALL ON", 17, Color.white, true);
        anTMP.alignment = TextAlignmentOptions.Center;
        anTMP.verticalAlignment = VerticalAlignmentOptions.Middle;
        var anRT = anTMP.GetComponent<RectTransform>();
        anRT.anchorMin = Vector2.zero; anRT.anchorMax = Vector2.one;
        anRT.offsetMin = anRT.offsetMax = Vector2.zero;

        anBtn.onClick.AddListener(() =>
        {
            allSelected = !allSelected;
            anImg.color = allSelected
                ? ActiveColor
                : InactiveColor;
            anTMP.text = allSelected ? "ALL ON" : "MUTE ALL";
            foreach (var t in toggles)
            {
                SetPlanetActive(t.planet, t.synth, allSelected);
                t.img.color = allSelected
                    ? ActiveColor
                    : InactiveColor;
                t.txt.text = allSelected ? "ON" : "MUTE";
            }
        });
    }

    // ─── Planet Row ──────────────────────────────────────────────────────

    void MakePlanetRow(int idx)
    {
        var planet = manager.Planets[idx];
        var synth  = manager.Synths[idx];
        var col    = manager.GetPlanetColor(idx);

        var card = CreateCard("Row_" + planet.planetName);
        planetCards.Add(card);
        var cardLE = card.GetComponent<LayoutElement>();
        cardLE.preferredHeight = IsPhoneLayout() ? 70 : 94;
        cardLE.minHeight = cardLE.preferredHeight;

        var topGO = new GameObject("Top");
        topGO.transform.SetParent(card.transform, false);
        var topLE = topGO.AddComponent<LayoutElement>();
        topLE.preferredHeight = IsPhoneLayout() ? 54 : 48;
        topLE.minHeight = topLE.preferredHeight;
        var topHLG = topGO.AddComponent<HorizontalLayoutGroup>();
        topHLG.childAlignment      = TextAnchor.MiddleCenter;
        topHLG.spacing             = IsPhoneLayout() ? 5 : 6;
        topHLG.childControlHeight  = true;
        topHLG.childForceExpandWidth = false;

        var dotGO = new GameObject("ColorDot");
        dotGO.transform.SetParent(topGO.transform, false);
        var dotLE = dotGO.AddComponent<LayoutElement>();
        dotLE.preferredWidth = IsPhoneLayout() ? 10 : 12;
        dotLE.preferredHeight = dotLE.preferredWidth;
        dotLE.flexibleWidth = 0;
        var dotImg = dotGO.AddComponent<Image>();
        dotImg.sprite = circleSprite;
        dotImg.color = col;
        dotImg.raycastTarget = false;

        var nameGO = new GameObject("NameBlock");
        nameGO.transform.SetParent(topGO.transform, false);
        nameGO.AddComponent<LayoutElement>().flexibleWidth = 1;
        var nameVLG = nameGO.AddComponent<VerticalLayoutGroup>();
        nameVLG.childAlignment = TextAnchor.MiddleLeft;
        nameVLG.childControlWidth = true;
        nameVLG.childControlHeight = true;
        nameVLG.childForceExpandWidth = true;
        nameVLG.childForceExpandHeight = false;
        nameVLG.spacing = -2;

        var nameTMP = MakeTMP(nameGO.transform, planet.planetName, IsPhoneLayout() ? 17 : 21, col, true);
        nameTMP.alignment = TextAlignmentOptions.Left;
        nameTMP.enableWordWrapping = false;
        nameTMP.overflowMode = TextOverflowModes.Ellipsis;
        nameTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = IsPhoneLayout() ? 22 : 26;

        var metaTMP = MakeTMP(nameGO.transform, synth.ModelLabel + "  /  " + planet.trajectoryType,
            IsPhoneLayout() ? 9 : 11, TextDim);
        metaTMP.alignment = TextAlignmentOptions.Left;
        metaTMP.enableWordWrapping = false;
        metaTMP.overflowMode = TextOverflowModes.Ellipsis;
        metaTMP.gameObject.AddComponent<LayoutElement>().preferredHeight = IsPhoneLayout() ? 14 : 16;

        var togGO = new GameObject("Tog");
        togGO.transform.SetParent(topGO.transform, false);
        var togLE = togGO.AddComponent<LayoutElement>();
        togLE.preferredWidth = IsPhoneLayout() ? 48 : 64;
        togLE.preferredHeight = IsPhoneLayout() ? 30 : 34;
        togLE.flexibleWidth = 0;
        var togImg = togGO.AddComponent<Image>();
        togImg.sprite = roundedSprite; togImg.type = Image.Type.Sliced;
        togImg.color = planet.isActive ? ActiveColor : InactiveColor;
        var togBtn = togGO.AddComponent<Button>();
        var togTMP = MakeTMP(togGO.transform, planet.isActive ? "ON" : "MUTE", IsPhoneLayout() ? 11 : 15, Color.white, true);
        togTMP.alignment = TextAlignmentOptions.Center;
        var togTMPRT = togTMP.GetComponent<RectTransform>();
        togTMPRT.anchorMin = Vector2.zero; togTMPRT.anchorMax = Vector2.one;
        togTMPRT.offsetMin = togTMPRT.offsetMax = Vector2.zero;

        toggles.Add((togImg, togTMP, planet, synth));

        togBtn.onClick.AddListener(() =>
        {
            planet.isActive = !planet.isActive;
            SetPlanetActive(planet, synth, planet.isActive);
            RefreshToggleVisual(togImg, togTMP, planet.isActive);
        });

        var soloGO = new GameObject("Solo");
        soloGO.transform.SetParent(topGO.transform, false);
        var soloLE = soloGO.AddComponent<LayoutElement>();
        soloLE.preferredWidth = IsPhoneLayout() ? 48 : 64;
        soloLE.preferredHeight = IsPhoneLayout() ? 30 : 34;
        soloLE.flexibleWidth = 0;
        var soloImg = soloGO.AddComponent<Image>();
        soloImg.sprite = roundedSprite; soloImg.type = Image.Type.Sliced;
        soloImg.color = CardLineColor;
        var soloBtn = soloGO.AddComponent<Button>();
        var soloTMP = MakeTMP(soloGO.transform, "SOLO", IsPhoneLayout() ? 11 : 14, Color.white, true);
        soloTMP.alignment = TextAlignmentOptions.Center;
        var soloTMPRT = soloTMP.GetComponent<RectTransform>();
        soloTMPRT.anchorMin = Vector2.zero; soloTMPRT.anchorMax = Vector2.one;
        soloTMPRT.offsetMin = soloTMPRT.offsetMax = Vector2.zero;
        soloBtn.onClick.AddListener(() =>
        {
            SoloPlanet(planet, synth);
        });

        // Detail section (hidden by default)
        var detailGO = new GameObject("Detail");
        detailGO.transform.SetParent(card.transform, false);
        var detailVLG = detailGO.AddComponent<VerticalLayoutGroup>();
        detailVLG.spacing              = 5;
        detailVLG.childControlWidth    = true;
        detailVLG.childControlHeight   = true;
        detailVLG.childForceExpandWidth  = true;
        detailVLG.childForceExpandHeight = false;
        detailGO.SetActive(false);
        planetDetails.Add(detailGO);

        AddModelSummary(detailGO.transform, synth);
        AddMeshRirTuneRows(detailGO.transform, synth);

        // Edit button → enters edit mode for this planet
        int capturedIdx = idx;
        Transform editParent = IsPhoneLayout() ? topGO.transform : card.transform;
        var editBtn = MakeRoundedButton(editParent, "TUNE",
            IsPhoneLayout() ? new Vector2(50, 30) : new Vector2(78, 32),
            IsPhoneLayout() ? Warm : CardLineColor, () =>
        {
            EnterEditMode(capturedIdx);
        });
        editButtons.Add(editBtn);
    }

    // ─── Edit Mode ───────────────────────────────────────────────────────

    void EnterEditMode(int idx)
    {
        editingIdx = idx;
        listHeaderGO?.SetActive(false);
        editHeaderGO?.SetActive(true);
        if (editHeaderTitle != null) editHeaderTitle.text = "← " + manager.Planets[idx].planetName;

        for (int i = 0; i < planetCards.Count; i++)
            planetCards[i].SetActive(i == idx);

        planetDetails[idx].SetActive(true);
        if (idx < editButtons.Count) editButtons[idx].SetActive(false);

        // Back button only visible when menu is currently open
        backBtnRT?.gameObject.SetActive(isMenuOpen);

        var selectedCardLE = planetCards[idx].GetComponent<LayoutElement>()
                  ?? planetCards[idx].AddComponent<LayoutElement>();
        selectedCardLE.preferredHeight = IsPhoneLayout() ? 430 : -1;
        selectedCardLE.minHeight = IsPhoneLayout() ? 360 : 0;

        // Save original content anchoring then stretch to fill viewport on large screens.
        if (!contentAnchorSaved)
        {
            contentOrigAnchorMin = content.anchorMin;
            contentOrigAnchorMax = content.anchorMax;
            contentOrigOffsetMin = content.offsetMin;
            contentOrigOffsetMax = content.offsetMax;
            contentAnchorSaved   = true;
        }
        var csf = content.GetComponent<ContentSizeFitter>();
        if (IsPhoneLayout())
        {
            if (csf != null) csf.enabled = true;
        }
        else
        {
            if (csf != null) csf.enabled = false;
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = content.offsetMax = Vector2.zero;
            selectedCardLE.flexibleHeight = 1;
        }

        var cardVLG = planetCards[idx].GetComponent<VerticalLayoutGroup>();
        if (cardVLG != null) cardVLG.childForceExpandHeight = !IsPhoneLayout();

        // Detail rows expand on desktop; iPhone keeps stable row heights and scrolls.
        var detailVLG = planetDetails[idx].GetComponent<VerticalLayoutGroup>();
        if (detailVLG != null) detailVLG.childForceExpandHeight = !IsPhoneLayout();
        foreach (Transform child in planetDetails[idx].transform)
        {
            var rLE = child.GetComponent<LayoutElement>() ?? child.gameObject.AddComponent<LayoutElement>();
            rLE.flexibleHeight = IsPhoneLayout() ? 0 : 1;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        StartCoroutine(RebuildNextFrame());
    }

    void ExitEditMode()
    {
        editingIdx = -1;
        // Returning to list view — menu still visible, keep blocking camera.
        CameraController.Blocked = isMenuOpen;
        listHeaderGO?.SetActive(true);
        editHeaderGO?.SetActive(false);

        foreach (var c in planetCards)   c.SetActive(true);
        foreach (var d in planetDetails) d.SetActive(false);
        foreach (var e in editButtons)   e.SetActive(true);

        backBtnRT?.gameObject.SetActive(false);

        // Restore all card layout settings
        foreach (var c in planetCards)
        {
            var cLE = c.GetComponent<LayoutElement>();
            if (cLE != null)
            {
                cLE.flexibleHeight = 0;
                cLE.preferredHeight = IsPhoneLayout() ? 70 : 94;
                cLE.minHeight = cLE.preferredHeight;
            }
            var cVLG = c.GetComponent<VerticalLayoutGroup>();
            if (cVLG != null) cVLG.childForceExpandHeight = false;
        }
        foreach (var d in planetDetails)
        {
            var dVLG = d.GetComponent<VerticalLayoutGroup>();
            if (dVLG != null) dVLG.childForceExpandHeight = false;
            foreach (Transform child in d.transform)
            {
                var rLE = child.GetComponent<LayoutElement>();
                if (rLE != null) rLE.flexibleHeight = -1;
            }
        }

        // Restore content sizing
        if (contentAnchorSaved)
        {
            var csf = content.GetComponent<ContentSizeFitter>();
            if (csf != null) csf.enabled = true;
            content.anchorMin = contentOrigAnchorMin;
            content.anchorMax = contentOrigAnchorMax;
            content.offsetMin = contentOrigOffsetMin;
            content.offsetMax = contentOrigOffsetMax;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        StartCoroutine(RebuildNextFrame());
    }

    // ─── Hamburger Button ────────────────────────────────────────────────

    void CreateHamburgerButton()
    {
        var rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        var parent = rootCanvas != null ? rootCanvas.transform : transform;

        // Remove old buttons
        var oldH = parent.Find("HamburgerBtn");
        if (oldH != null) Destroy(oldH.gameObject);
        var oldB = parent.Find("BackFloatBtn");
        if (oldB != null) Destroy(oldB.gameObject);
        var oldM = parent.Find("MuteBtn");
        if (oldM != null) Destroy(oldM.gameObject);
        var oldO = parent.Find("OriginBtn");
        if (oldO != null) Destroy(oldO.gameObject);

        // ── Hamburger (≡) ──
        var btnGO = new GameObject("HamburgerBtn");
        btnGO.transform.SetParent(parent, false);
        btnGO.transform.SetAsLastSibling();

        var rt = btnGO.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-24f, 24f);
        rt.sizeDelta = new Vector2(68f, 68f);
        hamburgerRT = rt;

        var img = btnGO.AddComponent<Image>();
        img.sprite = roundedSprite; img.type = Image.Type.Sliced;
        img.color = CardColor;

        float[] lineYs = { 17f, 0f, -17f };
        foreach (var lineY in lineYs)
        {
            var l = new GameObject("L");
            l.transform.SetParent(btnGO.transform, false);
            var lRT = l.AddComponent<RectTransform>();
            lRT.anchorMin = lRT.anchorMax = new Vector2(0.5f, 0.5f);
            lRT.pivot = new Vector2(0.5f, 0.5f);
            lRT.sizeDelta = new Vector2(36f, 5f);
            lRT.anchoredPosition = new Vector2(0f, lineY);
            var lImg = l.AddComponent<Image>();
            lImg.color = new Color(1f, 0.67f, 0.27f);
            lImg.raycastTarget = false;
        }

        var btn = btnGO.AddComponent<Button>();
        btn.onClick.AddListener(() =>
        {
            isMenuOpen = !isMenuOpen;
            menuPanel?.SetActive(isMenuOpen);
            CameraController.Blocked = isMenuOpen;
            img.color = isMenuOpen
                ? CardColor
                : ActiveColor;
            // back button only when menu open + in edit mode
            backBtnRT?.gameObject.SetActive(isMenuOpen && editingIdx >= 0);
            // mute + zoom slider only when menu is hidden
            muteBtnRT?.gameObject.SetActive(!isMenuOpen);
            originBtnRT?.gameObject.SetActive(!isMenuOpen);
            zoomSliderRT?.gameObject.SetActive(!isMenuOpen);
            // sync slider to current camera distance when it becomes visible
            if (!isMenuOpen && zoomSlider != null)
            {
                var cam = Camera.main?.GetComponent<CameraController>();
                if (cam != null)
                {
                    float syncVal = zoomSlider.maxValue - cam.ZoomDistance;
                    zoomSlider.SetValueWithoutNotify(
                        Mathf.Clamp(syncVal, zoomSlider.minValue, zoomSlider.maxValue));
                }
            }
        });

        // ── Back button (← floats above hamburger, hidden until edit mode) ──
        var backGO = new GameObject("BackFloatBtn");
        backGO.transform.SetParent(parent, false);
        backGO.transform.SetAsLastSibling();
        backGO.SetActive(false);

        var bRT = backGO.AddComponent<RectTransform>();
        bRT.anchorMin = bRT.anchorMax = new Vector2(1f, 0f);
        bRT.pivot = new Vector2(1f, 0f);
        bRT.sizeDelta = new Vector2(68f, 68f);
        // anchoredPosition set in SnapHamburgerToViewport
        backBtnRT = bRT;

        var bImg = backGO.AddComponent<Image>();
        bImg.sprite = roundedSprite; bImg.type = Image.Type.Sliced;
        bImg.color = ActiveColor;

        var bTMP = MakeTMP(backGO.transform, "←", 36, Color.white, true);
        bTMP.alignment = TextAlignmentOptions.Center;
        bTMP.verticalAlignment = VerticalAlignmentOptions.Middle;
        var bTMPRT = bTMP.GetComponent<RectTransform>();
        bTMPRT.anchorMin = Vector2.zero; bTMPRT.anchorMax = Vector2.one;
        bTMPRT.offsetMin = bTMPRT.offsetMax = Vector2.zero;

        var bBtn = backGO.AddComponent<Button>();
        bBtn.onClick.AddListener(ExitEditMode);

        // ── Mute button (♪ / muted, bottom-left, visible only when menu hidden) ──
        var muteGO = new GameObject("MuteBtn");
        muteGO.transform.SetParent(parent, false);
        muteGO.transform.SetAsLastSibling();
        muteGO.SetActive(false); // hidden while menu is open

        var mRT = muteGO.AddComponent<RectTransform>();
        mRT.anchorMin = mRT.anchorMax = new Vector2(1f, 0f); // placeholder, overridden in snap
        mRT.pivot     = new Vector2(1f, 0f);
        mRT.sizeDelta = new Vector2(68f, 68f);
        mRT.anchoredPosition = new Vector2(-24f, 102f); // placeholder
        muteBtnRT = mRT;

        var mImg = muteGO.AddComponent<Image>();
        mImg.sprite = roundedSprite; mImg.type = Image.Type.Sliced;
        mImg.color = ActiveColor;

        // Music note icon built from two Images
        foreach (var notePos in new[] { new Vector2(-5f, 4f), new Vector2(9f, 8f) })
        {
            var n = new GameObject("Note");
            n.transform.SetParent(muteGO.transform, false);
            var nRT = n.AddComponent<RectTransform>();
            nRT.anchorMin = nRT.anchorMax = new Vector2(0.5f, 0.5f);
            nRT.pivot = new Vector2(0.5f, 0.5f);
            nRT.sizeDelta = new Vector2(5f, 22f);
            nRT.anchoredPosition = notePos;
            n.AddComponent<Image>().color = Color.white;
            n.GetComponent<Image>().raycastTarget = false;
        }
        // crossout slash (shown when muted)
        var slashGO = new GameObject("MuteSlash");
        slashGO.transform.SetParent(muteGO.transform, false);
        var slashRT = slashGO.AddComponent<RectTransform>();
        slashRT.anchorMin = slashRT.anchorMax = new Vector2(0.5f, 0.5f);
        slashRT.pivot = new Vector2(0.5f, 0.5f);
        slashRT.sizeDelta = new Vector2(58f, 5f);
        slashRT.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var slashImg = slashGO.AddComponent<Image>();
        slashImg.color = new Color(1f, 0.3f, 0.3f, 0.95f);
        slashImg.raycastTarget = false;
        slashGO.SetActive(false);
        muteLine = slashGO;

        var mBtn = muteGO.AddComponent<Button>();
        mBtn.onClick.AddListener(() =>
        {
            isMuted = !isMuted;
            if (isMuted)
            {
                lastVol = AudioListener.volume > 0.01f ? AudioListener.volume : lastVol;
                AudioListener.volume = 0f;
                mImg.color = InactiveColor;
            }
            else
            {
                AudioListener.volume = lastVol;
                mImg.color = ActiveColor;
            }
            muteLine?.SetActive(isMuted);
        });

        // Listening reference reset, visible when the menu is collapsed.
        var originGO = new GameObject("OriginBtn");
        originGO.transform.SetParent(parent, false);
        originGO.transform.SetAsLastSibling();
        originGO.SetActive(false);

        var oRT = originGO.AddComponent<RectTransform>();
        oRT.anchorMin = oRT.anchorMax = new Vector2(1f, 0f);
        oRT.pivot = new Vector2(1f, 0f);
        oRT.sizeDelta = new Vector2(68f, 68f);
        originBtnRT = oRT;

        var oImg = originGO.AddComponent<Image>();
        oImg.sprite = roundedSprite;
        oImg.type = Image.Type.Sliced;
        oImg.color = CardColor;

        var oTMP = MakeTMP(originGO.transform, "HOME", 13, Warm, true);
        oTMP.alignment = TextAlignmentOptions.Center;
        oTMP.verticalAlignment = VerticalAlignmentOptions.Middle;
        oTMP.enableWordWrapping = false;
        var oTMPRT = oTMP.GetComponent<RectTransform>();
        oTMPRT.anchorMin = Vector2.zero;
        oTMPRT.anchorMax = Vector2.one;
        oTMPRT.offsetMin = oTMPRT.offsetMax = Vector2.zero;

        var oBtn = originGO.AddComponent<Button>();
        oBtn.onClick.AddListener(ResetListeningView);

        CreateZoomSlider(parent);
    }

    void ResetListeningView()
    {
        var cam = Camera.main?.GetComponent<CameraController>();
        if (cam == null)
            return;

        cam.ResetView();
        if (zoomSlider != null)
        {
            float syncVal = zoomSlider.maxValue - cam.ZoomDistance;
            zoomSlider.SetValueWithoutNotify(
                Mathf.Clamp(syncVal, zoomSlider.minValue, zoomSlider.maxValue));
        }
    }

    void CreateZoomSlider(Transform parent)
    {
        var old = parent.Find("ZoomSliderBar");
        if (old != null) Destroy(old.gameObject);

        var camCtrl = Camera.main?.GetComponent<CameraController>();

        var barGO = new GameObject("ZoomSliderBar");
        barGO.transform.SetParent(parent, false);
        barGO.transform.SetAsLastSibling();
        barGO.SetActive(false);

        // Position/size set by SnapHamburgerToViewport after layout
        var barRT = barGO.AddComponent<RectTransform>();
        barRT.anchorMin = barRT.anchorMax = new Vector2(0.5f, 0f);
        barRT.pivot     = new Vector2(0.5f, 0f);
        barRT.sizeDelta = new Vector2(300f, 56f); // placeholder
        zoomSliderRT = barRT;

        // Transparent Image on the container so any touch on the bar is raycasted as UI
        var barBlockImg = barGO.AddComponent<Image>();
        barBlockImg.color = new Color(0f, 0f, 0f, 0.01f);

        // Slider fills the full bar — no layout group needed
        var sliderGO = new GameObject("ZSlider");
        sliderGO.transform.SetParent(barGO.transform, false);
        var sliderRT = sliderGO.AddComponent<RectTransform>();
        sliderRT.anchorMin = Vector2.zero; sliderRT.anchorMax = Vector2.one;
        sliderRT.offsetMin = sliderRT.offsetMax = Vector2.zero;

        var slider = sliderGO.AddComponent<Slider>();
        slider.minValue  = 0f;
        slider.maxValue  = 950f;
        slider.value     = 0f;
        slider.direction = Slider.Direction.LeftToRight;
        zoomSlider = slider;

        // Track
        var bgGO = new GameObject("BG");
        bgGO.transform.SetParent(sliderGO.transform, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0, 0.5f); bgRT.anchorMax = new Vector2(1, 0.5f);
        bgRT.sizeDelta = new Vector2(0, 5);
        bgGO.AddComponent<Image>().color = new Color(0.22f, 0.22f, 0.28f, 0.9f);

        // Fill
        var faGO = new GameObject("FA");
        faGO.transform.SetParent(sliderGO.transform, false);
        var faRT = faGO.AddComponent<RectTransform>();
        faRT.anchorMin = new Vector2(0, 0.5f); faRT.anchorMax = new Vector2(1, 0.5f);
        faRT.sizeDelta = new Vector2(-8, 5); faRT.anchoredPosition = new Vector2(-4, 0);
        var fGO = new GameObject("F");
        fGO.transform.SetParent(faGO.transform, false);
        var fRT = fGO.AddComponent<RectTransform>();
        fRT.anchorMin = Vector2.zero; fRT.anchorMax = new Vector2(0, 1);
        fRT.sizeDelta = new Vector2(8, 0);
        fGO.AddComponent<Image>().color = new Color(0.39f, 0.86f, 1f, 0.7f);
        slider.fillRect = fRT;

        // Handle
        var haGO = new GameObject("HA");
        haGO.transform.SetParent(sliderGO.transform, false);
        var haRT = haGO.AddComponent<RectTransform>();
        haRT.anchorMin = Vector2.zero; haRT.anchorMax = Vector2.one;
        haRT.sizeDelta = new Vector2(-20, 0);
        var hGO = new GameObject("H");
        hGO.transform.SetParent(haGO.transform, false);
        var hRT = hGO.AddComponent<RectTransform>();
        hRT.sizeDelta = new Vector2(38, 38);
        hRT.anchorMin = new Vector2(0, 0.5f); hRT.anchorMax = new Vector2(0, 0.5f);
        hRT.pivot = new Vector2(0.5f, 0.5f);
        var hImg = hGO.AddComponent<Image>();
        hImg.sprite = circleSprite; hImg.color = Color.white; hImg.preserveAspect = true;
        slider.handleRect = hRT; slider.targetGraphic = hImg;

        // ── Crescendo hairpin — initial positions are placeholders;
        //    actual span is set in SnapHamburgerToViewport once width is known
        crescendoLineRTs[0] = MakeCrescendoLine(sliderGO.transform, new Vector2(5f, 0f), new Vector2(55f,  20f), 3.5f);
        crescendoLineRTs[1] = MakeCrescendoLine(sliderGO.transform, new Vector2(5f, 0f), new Vector2(55f, -20f), 3.5f);

        if (camCtrl != null)
            slider.onValueChanged.AddListener(v => camCtrl.ZoomDistance = slider.maxValue - v);
    }

    // Lines anchored to left-center of parent so coords are from the left edge
    RectTransform MakeCrescendoLine(Transform parent, Vector2 from, Vector2 to, float thickness)
    {
        var go  = new GameObject("CLine");
        go.transform.SetParent(parent, false);
        var dir = to - from;
        var rt  = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f); // left-center
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(dir.magnitude, thickness);
        rt.anchoredPosition = (from + to) * 0.5f;
        rt.localRotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.85f);
        img.raycastTarget = false;
        return rt;
    }

    void UpdateCrescendoLine(RectTransform rt, Vector2 from, Vector2 to)
    {
        if (rt == null) return;
        var dir = to - from;
        rt.sizeDelta = new Vector2(dir.magnitude, 3.5f);
        rt.anchoredPosition = (from + to) * 0.5f;
        rt.localRotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    void SnapHamburgerToViewport()
    {
        if (hamburgerRT == null || content == null) return;

        var rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (rootCanvas == null) return;
        var canvasRT = rootCanvas.GetComponent<RectTransform>();

        Vector2 brPt = new Vector2(canvasRT.rect.xMax - 20f, canvasRT.rect.yMin + 20f);
        Vector2 blPt = new Vector2(canvasRT.rect.xMin + 20f, canvasRT.rect.yMin + 20f);

        hamburgerRT.anchorMin = hamburgerRT.anchorMax = new Vector2(0.5f, 0.5f);
        hamburgerRT.pivot = new Vector2(1f, 0f);
        hamburgerRT.anchoredPosition = brPt;

        // Back button — directly above hamburger (edit mode + menu open)
        if (backBtnRT != null)
        {
            backBtnRT.anchorMin = backBtnRT.anchorMax = new Vector2(0.5f, 0.5f);
            backBtnRT.pivot = new Vector2(1f, 0f);
            backBtnRT.anchoredPosition = brPt + new Vector2(0f, 78f);
        }

        // Mute button — same slot as back button (mutually exclusive visibility)
        if (muteBtnRT != null)
        {
            muteBtnRT.anchorMin = muteBtnRT.anchorMax = new Vector2(0.5f, 0.5f);
            muteBtnRT.pivot = new Vector2(1f, 0f);
            muteBtnRT.anchoredPosition = brPt + new Vector2(0f, 78f);
        }

        if (originBtnRT != null)
        {
            originBtnRT.anchorMin = originBtnRT.anchorMax = new Vector2(0.5f, 0.5f);
            originBtnRT.pivot = new Vector2(1f, 0f);
            originBtnRT.anchoredPosition = brPt + new Vector2(-78f, 78f);
        }

        // Zoom slider — full viewport width, above mute button
        if (zoomSliderRT != null)
        {
            float viewportW  = brPt.x - blPt.x;
            float centerX    = (brPt.x + blPt.x) * 0.5f;
            // hamburger (68) + gap (10) = 78, then mute (68) + gap (14) = 160
            float sliderBase = brPt.y + 78f + 68f + 14f;

            zoomSliderRT.anchorMin = zoomSliderRT.anchorMax = new Vector2(0.5f, 0.5f);
            zoomSliderRT.pivot     = new Vector2(0.5f, 0f);
            zoomSliderRT.sizeDelta = new Vector2(viewportW - 20f, 56f);
            zoomSliderRT.anchoredPosition = new Vector2(centerX, sliderBase);

            // Update crescendo hairpin to span the full track width
            float sw = viewportW - 20f;
            UpdateCrescendoLine(crescendoLineRTs[0], new Vector2(5f, 0f), new Vector2(sw - 5f,  22f));
            UpdateCrescendoLine(crescendoLineRTs[1], new Vector2(5f, 0f), new Vector2(sw - 5f, -22f));
        }
    }

    // ─── Widget helpers ──────────────────────────────────────────────────

    IEnumerator RebuildNextFrame()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    void SetPlanetActive(PlanetOrbit p, FMSynthesizer s, bool active)
    {
        p.isActive = active;
        s.SetActive(active);
    }

    void SoloPlanet(PlanetOrbit planet, FMSynthesizer synth)
    {
        // Use manager lists as the canonical source so this works correctly even
        // when toggles has been cleared/replaced (e.g. inside BuildManualPhoneTunePage
        // which clears toggles and adds only a single entry for the edited planet).
        var planets = manager.Planets;
        var synths  = manager.Synths;

        if (_soloTarget == synth)
        {
            // ── Un-solo: restore every planet to its pre-solo active state ────
            for (int i = 0; i < planets.Count && i < _preSoloActives.Count; i++)
                SetPlanetActive(planets[i], synths[i], _preSoloActives[i]);
            _soloTarget = null;
            _preSoloActives.Clear();
        }
        else
        {
            // ── Solo: save states, mute everything except this planet ──────────
            _preSoloActives.Clear();
            foreach (var p in planets) _preSoloActives.Add(p.isActive);
            _soloTarget = synth;
            for (int i = 0; i < planets.Count; i++)
                SetPlanetActive(planets[i], synths[i], planets[i] == planet);
            allSelected = false;
        }

        // Refresh whatever toggle buttons are currently in the list.
        // In list view: all 9 card toggles. In tune page: just 1 detail toggle.
        // Either way, planet.isActive has already been updated above.
        foreach (var t in toggles)
            if (t.img != null)
                RefreshToggleVisual(t.img, t.txt, t.planet.isActive);
    }

    void RefreshToggleVisual(Image img, TextMeshProUGUI txt, bool active)
    {
        img.color = active ? ActiveColor : InactiveColor;
        txt.text = active ? "ON" : "MUTE";
    }

    GameObject CreateCard(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(content, false);
        var le = go.AddComponent<LayoutElement>();
        le.flexibleHeight = 0;
        var img = go.AddComponent<Image>();
        img.sprite = roundedSprite; img.type = Image.Type.Sliced;
        img.color = CardColor;
        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.padding              = IsPhoneLayout()
            ? new RectOffset(8, 8, 6, 6)
            : new RectOffset(9, 9, 7, 7);
        vlg.spacing              = IsPhoneLayout() ? 4 : 5;
        vlg.childControlWidth    = true;
        vlg.childControlHeight   = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        return go;
    }

    void MakeOrbitRow(Transform parent, PlanetOrbit planet)
    {
        var names = new string[] { "Ellip", "Circ", "Fig8", "Liss", "Rose" };
        var go = new GameObject("OrbitRow");
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 42; le.minHeight = 42;
        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 5;
        hlg.childControlHeight      = true;
        hlg.childForceExpandHeight  = true;
        hlg.childForceExpandWidth   = false;

        var lGO = new GameObject("Lbl");
        lGO.transform.SetParent(go.transform, false);
        lGO.AddComponent<LayoutElement>().preferredWidth = 62;
        var lGOLE = lGO.GetComponent<LayoutElement>(); lGOLE.flexibleWidth = 0;
        var lTMP = lGO.AddComponent<TextMeshProUGUI>();
        lTMP.text = "Path"; lTMP.fontSize = 16;
        lTMP.color = TextDim;
        lTMP.verticalAlignment = VerticalAlignmentOptions.Middle;
        lTMP.raycastTarget = false;

        var btnImgs = new Image[names.Length];
        var teal = ActiveColor;
        var dark = InactiveColor;

        for (int i = 0; i < names.Length; i++)
        {
            int idx = i;
            var bGO = new GameObject("OBtn_" + names[i]);
            bGO.transform.SetParent(go.transform, false);
            bGO.AddComponent<LayoutElement>().flexibleWidth = 1;
            var bImg = bGO.AddComponent<Image>();
            bImg.sprite = roundedSprite; bImg.type = Image.Type.Sliced;
            bImg.color = ((int)planet.trajectoryType == idx) ? teal : dark;
            btnImgs[i] = bImg;
            var bTMP = MakeTMP(bGO.transform, names[i], 13, Color.white);
            bTMP.alignment = TextAlignmentOptions.Center;
            bTMP.verticalAlignment = VerticalAlignmentOptions.Middle;
            var bRT = bTMP.GetComponent<RectTransform>();
            bRT.anchorMin = Vector2.zero; bRT.anchorMax = Vector2.one;
            bRT.offsetMin = bRT.offsetMax = Vector2.zero;
            var bBtn = bGO.AddComponent<Button>();
            bBtn.onClick.AddListener(() =>
            {
                planet.SetTrajectory((PlanetOrbit.TrajectoryType)idx);
                for (int j = 0; j < btnImgs.Length; j++)
                    btnImgs[j].color = (j == idx) ? teal : dark;
            });
        }
    }

    void AddModelSummary(Transform parent, FMSynthesizer synth)
    {
        var go = new GameObject("ModelSummary");
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 34;
        le.minHeight = 34;

        var img = go.AddComponent<Image>();
        img.sprite = roundedSprite;
        img.type = Image.Type.Sliced;
        img.color = new Color(0.035f, 0.065f, 0.1f, 0.82f);

        var tmp = MakeTMP(go.transform, synth.ModelLabel, IsPhoneLayout() ? 13 : 15, Accent, true);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        var rt = tmp.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(8f, 0f);
        rt.offsetMax = new Vector2(-8f, 0f);
    }

    void AddMeshRirTuneRows(Transform parent, FMSynthesizer synth)
    {
        var meshRir = synth.GetComponent<MeshRIRSpatializer>();
        var planet  = synth.GetComponent<PlanetOrbit>();

        // ── ORBIT ─────────────────────────────────────────────────────────────
        if (planet != null)
        {
            AddSectionLabel(parent, "ORBIT");
            // Speed: negative = reverse direction; sign encodes direction
            MakeSliderRow(parent, "Speed", -8f, 8f, planet.baseSpeed,
                v => planet.baseSpeed = v);
            MakeSliderRow(parent, "Inclin", -30f, 90f, planet.inclination,
                v => planet.SetInclination(v));
            MakeOrbitRow(parent, planet);
        }

        // ── SPACE ─────────────────────────────────────────────────────────────
        if (meshRir != null)
        {
            AddSectionLabel(parent, "SPACE");
            MakeSliderRow(parent, "Reverb",  0f, 1f, meshRir.energy,         v => meshRir.energy         = v);
            MakeSliderRow(parent, "Room",    0f, 1f, meshRir.depth,          v => meshRir.depth          = v);
            MakeSliderRow(parent, "Damp",    0f, 1f, meshRir.material,       v => meshRir.material       = v);
            MakeSliderRow(parent, "Density", 0f, 1f, meshRir.density,        v => meshRir.density        = v);
            MakeSliderRow(parent, "Sense",   0f, 1f, meshRir.flySense,    v => meshRir.flySense    = v);
            MakeSliderRow(parent, "Force",   0f, 2f, meshRir.flyStrength, v => meshRir.flyStrength = v);
        }

        // ── SOUND ─────────────────────────────────────────────────────────────
        AddSectionLabel(parent, "SOUND");
        AddModelSynthRows(parent, synth);
    }

    // Thin label bar between parameter groups
    void AddSectionLabel(Transform parent, string text)
    {
        var go = new GameObject("SecLabel_" + text);
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 22; le.minHeight = 22;
        var tmp = MakeTMP(go.transform, text, 9, TextDim, false);
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        tmp.characterSpacing = 2f;
        var rt = tmp.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(4f, 0f); rt.offsetMax = new Vector2(-4f, 0f);
    }

    // Two synthesis sliders whose labels and parameter targets depend on the model.
    void AddModelSynthRows(Transform parent, FMSynthesizer synth)
    {
        switch (synth.model)
        {
            case FMSynthesizer.PhysicalModel.Bubble:
                MakeSliderRow(parent, "Flow",   0f, 5f, synth.lfoRate,    v => synth.lfoRate    = v);
                MakeSliderRow(parent, "Pitch",  40f, 500f, synth.carrierNote, v => synth.carrierNote = v);
                break;
            case FMSynthesizer.PhysicalModel.SoftMetal:
                MakeSliderRow(parent, "Strike", 0f, 5f, synth.lfoRate,    v => synth.lfoRate    = v);
                MakeSliderRow(parent, "Ring",   0f, 10f, synth.modIndex,  v => synth.modIndex   = v);
                break;
            case FMSynthesizer.PhysicalModel.RunningWater:
                MakeSliderRow(parent, "Flow",   0f, 5f, synth.lfoRate,    v => synth.lfoRate    = v);
                MakeSliderRow(parent, "Turb",   0f, 10f, synth.modIndex,  v => synth.modIndex   = v);
                break;
            case FMSynthesizer.PhysicalModel.Fire:
                // Flame capped at 2.5 (was 5): high crack rate creates a sweeping
                // buzz that sounds too similar to the flyby spatial effect.
                MakeSliderRow(parent, "Flame",  0f, 2.5f, synth.lfoRate,  v => synth.lfoRate    = v);
                MakeSliderRow(parent, "Heat",   0f, 10f, synth.modIndex,  v => synth.modIndex   = v);
                break;
            case FMSynthesizer.PhysicalModel.Stone:
                MakeSliderRow(parent, "Roll",   0f, 5f, synth.lfoRate,    v => synth.lfoRate    = v);
                MakeSliderRow(parent, "Mass",   40f, 200f, synth.carrierNote, v => synth.carrierNote = v);
                break;
            case FMSynthesizer.PhysicalModel.WoodStickSlip:
                MakeSliderRow(parent, "Bow",    0f, 5f, synth.lfoRate,    v => synth.lfoRate    = v);
                MakeSliderRow(parent, "Tension",0f, 10f, synth.modIndex,  v => synth.modIndex   = v);
                break;
            case FMSynthesizer.PhysicalModel.IceMetal:
                MakeSliderRow(parent, "Rate",   0f, 5f, synth.lfoRate,    v => synth.lfoRate    = v);
                MakeSliderRow(parent, "Crystal",0.5f, 8f, synth.modRatio, v => synth.modRatio   = v);
                break;
            case FMSynthesizer.PhysicalModel.DeepPour:
                MakeSliderRow(parent, "Pour",   0f, 5f, synth.lfoRate,    v => synth.lfoRate    = v);
                MakeSliderRow(parent, "Depth",  40f, 400f, synth.carrierNote, v => synth.carrierNote = v);
                break;
            case FMSynthesizer.PhysicalModel.IceRain:
                MakeSliderRow(parent, "Rain",   0f, 5f, synth.lfoRate,    v => synth.lfoRate    = v);
                MakeSliderRow(parent, "Impact", 0f, 10f, synth.modIndex,  v => synth.modIndex   = v);
                break;
            default:
                MakeSliderRow(parent, "Flow",   0f, 5f, synth.lfoRate,    v => synth.lfoRate    = v);
                MakeSliderRow(parent, "Energy", 0f, 10f, synth.modIndex,  v => synth.modIndex   = v);
                break;
        }
    }

    void MakeOscRow(Transform parent, FMSynthesizer synth)
    {
        var names = new string[] { "Sine", "Tri", "Saw", "Sq" };
        var go = new GameObject("OscRow");
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 42; le.minHeight = 42;
        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 5;
        hlg.childControlHeight      = true;
        hlg.childForceExpandHeight  = true;
        hlg.childForceExpandWidth   = false;

        var lGO = new GameObject("Lbl");
        lGO.transform.SetParent(go.transform, false);
        var lLE = lGO.AddComponent<LayoutElement>();
        lLE.preferredWidth = 62; lLE.flexibleWidth = 0;
        var lTMP = lGO.AddComponent<TextMeshProUGUI>();
        lTMP.text = "Wave"; lTMP.fontSize = 16;
        lTMP.color = TextDim;
        lTMP.verticalAlignment = VerticalAlignmentOptions.Middle;
        lTMP.raycastTarget = false;

        var btnImgs = new Image[names.Length];
        var teal = ActiveColor;
        var dark = InactiveColor;

        for (int i = 0; i < names.Length; i++)
        {
            int idx = i;
            var bGO = new GameObject("OscBtn_" + names[i]);
            bGO.transform.SetParent(go.transform, false);
            bGO.AddComponent<LayoutElement>().flexibleWidth = 1;
            var bImg = bGO.AddComponent<Image>();
            bImg.sprite = roundedSprite; bImg.type = Image.Type.Sliced;
            bImg.color = ((int)synth.oscType == idx) ? teal : dark;
            btnImgs[i] = bImg;
            var bTMP = MakeTMP(bGO.transform, names[i], 13, Color.white);
            bTMP.alignment = TextAlignmentOptions.Center;
            bTMP.verticalAlignment = VerticalAlignmentOptions.Middle;
            var bRT = bTMP.GetComponent<RectTransform>();
            bRT.anchorMin = Vector2.zero; bRT.anchorMax = Vector2.one;
            bRT.offsetMin = bRT.offsetMax = Vector2.zero;
            var bBtn = bGO.AddComponent<Button>();
            bBtn.onClick.AddListener(() =>
            {
                synth.oscType = (FMSynthesizer.OscType)idx;
                for (int j = 0; j < btnImgs.Length; j++)
                    btnImgs[j].color = (j == idx) ? teal : dark;
            });
        }
    }

    void MakeToggleRow(Transform parent, string label,
        bool initialVal, System.Action<bool> onChange)
    {
        var go = new GameObject("TR_" + label);
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 42; le.minHeight = 42;
        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 5;
        hlg.childControlHeight      = true;
        hlg.childForceExpandHeight  = true;
        hlg.childForceExpandWidth   = false;

        var lGO = new GameObject("Lbl");
        lGO.transform.SetParent(go.transform, false);
        var lLE = lGO.AddComponent<LayoutElement>();
        lLE.preferredWidth = 76; lLE.flexibleWidth = 0;
        var lTMP = lGO.AddComponent<TextMeshProUGUI>();
        lTMP.text = label; lTMP.fontSize = 16;
        lTMP.color = TextDim;
        lTMP.verticalAlignment = VerticalAlignmentOptions.Middle;
        lTMP.raycastTarget = false;

        var state = new bool[] { initialVal };

        var btnGO = new GameObject("Btn");
        btnGO.transform.SetParent(go.transform, false);
        var btnLE = btnGO.AddComponent<LayoutElement>();
        btnLE.preferredWidth = 70; btnLE.preferredHeight = 34; btnLE.flexibleWidth = 0;
        var btnImg = btnGO.AddComponent<Image>();
        btnImg.sprite = roundedSprite; btnImg.type = Image.Type.Sliced;
        btnImg.color = state[0] ? ActiveColor : InactiveColor;
        var btnBtn = btnGO.AddComponent<Button>();
        var btnTMP = MakeTMP(btnGO.transform, state[0] ? "ON" : "OFF", 15, Color.white, true);
        btnTMP.alignment = TextAlignmentOptions.Center;
        var btnTMPRT = btnTMP.GetComponent<RectTransform>();
        btnTMPRT.anchorMin = Vector2.zero; btnTMPRT.anchorMax = Vector2.one;
        btnTMPRT.offsetMin = btnTMPRT.offsetMax = Vector2.zero;

        btnBtn.onClick.AddListener(() =>
        {
            state[0] = !state[0];
            onChange?.Invoke(state[0]);
            btnImg.color = state[0] ? ActiveColor : InactiveColor;
            btnTMP.text = state[0] ? "ON" : "OFF";
        });
    }

    void MakeSliderRow(Transform parent, string label,
        float min, float max, float val,
        System.Action<float> onChange)
    {
        var go = new GameObject("SR_" + label);
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 42; le.minHeight = 42;
        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 5;
        hlg.childControlHeight      = true;
        hlg.childForceExpandHeight  = true;
        hlg.childForceExpandWidth   = false;

        var lGO = new GameObject("L");
        lGO.transform.SetParent(go.transform, false);
        var lLE = lGO.AddComponent<LayoutElement>();
        lLE.preferredWidth = 76; lLE.flexibleWidth = 0;
        var lTMP = lGO.AddComponent<TextMeshProUGUI>();
        lTMP.text = label; lTMP.fontSize = 16;
        lTMP.color = TextDim;
        lTMP.alignment = TextAlignmentOptions.Left;
        lTMP.verticalAlignment = VerticalAlignmentOptions.Middle;
        lTMP.raycastTarget = false;

        var sGO = new GameObject("Sl");
        sGO.transform.SetParent(go.transform, false);
        sGO.AddComponent<LayoutElement>().flexibleWidth = 1;
        var slider = sGO.AddComponent<Slider>();
        slider.minValue = min; slider.maxValue = max;
        slider.value = val; slider.direction = Slider.Direction.LeftToRight;

        var bgGO = new GameObject("BG");
        bgGO.transform.SetParent(sGO.transform, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0, 0.5f); bgRT.anchorMax = new Vector2(1, 0.5f);
        bgRT.sizeDelta = new Vector2(0, 4);
        bgGO.AddComponent<Image>().color = CardLineColor;

        var faGO = new GameObject("FA");
        faGO.transform.SetParent(sGO.transform, false);
        var faRT = faGO.AddComponent<RectTransform>();
        faRT.anchorMin = new Vector2(0, 0.5f); faRT.anchorMax = new Vector2(1, 0.5f);
        faRT.sizeDelta = new Vector2(-8, 4); faRT.anchoredPosition = new Vector2(-4, 0);
        var fGO = new GameObject("F");
        fGO.transform.SetParent(faGO.transform, false);
        var fRT = fGO.AddComponent<RectTransform>();
        fRT.anchorMin = Vector2.zero; fRT.anchorMax = new Vector2(0, 1);
        fRT.sizeDelta = new Vector2(8, 0);
        fGO.AddComponent<Image>().color = Accent;
        slider.fillRect = fRT;

        var haGO = new GameObject("HA");
        haGO.transform.SetParent(sGO.transform, false);
        var haRT = haGO.AddComponent<RectTransform>();
        haRT.anchorMin = Vector2.zero; haRT.anchorMax = Vector2.one;
        haRT.sizeDelta = new Vector2(-24, 0);
        var hGO = new GameObject("H");
        hGO.transform.SetParent(haGO.transform, false);
        var hRT = hGO.AddComponent<RectTransform>();
        hRT.sizeDelta = new Vector2(30, 30);
        hRT.anchorMin = new Vector2(0, 0.5f); hRT.anchorMax = new Vector2(0, 0.5f);
        hRT.pivot = new Vector2(0.5f, 0.5f);
        var hImg = hGO.AddComponent<Image>();
        hImg.sprite = circleSprite;
        hImg.color = Accent;
        hImg.preserveAspect = true;
        slider.handleRect = hRT; slider.targetGraphic = hImg;

        var vGO = new GameObject("Val");
        vGO.transform.SetParent(go.transform, false);
        var vLE = vGO.AddComponent<LayoutElement>();
        vLE.preferredWidth = 44; vLE.flexibleWidth = 0;
        var vTMP = vGO.AddComponent<TextMeshProUGUI>();
        vTMP.text = val.ToString("F1"); vTMP.fontSize = 15;
        vTMP.color = Warm;
        vTMP.alignment = TextAlignmentOptions.Right;
        vTMP.verticalAlignment = VerticalAlignmentOptions.Middle;
        vTMP.raycastTarget = false;

        slider.onValueChanged.AddListener(v =>
        {
            vTMP.text = v.ToString("F1");
            onChange?.Invoke(v);
        });
    }

    void MakeLargeSlider(Transform parent, float min, float max,
        float val, System.Action<float> onChange)
    {
        var go = new GameObject("LargeSlider");
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 30; le.minHeight = 30;

        var slider = go.AddComponent<Slider>();
        slider.minValue = min; slider.maxValue = max; slider.value = val;

        var bgGO = new GameObject("BG");
        bgGO.transform.SetParent(go.transform, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0, 0.5f); bgRT.anchorMax = new Vector2(1, 0.5f);
        bgRT.sizeDelta = new Vector2(0, 5);
        bgGO.AddComponent<Image>().color = CardLineColor;

        var faGO = new GameObject("FA");
        faGO.transform.SetParent(go.transform, false);
        var faRT = faGO.AddComponent<RectTransform>();
        faRT.anchorMin = new Vector2(0, 0.5f); faRT.anchorMax = new Vector2(1, 0.5f);
        faRT.sizeDelta = new Vector2(-10, 5); faRT.anchoredPosition = new Vector2(-5, 0);
        var fGO = new GameObject("F");
        fGO.transform.SetParent(faGO.transform, false);
        var fRT = fGO.AddComponent<RectTransform>();
        fRT.anchorMin = Vector2.zero; fRT.anchorMax = new Vector2(0, 1);
        fRT.sizeDelta = new Vector2(10, 0);
        fGO.AddComponent<Image>().color = Accent;
        slider.fillRect = fRT;

        var haGO = new GameObject("HA");
        haGO.transform.SetParent(go.transform, false);
        var haRT = haGO.AddComponent<RectTransform>();
        haRT.anchorMin = Vector2.zero; haRT.anchorMax = Vector2.one;
        haRT.sizeDelta = new Vector2(-20, 0);

        var hGO = new GameObject("H");
        hGO.transform.SetParent(haGO.transform, false);
        var hRT = hGO.AddComponent<RectTransform>();
        hRT.sizeDelta = new Vector2(26, 26);
        hRT.anchorMin = new Vector2(0, 0.5f); hRT.anchorMax = new Vector2(0, 0.5f);
        hRT.pivot = new Vector2(0.5f, 0.5f);
        var hImg = hGO.AddComponent<Image>();
        hImg.sprite = circleSprite; hImg.color = Color.white; hImg.preserveAspect = true;
        slider.handleRect = hRT; slider.targetGraphic = hImg;

        slider.onValueChanged.AddListener(v => onChange?.Invoke(v));
    }

    GameObject MakeRoundedButton(Transform parent, string text,
        Vector2 size, Color col, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("Btn_" + text);
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = size.x; le.preferredHeight = size.y; le.flexibleWidth = 0;
        var img = go.AddComponent<Image>();
        img.sprite = roundedSprite; img.type = Image.Type.Sliced; img.color = col;
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);
        var tmp = MakeTMP(go.transform, text, 15, Color.white, true);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        var tmpRT = tmp.GetComponent<RectTransform>();
        tmpRT.anchorMin = Vector2.zero; tmpRT.anchorMax = Vector2.one;
        tmpRT.offsetMin = tmpRT.offsetMax = Vector2.zero;
        return go;
    }

    TextMeshProUGUI MakeTMP(Transform parent, string text,
        int size, Color col, bool bold = false)
    {
        var go = new GameObject("TMP_" + text);
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.color = col;
        if (bold) tmp.fontStyle = FontStyles.Bold;
        tmp.raycastTarget = false;
        return tmp;
    }

    IEnumerator ForceLayoutRebuild()
    {
        yield return null;
        yield return null;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        foreach (Transform child in content)
        {
            var rt = child.GetComponent<RectTransform>();
            if (rt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }
        yield return null;
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        SnapHamburgerToViewport();
    }

    Sprite CreateCircleSprite()
    {
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;
        float radius = size / 2f - 1f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dist  = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01(1f - (dist - radius + 1.5f));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    Sprite CreateRoundedRectSprite(int radius = 12)
    {
        int w = 64, h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int cx = Mathf.Clamp(x, radius, w - radius);
                int cy = Mathf.Clamp(y, radius, h - radius);
                float dist  = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                float alpha = Mathf.Clamp01(1f - (dist - radius + 1.5f));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }
}
