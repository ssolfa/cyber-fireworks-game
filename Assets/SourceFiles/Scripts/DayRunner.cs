using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Day-mode easter egg: a 2D side-scrolling runner. A pixel kid on a swim tube
// auto-floats while obstacles scroll in from the right; jump to dodge them.
// Uses SpriteRenderers (which render reliably in this project's URP 2D Renderer).
public class DayRunner : MonoBehaviour
{
    [Header("Sprites / fonts (assigned in scene)")]
    public Sprite dayBgSprite;
    public Sprite tubeKidSprite;
    public Sprite[] obstacleSprites;
    public Font uiFont;
    public Font pixelFont;
    public AudioSource musicDay;   // upbeat day BGM
    public AudioClip jumpSfx;
    public AudioClip gameOverSfx;
    AudioSource sfxSrc;
    const string BestKey = "DayRunnerBest";

    const float GroundY = -2.3f;
    const float PlayerX = -4.8f;
    const float Gravity = -30f;
    const float JumpV = 12f;

    bool active, gameOver;
    float vy, score, best, speed, spawnTimer;
    bool grounded;

    GameObject dayRoot;
    Transform player;
    readonly List<GameObject> obstacles = new List<GameObject>();

    Canvas canvas;
    Text scoreText, overText;
    GameObject overPanel;

    FireworkLauncher launcher;
    GameManager gm;
    Camera cam;

    public bool IsActive => active;

    void Awake()
    {
        launcher = FindFirstObjectByType<FireworkLauncher>();
        gm = FindFirstObjectByType<GameManager>();
        cam = Camera.main;
        if (uiFont == null) uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (pixelFont == null) pixelFont = uiFont;
        best = PlayerPrefs.GetFloat(BestKey, 0f); // persisted local high score
    }

    public void EnterDay()
    {
        if (active) return;
        active = true;
        if (cam == null) cam = Camera.main;
        if (gm != null) gm.SetNightUIVisible(false);
        if (launcher != null) { launcher.gameStarted = false; launcher.autoLaunch = false; }

        dayRoot = new GameObject("DayRunner_Root");

        var bg = new GameObject("DayBG"); bg.transform.SetParent(dayRoot.transform);
        var bgsr = bg.AddComponent<SpriteRenderer>();
        bgsr.sprite = dayBgSprite; bgsr.sortingLayerName = "Fireworks"; bgsr.sortingOrder = -50;
        FitToCamera(bg.transform, bgsr);

        var p = new GameObject("TubeKid"); p.transform.SetParent(dayRoot.transform);
        p.transform.position = new Vector3(PlayerX, GroundY, 0f);
        var psr = p.AddComponent<SpriteRenderer>();
        psr.sprite = tubeKidSprite; psr.sortingLayerName = "Fireworks"; psr.sortingOrder = 20;
        ScaleToHeight(p.transform, psr, 1.8f);
        player = p.transform;

        sfxSrc = dayRoot.AddComponent<AudioSource>();
        sfxSrc.playOnAwake = false;
        if (musicDay != null) { musicDay.Play(); }

        BuildUI();
        Reset();
    }

    void Reset()
    {
        gameOver = false; score = 0f; speed = 5.5f; spawnTimer = 0.8f; vy = 0f; grounded = true;
        foreach (var o in obstacles) if (o != null) Destroy(o);
        obstacles.Clear();
        if (player != null) player.position = new Vector3(PlayerX, GroundY, 0f);
        if (overPanel != null) overPanel.SetActive(false);
        if (musicDay != null && !musicDay.isPlaying) musicDay.Play();
    }

    void FitToCamera(Transform t, SpriteRenderer sr)
    {
        float h = cam.orthographicSize * 2f;
        float w = h * cam.aspect;
        var size = sr.sprite.bounds.size;
        t.localScale = new Vector3(w / size.x, h / size.y, 1f);
        t.position = new Vector3(0, 0, 4.5f);
    }

    void ScaleToHeight(Transform t, SpriteRenderer sr, float worldH)
    {
        float s = worldH / sr.sprite.bounds.size.y;
        t.localScale = new Vector3(s, s, 1f);
    }

    void Update()
    {
        if (!active) return;

        if (EscPressed()) { ExitDay(); return; }
        if (gameOver) { if (RetryPressed()) Reset(); return; }

        speed += Time.deltaTime * 0.25f;
        score += Time.deltaTime * 12f;
        if (scoreText != null) scoreText.text = "SCORE  " + Mathf.FloorToInt(score);

        if (JumpPressed() && grounded)
        {
            vy = JumpV; grounded = false;
            if (sfxSrc != null && jumpSfx != null) sfxSrc.PlayOneShot(jumpSfx, 0.5f);
        }
        vy += Gravity * Time.deltaTime;
        var pp = player.position;
        pp.y += vy * Time.deltaTime;
        if (pp.y <= GroundY) { pp.y = GroundY; vy = 0f; grounded = true; }
        player.position = pp;

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            SpawnObstacle();
            spawnTimer = Random.Range(0.95f, 1.7f) * Mathf.Clamp(6f / speed, 0.55f, 1.15f);
        }

        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            var o = obstacles[i];
            if (o == null) { obstacles.RemoveAt(i); continue; }
            o.transform.position += Vector3.left * speed * Time.deltaTime;
            if (o.transform.position.x < -11f) { Destroy(o); obstacles.RemoveAt(i); continue; }
            var d = o.transform.position - player.position;
            if (Mathf.Abs(d.x) < 0.72f && Mathf.Abs(d.y) < 0.82f) { GameOver(); break; }
        }
    }

    void SpawnObstacle()
    {
        if (obstacleSprites == null || obstacleSprites.Length == 0) return;
        var spr = obstacleSprites[Random.Range(0, obstacleSprites.Length)];
        if (spr == null) return;
        var o = new GameObject("Obstacle");
        o.transform.SetParent(dayRoot.transform);
        o.transform.position = new Vector3(10.5f, GroundY, 0f);
        var sr = o.AddComponent<SpriteRenderer>();
        sr.sprite = spr; sr.sortingLayerName = "Fireworks"; sr.sortingOrder = 10;
        ScaleToHeight(o.transform, sr, 1.15f);
        obstacles.Add(o);
    }

    void GameOver()
    {
        gameOver = true;
        bool newBest = score > best;
        best = Mathf.Max(best, score);
        PlayerPrefs.SetFloat(BestKey, best);
        PlayerPrefs.Save();

        if (musicDay != null) musicDay.Stop();
        if (sfxSrc != null && gameOverSfx != null) sfxSrc.PlayOneShot(gameOverSfx, 0.7f);

        if (overPanel != null) overPanel.SetActive(true);
        if (overText != null)
            overText.text = "GAME OVER\n\nSCORE  " + Mathf.FloorToInt(score)
                + "\nBEST  " + Mathf.FloorToInt(best) + (newBest ? "   NEW!" : "");
    }

    void ExitDay()
    {
        active = false;
        if (musicDay != null) musicDay.Stop();
        foreach (var o in obstacles) if (o != null) Destroy(o);
        obstacles.Clear();
        if (dayRoot != null) Destroy(dayRoot);
        if (canvas != null) Destroy(canvas.gameObject);
        player = null;
        if (gm != null) gm.SetNightUIVisible(true);
        if (launcher != null) { launcher.gameStarted = true; launcher.SuppressClicks(0.35f); }
    }

    // ---------- input (new Input System with legacy fallback) ----------
    bool JumpPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
#endif
    }
    bool EscPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }
    bool RetryPressed()
    {
        // Clicking a UI button (e.g. feedback) must not also restart the run.
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
#if ENABLE_INPUT_SYSTEM
        bool key = Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
        bool click = !overUI && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        return key || click;
#else
        bool key = Input.GetKeyDown(KeyCode.R);
        bool click = !overUI && Input.GetMouseButtonDown(0);
        return key || click;
#endif
    }

    // ---------- UI ----------
    void BuildUI()
    {
        var go = new GameObject("DayRunnerCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        var sc = go.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);

        scoreText = MakeText(canvas.transform, "SCORE  0", 44, pixelFont, new Color(1f, 0.95f, 0.4f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(700, 70));
        var scSh = scoreText.gameObject.AddComponent<Shadow>();
        scSh.effectColor = new Color(0.1f, 0.2f, 0.5f, 0.7f); scSh.effectDistance = new Vector2(3, -3);

        MakeText(canvas.transform, "SPACE / 클릭 : 점프      ESC : 나가기", 26, uiFont, new Color(0.1f, 0.15f, 0.3f, 0.9f),
            new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(1200, 46));

        // game over panel
        var opRt = NewRect("OverPanel", canvas.transform);
        opRt.anchorMin = opRt.anchorMax = new Vector2(0.5f, 0.5f); opRt.pivot = new Vector2(0.5f, 0.5f);
        opRt.sizeDelta = new Vector2(760, 420);
        var opImg = opRt.gameObject.AddComponent<Image>(); opImg.color = new Color(0.05f, 0.06f, 0.13f, 0.94f);
        var ol = opRt.gameObject.AddComponent<Outline>(); ol.effectColor = new Color(0.4f, 0.92f, 1f, 1f); ol.effectDistance = new Vector2(3, -3);
        overPanel = opRt.gameObject;

        overText = MakeText(opRt, "GAME OVER", 48, pixelFont, new Color(1f, 0.3f, 0.4f, 1f),
            new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(700, 260));

        MakeText(opRt, "R : 다시하기      ESC : 나가기", 28, uiFont, new Color(0.4f, 0.92f, 1f, 1f),
            new Vector2(0.5f, 0f), new Vector2(0, 108), new Vector2(700, 50));

        // feedback button (opens the Google Form)
        var fbRt = NewRect("FeedbackBtn", opRt);
        fbRt.anchorMin = fbRt.anchorMax = new Vector2(0.5f, 0); fbRt.pivot = new Vector2(0.5f, 0);
        fbRt.sizeDelta = new Vector2(340, 58); fbRt.anchoredPosition = new Vector2(0, 34);
        var fimg = fbRt.gameObject.AddComponent<Image>(); fimg.color = new Color(0.12f, 0.18f, 0.34f, 1f);
        var fol = fbRt.gameObject.AddComponent<Outline>(); fol.effectColor = new Color(1f, 0.83f, 0.35f, 1f); fol.effectDistance = new Vector2(2, -2);
        var fbtn = fbRt.gameObject.AddComponent<Button>(); fbtn.targetGraphic = fimg;
        MakeText(fbRt, "💬 의견 남기기", 26, uiFont, new Color(1f, 0.83f, 0.35f, 1f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340, 58));
        fbtn.onClick.AddListener(() => Application.OpenURL(GameManager.FeedbackUrl));

        overPanel.SetActive(false);
    }

    RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    Text MakeText(Transform parent, string txt, int size, Font f, Color color, Vector2 anchor, Vector2 pos, Vector2 sizeDelta)
    {
        var rt = NewRect("Txt", parent);
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
        var t = rt.gameObject.AddComponent<Text>();
        t.font = f; t.text = txt; t.fontSize = size; t.color = color;
        t.alignment = TextAnchor.MiddleCenter; t.fontStyle = FontStyle.Bold;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }
}
