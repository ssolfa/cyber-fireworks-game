using UnityEngine;

// Lightweight idle motion so things don't feel static: gentle floating bob
// (position) or a soft pulse (scale). Works for world objects and UI.
public class Bob : MonoBehaviour
{
    public enum Mode { PositionY, Scale }
    public Mode mode = Mode.PositionY;
    public float amplitude = 0.15f;
    public float speed = 2f;
    public float phase = 0f;

    Vector3 basePos;
    Vector3 baseScale;

    void OnEnable()
    {
        basePos = transform.localPosition;
        baseScale = transform.localScale == Vector3.zero ? Vector3.one : transform.localScale;
    }

    void Update()
    {
        float o = Mathf.Sin(Time.unscaledTime * speed + phase) * amplitude;
        if (mode == Mode.PositionY)
            transform.localPosition = basePos + new Vector3(0f, o, 0f);
        else
            transform.localScale = baseScale * (1f + o);
    }
}
