using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class FireworkLauncher : MonoBehaviour
{
    [Header("Prefabs & Audio")]
    public GameObject fireworkPrefab;
    public AudioSource audioSource;
    public AudioClip launchSFX;
    public AudioClip explosionSFX;

    [Header("Auto Launch Settings")]
    public bool autoLaunch = false;
    public float minLaunchDelay = 0.7f;
    public float maxLaunchDelay = 1.6f;

    [Header("Idle Ambient (gentle life when idle)")]
    public bool idleAmbient = true;
    public float idleMin = 2.6f;
    public float idleMax = 4.8f;
    private float idleTime = -1f;

    [Header("Runtime state")]
    public bool gameStarted = false;
    public Color[] currentColors = new Color[] {
        new Color(1f, 0.776f, 0.302f), // gold
        new Color(1f, 0.541f, 0.169f)  // orange
    };
    public float currentIntensity = 2.6f;

    [Header("NPC (auto-show toggle)")]
    // GameManager subscribes to these.
    public System.Action NpcClicked;
    public System.Action MoonClicked;   // easter-egg: clicking the moon

    private float nextLaunchTime;
    private Camera mainCamera;
    private float suppressClicksUntil;

    // Ignore player clicks for a short time (e.g. after closing a panel or leaving day mode).
    public void SuppressClicks(float seconds)
    {
        suppressClicksUntil = Time.unscaledTime + seconds;
    }

    private void Start()
    {
        mainCamera = Camera.main;
        ScheduleNextLaunch();
    }

    private void Update()
    {
        if (!gameStarted) return;
        HandlePlayerInput();
        HandleAutoLaunch();
        HandleIdleAmbient();
    }

    private void HandleIdleAmbient()
    {
        if (!idleAmbient || autoLaunch) return; // NPC auto-show takes over when active
        if (idleTime < 0f) { idleTime = Time.time + Random.Range(idleMin, idleMax); return; }
        if (Time.time >= idleTime)
        {
            float rx = Random.Range(-7f, 7f);
            float ry = Random.Range(1.5f, 4f);
            LaunchFirework(new Vector3(rx, ry, 0f));
            idleTime = Time.time + Random.Range(idleMin, idleMax);
        }
    }

    private void HandlePlayerInput()
    {
        bool tapped = false;
        Vector2 screenPos = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
        {
            screenPos = Pointer.current.position.ReadValue();
            tapped = true;
        }
#else
        if (Input.GetMouseButtonDown(0))
        {
            screenPos = Input.mousePosition;
            tapped = true;
        }
#endif

        if (!tapped) return;
        if (Time.unscaledTime < suppressClicksUntil) return;
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        // Ignore clicks over UI (shop / start buttons / dim overlay).
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        // Clicked an interactive object? (NPC = auto-show, Moon = easter egg)
        var hitGO = RaycastClick(screenPos);
        if (hitGO != null)
        {
            if (hitGO.GetComponentInParent<FireworkNpc>() != null) { NpcClicked?.Invoke(); return; }
            if (hitGO.GetComponentInParent<MoonTrigger>() != null) { MoonClicked?.Invoke(); return; }
        }

        Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -mainCamera.transform.position.z));
        if (worldPos.y > -2.0f && worldPos.y < 5.0f && Mathf.Abs(worldPos.x) < 9.0f)
        {
            LaunchFirework(worldPos);
        }
    }

    private GameObject RaycastClick(Vector2 screenPos)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        RaycastHit2D hit2d = Physics2D.GetRayIntersection(ray, 100f);
        return hit2d.collider != null ? hit2d.collider.gameObject : null;
    }

    private void HandleAutoLaunch()
    {
        if (!autoLaunch) return;
        if (Time.time >= nextLaunchTime)
        {
            float randomX = Random.Range(-7.0f, 7.0f);
            float randomY = Random.Range(0.5f, 4.0f);
            LaunchFirework(new Vector3(randomX, randomY, 0.0f));
            ScheduleNextLaunch();
        }
    }

    private void ScheduleNextLaunch()
    {
        nextLaunchTime = Time.time + Random.Range(minLaunchDelay, maxLaunchDelay);
    }

    public void LaunchFirework(Vector3 targetPos)
    {
        if (fireworkPrefab == null) return;
        StartCoroutine(LaunchRoutine(targetPos));
    }

    // Instant small burst exactly at a position (no rising rocket). Used to draw
    // letter fireworks as a dot matrix in the sky. Returns the spawned object so
    // callers can track/clear it. Optional size for auto-fit letters.
    public GameObject LaunchBurstAt(Vector3 pos, float dotSize = 0.34f)
    {
        if (fireworkPrefab == null) return null;
        Color baseColor = (currentColors != null && currentColors.Length > 0)
            ? currentColors[Random.Range(0, currentColors.Length)]
            : Color.white;
        Color hdr = new Color(baseColor.r * currentIntensity, baseColor.g * currentIntensity, baseColor.b * currentIntensity, 1f);

        GameObject obj = Instantiate(fireworkPrefab, pos, Quaternion.identity);
        var root = obj.GetComponent<ParticleSystem>();
        if (root != null) root.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var burst = obj.transform.Find("Burst")?.GetComponent<ParticleSystem>();
        if (burst != null)
        {
            var bm = burst.main;
            bm.startColor = hdr;
            // Tight, slow, low-gravity so the dot stays put and reads as a letter pixel.
            bm.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * (dotSize / 0.34f));
            bm.startSize = new ParticleSystem.MinMaxCurve(dotSize);
            bm.startLifetime = new ParticleSystem.MinMaxCurve(2.8f);
            bm.gravityModifier = new ParticleSystem.MinMaxCurve(0f);
            burst.Emit(20);
        }
        Destroy(obj, 3f);
        return obj;
    }

    // Rocket rises from the sea, then the burst is emitted DIRECTLY at the apex.
    // (The prefab's death sub-emitter does not fire reliably, so we Emit ourselves.)
    private System.Collections.IEnumerator LaunchRoutine(Vector3 targetPos)
    {
        float startY = -4.5f;
        float speed = 12.0f;
        float rise = Mathf.Max(0.12f, (targetPos.y - startY) / speed);

        Color baseColor = (currentColors != null && currentColors.Length > 0)
            ? currentColors[Random.Range(0, currentColors.Length)]
            : Color.white;
        Color hdr = new Color(baseColor.r * currentIntensity, baseColor.g * currentIntensity, baseColor.b * currentIntensity, 1f);

        GameObject obj = Instantiate(fireworkPrefab, new Vector3(targetPos.x, startY, 0f), Quaternion.identity);
        var root = obj.GetComponent<ParticleSystem>();
        var burst = obj.transform.Find("Burst")?.GetComponent<ParticleSystem>();

        if (root != null)
        {
            var m = root.main;
            m.startColor = hdr; m.startSpeed = speed; m.startLifetime = rise;
            root.Clear(true);
            root.Play();
        }
        if (audioSource != null && launchSFX != null)
            audioSource.PlayOneShot(launchSFX, 0.06f); // very quiet launch whistle

        yield return new WaitForSeconds(rise);

        // Explode at the target — emit the burst ourselves (this is what actually shows).
        if (root != null) root.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        obj.transform.position = new Vector3(targetPos.x, targetPos.y, 0f);
        if (burst != null)
        {
            var bm = burst.main;
            bm.startColor = hdr;
            burst.Emit(90);
        }

        if (audioSource != null && explosionSFX != null)
        {
            audioSource.pitch = Random.Range(0.85f, 1.25f);
            audioSource.PlayOneShot(explosionSFX, 0.7f);
            audioSource.pitch = 1.0f;
        }

        Destroy(obj, 4f);
    }
}
