using System;
using UnityEngine;

// Tracks which SPECIFIC planet has Explosive Reproduction active — locked
// to whichever planet it was activated on, independent of whatever planet
// the player is currently viewing (per design: "stays locked to Mars even
// while viewing Venus").
//
// Wire this on the boost_explosive_reproduction BoostDefinition asset:
//   onActivated -> ExplosiveReproductionBoost.Activate()
//   onExpired   -> ExplosiveReproductionBoost.Deactivate()
public class ExplosiveReproductionBoost : MonoBehaviour
{
    public static ExplosiveReproductionBoost Instance { get; private set; }

    public const string ItemId = "boost_explosive_reproduction";
    public const float Multiplier = 10f;

    private PlanetManager.Planet? boostedPlanet = null;

    // Kept independently of BoostActivationManager's own copy, because
    // that one intentionally DROPS entries that expired while the app was
    // closed (see its RestoreSaveData comment) — but offline-earnings math
    // still needs to know exactly when this boost expired, even if it's
    // no longer "active" by the time the save is loaded. This value is
    // only meaningful together with boostedPlanet != null.
    private long expiryEpochAtLastActivation = 0;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // Wire to BoostDefinition.onActivated.
    public void Activate()
    {
        if (PlanetManager.Instance == null) return;

        boostedPlanet = PlanetManager.Instance.currentPlanet;
        expiryEpochAtLastActivation = BoostActivationManager.Instance != null
            ? BoostActivationManager.Instance.GetExpiryEpoch(ItemId)
            : 0;

        // Refresh the currently active planet's live reproduction rate —
        // only actually changes anything if currentPlanet == boostedPlanet,
        // which it always is right when this fires (boost was just
        // activated on whatever planet the player is standing on).
        UpgradeManager.Instance?.ApplyReproductionPublic();
    }

    // Wire to BoostDefinition.onExpired.
    public void Deactivate()
    {
        boostedPlanet = null;
        UpgradeManager.Instance?.ApplyReproductionPublic();
    }

    // True only while genuinely active right now (not just "was active
    // when the save was written") — this is what live reproduction and
    // real-time UI should check.
    public static float GetMultiplierFor(PlanetManager.Planet planet)
    {
        if (Instance == null || Instance.boostedPlanet == null) return 1f;
        if (Instance.boostedPlanet.Value != planet) return 1f;
        if (BoostActivationManager.Instance == null || !BoostActivationManager.Instance.IsActive(ItemId)) return 1f;
        return Multiplier;
    }

    // For offline-earnings math specifically — returns the boosted planet
    // and its expiry even if the boost has since expired, so the caller
    // can compute exactly how much of an offline window fell before vs.
    // after expiry. Returns (null, 0) if no boost was recorded at all.
    public (PlanetManager.Planet? planet, long expiryEpoch) GetPlanetAndExpiryForOfflineMath()
    {
        return (boostedPlanet, expiryEpochAtLastActivation);
    }

    // ---- Save/Load ----
    // Piggybacks on SaveManager's SaveData directly (two plain fields)
    // rather than a list-based entry like other managers, since there's
    // only ever at most one of these active at a time.

    public string GetSavePlanetName()
    {
        return boostedPlanet.HasValue ? boostedPlanet.Value.ToString() : null;
    }

    public long GetSaveExpiryEpoch()
    {
        return expiryEpochAtLastActivation;
    }

    // Must be called BEFORE UpgradeManager/PlanetManager push the active
    // planet's live reproduction rate, and before offline earnings are
    // calculated.
    public void RestoreSaveData(string planetName, long expiryEpoch)
    {
        if (string.IsNullOrEmpty(planetName))
        {
            boostedPlanet = null;
            expiryEpochAtLastActivation = 0;
            return;
        }

        if (Enum.TryParse(planetName, out PlanetManager.Planet parsed))
        {
            boostedPlanet = parsed;
            expiryEpochAtLastActivation = expiryEpoch;
        }
        else
        {
            boostedPlanet = null;
            expiryEpochAtLastActivation = 0;
        }
    }
}
