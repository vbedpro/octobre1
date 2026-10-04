using UnityEngine;

// Attach this to the skeleton's root bone — "RigRoot" in FOX PREFAB_OK.
//
// The trot animation clip bakes forward/backward translation directly into
// RigRoot's local position (this is separate from Unity's root motion
// system, which should already be disabled on this Animator). That
// translation is what makes the fox visually run forward each loop and
// then snap back — stacking with FoxMover's own waypoint-driven movement.
//
// This script re-locks RigRoot to its original local position every frame,
// AFTER the Animator has evaluated the clip (LateUpdate runs after Unity's
// animation update pass). Rotation and every other bone's local motion are
// left untouched, so the legs/body still animate normally — the character
// just no longer physically travels via the animation itself.
public class FreezeRigRootPosition : MonoBehaviour
{
    [Tooltip("If true, only X/Z are locked and Y (vertical bob) is left free — use this if you want to keep an up/down bounce while removing forward drift.")]
    public bool lockHorizontalOnly = false;

    private Vector3 fixedLocalPosition;

    void Awake()
    {
        fixedLocalPosition = transform.localPosition;
    }

    void LateUpdate()
    {
        if (lockHorizontalOnly)
        {
            Vector3 p = transform.localPosition;
            transform.localPosition = new Vector3(fixedLocalPosition.x, p.y, fixedLocalPosition.z);
        }
        else
        {
            transform.localPosition = fixedLocalPosition;
        }
    }
}
