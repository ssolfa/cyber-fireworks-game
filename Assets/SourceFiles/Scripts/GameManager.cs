using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

// Builds the whole game UI at runtime (start screen, HUD, shop) and manages
// firework styles (free + unlock), style selection, and the NPC auto-show toggle.
public class GameManager : MonoBehaviour
{
    [System.Serializable]
    public class FireworkStyle
    {
        public string name;
        public Color swatch;       // representative color for the shop swatch
        public Color[] colors;     // palette the launcher picks from
        public float intensity = 2.6f;
        public bool unlocked;
    }

    // ---- Palette (neon-ish but works with alpha particles) ----
    static Color C(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        return c;
    }

    private bool started = false;
    private bool autoShow = false;

    // Default colorful firework palette (shop no longer selects colors).
    static readonly Color[] DefaultFwColors = {
        C("#FFC64D"), C("#FF3A2E"), C("#7CFF6A"), C("#4D8CFF"), C("#C77DFF"), C("#FF5EC4"), C("#FFF4E0")
    };

    [Header("Assigned in scene")]
    public Font uiFont;      // Mulmaru (Korean/body); falls back to built-in
    public Font pixelFont;   // PixelifySans (retro pixel display); falls back to uiFont
    public AudioSource musicNight;  // calm night BGM

    private FireworkLauncher launcher;
    private DayRunner dayRunner;
    private BeachDecor decor;
    private int moonClicks;
    private const int MoonClicksToDay = 5;
    private Sprite fireworkIcon;
    private Font pxFont;

    // UI refs
    private Canvas canvas;
    private GameObject startPanel, hudPanel, shopPanel;
    private GameObject dimOverlay;
    private Text autoLabel, hintLabel;
    private readonly List<Button> styleButtons = new List<Button>();
    private readonly List<Text> styleButtonTexts = new List<Text>();
    private readonly List<Image> styleCardBgs = new List<Image>();
    private LetterFireworks letterFw;
    private GameObject letterPanel;
    private InputField letterInput;
    private Text idleBtnLabel;
    private bool idleOn = true;
    private GameObject pausePanel;
    private bool paused = false;
    private Font font;

    // pixel palette
    static readonly Color NAVY = new Color(0.05f, 0.06f, 0.13f, 0.92f);
    static readonly Color PANEL = new Color(0.08f, 0.10f, 0.20f, 0.96f);
    static readonly Color CYAN = new Color(0.40f, 0.92f, 1f, 1f);
    static readonly Color GOLD = new Color(1f, 0.83f, 0.35f, 1f);
    static readonly Color INK = new Color(0.92f, 0.95f, 1f, 1f);

    void Awake()
    {
        launcher = FindFirstObjectByType<FireworkLauncher>();
        dayRunner = FindFirstObjectByType<DayRunner>();
        letterFw = FindFirstObjectByType<LetterFireworks>();
        decor = FindFirstObjectByType<BeachDecor>();
        font = uiFont != null ? uiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        pxFont = pixelFont != null ? pixelFont : font;
        fireworkIcon = LoadSprite("Pixel_FireworkIcon");

        if (launcher != null) { launcher.currentColors = DefaultFwColors; launcher.currentIntensity = 2.6f; }

        BuildUI();
        ShowIntro();
    }

    void OnEnable()
    {
        if (launcher != null) { launcher.NpcClicked += ToggleAutoShow; launcher.MoonClicked += OnMoonClicked; }
    }
    void OnDisable()
    {
        if (launcher != null) { launcher.NpcClicked -= ToggleAutoShow; launcher.MoonClicked -= OnMoonClicked; }
    }

    // Easter egg: click the moon a few times to enter day-runner mode.
    void OnMoonClicked()
    {
        if (dayRunner == null || dayRunner.IsActive) return;
        moonClicks++;
        if (autoLabel != null) autoLabel.text = moonClicks < MoonClicksToDay ? "달빛이 일렁인다... (" + moonClicks + "/" + MoonClicksToDay + ")" : "";
        if (moonClicks >= MoonClicksToDay)
        {
            moonClicks = 0;
            dayRunner.EnterDay();
        }
    }

    // Called by DayRunner when entering/leaving day mode.
    public void SetNightUIVisible(bool v)
    {
        if (hudPanel != null) hudPanel.SetActive(v);
        if (shopPanel != null) shopPanel.SetActive(false);
        if (letterPanel != null) letterPanel.SetActive(false);
        if (letterFw != null) letterFw.Clear();
        if (decor != null) decor.SetAllActive(v); // hide decorations in day mode
        if (v && autoLabel != null) autoLabel.text = "";
    }

    Sprite LoadSprite(string name)
    {
        var all = Resources.FindObjectsOfTypeAll<Sprite>();
        foreach (var s in all) if (s != null && s.name == name) return s;
        return null;
    }

    // ---------------- UI construction ----------------
    void BuildUI()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.transform.SetParent(transform, false);
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        var canvasGO = new GameObject("GameUICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        BuildStartPanel();
        BuildHud();

        // Full-screen dim/blocker shown behind modal panels (shop / letter input).
        // Its raycastTarget swallows clicks so fireworks don't fire behind panels.
        var dimRt = NewRect("DimOverlay", canvas.transform); Stretch(dimRt);
        AddImage(dimRt, new Color(0f, 0f, 0f, 0.55f));
        dimOverlay = dimRt.gameObject;
        dimOverlay.SetActive(false);

        BuildShopPanel();
        BuildPausePanel();
    }

    void BuildPausePanel()
    {
        var rt = NewRect("PausePanel", canvas.transform);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(560, 460); rt.anchoredPosition = Vector2.zero;
        pausePanel = rt.gameObject;
        AddImage(rt, PANEL); AddPixelBorder(rt.gameObject, CYAN);

        var t = NewRect("PauseTitle", rt);
        t.anchorMin = new Vector2(0,1); t.anchorMax = new Vector2(1,1); t.pivot = new Vector2(0.5f,1);
        t.sizeDelta = new Vector2(0, 90); t.anchoredPosition = new Vector2(0, -30);
        var pt = AddText(t, "PAUSED", 56, GOLD, TextAnchor.MiddleCenter); pt.font = pxFont;
        var psh = t.gameObject.AddComponent<Shadow>(); psh.effectColor = new Color(0.92f,0.12f,0.52f,0.72f); psh.effectDistance = new Vector2(4,-4);

        var resume = MakeButton(rt, "계속하기", new Vector2(360, 76), new Vector2(0, 40), new Vector2(0.5f, 0.5f), new Color(0.12f,0.18f,0.34f,1f), CYAN, 34, out _);
        resume.onClick.AddListener(Resume);
        var fb = MakeButton(rt, "💬 의견 남기기", new Vector2(360, 68), new Vector2(0, -50), new Vector2(0.5f, 0.5f), new Color(0.10f,0.14f,0.28f,1f), GOLD, 28, out _);
        fb.onClick.AddListener(OpenFeedback);
        var quit = MakeButton(rt, "게임 종료", new Vector2(360, 68), new Vector2(0, -140), new Vector2(0.5f, 0.5f), new Color(0.10f,0.14f,0.28f,1f), new Color(1f,0.5f,0.5f), 30, out _);
        quit.onClick.AddListener(QuitGame);

        pausePanel.SetActive(false);
    }

    void Update()
    {
        if (!started) return;
        if (dayRunner != null && dayRunner.IsActive) return; // day mode owns ESC
        bool esc;
#if ENABLE_INPUT_SYSTEM
        esc = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        esc = Input.GetKeyDown(KeyCode.Escape);
#endif
        if (esc)
        {
            // ESC closes shop/letter first, otherwise toggles pause
            if (shopPanel != null && shopPanel.activeSelf) { CloseShop(); }
            else if (letterPanel != null && letterPanel.activeSelf) { letterPanel.SetActive(false); ShowDim(false); }
            else TogglePause();
        }
    }

    void TogglePause() { if (paused) Resume(); else Pause(); }

    void Pause()
    {
        paused = true;
        Time.timeScale = 0f;
        ShowDim(true);
        pausePanel.SetActive(true);
        pausePanel.transform.SetAsLastSibling();
    }

    public void Resume()
    {
        paused = false;
        Time.timeScale = 1f;
        pausePanel.SetActive(false);
        ShowDim(false);
    }

    void QuitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void ShowDim(bool on)
    {
        if (dimOverlay == null) return;
        dimOverlay.SetActive(on);
        if (on) dimOverlay.transform.SetAsLastSibling(); // just under the panel opened next
    }

    RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    Image AddImage(RectTransform rt, Color color)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    void AddPixelBorder(GameObject go, Color color)
    {
        var o = go.AddComponent<Outline>();
        o.effectColor = color;
        o.effectDistance = new Vector2(3, -3);
        o.useGraphicAlpha = false;
    }

    Text AddText(RectTransform rt, string txt, int size, Color color, TextAnchor anchor, FontStyle style = FontStyle.Bold)
    {
        var t = rt.gameObject.AddComponent<Text>();
        t.font = font; t.text = txt; t.fontSize = size; t.color = color;
        t.alignment = anchor; t.fontStyle = style;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    Button MakeButton(RectTransform parent, string label, Vector2 size, Vector2 anchoredPos, Vector2 anchor, Color bg, Color border, int fontSize, out Text labelText)
    {
        var rt = NewRect("Btn_" + label, parent);
        rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = anchoredPos;
        var img = AddImage(rt, bg);
        AddPixelBorder(rt.gameObject, border);
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var cb = btn.colors; cb.highlightedColor = new Color(bg.r+0.12f, bg.g+0.12f, bg.b+0.16f, 1f);
        cb.pressedColor = new Color(bg.r*0.7f, bg.g*0.7f, bg.b*0.7f, 1f);
        cb.normalColor = Color.white; btn.colors = cb;
        var lrt = NewRect("Label", rt); Stretch(lrt);
        labelText = AddText(lrt, label, fontSize, INK, TextAnchor.MiddleCenter);
        return btn;
    }

    void BuildStartPanel()
    {
        var rt = NewRect("StartPanel", canvas.transform); Stretch(rt);
        startPanel = rt.gameObject;
        AddImage(rt, new Color(0.02f, 0.02f, 0.06f, 0.55f));

        // firework icon
        if (fireworkIcon != null)
        {
            var icon = NewRect("Icon", rt);
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f); icon.pivot = new Vector2(0.5f, 0.5f);
            icon.sizeDelta = new Vector2(190, 190); icon.anchoredPosition = new Vector2(0, 250);
            var im = icon.gameObject.AddComponent<Image>(); im.sprite = fireworkIcon; im.preserveAspect = true;
        }

        // Big retro PIXEL title with a drop shadow (references: "Match"/"Retrobit"/"PRESS START")
        var title = NewRect("Title", rt);
        title.anchorMin = title.anchorMax = new Vector2(0.5f, 0.5f); title.pivot = new Vector2(0.5f, 0.5f);
        title.sizeDelta = new Vector2(1750, 170); title.anchoredPosition = new Vector2(0, 70);
        var tt = AddText(title, "CYBER FIREWORKS", 96, GOLD, TextAnchor.MiddleCenter);
        tt.font = pxFont;
        var sh = title.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0.92f, 0.12f, 0.52f, 0.8f); // neon magenta shadow
        sh.effectDistance = new Vector2(6, -6);

        // Korean title (Pretendard)
        var kr = NewRect("TitleKR", rt);
        kr.anchorMin = kr.anchorMax = new Vector2(0.5f, 0.5f); kr.pivot = new Vector2(0.5f, 0.5f);
        kr.sizeDelta = new Vector2(1200, 70); kr.anchoredPosition = new Vector2(0, -30);
        AddText(kr, "사이버 불꽃놀이", 48, CYAN, TextAnchor.MiddleCenter, FontStyle.Normal);

        // bottom tagline (Pretendard)
        var tag = NewRect("Tagline", rt);
        tag.anchorMin = new Vector2(0.5f, 0); tag.anchorMax = new Vector2(0.5f, 0); tag.pivot = new Vector2(0.5f, 0);
        tag.sizeDelta = new Vector2(1400, 56); tag.anchoredPosition = new Vector2(0, 54);
        AddText(tag, "방구석에서 불꽃놀이를 즐겨보세요", 34, new Color(1f, 1f, 1f, 0.92f), TextAnchor.MiddleCenter, FontStyle.Normal);

        // PRESS START button (pixel font)
        var start = MakeButton(rt, "▶ PRESS START", new Vector2(460, 108), new Vector2(0, -190), new Vector2(0.5f, 0.5f), new Color(0.10f,0.14f,0.28f,0.95f), CYAN, 44, out var startLabel);
        startLabel.font = pxFont;
        start.onClick.AddListener(StartGame);
        var throb = start.gameObject.AddComponent<Bob>();
        throb.mode = Bob.Mode.Scale; throb.amplitude = 0.045f; throb.speed = 3.2f;

        // developer credit + social links (bottom-right)
        var cr = NewRect("Credit", rt);
        cr.anchorMin = new Vector2(1, 0); cr.anchorMax = new Vector2(1, 0); cr.pivot = new Vector2(1, 0);
        cr.sizeDelta = new Vector2(300, 34); cr.anchoredPosition = new Vector2(-26, 96);
        AddText(cr, "made by ssolfa", 22, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleRight, FontStyle.Normal);

        var ig = MakeButton(rt, "Instagram", new Vector2(210, 54), new Vector2(-131, 30), new Vector2(1, 0), new Color(0.10f,0.14f,0.28f,0.92f), GOLD, 26, out _);
        ig.onClick.AddListener(() => Application.OpenURL("https://www.instagram.com/ssolfa8_8/"));
        var gh = MakeButton(rt, "GitHub", new Vector2(190, 54), new Vector2(-351, 30), new Vector2(1, 0), new Color(0.10f,0.14f,0.28f,0.92f), CYAN, 26, out _);
        gh.onClick.AddListener(() => Application.OpenURL("https://github.com/ssolfa"));

        // feedback (Google Form) — bottom-left
        var fb = MakeButton(rt, "💬 의견 남기기", new Vector2(300, 60), new Vector2(190, 34), new Vector2(0, 0), new Color(0.10f,0.14f,0.28f,0.92f), GOLD, 28, out _);
        fb.onClick.AddListener(OpenFeedback);
    }

    public const string FeedbackUrl = "https://docs.google.com/forms/d/e/1FAIpQLSfs-A27aDv_HxQgqiAuGpst1HaX-cUs98V524fXSEBWedVrOA/viewform";
    public void OpenFeedback() { Application.OpenURL(FeedbackUrl); }

    void BuildHud()
    {
        var rt = NewRect("HUD", canvas.transform); Stretch(rt);
        hudPanel = rt.gameObject;

        // top-left: title chip
        var chip = NewRect("TitleChip", rt);
        chip.anchorMin = new Vector2(0, 1); chip.anchorMax = new Vector2(0, 1); chip.pivot = new Vector2(0, 1);
        chip.sizeDelta = new Vector2(300, 64); chip.anchoredPosition = new Vector2(28, -24);
        AddImage(chip, PANEL); AddPixelBorder(chip.gameObject, GOLD);
        var slt = NewRect("t", chip);
        slt.anchorMin = new Vector2(0,0); slt.anchorMax = new Vector2(1,1); slt.offsetMin = new Vector2(16,0); slt.offsetMax = new Vector2(-10,0);
        AddText(slt, "🌙 여름밤 해변", 26, GOLD, TextAnchor.MiddleLeft);

        // top-right: shop + letter-firework buttons
        var shopBtn = MakeButton(rt, "상점", new Vector2(200, 68), new Vector2(-125, -58), new Vector2(1, 1), new Color(0.10f,0.14f,0.28f,0.95f), CYAN, 30, out _);
        shopBtn.onClick.AddListener(OpenShop);
        var letterBtn = MakeButton(rt, "글자불꽃", new Vector2(220, 68), new Vector2(-350, -58), new Vector2(1, 1), new Color(0.10f,0.14f,0.28f,0.95f), GOLD, 28, out _);
        letterBtn.onClick.AddListener(OpenLetterPanel);
        var idleBtn = MakeButton(rt, "", new Vector2(300, 68), new Vector2(-590, -58), new Vector2(1, 1), new Color(0.10f,0.14f,0.28f,0.95f), CYAN, 26, out idleBtnLabel);
        idleBtn.onClick.AddListener(ToggleIdle);

        BuildLetterPanel(canvas.transform); // direct canvas child so it draws above the dim overlay

        // auto-show indicator (top-center)
        var al = NewRect("AutoLabel", rt);
        al.anchorMin = new Vector2(0.5f, 1); al.anchorMax = new Vector2(0.5f, 1); al.pivot = new Vector2(0.5f, 1);
        al.sizeDelta = new Vector2(840, 54); al.anchoredPosition = new Vector2(0, -26);
        autoLabel = AddText(al, "", 27, CYAN, TextAnchor.MiddleCenter);

        // bottom hint
        var hl = NewRect("Hint", rt);
        hl.anchorMin = new Vector2(0.5f, 0); hl.anchorMax = new Vector2(0.5f, 0); hl.pivot = new Vector2(0.5f, 0);
        hl.sizeDelta = new Vector2(1600, 44); hl.anchoredPosition = new Vector2(0, 26);
        hintLabel = AddText(hl, "하늘 클릭 → 불꽃 발사   ·   아이 클릭 → 자동 불꽃쇼   ·   달 여러 번 클릭 → ???   ·   ESC → 메뉴", 24, new Color(1,1,1,0.8f), TextAnchor.MiddleCenter, FontStyle.Normal);
    }

    void BuildLetterPanel(Transform parent)
    {
        var rt = NewRect("LetterPanel", parent);
        rt.anchorMin = new Vector2(0.5f, 1); rt.anchorMax = new Vector2(0.5f, 1); rt.pivot = new Vector2(0.5f, 1);
        rt.sizeDelta = new Vector2(880, 120); rt.anchoredPosition = new Vector2(0, -110);
        letterPanel = rt.gameObject;
        AddImage(rt, PANEL); AddPixelBorder(rt.gameObject, GOLD);

        // input field
        var fieldRt = NewRect("Field", rt);
        fieldRt.anchorMin = new Vector2(0, 0.5f); fieldRt.anchorMax = new Vector2(0, 0.5f); fieldRt.pivot = new Vector2(0, 0.5f);
        fieldRt.sizeDelta = new Vector2(560, 74); fieldRt.anchoredPosition = new Vector2(24, 0);
        var fieldImg = AddImage(fieldRt, new Color(0.03f, 0.04f, 0.09f, 1f)); AddPixelBorder(fieldRt.gameObject, CYAN);
        letterInput = fieldRt.gameObject.AddComponent<InputField>();
        letterInput.textComponent = MakeChildText(fieldRt, 34, INK, TextAnchor.MiddleLeft);
        var ph = MakeChildText(fieldRt, 30, new Color(1,1,1,0.4f), TextAnchor.MiddleLeft);
        ph.text = "영문/숫자 입력 (예: I LOVE U)";
        letterInput.placeholder = ph;
        letterInput.targetGraphic = fieldImg;
        letterInput.characterLimit = 10;
        letterInput.onSubmit.AddListener(SubmitLetters);

        var go = MakeButton(rt, "쏘기", new Vector2(120, 74), new Vector2(-150, 0), new Vector2(1, 0.5f), new Color(0.12f,0.18f,0.34f,1f), CYAN, 30, out _);
        go.onClick.AddListener(() => SubmitLetters(letterInput.text));
        var x = MakeButton(rt, "✕", new Vector2(64, 74), new Vector2(-24, 0), new Vector2(1, 0.5f), new Color(0.10f,0.14f,0.28f,1f), GOLD, 30, out _);
        x.onClick.AddListener(() => { letterPanel.SetActive(false); ShowDim(false); });

        letterPanel.SetActive(false);
    }

    Text MakeChildText(RectTransform parent, int size, Color color, TextAnchor anchor)
    {
        var rt = NewRect("t", parent);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(16, 6); rt.offsetMax = new Vector2(-16, -6);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = font; t.fontSize = size; t.color = color; t.alignment = anchor;
        t.supportRichText = false;
        return t;
    }

    public void OpenLetterPanel()
    {
        if (letterPanel == null) return;
        if (shopPanel != null) shopPanel.SetActive(false);
        ShowDim(true);
        letterPanel.SetActive(true);
        letterPanel.transform.SetAsLastSibling();
        if (letterInput != null) { letterInput.text = ""; letterInput.ActivateInputField(); }
    }

    void SubmitLetters(string text)
    {
        if (letterFw != null) letterFw.Spell(text);
        if (letterPanel != null) letterPanel.SetActive(false);
        ShowDim(false);
        if (launcher != null) launcher.SuppressClicks(0.25f); // don't fire from the submit click
    }

    void BuildShopPanel()
    {
        var rt = NewRect("ShopPanel", canvas.transform);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(900, 760); rt.anchoredPosition = Vector2.zero;
        shopPanel = rt.gameObject;
        AddImage(rt, PANEL); AddPixelBorder(rt.gameObject, CYAN);

        // pixel title with shadow
        var titleR = NewRect("ShopTitleEN", rt);
        titleR.anchorMin = new Vector2(0, 1); titleR.anchorMax = new Vector2(1, 1); titleR.pivot = new Vector2(0.5f, 1);
        titleR.sizeDelta = new Vector2(0, 78); titleR.anchoredPosition = new Vector2(0, -22);
        var tEN = AddText(titleR, "DECOR SHOP", 52, GOLD, TextAnchor.MiddleCenter); tEN.font = pxFont;
        var shp = titleR.gameObject.AddComponent<Shadow>(); shp.effectColor = new Color(0.92f,0.12f,0.52f,0.72f); shp.effectDistance = new Vector2(4,-4);

        var hintR = NewRect("ShopHint", rt);
        hintR.anchorMin = new Vector2(0, 1); hintR.anchorMax = new Vector2(1, 1); hintR.pivot = new Vector2(0.5f, 1);
        hintR.sizeDelta = new Vector2(0, 40); hintR.anchoredPosition = new Vector2(0, -108);
        AddText(hintR, "아이템을 잠금해제하고 해변을 꾸며보세요", 24, CYAN, TextAnchor.MiddleCenter, FontStyle.Normal);

        // 2-column card grid
        var grid = NewRect("Grid", rt);
        grid.anchorMin = new Vector2(0, 0); grid.anchorMax = new Vector2(1, 1);
        grid.offsetMin = new Vector2(40, 120); grid.offsetMax = new Vector2(-40, -160);
        var glg = grid.gameObject.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(390, 150); glg.spacing = new Vector2(20, 20);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount; glg.constraintCount = 2;
        glg.childAlignment = TextAnchor.UpperCenter;

        int count = decor != null ? decor.Count : 0;
        for (int i = 0; i < count; i++)
        {
            int idx = i;
            var item = decor.Get(i);
            var card = NewRect("Card_" + item.name, grid);
            var bg = AddImage(card, new Color(0.06f,0.08f,0.16f,1f));
            AddPixelBorder(card.gameObject, new Color(0.3f,0.4f,0.6f,1f));
            styleCardBgs.Add(bg);

            // item preview icon (left)
            var icon = NewRect("Icon", card);
            icon.anchorMin = new Vector2(0, 0.5f); icon.anchorMax = new Vector2(0, 0.5f); icon.pivot = new Vector2(0, 0.5f);
            icon.sizeDelta = new Vector2(96, 96); icon.anchoredPosition = new Vector2(16, 18);
            var iimg = icon.gameObject.AddComponent<Image>(); iimg.sprite = item.sprite; iimg.preserveAspect = true;

            // name (Mulmaru)
            var nm = NewRect("Name", card);
            nm.anchorMin = new Vector2(0, 1); nm.anchorMax = new Vector2(1, 1); nm.pivot = new Vector2(0.5f, 1);
            nm.offsetMin = new Vector2(120, -60); nm.offsetMax = new Vector2(-12, -14);
            AddText(nm, item.name, 30, INK, TextAnchor.MiddleLeft);

            // action button — anchored bottom-center of the card (pivot stays 0.5,0.5).
            // card is 390 wide, button 220 → fits with margin; nudged right of the icon.
            var actBtn = MakeButton(card, "", new Vector2(220, 46), new Vector2(58, 32), new Vector2(0.5f, 0), new Color(0.12f,0.18f,0.34f,1f), CYAN, 22, out var at);
            at.font = pxFont;
            actBtn.onClick.AddListener(() => OnItemButton(idx));
            styleButtons.Add(actBtn); styleButtonTexts.Add(at);
        }

        var close = MakeButton(rt, "CLOSE", new Vector2(220, 60), new Vector2(0, 24), new Vector2(0.5f, 0), new Color(0.10f,0.14f,0.28f,1f), GOLD, 30, out var closeLabel);
        closeLabel.font = pxFont;
        close.onClick.AddListener(CloseShop);
    }

    // ---------------- State / actions ----------------
    void ShowIntro()
    {
        started = false;
        if (launcher != null) { launcher.gameStarted = false; launcher.autoLaunch = false; }
        paused = false;
        Time.timeScale = 1f;
        startPanel.SetActive(true);
        hudPanel.SetActive(false);
        shopPanel.SetActive(false);
        if (letterPanel != null) letterPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (letterFw != null) letterFw.Clear();
    }

    public void StartGame()
    {
        started = true;
        startPanel.SetActive(false);
        hudPanel.SetActive(true);
        shopPanel.SetActive(false);
        if (launcher != null) { launcher.gameStarted = true; launcher.idleAmbient = idleOn; }
        UpdateAutoLabel();
        UpdateIdleLabel();
    }

    public void OpenShop()
    {
        if (letterPanel != null) letterPanel.SetActive(false);
        ShowDim(true);
        shopPanel.SetActive(true);
        shopPanel.transform.SetAsLastSibling();
        RefreshShop();
    }
    public void CloseShop() { shopPanel.SetActive(false); ShowDim(false); }

    void OnItemButton(int i)
    {
        if (decor != null) decor.ToggleUnlockOrPlace(i); // free unlock, then place/remove toggle
        RefreshShop();
    }

    void RefreshShop()
    {
        if (decor == null) return;
        for (int i = 0; i < styleButtons.Count && i < decor.Count; i++)
        {
            var it = decor.Get(i);
            var txt = styleButtonTexts[i];
            if (!it.unlocked) { txt.text = "UNLOCK"; txt.color = GOLD; }
            else if (it.placed) { txt.text = "REMOVE"; txt.color = new Color(1f,0.5f,0.5f); }
            else { txt.text = "PLACE"; txt.color = CYAN; }
            if (i < styleCardBgs.Count)
                styleCardBgs[i].color = (it.unlocked && it.placed)
                    ? new Color(0.13f, 0.19f, 0.34f, 1f)
                    : new Color(0.06f, 0.08f, 0.16f, 1f);
        }
    }

    public void ToggleAutoShow()
    {
        if (!started) return;
        autoShow = !autoShow;
        if (launcher != null) launcher.autoLaunch = autoShow;
        UpdateAutoLabel();
    }

    // Turn the ambient auto-fireworks (self-firing night sky) on/off.
    void ToggleIdle()
    {
        idleOn = !idleOn;
        if (launcher != null) launcher.idleAmbient = idleOn;
        UpdateIdleLabel();
    }

    void UpdateIdleLabel()
    {
        if (idleBtnLabel != null)
        {
            idleBtnLabel.text = idleOn ? "자동발사 ON" : "자동발사 OFF";
            idleBtnLabel.color = idleOn ? CYAN : new Color(1f, 1f, 1f, 0.55f);
        }
    }

    void UpdateAutoLabel()
    {
        if (autoLabel == null) return;
        autoLabel.text = autoShow ? "★ 자동 불꽃쇼 진행 중 — 아이를 클릭해 멈추기" : "";
    }
}
