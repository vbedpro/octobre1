using System.Collections.Generic;
using UnityEngine;

// Tracks which ONE cosmetic is equipped per CosmeticType (Nickname,
// Background, etc.). Equipping a cosmetic auto-unequips whatever else was
// equipped in that same type slot. Doesn't care about ownership —
// InventoryItemRow/InventoryPanel already only show Equip buttons for
// cosmetics the player owns.
public class CosmeticEquipManager : MonoBehaviour
{
    public static CosmeticEquipManager Instance { get; private set; }

    // type -> currently equipped itemId (absent key = nothing equipped for that type)
    private Dictionary<CosmeticType, string> equipped = new Dictionary<CosmeticType, string>();

    // itemId -> how many times it's been (re)activated, i.e. transitioned
    // from unequipped to equipped. Used by cosmetics like the cycling-color
    // Colored Nickname to pick a different color each time it's toggled
    // back on. Persists across sessions (see save/load below).
    private Dictionary<string, int> activationCounts = new Dictionary<string, int>();

    // Fired whenever equip state changes, passing the type slot that changed.
    public event System.Action<CosmeticType> OnEquipChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public bool IsEquipped(string itemId, CosmeticType type)
    {
        return equipped.TryGetValue(type, out string current) && current == itemId;
    }

    public string GetEquipped(CosmeticType type)
    {
        return equipped.TryGetValue(type, out string current) ? current : null;
    }

    public int GetActivationCount(string itemId)
    {
        return activationCounts.TryGetValue(itemId, out int count) ? count : 0;
    }

    public void Equip(string itemId, CosmeticType type)
    {
        if (string.IsNullOrEmpty(itemId)) return;

        // Only counts as a genuine (re)activation if this itemId wasn't
        // already the one equipped for this slot — avoids double-counting
        // if Equip() gets called again on something already active.
        bool alreadyEquipped = IsEquipped(itemId, type);
        equipped[type] = itemId;

        if (!alreadyEquipped)
        {
            if (!activationCounts.ContainsKey(itemId)) activationCounts[itemId] = 0;
            activationCounts[itemId]++;
        }

        OnEquipChanged?.Invoke(type);
    }

    public void Unequip(CosmeticType type)
    {
        if (equipped.Remove(type))
            OnEquipChanged?.Invoke(type);
    }

    // Wire this to the row's Equip/Disable button — handles both directions.
    public void ToggleEquip(string itemId, CosmeticType type)
    {
        if (IsEquipped(itemId, type))
            Unequip(type);
        else
            Equip(itemId, type);
    }

    // ---- Save/Load (same pattern as InventoryManager) ----

    [System.Serializable]
    public class EquipSaveEntry
    {
        public CosmeticType type;
        public string itemId;
    }

    public List<EquipSaveEntry> GetSaveData()
    {
        List<EquipSaveEntry> result = new List<EquipSaveEntry>();
        foreach (KeyValuePair<CosmeticType, string> kvp in equipped)
            result.Add(new EquipSaveEntry { type = kvp.Key, itemId = kvp.Value });
        return result;
    }

    public void RestoreSaveData(List<EquipSaveEntry> saved)
    {
        equipped.Clear();
        if (saved == null) return;

        foreach (EquipSaveEntry entry in saved)
        {
            if (!string.IsNullOrEmpty(entry.itemId))
                equipped[entry.type] = entry.itemId;
        }
    }

    // ---- Activation count save/load (separate list — different shape) ----

    [System.Serializable]
    public class ActivationCountEntry
    {
        public string itemId;
        public int count;
    }

    public List<ActivationCountEntry> GetActivationSaveData()
    {
        List<ActivationCountEntry> result = new List<ActivationCountEntry>();
        foreach (KeyValuePair<string, int> kvp in activationCounts)
            result.Add(new ActivationCountEntry { itemId = kvp.Key, count = kvp.Value });
        return result;
    }

    public void RestoreActivationSaveData(List<ActivationCountEntry> saved)
    {
        activationCounts.Clear();
        if (saved == null) return;

        foreach (ActivationCountEntry entry in saved)
        {
            if (!string.IsNullOrEmpty(entry.itemId))
                activationCounts[entry.itemId] = entry.count;
        }
    }
}
