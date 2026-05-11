using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class SolarSystemUI : MonoBehaviour
{
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

    private GameObject scrollPanel;
    private Sprite circleSprite;
    private Sprite roundedSprite;
    private RectTransform hamburgerRT;
    private RectTransform backBtnRT;
    private RectTransform muteBtnRT;
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

    // Content anchor saved for edit-mode restore
    private Vector2 contentOrigAnchorMin, contentOrigAnchorMax;
    private Vector2 contentOrigOffsetMin, contentOrigOffsetMax;
    private bool contentAnchorSaved = false;

    void Start()
    {
        manager = SolarSystemManager.Instance;
        if (manager == null) return;

        circleSprite  = CreateCircleSprite();
        roundedSprite = CreateRoundedRectSprite(12);

        StartCoroutine(WaitAndBuild());
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

            foreach (Transform child in content) Destroy(child.gameObject);
            toggles.Clear();
            planetCards.Clear();
            planetDetails.Clear();
            editButtons.Clear();

            var sr = content.GetComponentInParent<ScrollRect>();
            if (sr != null)
            {
                scrollPanel = sr.gameObject;
                sr.scrollSensitivity = 40f;
                sr.horizontal = false;
                sr.vertical = true;
                sr.movementType = ScrollRect.MovementType.Clamped;
                sr.content = content;
            }

            var contentCSF = content.GetComponent<ContentSizeFitter>()
                ?? content.gameObject.AddComponent<ContentSizeFitter>();
            contentCSF.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            contentCSF.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var vlg = content.GetComponent<VerticalLayoutGroup>()
                   ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing              = 12f;
            vlg.padding              = new RectOffset(16, 16, 20, 20);
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

    // ─── Header ─────────────────────────────────────────────────────────

    void CreateHeader()
    {
        // List header — "Planet Sequencer"
        listHeaderGO = new GameObject("ListHeader");
        listHeaderGO.transform.SetParent(content, false);
        var le = listHeaderGO.AddComponent<LayoutElement>();
        le.preferredHeight = 72; le.minHeight = 72;
        var hlg = listHeaderGO.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment    = TextAnchor.MiddleLeft;
        hlg.spacing           = 8;
        hlg.padding           = new RectOffset(4, 80, 0, 0);
        hlg.childControlHeight      = true;
        hlg.childForceExpandWidth   = false;
        var title = MakeTMP(listHeaderGO.transform, "Planet Sequencer",
            42, new Color(1f, 0.67f, 0.27f), true);
        title.alignment = TextAlignmentOptions.Left;
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        // Edit header — "← PlanetName" (hidden until edit mode)
        editHeaderGO = new GameObject("EditHeader");
        editHeaderGO.transform.SetParent(content, false);
        var ele = editHeaderGO.AddComponent<LayoutElement>();
        ele.preferredHeight = 72; ele.minHeight = 72;
        editHeaderGO.SetActive(false);

        var backBtnGO = new GameObject("BackBtn");
        backBtnGO.transform.SetParent(editHeaderGO.transform, false);
        var backRT = backBtnGO.AddComponent<RectTransform>();
        backRT.anchorMin = Vector2.zero; backRT.anchorMax = Vector2.one;
        backRT.offsetMin = Vector2.zero; backRT.offsetMax = Vector2.zero;

        var backImg = backBtnGO.AddComponent<Image>();
        backImg.sprite = roundedSprite; backImg.type = Image.Type.Sliced;
        backImg.color = new Color(0.15f, 0.2f, 0.25f, 0.9f);

        editHeaderTitle = MakeTMP(backBtnGO.transform, "← ", 38,
            new Color(1f, 0.67f, 0.27f), true);
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

        var label = MakeTMP(go.transform, "Master Volume",
            30, new Color(0.75f, 0.75f, 0.75f));
        label.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;

        AudioListener.volume = 0.35f;
        MakeLargeSlider(go.transform, 0f, 1f, AudioListener.volume,
            v => AudioListener.volume = v);

        var dopplerLabel = MakeTMP(go.transform, "Doppler",
            30, new Color(0.75f, 0.75f, 0.75f));
        dopplerLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;

        MakeLargeSlider(go.transform, 0f, 1f, 0.02f, v =>
        {
            foreach (var s in manager.Synths)
                s.SetDopplerLevel(v);
        });

        // All / None
        var anGO = new GameObject("Btn_AllNone");
        anGO.transform.SetParent(go.transform, false);
        var anLE = anGO.AddComponent<LayoutElement>();
        anLE.preferredWidth = 170; anLE.preferredHeight = 60; anLE.flexibleWidth = 0;
        var anImg = anGO.AddComponent<Image>();
        anImg.sprite = roundedSprite; anImg.type = Image.Type.Sliced;
        anImg.color = new Color(0.15f, 0.53f, 0.62f, 0.9f);
        var anBtn = anGO.AddComponent<Button>();
        var anTMP = MakeTMP(anGO.transform, "ALL", 28, Color.white);
        anTMP.alignment = TextAlignmentOptions.Center;
        anTMP.verticalAlignment = VerticalAlignmentOptions.Middle;
        var anRT = anTMP.GetComponent<RectTransform>();
        anRT.anchorMin = Vector2.zero; anRT.anchorMax = Vector2.one;
        anRT.offsetMin = anRT.offsetMax = Vector2.zero;

        anBtn.onClick.AddListener(() =>
        {
            allSelected = !allSelected;
            anImg.color = allSelected
                ? new Color(0.15f, 0.53f, 0.62f, 0.9f)
                : new Color(0.2f, 0.2f, 0.2f, 0.9f);
            anTMP.text = allSelected ? "ALL" : "NONE";
            foreach (var t in toggles)
            {
                SetPlanetActive(t.planet, t.synth, allSelected);
                t.img.color = allSelected
                    ? new Color(0.15f, 0.53f, 0.62f, 0.9f)
                    : new Color(0.2f, 0.2f, 0.2f, 0.9f);
                t.txt.text = allSelected ? "ON" : "OFF";
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

        // Top: name + ON/OFF
        var topGO = new GameObject("Top");
        topGO.transform.SetParent(card.transform, false);
        var topLE = topGO.AddComponent<LayoutElement>();
        topLE.preferredHeight = 72; topLE.minHeight = 72;
        var topHLG = topGO.AddComponent<HorizontalLayoutGroup>();
        topHLG.childAlignment      = TextAnchor.MiddleCenter;
        topHLG.spacing             = 10;
        topHLG.childControlHeight  = true;
        topHLG.childForceExpandWidth = false;

        var nameTMP = MakeTMP(topGO.transform, planet.planetName, 36, col, true);
        nameTMP.alignment = TextAlignmentOptions.Left;
        nameTMP.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        var togGO = new GameObject("Tog");
        togGO.transform.SetParent(topGO.transform, false);
        var togLE = togGO.AddComponent<LayoutElement>();
        togLE.preferredWidth = 96; togLE.preferredHeight = 58; togLE.flexibleWidth = 0;
        var togImg = togGO.AddComponent<Image>();
        togImg.sprite = roundedSprite; togImg.type = Image.Type.Sliced;
        togImg.color = planet.isActive
            ? new Color(0.15f, 0.53f, 0.62f, 0.9f)
            : new Color(0.2f, 0.2f, 0.2f, 0.9f);
        var togBtn = togGO.AddComponent<Button>();
        var togTMP = MakeTMP(togGO.transform, planet.isActive ? "ON" : "OFF", 28, Color.white);
        togTMP.alignment = TextAlignmentOptions.Center;
        var togTMPRT = togTMP.GetComponent<RectTransform>();
        togTMPRT.anchorMin = Vector2.zero; togTMPRT.anchorMax = Vector2.one;
        togTMPRT.offsetMin = togTMPRT.offsetMax = Vector2.zero;

        toggles.Add((togImg, togTMP, planet, synth));

        togBtn.onClick.AddListener(() =>
        {
            planet.isActive = !planet.isActive;
            SetPlanetActive(planet, synth, planet.isActive);
            togImg.color = planet.isActive
                ? new Color(0.15f, 0.53f, 0.62f, 0.9f)
                : new Color(0.2f, 0.2f, 0.2f, 0.9f);
            togTMP.text = planet.isActive ? "ON" : "OFF";
        });

        // Detail section (hidden by default)
        var detailGO = new GameObject("Detail");
        detailGO.transform.SetParent(card.transform, false);
        var detailVLG = detailGO.AddComponent<VerticalLayoutGroup>();
        detailVLG.spacing              = 8;
        detailVLG.childControlWidth    = true;
        detailVLG.childControlHeight   = true;
        detailVLG.childForceExpandWidth  = true;
        detailVLG.childForceExpandHeight = false;
        detailGO.SetActive(false);
        planetDetails.Add(detailGO);

        synth.volumeScale = 0.6f;
        MakeSliderRow(detailGO.transform, "Vol",
            0f, 1f, synth.volumeScale, v => synth.volumeScale = v);
        MakeOrbitRow(detailGO.transform, planet);
        MakeSliderRow(detailGO.transform, "Speed",
            -10f, 10f, planet.baseSpeed, v => planet.baseSpeed = v);
        MakeSliderRow(detailGO.transform, "Tilt",
            -45f, 45f, planet.inclination, v => planet.SetInclination(v));
        MakeSliderRow(detailGO.transform, "Freq",
            40f, 500f, synth.carrierNote, v => synth.carrierNote = v);
        MakeOscRow(detailGO.transform, synth);
        MakeSliderRow(detailGO.transform, "Mod R",
            0.5f, 8f, synth.modRatio, v => synth.modRatio = v);
        MakeSliderRow(detailGO.transform, "Mod I",
            0f, 10f, synth.modIndex, v => synth.modIndex = v);
        MakeSliderRow(detailGO.transform, "LFO",
            0f, 5f, synth.lfoRate, v => synth.lfoRate = v);
        MakeToggleRow(detailGO.transform, "Pulse",
            synth.pulseEnabled, v => synth.pulseEnabled = v);
        synth.pulseRate = 6.5f;
        MakeSliderRow(detailGO.transform, "P.Rate",
            0.1f, 8f, synth.pulseRate, v => synth.pulseRate = v);
        MakeSliderRow(detailGO.transform, "P.Dec",
            0.01f, 0.3f, synth.pulseDecay, v => synth.pulseDecay = v);

        // Edit button → enters edit mode for this planet
        int capturedIdx = idx;
        var editBtn = MakeRoundedButton(card.transform, "Edit +",
            new Vector2(136, 60), new Color(0.15f, 0.2f, 0.25f), () =>
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

        // Save original content anchoring then stretch to fill viewport
        if (!contentAnchorSaved)
        {
            contentOrigAnchorMin = content.anchorMin;
            contentOrigAnchorMax = content.anchorMax;
            contentOrigOffsetMin = content.offsetMin;
            contentOrigOffsetMax = content.offsetMax;
            contentAnchorSaved   = true;
        }
        var csf = content.GetComponent<ContentSizeFitter>();
        if (csf != null) csf.enabled = false;
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.offsetMin = content.offsetMax = Vector2.zero;

        // Planet card expands to fill remaining height
        var cardLE = planetCards[idx].GetComponent<LayoutElement>()
                  ?? planetCards[idx].AddComponent<LayoutElement>();
        cardLE.flexibleHeight = 1;
        var cardVLG = planetCards[idx].GetComponent<VerticalLayoutGroup>();
        if (cardVLG != null) cardVLG.childForceExpandHeight = true;

        // Detail rows expand too
        var detailVLG = planetDetails[idx].GetComponent<VerticalLayoutGroup>();
        if (detailVLG != null) detailVLG.childForceExpandHeight = true;
        foreach (Transform child in planetDetails[idx].transform)
        {
            var rLE = child.GetComponent<LayoutElement>() ?? child.gameObject.AddComponent<LayoutElement>();
            rLE.flexibleHeight = 1;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        StartCoroutine(RebuildNextFrame());
    }

    void ExitEditMode()
    {
        editingIdx = -1;
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
            if (cLE != null) cLE.flexibleHeight = -1;
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
        img.color = new Color(0.1f, 0.12f, 0.16f, 0.95f);

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
            img.color = isMenuOpen
                ? new Color(0.1f, 0.12f, 0.16f, 0.95f)
                : new Color(0.15f, 0.53f, 0.62f, 0.95f);
            // back button only when menu open + in edit mode
            backBtnRT?.gameObject.SetActive(isMenuOpen && editingIdx >= 0);
            // mute + zoom slider only when menu is hidden
            muteBtnRT?.gameObject.SetActive(!isMenuOpen);
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
        bImg.color = new Color(0.15f, 0.53f, 0.62f, 0.95f);

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
        mImg.color = new Color(0.15f, 0.53f, 0.62f, 0.95f); // teal = sound on

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
                mImg.color = new Color(0.25f, 0.25f, 0.28f, 0.95f);
            }
            else
            {
                AudioListener.volume = lastVol;
                mImg.color = new Color(0.15f, 0.53f, 0.62f, 0.95f);
            }
            muteLine?.SetActive(isMuted);
        });

        CreateZoomSlider(parent);
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

        var viewport  = content.parent?.GetComponent<RectTransform>();
        if (viewport == null) return;

        var rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (rootCanvas == null) return;
        var canvasRT = rootCanvas.GetComponent<RectTransform>();

        var corners = new Vector3[4];
        viewport.GetWorldCorners(corners); // [0]=BL [1]=TL [2]=TR [3]=BR

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRT, corners[3], rootCanvas.worldCamera, out Vector2 brPt); // bottom-right
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRT, corners[0], rootCanvas.worldCamera, out Vector2 blPt); // bottom-left

        // Hamburger — bottom-right corner of viewport
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

    GameObject CreateCard(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(content, false);
        var img = go.AddComponent<Image>();
        img.sprite = roundedSprite; img.type = Image.Type.Sliced;
        img.color = new Color(0.08f, 0.1f, 0.13f, 0.85f);
        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.padding              = new RectOffset(14, 14, 12, 12);
        vlg.spacing              = 10;
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
        le.preferredHeight = 68; le.minHeight = 68;
        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 5;
        hlg.childControlHeight      = true;
        hlg.childForceExpandHeight  = true;
        hlg.childForceExpandWidth   = false;

        var lGO = new GameObject("Lbl");
        lGO.transform.SetParent(go.transform, false);
        lGO.AddComponent<LayoutElement>().preferredWidth = 72;
        var lGOLE = lGO.GetComponent<LayoutElement>(); lGOLE.flexibleWidth = 0;
        var lTMP = lGO.AddComponent<TextMeshProUGUI>();
        lTMP.text = "Orbit"; lTMP.fontSize = 30;
        lTMP.color = new Color(0.75f, 0.75f, 0.75f);
        lTMP.verticalAlignment = VerticalAlignmentOptions.Middle;
        lTMP.raycastTarget = false;

        var btnImgs = new Image[names.Length];
        var teal = new Color(0.15f, 0.53f, 0.62f, 0.9f);
        var dark = new Color(0.15f, 0.18f, 0.22f, 1f);

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
            var bTMP = MakeTMP(bGO.transform, names[i], 24, Color.white);
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

    void MakeOscRow(Transform parent, FMSynthesizer synth)
    {
        var names = new string[] { "Sine", "Tri", "Saw", "Sq" };
        var go = new GameObject("OscRow");
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 68; le.minHeight = 68;
        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 5;
        hlg.childControlHeight      = true;
        hlg.childForceExpandHeight  = true;
        hlg.childForceExpandWidth   = false;

        var lGO = new GameObject("Lbl");
        lGO.transform.SetParent(go.transform, false);
        var lLE = lGO.AddComponent<LayoutElement>();
        lLE.preferredWidth = 72; lLE.flexibleWidth = 0;
        var lTMP = lGO.AddComponent<TextMeshProUGUI>();
        lTMP.text = "Osc"; lTMP.fontSize = 30;
        lTMP.color = new Color(0.75f, 0.75f, 0.75f);
        lTMP.verticalAlignment = VerticalAlignmentOptions.Middle;
        lTMP.raycastTarget = false;

        var btnImgs = new Image[names.Length];
        var teal = new Color(0.15f, 0.53f, 0.62f, 0.9f);
        var dark = new Color(0.15f, 0.18f, 0.22f, 1f);

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
            var bTMP = MakeTMP(bGO.transform, names[i], 24, Color.white);
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
        le.preferredHeight = 68; le.minHeight = 68;
        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childControlHeight      = true;
        hlg.childForceExpandHeight  = true;
        hlg.childForceExpandWidth   = false;

        var lGO = new GameObject("Lbl");
        lGO.transform.SetParent(go.transform, false);
        var lLE = lGO.AddComponent<LayoutElement>();
        lLE.preferredWidth = 90; lLE.flexibleWidth = 0;
        var lTMP = lGO.AddComponent<TextMeshProUGUI>();
        lTMP.text = label; lTMP.fontSize = 32;
        lTMP.color = new Color(0.75f, 0.75f, 0.75f);
        lTMP.verticalAlignment = VerticalAlignmentOptions.Middle;
        lTMP.raycastTarget = false;

        var state = new bool[] { initialVal };

        var btnGO = new GameObject("Btn");
        btnGO.transform.SetParent(go.transform, false);
        var btnLE = btnGO.AddComponent<LayoutElement>();
        btnLE.preferredWidth = 100; btnLE.preferredHeight = 58; btnLE.flexibleWidth = 0;
        var btnImg = btnGO.AddComponent<Image>();
        btnImg.sprite = roundedSprite; btnImg.type = Image.Type.Sliced;
        btnImg.color = state[0]
            ? new Color(0.15f, 0.53f, 0.62f, 0.9f)
            : new Color(0.2f, 0.2f, 0.2f, 0.9f);
        var btnBtn = btnGO.AddComponent<Button>();
        var btnTMP = MakeTMP(btnGO.transform, state[0] ? "ON" : "OFF", 30, Color.white);
        btnTMP.alignment = TextAlignmentOptions.Center;
        var btnTMPRT = btnTMP.GetComponent<RectTransform>();
        btnTMPRT.anchorMin = Vector2.zero; btnTMPRT.anchorMax = Vector2.one;
        btnTMPRT.offsetMin = btnTMPRT.offsetMax = Vector2.zero;

        btnBtn.onClick.AddListener(() =>
        {
            state[0] = !state[0];
            onChange?.Invoke(state[0]);
            btnImg.color = state[0]
                ? new Color(0.15f, 0.53f, 0.62f, 0.9f)
                : new Color(0.2f, 0.2f, 0.2f, 0.9f);
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
        le.preferredHeight = 68; le.minHeight = 68;
        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childControlHeight      = true;
        hlg.childForceExpandHeight  = true;
        hlg.childForceExpandWidth   = false;

        var lGO = new GameObject("L");
        lGO.transform.SetParent(go.transform, false);
        var lLE = lGO.AddComponent<LayoutElement>();
        lLE.preferredWidth = 96; lLE.flexibleWidth = 0;
        var lTMP = lGO.AddComponent<TextMeshProUGUI>();
        lTMP.text = label; lTMP.fontSize = 32;
        lTMP.color = new Color(0.75f, 0.75f, 0.75f);
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
        bgGO.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.22f);

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
        fGO.AddComponent<Image>().color = new Color(0.39f, 0.86f, 1f);
        slider.fillRect = fRT;

        var haGO = new GameObject("HA");
        haGO.transform.SetParent(sGO.transform, false);
        var haRT = haGO.AddComponent<RectTransform>();
        haRT.anchorMin = Vector2.zero; haRT.anchorMax = Vector2.one;
        haRT.sizeDelta = new Vector2(-24, 0);
        var hGO = new GameObject("H");
        hGO.transform.SetParent(haGO.transform, false);
        var hRT = hGO.AddComponent<RectTransform>();
        hRT.sizeDelta = new Vector2(44, 44);
        hRT.anchorMin = new Vector2(0, 0.5f); hRT.anchorMax = new Vector2(0, 0.5f);
        hRT.pivot = new Vector2(0.5f, 0.5f);
        var hImg = hGO.AddComponent<Image>();
        hImg.sprite = circleSprite;
        hImg.color = new Color(0.39f, 0.86f, 1f);
        hImg.preserveAspect = true;
        slider.handleRect = hRT; slider.targetGraphic = hImg;

        var vGO = new GameObject("Val");
        vGO.transform.SetParent(go.transform, false);
        var vLE = vGO.AddComponent<LayoutElement>();
        vLE.preferredWidth = 62; vLE.flexibleWidth = 0;
        var vTMP = vGO.AddComponent<TextMeshProUGUI>();
        vTMP.text = val.ToString("F1"); vTMP.fontSize = 29;
        vTMP.color = new Color(1f, 0.67f, 0.27f);
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
        le.preferredHeight = 44; le.minHeight = 44;

        var slider = go.AddComponent<Slider>();
        slider.minValue = min; slider.maxValue = max; slider.value = val;

        var bgGO = new GameObject("BG");
        bgGO.transform.SetParent(go.transform, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0, 0.5f); bgRT.anchorMax = new Vector2(1, 0.5f);
        bgRT.sizeDelta = new Vector2(0, 5);
        bgGO.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f);

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
        fGO.AddComponent<Image>().color = new Color(0.39f, 0.86f, 1f);
        slider.fillRect = fRT;

        var haGO = new GameObject("HA");
        haGO.transform.SetParent(go.transform, false);
        var haRT = haGO.AddComponent<RectTransform>();
        haRT.anchorMin = Vector2.zero; haRT.anchorMax = Vector2.one;
        haRT.sizeDelta = new Vector2(-20, 0);

        var hGO = new GameObject("H");
        hGO.transform.SetParent(haGO.transform, false);
        var hRT = hGO.AddComponent<RectTransform>();
        hRT.sizeDelta = new Vector2(36, 36);
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
        var tmp = MakeTMP(go.transform, text, 28, Color.white);
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
