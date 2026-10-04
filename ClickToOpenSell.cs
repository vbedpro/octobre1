using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.EventSystems;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

[RequireComponent(typeof(Collider))]
public class ClickToOpenSell : MonoBehaviour
{
    [Header("Tap")]
    [Tooltip("Max finger travel that still counts as a tap, in inches (density-independent).")]
    public float tapMoveToleranceInches = 0.1f;
    [Tooltip("Used only if the device reports no DPI.")]
    public float tapMoveToleranceFallbackPixels = 35f;

    public Camera cam;

    private Vector2 pressPos;
    private bool pressedHere;
    private int pressFingerId = -1;

    void OnEnable()  { EnhancedTouchSupport.Enable(); }
    void OnDisable() { EnhancedTouchSupport.Disable(); }

    void Start()
    {
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        // don't open if a popup is already open
        if (SellManager.popupOpen || ShopManager.shopOpen || HutUpgradeManager.popupOpen || AuthenticationPopUp.popupOpen || OfflineEarningsPopup.popupOpen)
        {
            pressedHere = false;
            pressFingerId = -1;
            return;
        }

        if (Touchscreen.current != null) { DetectTouch(); return; }
        DetectMouse();
    }

    // ---- TOUCH ----
    void DetectTouch()
    {
        var touches = ETouch.activeTouches;

        if (pressFingerId == -1)
        {
            for (int i = 0; i < touches.Count; i++)
            {
                var t = touches[i];
                if (t.phase != UnityEngine.InputSystem.TouchPhase.Began) continue;
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.touchId)) continue;

                pressFingerId = t.touchId;
                pressPos = t.screenPosition;
                pressedHere = RaycastSelf(pressPos);
                return;
            }
            return;
        }

        for (int i = 0; i < touches.Count; i++)
        {
            var t = touches[i];
            if (t.touchId != pressFingerId) continue;

            if (t.phase == UnityEngine.InputSystem.TouchPhase.Ended)
            {
                TryOpen(t.screenPosition);
                pressFingerId = -1;
                pressedHere = false;
            }
            else if (t.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                pressFingerId = -1;
                pressedHere = false;
            }
            return;
        }

        // tracked finger vanished without an Ended event
        pressFingerId = -1;
        pressedHere = false;
    }

    // ---- MOUSE ----
    void DetectMouse()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            pressedHere = false;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            pressPos = mouse.position.ReadValue();
            pressedHere = RaycastSelf(pressPos);
        }
        else if (mouse.leftButton.wasReleasedThisFrame)
        {
            TryOpen(mouse.position.ReadValue());
            pressedHere = false;
        }
    }

    void TryOpen(Vector2 releasePos)
    {
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsActive) return;

        if (!pressedHere) return;
        bool moved = Vector2.Distance(pressPos, releasePos) > TapMoveTolerancePixels();
        if (moved) return;
        if (!RaycastSelf(releasePos)) return;

        AudioManager.Instance?.PlayUiElementClick();

        SellManager sm = FindAnyObjectByType<SellManager>();
        if (sm != null) sm.OpenSellPopup();
    }

    float TapMoveTolerancePixels()
    {
        float dpi = Screen.dpi;
        return dpi > 0f ? tapMoveToleranceInches * dpi : tapMoveToleranceFallbackPixels;
    }

    bool RaycastSelf(Vector2 screenPos)
    {
        if (cam == null) return false;
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            return hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform);
        return false;
    }
}
