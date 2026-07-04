using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Draws any text (Korean + Latin + digits) in the sky as firework dots.
// It rasterizes the text with a real font into an offscreen texture, samples the
// lit pixels on a grid, then spawns a firework burst at each sampled point.
// Auto-scales so short text is big and long text still fits the screen width.
public class LetterFireworks : MonoBehaviour
{
    public Font font;   // Mulmaru (assigned in scene) for Korean support

    FireworkLauncher launcher;
    readonly List<GameObject> spawned = new List<GameObject>();

    // Sky placement (world units). Camera view is ~x[-8.9,8.9], y[-5,5].
    const float MaxWidth = 15f;    // fill most of the screen width
    const float MaxHeight = 4.2f;  // cap height so it stays in the sky
    const float CenterY = 2.4f;

    void Awake()
    {
        launcher = FindFirstObjectByType<FireworkLauncher>();
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    public void Clear()
    {
        StopAllCoroutines();
        foreach (var o in spawned) if (o != null) Destroy(o);
        spawned.Clear();
    }

    public void Spell(string text)
    {
        if (launcher == null) launcher = FindFirstObjectByType<FireworkLauncher>();
        if (launcher == null || string.IsNullOrWhiteSpace(text)) return;
        Clear();
        var points = RasterizeToPoints(text.Trim());
        StartCoroutine(SpawnRoutine(points));
    }

    struct Dot { public Vector3 pos; public float size; }

    // True if any eroded (kept) cell exists within 1 cell — used to preserve thin
    // strokes that erosion would otherwise erase entirely.
    static bool AnyErodedNear(bool[,] eroded, int gx, int gy, int gw, int gh)
    {
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = gx + dx, ny = gy + dy;
                if (nx >= 0 && ny >= 0 && nx < gw && ny < gh && eroded[nx, ny]) return true;
            }
        return false;
    }

    List<Dot> RasterizeToPoints(string text)
    {
        var dots = new List<Dot>();

        // 1) Render text to an offscreen texture using a temp camera + TextMesh.
        int texH = 96;
        int fontSize = 64;
        var camGO = new GameObject("__LF_Cam");
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.cullingMask = 1 << 31;           // dedicated layer
        cam.enabled = false;

        var tmGO = new GameObject("__LF_Text");
        tmGO.layer = 31;
        var tm = tmGO.AddComponent<TextMesh>();
        tm.text = text;
        tm.font = font;
        tm.fontSize = fontSize;
        tm.color = Color.white;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.characterSize = 1f;
        var mr = tmGO.GetComponent<MeshRenderer>();
        if (font != null && font.material != null) mr.sharedMaterial = font.material;

        // Force glyphs to exist for dynamic font
        font.RequestCharactersInTexture(text, fontSize);

        tmGO.transform.position = new Vector3(0, 0, 0);
        camGO.transform.position = new Vector3(0, 0, -10);
        camGO.transform.rotation = Quaternion.identity;

        // Fit camera to text bounds
        var bounds = mr.bounds;
        float tw = Mathf.Max(0.01f, bounds.size.x);
        float th = Mathf.Max(0.01f, bounds.size.y);
        cam.orthographicSize = th * 0.5f + 0.1f;
        float aspect = tw / th;
        int texW = Mathf.Clamp(Mathf.RoundToInt(texH * aspect), 16, 1024);
        cam.aspect = (float)texW / texH;
        camGO.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10);

        var rt = new RenderTexture(texW, texH, 16, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(texW, texH, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, texW, texH), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;

        // 2) Sample on a grid into a bool map, then erode once so strokes get thinner.
        int step = 3;
        int gw = texW / step, gh = texH / step;
        var on = new bool[gw, gh];
        for (int gy = 0; gy < gh; gy++)
            for (int gx = 0; gx < gw; gx++)
            {
                var c = tex.GetPixel(gx * step, gy * step);
                on[gx, gy] = (c.r + c.g + c.b) > 0.7f; // white-ish glyph on black
            }
        // erosion: drop cells touching an empty 4-neighbour -> removes outer rim, thinning strokes
        var eroded = new bool[gw, gh];
        for (int gy = 0; gy < gh; gy++)
            for (int gx = 0; gx < gw; gx++)
            {
                if (!on[gx, gy]) continue;
                bool edge = gx == 0 || gy == 0 || gx == gw - 1 || gy == gh - 1
                    || !on[gx - 1, gy] || !on[gx + 1, gy] || !on[gx, gy - 1] || !on[gx, gy + 1];
                eroded[gx, gy] = !edge;
            }
        var lit = new List<Vector2>();
        for (int gy = 0; gy < gh; gy++)
            for (int gx = 0; gx < gw; gx++)
            {
                // keep eroded interior; but if a whole stroke was thin (nothing left nearby), keep original
                if (eroded[gx, gy] || (on[gx, gy] && !AnyErodedNear(eroded, gx, gy, gw, gh)))
                    lit.Add(new Vector2((float)(gx * step) / texW, (float)(gy * step) / texH));
            }

        // 3) Map normalized points into the sky, fit-to-width with height cap, centered.
        float worldW = MaxWidth;
        float worldH = worldW / aspect;
        if (worldH > MaxHeight) { worldH = MaxHeight; worldW = worldH * aspect; }
        float dotSize = Mathf.Clamp(0.10f + worldH * 0.038f, 0.12f, 0.26f); // thinner strokes

        foreach (var p in lit)
        {
            float wx = (p.x - 0.5f) * worldW;
            float wy = CenterY + (p.y - 0.5f) * worldH;
            dots.Add(new Dot { pos = new Vector3(wx, wy, 0f), size = dotSize });
        }

        // cleanup temp objects
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tmGO);
        Object.DestroyImmediate(camGO);

        return dots;
    }

    IEnumerator SpawnRoutine(List<Dot> dots)
    {
        // Spawn left-to-right in small batches for a "writing" feel.
        dots.Sort((a, b) => a.pos.x.CompareTo(b.pos.x));
        int perBatch = Mathf.Max(3, dots.Count / 14);
        int i = 0;
        while (i < dots.Count)
        {
            for (int k = 0; k < perBatch && i < dots.Count; k++, i++)
            {
                var go = launcher.LaunchBurstAt(dots[i].pos, dots[i].size);
                if (go != null) spawned.Add(go);
            }
            yield return new WaitForSeconds(0.04f);
        }
        // let them fade, then drop references
        yield return new WaitForSeconds(2f);
        spawned.RemoveAll(o => o == null);
    }
}
