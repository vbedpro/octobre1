using UnityEngine;
using System.Collections;

// Attach to the same object as GlobeDot / GlobeDotGlowPulse. Plays a brief
// celebratory animation — flash purple, scale up, optional particle burst,
// settle back to normal color and size — whenever this player is detected
// to have just triggered a BigBang. Triggered by GlobeDotSpawner comparing
// bigBangCount between leaderboard refreshes; NOT real-time, updates at
// whatever interval the leaderboard itself already refreshes on.
[RequireComponent(typeof(Renderer))]
public class GlobeDotCelebration : MonoBehaviour
{
    [Header("Color Flash")]
    public Color celebrationColor = new Color(0.6f, 0.2f, 1f); // purple
    [Tooltip("Also flash the glow ring (_GlowColor) to match, not just the dot fill itself — usually looks more dramatic.")]
    public bool alsoFlashGlow = true;
    public float colorFadeInDuration = 0.15f;
    public float colorHoldDuration = 0.4f;
    public float colorFadeOutDuration = 0.5f;

    [Header("Scale Punch")]
    public float scaleMultiplier = 2.5f;
    public float scaleUpDuration = 0.2f;
    public float scaleHoldDuration = 0.3f;
    public float scaleDownDuration = 0.6f;

    [Header("Optional Particle Burst")]
    [Tooltip("Optional — plays once if assigned, purely additive to the color/scale animation. Leave empty if you don't want a particle effect.")]
    public ParticleSystem explosionParticles;

    private static readonly int DotColorPropertyId = Shader.PropertyToID("_DotColor");
    private static readonly int GlowColorPropertyId = Shader.PropertyToID("_GlowColor");

    private Renderer rend;
    private MaterialPropertyBlock props;
    private Coroutine activeCelebration;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        props = new MaterialPropertyBlock();
    }

    public void PlayCelebration()
    {
        Color originalDotColor = GetColorProperty(DotColorPropertyId);
        Color originalGlowColor = GetColorProperty(GlowColorPropertyId);
        Vector3 originalScale = transform.localScale; // capture fresh each time — rank-based scale may have changed since last celebration

        if (activeCelebration != null) StopCoroutine(activeCelebration);
        activeCelebration = StartCoroutine(CelebrationRoutine(originalDotColor, originalGlowColor, originalScale));

        if (explosionParticles != null) explosionParticles.Play();
    }

    // Reads a color property's normal resting value directly from the
    // shared material (not any leftover MaterialPropertyBlock override).
    Color GetColorProperty(int propertyId)
    {
        if (rend.sharedMaterial == null) return Color.white;
        if (rend.sharedMaterial.HasProperty(propertyId)) return rend.sharedMaterial.GetColor(propertyId);
        return Color.white;
    }

    void SetColor(Color dotColor, Color glowColor)
    {
        rend.GetPropertyBlock(props);
        props.SetColor(DotColorPropertyId, dotColor);
        if (alsoFlashGlow) props.SetColor(GlowColorPropertyId, glowColor);
        rend.SetPropertyBlock(props);
    }

    IEnumerator CelebrationRoutine(Color originalDotColor, Color originalGlowColor, Vector3 originalScale)
    {
        Color targetGlow = alsoFlashGlow ? celebrationColor : originalGlowColor;

        // Fade TO purple, scale up TO peak, simultaneously.
        yield return AnimateBoth(originalDotColor, celebrationColor, originalGlowColor, targetGlow,
                                  originalScale, originalScale * scaleMultiplier,
                                  Mathf.Max(colorFadeInDuration, scaleUpDuration));

        // Hold briefly at peak — this is the "explosion" beat.
        yield return new WaitForSeconds(Mathf.Max(colorHoldDuration, scaleHoldDuration));

        // Fade back to normal color AND size together.
        yield return AnimateBoth(celebrationColor, originalDotColor, targetGlow, originalGlowColor,
                                  transform.localScale, originalScale,
                                  Mathf.Max(colorFadeOutDuration, scaleDownDuration));

        SetColor(originalDotColor, originalGlowColor);
        transform.localScale = originalScale;
        activeCelebration = null;
    }

    IEnumerator AnimateBoth(Color fromDot, Color toDot, Color fromGlow, Color toGlow, Vector3 fromScale, Vector3 toScale, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t); // smoothstep

            SetColor(Color.Lerp(fromDot, toDot, t), Color.Lerp(fromGlow, toGlow, t));
            transform.localScale = Vector3.Lerp(fromScale, toScale, t);

            yield return null;
        }

        SetColor(toDot, toGlow);
        transform.localScale = toScale;
    }
}
