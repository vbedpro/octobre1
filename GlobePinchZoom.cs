using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

// Two-finger pinch-to-zoom (mobile) and scroll wheel zoom (PC/Editor
// testing) for the globe. Only responds when the input is over the Raw
// Image displaying it. Dollies GlobeCamera closer to/further from the
// globe rather than changing FOV, clamped between minDistance/maxDistance.
//
// Dot SIZE is never touched here — normal 3D perspective already makes
// dots look proportionally bigger as the camera moves closer, for free.
// Dots are never hidden/shown by zoom either — a dot's own natural scale
// (set once at spawn, based purely on score) is what makes weak players
// fade toward invisible, not any active show/hide logic.
public class GlobePinchZoom : MonoBehaviour
{
    [Header("References")]
    public RectTransform rawImageRect;
    public Transform globeCameraTransform;
    public Transform globeTarget;
    [Tooltip("The canvas's own camera for Screen Space - Camera mode. Leave null for Screen Space - Overlay.")]
    public Camera uiCamera;
    [Tooltip("Needed for the two-finger twist/roll gesture.")]
    public GlobeController globeController;

    [Header("Zoom Limits")]
    public float minDistance = 1.5f;
    public float maxDistance = 6f;
    public float pinchZoomSpeed = 1f;
    public float scrollZoomSpeed = 0.3f;

    [Header("Two-Finger Roll (righting an upside-down globe)")]
    public float rollSpeed = 1f;

    private float previousPinchDistance = -1f;
    private float previousPinchAngle = float.NaN;

    void OnEnable()  { EnhancedTouchSupport.Enable(); }
    void OnDisable() { EnhancedTouchSupport.Disable(); }

    void Update()
    {
        HandlePinch();
        HandleScrollWheel();
    }

    void HandlePinch()
    {
        if (Touchscreen.current == null) return;

        var touches = ETouch.activeTouches;
        if (touches.Count != 2)
        {
            previousPinchDistance = -1f;
            previousPinchAngle = float.NaN;
            return;
        }

        Vector2 posA = touches[0].screenPosition;
        Vector2 posB = touches[1].screenPosition;

        if (!IsOverRawImage(posA) || !IsOverRawImage(posB))
        {
            previousPinchDistance = -1f;
            previousPinchAngle = float.NaN;
            return;
        }

        float currentDistance = Vector2.Distance(posA, posB);
        float currentAngle = Mathf.Atan2(posB.y - posA.y, posB.x - posA.x) * Mathf.Rad2Deg;

        if (previousPinchDistance > 0f)
        {
            float delta = currentDistance - previousPinchDistance;
            Zoom(-delta * pinchZoomSpeed * 0.01f);
        }

        if (!float.IsNaN(previousPinchAngle) && globeController != null)
        {
            float angleDelta = Mathf.DeltaAngle(previousPinchAngle, currentAngle);
            globeController.Roll(angleDelta * rollSpeed);
        }

        previousPinchDistance = currentDistance;
        previousPinchAngle = currentAngle;
    }

    void HandleScrollWheel()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(scroll, 0f)) return;

        bool overImage = IsOverRawImage(mouse.position.ReadValue());

        if (!overImage) return;

        // Scroll up/forward = zoom in (move camera closer), matching the
        // conventional direction for most 3D viewers.
        Zoom(-scroll * scrollZoomSpeed * 0.01f);
    }

    bool IsOverRawImage(Vector2 screenPos)
    {
        return rawImageRect != null && RectTransformUtility.RectangleContainsScreenPoint(rawImageRect, screenPos, uiCamera);
    }

    void Zoom(float amount)
    {
        if (globeCameraTransform == null || globeTarget == null) return;

        Vector3 toCamera = globeCameraTransform.position - globeTarget.position;
        float currentDist = toCamera.magnitude;
        float newDist = Mathf.Clamp(currentDist + amount, minDistance, maxDistance);

        globeCameraTransform.position = globeTarget.position + toCamera.normalized * newDist;
    }
}
