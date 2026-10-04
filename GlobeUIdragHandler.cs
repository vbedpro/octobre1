using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

// Drives GlobeController's rotation from standard Unity UI drag events, AND
// detects taps on dots by converting the tap's position within this Raw
// Image into a ray from GlobeCamera (not the farm camera) into the 3D
// globe scene. Attach directly to the Raw Image displaying the globe's
// render texture. Requires "Raycast Target" checked on the Raw Image (it
// is, by default).
//
// Unity's EventSystem correctly distinguishes tap vs drag on its own here:
// OnPointerClick only fires if the pointer didn't move past the drag
// threshold; if OnDrag actually fired, the click is automatically
// suppressed. No manual tolerance checking needed.
public class GlobeUIDragHandler : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IPointerClickHandler
{
    [Header("Rotation")]
    public GlobeController globeController;

    [Header("Tap Detection")]
    [Tooltip("The camera actually rendering the globe into the Raw Image's texture — NOT the farm camera.")]
    public Camera globeCamera;
    [Tooltip("Only colliders on this layer are considered tappable (globe + dots, since they share a layer for rendering purposes).")]
    public LayerMask globeLayer;
    public GlobeInfoPopup infoPopup;

    private RectTransform rectTransform;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (globeController != null) globeController.BeginDrag();
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Two or more fingers down means a pinch/roll gesture is active —
        // let GlobePinchZoom handle it exclusively, otherwise both
        // systems would rotate the globe simultaneously and fight.
        if (Touchscreen.current != null && ETouch.activeTouches.Count >= 2) return;

        if (globeController != null) globeController.Drag(eventData.delta);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (globeController != null) globeController.EndDrag();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (globeCamera == null || rectTransform == null) return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            return;

        Rect rect = rectTransform.rect;
        float u = (localPoint.x - rect.x) / rect.width;
        float v = (localPoint.y - rect.y) / rect.height;

        // Outside the Raw Image's actual bounds (can happen right at the edges) — ignore.
        if (u < 0f || u > 1f || v < 0f || v > 1f) return;

        Ray ray = globeCamera.ViewportPointToRay(new Vector3(u, v, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, globeLayer))
        {
            GlobeGiftBoxIcon giftBox = hit.collider.GetComponentInParent<GlobeGiftBoxIcon>();
            if (giftBox != null)
            {
                GlobeGiftBoxManager.Instance?.OnBoxTapped(eventData.position);
                return;
            }

            GlobeDot dot = hit.collider.GetComponentInParent<GlobeDot>();
            if (dot != null && infoPopup != null)
                infoPopup.Show(dot);
            // Hit was the globe sphere itself, not a dot — no-op, tap does nothing.
        }
    }
}
