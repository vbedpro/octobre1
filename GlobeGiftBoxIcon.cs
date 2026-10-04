using UnityEngine;

// Marker component on the gift box icon prefab — needs a Collider so
// GlobeUIDragHandler's raycast can detect it, same as GlobeDot. No logic
// of its own; the actual tap handling lives in GlobeGiftBoxManager.
public class GlobeGiftBoxIcon : MonoBehaviour
{
    [Header("Attention Pulse")]
    [Tooltip("Smallest scale multiplier during the loop.")]
    public float minScale = 0.85f;
    [Tooltip("Largest scale multiplier during the loop.")]
    public float maxScale = 1.15f;
    [Tooltip("Seconds per full scale-up-and-down cycle.")]
    public float pulseSpeed = 1.2f;

    private Vector3 baseScale;

    void Awake()
    {
        baseScale = transform.localScale;

        // Force this to render AFTER the globe's land material (queue
        // 3100) AND after player dots (queue 3300) — so it always wins
        // if it ever happens to spawn at the exact same spot as a dot,
        // rather than leaving that case to unreliable distance-based
        // sorting between two objects on an identical queue value.
        // Transparent objects draw in ascending render-queue order, NOT
        // true 3D depth — without this override, land always painted
        // over the box regardless of actual 3D position. This shader
        // (URP/Unlit) doesn't expose a Render Queue field in the
        // Inspector, but it's still settable directly at runtime here.
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
            rend.material.renderQueue = 3400;
    }

    void Update()
    {
        // Smooth infinite 0->1->0 ping-pong via a sine wave, same
        // technique as BoostColorPulse — entices the player to notice
        // and tap it.
        float t = (Mathf.Sin(Time.time * (2f * Mathf.PI / pulseSpeed)) + 1f) * 0.5f;
        float scaleMultiplier = Mathf.Lerp(minScale, maxScale, t);
        transform.localScale = baseScale * scaleMultiplier;
    }
}
