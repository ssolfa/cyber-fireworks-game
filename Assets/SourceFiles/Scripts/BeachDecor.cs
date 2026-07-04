using System.Collections.Generic;
using UnityEngine;

// Cozy decoration items placed around the beach NPC. The shop unlocks (free) and
// toggles each item's presence in the scene. Items render as SpriteRenderers on
// the "Sea" sorting layer (same as the NPC) with per-item order for depth.
public class BeachDecor : MonoBehaviour
{
    [System.Serializable]
    public class Item
    {
        public string name;
        public string enName;
        public Sprite sprite;
        public Vector2 pos;       // world position
        public float worldHeight; // target height in world units
        public int order;         // sorting order within "Sea" layer (NPC = 0)
        public bool bob;          // gentle idle motion
        [HideInInspector] public bool unlocked;
        [HideInInspector] public bool placed;
        [HideInInspector] public GameObject instance;
    }

    public Item[] items;

    public int Count => items != null ? items.Length : 0;
    public Item Get(int i) => items[i];

    public void ToggleUnlockOrPlace(int i)
    {
        var it = items[i];
        if (!it.unlocked) { it.unlocked = true; SetPlaced(i, true); return; }
        SetPlaced(i, !it.placed);
    }

    public void SetPlaced(int i, bool on)
    {
        var it = items[i];
        it.placed = on;
        if (on)
        {
            if (it.instance == null) it.instance = Build(it);
            it.instance.SetActive(true);
        }
        else if (it.instance != null) it.instance.SetActive(false);
    }

    GameObject Build(Item it)
    {
        var go = new GameObject("Decor_" + it.name);
        go.transform.SetParent(transform);
        go.transform.position = new Vector3(it.pos.x, it.pos.y, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = it.sprite;
        sr.sortingLayerName = "Sea";
        sr.sortingOrder = it.order;
        if (it.sprite != null)
        {
            float s = it.worldHeight / it.sprite.bounds.size.y;
            go.transform.localScale = new Vector3(s, s, 1f);
        }
        if (it.bob)
        {
            var b = go.AddComponent<Bob>();
            b.mode = Bob.Mode.PositionY; b.amplitude = 0.06f; b.speed = 2f;
        }
        return go;
    }

    // Hide all placed items (e.g. when leaving to day mode); keeps unlock/placed state.
    public void SetAllActive(bool on)
    {
        if (items == null) return;
        foreach (var it in items)
            if (it.instance != null) it.instance.SetActive(on && it.placed);
    }
}
