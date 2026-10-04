using System;
using System.Collections.Generic;
using UnityEngine;

// Tracks which boosts are CURRENTLY ACTIVE and when they expire, using
// real wall-clock time (Unix epoch seconds via DateTimeOffset.UtcNow) —
// NOT Time.time, which resets to 0 every time the app launches. This is
// what lets a boost survive the player closing and reopening the game:
// its expiry is a fixed point in real time, not a countdown that only
// ticks while the app happens to be running.
//
// Separate from InventoryManager (which only knows "how many do I own")
// so any other script (SellManager, UpgradeManager, etc.) can ask
// "is GoldRush active right now" without touching the inventory panel.
public class BoostActivationManager : MonoBehaviour
{
    public static BoostActivationManager Instance { get; private set; }

    [Tooltip("Every BoostDefinition asset in the game — used to resolve a saved itemId back to its data (duration was already spent; this is just for onExpired lookups) when a save is loaded.")]
    public List<BoostDefinition> allDefinitions;

    // itemId -> Unix epoch seconds at which the boost expires.
    private readonly Dictionary<string, long> activeUntilEpoch = new Dictionary<string, long>();

    // itemId -> its definition. Built from allDefinitions in Awake, plus
    // whatever gets activated at runtime (covers definitions not listed,
    // though every real boost should be listed).
    private readonly Dictionary<string, BoostDefinition> defsById = new Dictionary<string, BoostDefinition>();

    // Fired once per activation and once per expiry — lets UI (and things
    // like the Robotic Hatching pause-on-planet-switch behaviour) react
    // immediately instead of polling.
    public event Action<string> OnBoostActivated;
    public event Action<string> OnBoostExpired;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (allDefinitions != null)
        {
            foreach (BoostDefinition def in allDefinitions)
            {
                if (def != null && !string.IsNullOrEmpty(def.itemId))
                    defsById[def.itemId] = def;
            }
        }
    }

    void Update()
    {
        if (activeUntilEpoch.Count == 0) return;

        long nowEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        List<string> expired = null;
        foreach (KeyValuePair<string, long> kvp in activeUntilEpoch)
        {
            if (nowEpoch >= kvp.Value)
            {
                expired ??= new List<string>();
                expired.Add(kvp.Key);
            }
        }

        if (expired == null) return;

        foreach (string itemId in expired)
            ExpireBoost(itemId);
    }

    void ExpireBoost(string itemId)
    {
        activeUntilEpoch.Remove(itemId);
        ApplyBoostEffect(itemId, activating: false);
        OnBoostExpired?.Invoke(itemId);
    }

    public bool IsActive(string itemId)
    {
        return activeUntilEpoch.ContainsKey(itemId);
    }

    // For planet-specific boosts (BoostDefinition.isPlanetSpecific), which
    // planet is it currently locked to — or null if not active, or if this
    // boost isn't planet-specific at all. Hardcoded per-boost, same
    // pattern as ApplyBoostEffect() above.
    public PlanetManager.Planet? GetActivePlanetFor(string itemId)
    {
        if (!IsActive(itemId)) return null;

        switch (itemId)
        {
            case ExplosiveReproductionBoost.ItemId:
                return ExplosiveReproductionBoost.Instance != null
                    ? ExplosiveReproductionBoost.Instance.GetPlanetAndExpiryForOfflineMath().planet
                    : null;

            // Add a case here for each future planet-specific boost.
            default:
                return null;
        }
    }

    // Seconds remaining, or 0 if not active.
    public float GetTimeRemaining(string itemId)
    {
        if (!activeUntilEpoch.TryGetValue(itemId, out long endEpoch)) return 0f;
        long nowEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return Mathf.Max(0f, endEpoch - nowEpoch);
    }

    // Absolute Unix epoch second the boost expires at, or 0 if not active.
    // Used by offline-earnings math (e.g. Explosive Reproduction) to know
    // exactly how much of the offline window the boost was still active
    // for.
    public long GetExpiryEpoch(string itemId)
    {
        return activeUntilEpoch.TryGetValue(itemId, out long endEpoch) ? endEpoch : 0L;
    }

    // Consumes one from InventoryManager and either starts the active
    // timer (normal timed boosts) or applies the effect immediately with
    // no active state at all (instant boosts — durationSeconds <= 0).
    // Returns false (does nothing) if the player doesn't own one, or if
    // this boost is a timed boost that's already active.
    public bool TryActivate(BoostDefinition def)
    {
        if (def == null || string.IsNullOrEmpty(def.itemId)) return false;

        bool isInstant = def.durationSeconds <= 0f;
        if (!isInstant && IsActive(def.itemId)) return false;

        if (InventoryManager.Instance == null) return false;
        if (InventoryManager.Instance.GetCount(def.itemId) <= 0) return false;

        defsById[def.itemId] = def;

        // IMPORTANT: mark the boost active BEFORE consuming the item.
        // ConsumeItem() below fires InventoryManager.OnInventoryChanged
        // SYNCHRONOUSLY — any UI listening to that event (like
        // InventoryPanel, to decide whether to keep a row visible for an
        // active-but-0-owned boost) reads IsActive() at that exact
        // moment. If this were set AFTER consuming, the event would fire
        // while IsActive() still incorrectly says false, causing a row to
        // flicker hidden for a frame with no further refresh afterward.
        if (!isInstant)
        {
            long nowEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            activeUntilEpoch[def.itemId] = nowEpoch + (long)def.durationSeconds;
        }
        // Instant boosts skip activeUntilEpoch entirely — IsActive() stays
        // false, so InventoryItemRow never shows a timer for these, just
        // the item count going down by 1.

        InventoryManager.Instance.ConsumeItem(def.itemId, 1);

        ApplyBoostEffect(def.itemId, activating: true);
        OnBoostActivated?.Invoke(def.itemId);
        return true;
    }

    // Hardcoded per-boost hookup — same pattern as EpicUpgradeManager's
    // switch over upgrade type, instead of routing through ScriptableObject
    // UnityEvents (which kept hitting a "Type mismatch" Inspector bug when
    // wiring scene object references into asset fields).
    void ApplyBoostEffect(string itemId, bool activating)
    {
        switch (itemId)
        {
            case ExplosiveReproductionBoost.ItemId:
                if (activating) ExplosiveReproductionBoost.Instance?.Activate();
                else ExplosiveReproductionBoost.Instance?.Deactivate();
                break;

            case RoboticHatchingBoost.ItemId:
                if (activating) RoboticHatchingBoost.Instance?.Activate();
                else RoboticHatchingBoost.Instance?.Deactivate();
                break;

            case "boost_cash_injection":
                // Instant, current-planet-only — GameManager.totalCoins IS
                // the active planet's balance (coins are per-planet,
                // swapped in/out on planet switch), so a plain AddCoins()
                // here is already correctly scoped to "current planet only".
                if (activating) GameManager.Instance?.AddCoins(30000);
                break;

            // Add a case here for each new boost as it's implemented.
        }
    }

    // ---- Save/Load ----

    [System.Serializable]
    public class BoostSaveEntry
    {
        public string itemId;
        public long expiryEpoch;
    }

    public List<BoostSaveEntry> GetSaveData()
    {
        List<BoostSaveEntry> result = new List<BoostSaveEntry>();
        foreach (KeyValuePair<string, long> kvp in activeUntilEpoch)
        {
            result.Add(new BoostSaveEntry { itemId = kvp.Key, expiryEpoch = kvp.Value });
        }
        return result;
    }

    // IMPORTANT: call this BEFORE offline earnings get calculated on load
    // — boosts like Explosive Reproduction need their real expiry known
    // ahead of that math, not after.
    public void RestoreSaveData(List<BoostSaveEntry> saved)
    {
        activeUntilEpoch.Clear();
        if (saved == null) return;

        long nowEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        foreach (BoostSaveEntry entry in saved)
        {
            if (string.IsNullOrEmpty(entry.itemId)) continue;

            if (entry.expiryEpoch > nowEpoch)
            {
                // Still had time left when the game was closed — stays active.
                activeUntilEpoch[entry.itemId] = entry.expiryEpoch;
            }
            // else: expired while the app was closed. Intentionally NOT
            // invoking onExpired here — this restore step runs during
            // load, before other systems (planets, huts, etc.) are fully
            // wired up, and onExpired hooks are meant to undo a live
            // gameplay effect that, in this case, was never re-applied
            // this session in the first place. Offline-earnings math
            // (which runs right after this) is what actually accounts
            // for the time the boost WAS active before it expired.
        }
    }
}
