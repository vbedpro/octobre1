using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.EventSystems;
using System.Collections;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class CameraController : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("Camera used to turn screen taps into world points. Defaults to this object's Camera, then Camera.main.")]
    public Camera cam;

    [Header("Ground Plane")]
    [Tooltip("World Y height of the flat ground the camera looks at. The drag math pins this plane under your finger.")]
    public float groundY = 0f;

    [Header("Bounds")]
    public float minX = -50f;
    public float maxX = 50f;
    public float minZ = -50f;
    public float maxZ = 50f;

    [Header("Rubber-Band (soft resistance past bounds, then spring back)")]
    [Tooltip("0 = no give at all (hard wall). 1 = no resistance (bounds do nothing while dragging). ~0.35 feels like a gentle stretch.")]
    [Range(0f, 1f)]
    public float rubberBandResistance = 0.35f;
    [Tooltip("Maximum distance past a bound the camera can ever stretch, no matter how long/far you keep dragging. Once hit, the camera auto-snaps back even if you're still holding the drag.")]
    public float maxOvershoot = 2f;
    [Tooltip("How long the spring-back animation takes once you release past a bound, or once maxOvershoot is hit mid-drag.")]
    public float bounceBackDuration = 0.35f;

    [Header("Momentum")]
    [Tooltip("Higher = momentum dies out faster. ~5 feels natural.")]
    public float momentumDecay = 5f;
    [Tooltip("Below this speed (world units/sec) momentum stops completely.")]
    public float momentumStopThreshold = 0.05f;
    [Tooltip("0 = snappy flicks (can be jittery). ~0.2 smooths the throw a little.")]
    [Range(0f, 0.9f)]
    public float velocitySmoothing = 0.2f;

    [Header("Zoom (scroll wheel on PC, pinch on mobile)")]
    [Tooltip("Used if the camera is Perspective.")]
    public float minFOV = 20f;
    public float maxFOV = 70f;
    [Tooltip("Used if the camera is Orthographic — smaller = more zoomed in.")]
    public float minOrthoSize = 3f;
    public float maxOrthoSize = 15f;
    public float scrollZoomSpeed = 15f;
    public float pinchZoomSpeed = 0.15f;
    [Tooltip("Higher = snappier zoom response, lower = smoother/laggier.")]
    public float zoomSmoothTime = 0.08f;

    private float targetZoom; // FOV degrees OR orthographic size, depending on cam.orthographic
    private float zoomVelocity; // used by SmoothDamp

    // ── internal state ────────────────────────────────────────────────
    enum DragSource { None, Mouse, Touch }
    DragSource source = DragSource.None;
    int activeFingerId = -1;

    Plane groundPlane;
    Vector3 anchorWorld;     // the world point currently pinned under the pointer
    bool dragging;

    Vector3 velocity;        // smoothed world units/sec while dragging
    Vector3 momentum;        // world units/sec after release
    bool hasMomentum;

    Coroutine bounceCoroutine;
    float previousPinchDistance = -1f;

    void OnEnable()  { EnhancedTouchSupport.Enable(); }
    void OnDisable() { EnhancedTouchSupport.Disable(); }

    void Start()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;

        if (cam != null) targetZoom = cam.orthographic ? cam.orthographicSize : cam.fieldOfView;
    }

    [Header("External Control")]
    [Tooltip("When true, CameraController does nothing — used by PlanetTransitionController to fully own the camera during a planet-travel zoom.")]
    public bool externalControl = false;

    // Lets an external script (PlanetTransitionController) tell us what the
    // current zoom actually is after it's done driving the camera directly,
    // so our own smoothing doesn't fight it / snap on the next frame.
    public void SetZoomTargetImmediate(float value)
    {
        targetZoom = value;
        zoomVelocity = 0f;
    }

    void Update()
    {
        if (externalControl) return;

        groundPlane = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));

        // Don't pan/zoom while a popup is open (including the leaderboard's
        // globe overview — pinching the globe must NEVER move/zoom the
        // main camera), and clear any leftover motion.
        if (SellManager.popupOpen || ShopManager.shopOpen || HutUpgradeManager.popupOpen ||
            AuthenticationPopUp.popupOpen || OfflineEarningsPopup.popupOpen || LeaderboardUI.leaderboardOpen ||
            SettingsPanelController.settingsOpen ||
            AchievementsPanelUI.achievementsOpen ||
            PlanetTransitionController.IsTransitioning)
        {
            EndDrag(applyMomentum: false);
            momentum = Vector3.zero;
            hasMomentum = false;
            previousPinchDistance = -1f;
            return;
        }

        bool handledByTouch = HandleTouch();
        if (!handledByTouch && source != DragSource.Touch)
            HandleMouse();

        HandlePinchZoom();
        HandleScrollZoom();
        ApplyZoomSmoothing();

        ApplyMomentum();
    }

    // ── Touch (mobile) ────────────────────────────────────────────────
    bool HandleTouch()
    {
        if (Touchscreen.current == null) return false;

        var touches = ETouch.activeTouches;
        int nonUICount = CountNonUITouches();

        // Two+ NON-UI fingers = an actual pinch gesture, not pan — bail so
        // HandlePinchZoom owns it. A finger held on UI (e.g. the hold-to-
        // hatch button) doesn't count here, so pan+hatch together still works.
        if (nonUICount >= 2)
        {
            EndDrag(applyMomentum: false);
            return true;
        }

        // Already tracking a finger? Follow that exact finger.
        if (source == DragSource.Touch && dragging)
        {
            for (int i = 0; i < touches.Count; i++)
            {
                if (touches[i].touchId != activeFingerId) continue;

                var t = touches[i];
                if (t.phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                    t.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                    EndDrag(applyMomentum: true);
                else
                    PanTo(t.screenPosition);

                return true;
            }

            // Finger vanished without an Ended event — end the drag safely.
            EndDrag(applyMomentum: true);
            return true;
        }

        // Not dragging yet: claim the first finger that did NOT start on UI.
        for (int i = 0; i < touches.Count; i++)
        {
            var t = touches[i];
            if (t.phase != UnityEngine.InputSystem.TouchPhase.Began) continue;

            // This finger belongs to a UI element (e.g. the hold-to-hatch button) — leave it alone.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.touchId))
                continue;

            BeginDrag(DragSource.Touch, t.screenPosition, t.touchId);
            return true;
        }

        return false;
    }

    // ── Mouse (PC / editor Game view) ─────────────────────────────────
    void HandleMouse()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return; // started on UI
            BeginDrag(DragSource.Mouse, mouse.position.ReadValue(), -1);
        }
        else if (mouse.leftButton.isPressed && dragging && source == DragSource.Mouse)
        {
            PanTo(mouse.position.ReadValue());
        }
        else if (mouse.leftButton.wasReleasedThisFrame && source == DragSource.Mouse)
        {
            EndDrag(applyMomentum: true);
        }
    }

    // ── Zoom ─────────────────────────────────────────────────────────
    void HandlePinchZoom()
    {
        if (Touchscreen.current == null || cam == null) return;

        var touches = ETouch.activeTouches;
        var nonUI = GetNonUITouches();

        if (nonUI.Count != 2)
        {
            previousPinchDistance = -1f;
            return;
        }

        Vector2 posA = nonUI[0].screenPosition;
        Vector2 posB = nonUI[1].screenPosition;
        float currentDistance = Vector2.Distance(posA, posB);

        if (previousPinchDistance > 0f)
        {
            float delta = currentDistance - previousPinchDistance;
            float minZoom = cam.orthographic ? minOrthoSize : minFOV;
            float maxZoom = cam.orthographic ? maxOrthoSize : maxFOV;
            // Pinching OUT (fingers apart, delta positive) should zoom IN
            // (lower FOV, or smaller ortho size — both "less area visible").
            targetZoom = Mathf.Clamp(targetZoom - delta * pinchZoomSpeed, minZoom, maxZoom);
        }

        previousPinchDistance = currentDistance;
    }

    // A finger counts as "UI" if it began on a UI element (e.g. the
    // hold-to-hatch button) — those never contribute to pan/pinch gestures.
    int CountNonUITouches()
    {
        var touches = ETouch.activeTouches;
        int count = 0;
        for (int i = 0; i < touches.Count; i++)
            if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject(touches[i].touchId))
                count++;
        return count;
    }

    System.Collections.Generic.List<ETouch> GetNonUITouches()
    {
        var touches = ETouch.activeTouches;
        var result = new System.Collections.Generic.List<ETouch>();
        for (int i = 0; i < touches.Count; i++)
            if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject(touches[i].touchId))
                result.Add(touches[i]);
        return result;
    }

    void HandleScrollZoom()
    {
        var mouse = Mouse.current;
        if (mouse == null || cam == null) return;

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(scroll, 0f)) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        float minZoom = cam.orthographic ? minOrthoSize : minFOV;
        float maxZoom = cam.orthographic ? maxOrthoSize : maxFOV;

        // Scroll up = zoom in (lower FOV / smaller ortho size), matching conventional direction.
        targetZoom = Mathf.Clamp(targetZoom - scroll * scrollZoomSpeed * 0.01f, minZoom, maxZoom);
    }

    void ApplyZoomSmoothing()
    {
        if (cam == null) return;

        float current = cam.orthographic ? cam.orthographicSize : cam.fieldOfView;
        if (Mathf.Approximately(current, targetZoom)) return;

        float smoothed = Mathf.SmoothDamp(current, targetZoom, ref zoomVelocity, zoomSmoothTime);

        if (cam.orthographic) cam.orthographicSize = smoothed;
        else cam.fieldOfView = smoothed;
    }

    // ── Drag lifecycle ────────────────────────────────────────────────
    void BeginDrag(DragSource src, Vector2 screenPos, int fingerId)
    {
        if (!ScreenToGround(screenPos, out Vector3 world)) return;

        // Cancel any in-progress spring-back the moment the player grabs
        // the camera again — they're taking manual control.
        if (bounceCoroutine != null) { StopCoroutine(bounceCoroutine); bounceCoroutine = null; }

        source = src;
        activeFingerId = fingerId;
        anchorWorld = world;
        dragging = true;
        velocity = Vector3.zero;
        momentum = Vector3.zero;
        hasMomentum = false;
    }

    void PanTo(Vector2 screenPos)
    {
        if (!dragging) return;
        if (!ScreenToGround(screenPos, out Vector3 currentWorld)) return;

        // Move the camera so the grabbed world point stays under the finger.
        Vector3 delta = anchorWorld - currentWorld;
        delta.y = 0f;

        Vector3 before = transform.position;
        MoveByRubberBanded(delta);
        Vector3 applied = transform.position - before;

        // Estimate throw velocity from how far we actually moved this frame.
        float dt = Mathf.Max(Time.deltaTime, 1e-5f);
        Vector3 instant = applied / dt;
        velocity = Vector3.Lerp(instant, velocity, velocitySmoothing);

        // Hit the hard overshoot cap — snap back NOW, even though the
        // pointer is still down. The player has to release and press again
        // to grab the camera once more; this is what actually keeps the
        // world bounds visible/enforced instead of an infinite soft drag.
        if (IsAtOvershootCap(transform.position))
            ForceReleaseAndBounce();
    }

    bool IsAtOvershootCap(Vector3 pos)
    {
        const float eps = 0.01f;
        return pos.x >= maxX + maxOvershoot - eps || pos.x <= minX - maxOvershoot + eps ||
               pos.z >= maxZ + maxOvershoot - eps || pos.z <= minZ - maxOvershoot + eps;
    }

    void ForceReleaseAndBounce()
    {
        dragging = false;
        source = DragSource.None;
        activeFingerId = -1;
        velocity = Vector3.zero;
        momentum = Vector3.zero;
        hasMomentum = false;
        StartBounceBack();
    }

    void EndDrag(bool applyMomentum)
    {
        if (!dragging)
        {
            source = DragSource.None;
            activeFingerId = -1;
            return;
        }

        dragging = false;
        source = DragSource.None;
        activeFingerId = -1;

        // If we ended up outside bounds (rubber-banded during drag), spring
        // back smoothly instead of just snapping or leaving it stretched.
        if (IsOutOfBounds(transform.position))
        {
            StartBounceBack();
            velocity = Vector3.zero;
            return;
        }

        if (applyMomentum &&
            velocity.sqrMagnitude > momentumStopThreshold * momentumStopThreshold)
        {
            momentum = velocity;
            hasMomentum = true;
        }
        velocity = Vector3.zero;
    }

    // ── Momentum ──────────────────────────────────────────────────────
    void ApplyMomentum()
    {
        if (!hasMomentum || dragging) return;

        float dt = Time.deltaTime;
        Vector3 before = transform.position;
        MoveByRubberBanded(momentum * dt);
        Vector3 applied = transform.position - before;

        if (Mathf.Abs(applied.x) < Mathf.Abs(momentum.x * dt) - 1e-6f) momentum.x = 0f;
        if (Mathf.Abs(applied.z) < Mathf.Abs(momentum.z * dt) - 1e-6f) momentum.z = 0f;

        momentum *= Mathf.Exp(-momentumDecay * dt); // exponential slow-down

        if (momentum.magnitude < momentumStopThreshold)
        {
            momentum = Vector3.zero;
            hasMomentum = false;
        }

        // Momentum carried us out of bounds — stop coasting and spring back.
        if (IsOutOfBounds(transform.position))
        {
            hasMomentum = false;
            momentum = Vector3.zero;
            StartBounceBack();
        }
    }

    // ── Bounds / rubber-band / bounce-back ─────────────────────────────

    bool IsOutOfBounds(Vector3 pos)
    {
        return pos.x < minX || pos.x > maxX || pos.z < minZ || pos.z > maxZ;
    }

    // Moves the camera, but with soft resistance once past a bound instead
    // of a hard clamp — each additional unit of overshoot moves the camera
    // less and less, giving a rubber-band stretch feel while dragging.
    void MoveByRubberBanded(Vector3 delta)
    {
        Vector3 pos = transform.position;

        pos.x = ApplyAxisRubberBand(pos.x, delta.x, minX, maxX);
        pos.z = ApplyAxisRubberBand(pos.z, delta.z, minZ, maxZ);

        transform.position = pos;
    }

    float ApplyAxisRubberBand(float current, float delta, float min, float max)
    {
        float next = current + delta;

        // Already past the bound and moving FURTHER past it — apply resistance,
        // capped so the stretch can never exceed maxOvershoot regardless of
        // how much more the player keeps dragging.
        if (next > max)
        {
            float overshoot = next - Mathf.Max(current, max);
            float stretched = Mathf.Max(current, max) + overshoot * rubberBandResistance;
            next = Mathf.Min(stretched, max + maxOvershoot);
        }
        else if (next < min)
        {
            float overshoot = Mathf.Min(current, min) - next;
            float stretched = Mathf.Min(current, min) - overshoot * rubberBandResistance;
            next = Mathf.Max(stretched, min - maxOvershoot);
        }

        return next;
    }

    void StartBounceBack()
    {
        if (bounceCoroutine != null) StopCoroutine(bounceCoroutine);
        bounceCoroutine = StartCoroutine(BounceBackRoutine());
    }

    IEnumerator BounceBackRoutine()
    {
        Vector3 start = transform.position;
        Vector3 target = new Vector3(
            Mathf.Clamp(start.x, minX, maxX),
            start.y,
            Mathf.Clamp(start.z, minZ, maxZ)
        );

        float elapsed = 0f;
        while (elapsed < bounceBackDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / bounceBackDuration);
            t = EaseOutBack(t);
            transform.position = Vector3.Lerp(start, target, t);
            yield return null;
        }

        transform.position = target;
        bounceCoroutine = null;
    }

    // Same easing curve BigBangAnimator already uses for its morph — slight
    // overshoot then settle, gives the bounce a springy feel.
    float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    // ── Helpers ───────────────────────────────────────────────────────
    bool ScreenToGround(Vector2 screenPos, out Vector3 world)
    {
        world = default;
        if (cam == null) return false;
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (groundPlane.Raycast(ray, out float enter))
        {
            world = ray.GetPoint(enter);
            return true;
        }
        return false;
    }
}

