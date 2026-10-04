using UnityEngine;

// Generic "show these while boost X is active" component. Attach this to
// a PERSISTENT object (e.g. your BoostsManager, or any always-active
// object) — NOT to the sprites themselves. Set itemId to whichever boost
// it should track, and drag in as many target GameObjects as you need
// (1, 2, or more) to show/hide together.
//
// IMPORTANT: don't attach this script to one of its own targets. If a
// target's GameObject gets deactivated and this script lives on that same
// object, Unity stops calling this script's own Update() the moment it
// disables itself — permanently freezing it. Keeping this component on a
// separate, always-active object avoids that entirely.
public class BoostIndicatorIcon : MonoBehaviour
{
    [Tooltip("Must exactly match the boost's Item Id, e.g. 'boost_robotic_hatching'.")]
    public string itemId;

    [Tooltip("Every object to show while the boost is active, hide while it isn't. Add as many as you need.")]
    public GameObject[] targets;

    private bool wasActive = false;

    void Start()
    {
        SetTargetsActive(false);
    }

    void Update()
    {
        if (targets == null || targets.Length == 0 || string.IsNullOrEmpty(itemId)) return;

        bool active = BoostActivationManager.Instance != null && BoostActivationManager.Instance.IsActive(itemId);
        if (active != wasActive)
            SetTargetsActive(active);

        wasActive = active;
    }

    void SetTargetsActive(bool active)
    {
        if (targets == null) return;
        foreach (GameObject target in targets)
        {
            if (target != null) target.SetActive(active);
        }
    }
}

