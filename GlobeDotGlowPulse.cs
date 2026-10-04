using UnityEngine;

// Attach directly to the dot itself (same object as GlobeDot, not a
// separate child) — animates the glow's outer radius as a sine-wave pulse
// via MaterialPropertyBlock, so no per-dot material instances are created
// (important for keeping 999 of these cheap).
[RequireComponent(typeof(Renderer))]
public class GlobeDotGlowPulse : MonoBehaviour
{
    [Tooltip("Glow radius at the peak of its pulse, in the same UV-space units as the shader's _DotRadius/_GlowRadius properties (0-0.5).")]
    public float maxGlowRadius = 0.4f;
    [Tooltip("Glow radius at the trough of its pulse.")]
    public float minGlowRadius = 0.2f;
    public float pulseSpeed = 1.5f;
    [Tooltip("Randomizes each dot's pulse timing so they don't all beat in perfect unison.")]
    public bool randomizePhase = true;

    private static readonly int GlowRadiusId = Shader.PropertyToID("_GlowRadius");

    private Renderer rend;
    private MaterialPropertyBlock props;
    private float phaseOffset;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        props = new MaterialPropertyBlock();
        phaseOffset = randomizePhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;
    }

    void Update()
    {
        float t = (Mathf.Sin(Time.time * pulseSpeed + phaseOffset) + 1f) * 0.5f; // 0..1
        float radius = Mathf.Lerp(minGlowRadius, maxGlowRadius, t);

        rend.GetPropertyBlock(props);
        props.SetFloat(GlowRadiusId, radius);
        rend.SetPropertyBlock(props);
    }
}
