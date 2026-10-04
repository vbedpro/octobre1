using UnityEngine;
using System.Collections.Generic;

// Tracks the level of every Epic upgrade. Epic upgrades are bought with
// Golden Carrots and are PERMANENT — never reset by PrestigeManager.
public class EpicUpgradeManager : MonoBehaviour
{
    public static EpicUpgradeManager Instance;

    public enum EpicUpgradeType
    {
        GoldenRabbits,      // batches sold -> +1 carrot
        SharpReflexes,      // +10% fox tap coin reward
        BloodlineBoost,     // +1% permanent reproduction per Big Bang
        DomainExpansion,    // +10% permanent hut capacity per Big Bang
        ExtraWheels,        // +3 permanent truck capacity per Big Bang
        NightShift,         // +5 min offline earnings cap per level, permanent — see SaveManager.ApplyOfflineEarnings()
        FasterHands,        // +1 rabbit/sec base hatch speed
        PremiumFur,         // +1 coin per rabbit value
        LuckyClover,        // +1 carrot from quest rewards
        GenerousRewards,    // +10% quest coin rewards
        AvidBreeder,        // +10% quest rabbit rewards
        GreedyBargainer     // -1% common upgrade costs
    }

    private readonly Dictionary<EpicUpgradeType, int> levels = new Dictionary<EpicUpgradeType, int>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public int GetLevel(EpicUpgradeType type)
    {
        return levels.TryGetValue(type, out int lvl) ? lvl : 0;
    }

    // Called by EpicUpgradeRow whenever a level is bought.
    public void OnEpicUpgradeChanged(EpicUpgradeType type, int newLevel)
    {
        levels[type] = newLevel;

        switch (type)
        {
            case EpicUpgradeType.BloodlineBoost:
                // Recompute reproduction now so the new per-prestige rate
                // is reflected immediately, not just on the next Big Bang.
                if (UpgradeManager.Instance != null)
                    UpgradeManager.Instance.OnCategoryChanged(UpgradeManager.UpgradeCategory.Reproduction);
                break;

            case EpicUpgradeType.DomainExpansion:
                if (HutUpgradeManager.Instance != null)
                    HutUpgradeManager.Instance.RecalculateAllCapacities();
                break;

            case EpicUpgradeType.ExtraWheels:
                if (UpgradeManager.Instance != null)
                    UpgradeManager.Instance.OnCategoryChanged(UpgradeManager.UpgradeCategory.TruckCapacity);
                break;

            case EpicUpgradeType.FasterHands:
                if (UpgradeManager.Instance != null)
                    UpgradeManager.Instance.OnCategoryChanged(UpgradeManager.UpgradeCategory.HatchSpeed);
                break;

            case EpicUpgradeType.PremiumFur:
                if (UpgradeManager.Instance != null)
                    UpgradeManager.Instance.OnCategoryChanged(UpgradeManager.UpgradeCategory.RabbitValue);
                break;

            case EpicUpgradeType.GreedyBargainer:
                // Refresh every Common row's cost text so the discount shows immediately.
                foreach (ShopUpgradeRow row in FindObjectsByType<ShopUpgradeRow>(FindObjectsInactive.Include))
                    if (!row.isEpic) row.RefreshUIPublic();
                break;

            // NightShift needs no immediate action — it only affects the
            // NEXT offline-earnings calculation (SaveManager.LoadGame),
            // not any live/running state.

            // Other upgrade types wired in separately.
        }
    }
}
