using UnityEngine;

// One asset per boost type. This is the "what is this boost" data —
// separate from InventoryManager (which only tracks "how many do I own")
// and BoostActivationManager (which tracks "is it active right now").
//
// itemId MUST exactly match the Item Id you set on that boost's
// BoostRow/AdBoostRow, since that's how a purchased item gets linked back
// to its icon/description/duration here.
//
// NOTE: what a boost actually DOES is wired directly in code —
// BoostActivationManager.ApplyBoostEffect() — the same pattern
// EpicUpgradeManager uses for its upgrades, rather than a UnityEvent on
// this asset. That's an intentional choice, not an oversight: a
// ScriptableObject asset can't hold a working reference to a scene object
// (there's a Unity Editor bug that blocks it), so effects live in code
// instead.
[CreateAssetMenu(fileName = "NewBoostDefinition", menuName = "Rabbit Universe/Boost Definition")]
public class BoostDefinition : ScriptableObject
{
    [Tooltip("Must exactly match the Item Id on this boost's BoostRow/AdBoostRow, AND the case in BoostActivationManager.ApplyBoostEffect().")]
    public string itemId;

    public Sprite icon;
    [TextArea] public string description;

    [Tooltip("How long the boost stays active once used, in seconds.")]
    public float durationSeconds = 600f;

    [Tooltip("True for boosts locked to whichever planet they were activated on (e.g. Explosive Reproduction). False for boosts that apply globally regardless of which planet you're viewing.")]
    public bool isPlanetSpecific = false;
}
