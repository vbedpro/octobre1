using System.Collections.Generic;
using UnityEngine;

// Central lookup: itemId -> CosmeticDefinition. Needed because leaderboard
// rows, globe popups, etc. only receive the equipped cosmetic's itemId
// (a plain string, via LeaderboardScoreMetadata) and need to resolve that
// into an actual CosmeticDefinition (colors/icon) — without depending on
// InventoryPanel, which may not even be open.
//
// Put this on a persistent object (e.g. same object as InventoryManager)
// and assign every CosmeticDefinition asset in the game to "allCosmetics".
public class CosmeticDatabase : MonoBehaviour
{
    public static CosmeticDatabase Instance { get; private set; }

    [Tooltip("Every CosmeticDefinition asset in the game — same set you'd put in InventoryPanel's list.")]
    public List<CosmeticDefinition> allCosmetics;

    private Dictionary<string, CosmeticDefinition> lookup;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        lookup = new Dictionary<string, CosmeticDefinition>();
        if (allCosmetics == null) return;

        foreach (CosmeticDefinition def in allCosmetics)
        {
            if (def == null || string.IsNullOrEmpty(def.itemId)) continue;
            lookup[def.itemId] = def;
        }
    }

    // Returns null if itemId is empty or not found — callers should fall
    // back to default styling in that case.
    public CosmeticDefinition GetById(string itemId)
    {
        if (string.IsNullOrEmpty(itemId) || lookup == null) return null;
        return lookup.TryGetValue(itemId, out CosmeticDefinition def) ? def : null;
    }
}
