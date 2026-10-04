using UnityEngine;

// Keeps a flat 2D sprite always facing the given camera, regardless of
// how the globe itself rotates underneath it — without this, a flat
// sprite parented to the rotating globe would appear to tilt/foreshorten
// or face away as the globe spins.
public class BillboardToCamera : MonoBehaviour
{
    [Tooltip("Usually the same camera GlobeUIDragHandler uses to render the globe.")]
    public Camera targetCamera;

    void LateUpdate()
    {
        if (targetCamera == null) return;
        transform.rotation = targetCamera.transform.rotation;
    }
}
