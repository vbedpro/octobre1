using UnityEngine;

// TRUE trackball rotation — every drag rotates the globe relative to the
// CAMERA's current view (its own up/right/forward axes), composed
// directly as incremental quaternions onto the existing rotation. This is
// what makes rotation genuinely free in every direction with no gimbal
// lock, no inversion, and no artificial pole limit — unlike a classic
// separate-yaw/pitch camera-orbit controller, which inherently breaks
// down (and inverts) once rotated far enough from its starting
// orientation.
public class GlobeController : MonoBehaviour
{
    [Header("Rotation")]
    [Tooltip("The camera looking at the globe — rotation axes are derived from ITS current orientation, not the globe's own local axes or world up.")]
    public Transform cameraTransform;
    [Tooltip("Degrees per pixel of drag.")]
    public float dragSensitivity = 0.3f;
    public float momentumDecay = 3f;
    public float momentumStopThreshold = 0.5f;

    private bool dragging = false;
    private bool wasDraggingLastFrame = false;
    private float yawVelocity;   // degrees/sec, screen-horizontal drag rate
    private float pitchVelocity; // degrees/sec, screen-vertical drag rate

    // Fired the instant a new drag begins (not during momentum coasting) —
    // used to close things like the dot info popup as soon as the player
    // starts rotating again.
    public event System.Action OnRotationStarted;

    void Update()
    {
        ApplyMomentum();

        if (dragging && !wasDraggingLastFrame)
            OnRotationStarted?.Invoke();
        wasDraggingLastFrame = dragging;
    }

    // ── Called by GlobeUIDragHandler (single-finger drag) ──────────────

    public void BeginDrag()
    {
        dragging = true;
        yawVelocity = 0f;
        pitchVelocity = 0f;
    }

    public void Drag(Vector2 pixelDelta)
    {
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        float yawDelta = pixelDelta.x * dragSensitivity;
        float pitchDelta = pixelDelta.y * dragSensitivity;

        RotateGlobe(yawDelta, pitchDelta);

        yawVelocity = yawDelta / dt;
        pitchVelocity = pitchDelta / dt;
    }

    public void EndDrag()
    {
        dragging = false;
    }

    // ── Called by GlobePinchZoom (two-finger twist gesture) ────────────
    // Rotates around the camera's OWN forward axis — this is what lets
    // you "right" an upside-down globe, independent of yaw/pitch drag.
    public void Roll(float degrees)
    {
        if (cameraTransform == null) return;
        Quaternion rollRot = Quaternion.AngleAxis(degrees, cameraTransform.forward);
        transform.rotation = rollRot * transform.rotation;
    }

    // ── Internal ──────────────────────────────────────────────────────

    void RotateGlobe(float yawDegrees, float pitchDegrees)
    {
        if (cameraTransform == null) return;

        // Rotating around the CAMERA's current up/right (not the globe's
        // own local axes, and not a fixed world up) is what guarantees
        // drag direction always matches screen direction, no matter how
        // the globe is currently oriented.
        Quaternion yawRot = Quaternion.AngleAxis(-yawDegrees, cameraTransform.up);
        Quaternion pitchRot = Quaternion.AngleAxis(pitchDegrees, cameraTransform.right);
        transform.rotation = yawRot * pitchRot * transform.rotation;
    }

    void ApplyMomentum()
    {
        if (dragging) return;
        if (Mathf.Abs(yawVelocity) < momentumStopThreshold && Mathf.Abs(pitchVelocity) < momentumStopThreshold) return;

        float dt = Time.deltaTime;
        RotateGlobe(yawVelocity * dt, pitchVelocity * dt);

        yawVelocity *= Mathf.Exp(-momentumDecay * dt);
        pitchVelocity *= Mathf.Exp(-momentumDecay * dt);
    }
}
