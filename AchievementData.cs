using UnityEngine;

// One achievement's full progression, normally 4 tiers (Bronze/Silver/
// Gold/Platinum).
//
// IMPORTANT — target semantics depend on achievement type:
//   - Normal achievements (not lifetime, not inverted): each tier's
//     Target is the amount needed for JUST THAT TIER, counted fresh from
//     0 — NOT a cumulative total. E.g. Bronze Target=5, Silver Target=10
//     means "build 5 huts for Bronze, THEN build 10 MORE (not 5 more) for
//     Silver." currentStatValue resets to 0 the moment a tier is claimed.
//   - isLifetimeStat achievements (Lifetime Rabbits, BigBang count):
//     mirror an already-existing PERMANENT external total, so targets
//     stay CUMULATIVE/absolute and currentStatValue is never reset.
//   - isInvertedProgress achievements (Global Rank): rank is always an
//     absolute live position, not something that resets per tier — stays
//     cumulative/absolute regardless.
//
// Also supports:
//   - singleTier: only Bronze exists; claiming it jumps straight to fully
//     complete (currentTierIndex = 4) — "Unlock all planets", "Spend to 0".
[System.Serializable]
public class AchievementData
{
    public string id;
    public enum RewardType { Coins, Carrots, Cosmetic }
    [Tooltip("What currency this achievement's rewards actually grant, across all its tiers.")]
    public RewardType rewardType = RewardType.Coins;
    public string description;

    [Tooltip("Check this for achievements with only ONE tier (e.g. 'Unlock all planets') — only Bronze Target/Reward are used; claiming it jumps straight to fully complete.")]
    public bool singleTier = false;

    [Tooltip("Check this ONLY for achievements mirroring an already-existing PERMANENT stat elsewhere in the game (Lifetime Rabbits, BigBang count). These use CUMULATIVE/absolute targets and never reset currentStatValue on claim. Also still pauses (stops incrementing) once ready-to-claim... actually NO: lifetime stats are exempt from the pause too, since they must always reflect the true external value.")]
    public bool isLifetimeStat = false;

    [Tooltip("Check this for achievements where LOWER is better (e.g. leaderboard rank: bronze=5, silver=3, gold=2, platinum=1). Always cumulative/absolute — rank doesn't 'reset' per tier.")]
    public bool isInvertedProgress = false;
    [Tooltip("Only used when Is Inverted Progress is checked — auto-captured the first time a rank is ever recorded. Leave at 0; it fills itself in.")]
    public long rankStartingReference = 0;

    [Tooltip("For NORMAL achievements: the amount needed for JUST this tier (not cumulative). For Lifetime/Inverted achievements: the cumulative absolute total.")]
    public long bronzeTarget;
    public long silverTarget;
    public long goldTarget;
    public long platinumTarget;

    public Sprite bronzeRewardIcon;
    public long bronzeRewardQuantity;
    public Sprite silverRewardIcon;
    public long silverRewardQuantity;
    public Sprite goldRewardIcon;
    public long goldRewardQuantity;
    public Sprite platinumRewardIcon;
    public long platinumRewardQuantity;

    [Header("Only used when Reward Type = Cosmetic")]
    [Tooltip("Inventory item ID granted for each tier — same string convention as boosts (e.g. 'cosmetic_golden_crown'). Ignored entirely unless Reward Type is set to Cosmetic. Always grants exactly 1, regardless of the Reward Quantity fields above (equippable cosmetics aren't stackable amounts).")]
    public string bronzeCosmeticId;
    public string silverCosmeticId;
    public string goldCosmeticId;
    public string platinumCosmeticId;

    [Header("Cosmetic Row Visuals (only used when Reward Type = Cosmetic)")]
    [Tooltip("Shown by CosmeticAchievementRowUI while NOT yet claimed — a preview of what the cosmetic actually looks like.")]
    public Sprite cosmeticPreviewSprite;
    [Tooltip("Shown by CosmeticAchievementRowUI once this achievement IS claimed.")]
    public Sprite cosmeticCompletedSprite;

    // For NORMAL achievements: progress toward the CURRENT tier only,
    // reset to 0 each time a tier is claimed.
    // For Lifetime/Inverted achievements: the raw cumulative/absolute
    // value, never reset.
    public long currentStatValue;

    // 0 = nothing claimed, 1 = Bronze claimed, 2 = Silver claimed,
    // 3 = Gold claimed, 4 = Platinum claimed (fully complete).
    public int currentTierIndex;

    // True for achievements that use cumulative/absolute targets and
    // never reset currentStatValue on claim.
    bool UsesCumulativeTargets => isLifetimeStat || isInvertedProgress;

    public long TargetForTier(int tierIndex)
    {
        if (singleTier) return bronzeTarget;
        return tierIndex switch
        {
            0 => bronzeTarget,
            1 => silverTarget,
            2 => goldTarget,
            3 => platinumTarget,
            _ => platinumTarget
        };
    }

    // Which cosmeticId is relevant to show RIGHT NOW — used by
    // CosmeticAchievementRowUI to resolve the actual CosmeticDefinition
    // (and its icon) via CosmeticDatabase. currentTierIndex 4 means fully
    // complete, so clamp to 3 (platinum) rather than going out of range.
    public string CosmeticIdForCurrentTier()
    {
        if (singleTier) return bronzeCosmeticId;

        return Mathf.Clamp(currentTierIndex, 0, 3) switch
        {
            0 => bronzeCosmeticId,
            1 => silverCosmeticId,
            2 => goldCosmeticId,
            _ => platinumCosmeticId
        };
    }

    public bool IsReadyToClaim()
    {
        if (currentTierIndex >= 4) return false;
        long target = TargetForTier(currentTierIndex);

        if (isInvertedProgress)
        {
            // 0 is the un-set default — for a NORMAL achievement that
            // correctly means "no progress yet," but for an INVERTED one
            // (lower is better) it looks like the best possible value,
            // instantly satisfying currentStatValue <= target before any
            // real rank has ever actually been recorded. Guard against it.
            if (currentStatValue <= 0) return false;
            return currentStatValue <= target;
        }

        return currentStatValue >= target;
    }

    public float GetDisplayedFraction()
    {
        if (currentTierIndex >= 4) return 1f;
        if (IsReadyToClaim()) return 1f;

        long thisTarget = TargetForTier(currentTierIndex);

        if (isInvertedProgress)
        {
            long previousTarget = currentTierIndex == 0 ? rankStartingReference : TargetForTier(currentTierIndex - 1);
            long span = previousTarget - thisTarget;
            if (span <= 0) return 0f;
            float fraction = (float)(previousTarget - currentStatValue) / span;
            return Mathf.Clamp01(fraction);
        }
        else if (isLifetimeStat)
        {
            long previousTarget = currentTierIndex == 0 ? 0 : TargetForTier(currentTierIndex - 1);
            long span = thisTarget - previousTarget;
            if (span <= 0) return 0f;
            float fraction = (float)(currentStatValue - previousTarget) / span;
            return Mathf.Clamp01(fraction);
        }
        else
        {
            // Normal achievement — currentStatValue is ALREADY relative
            // to the current tier (reset to 0 on the last claim), so no
            // previous-tier baseline subtraction needed.
            if (thisTarget <= 0) return 0f;
            return Mathf.Clamp01((float)currentStatValue / thisTarget);
        }
    }

    public void ClaimCurrentTier()
    {
        if (!IsReadyToClaim()) return;
        currentTierIndex = singleTier ? 4 : currentTierIndex + 1;

        // Normal achievements start the next tier fresh from 0 — Lifetime
        // and Inverted achievements keep their true cumulative/absolute
        // value untouched.
        if (!UsesCumulativeTargets)
            currentStatValue = 0;
    }
}
