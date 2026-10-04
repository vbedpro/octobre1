using UnityEngine;

public class CheatButton : MonoBehaviour
{
    public void GiveMoney()
    {
        GameManager.Instance.AddCoins(999999999);
        GameManager.Instance.AddCarrots(1000);
    }

    // Directly grants +1 BigBang (prestige) count, bypassing the normal
    // rabbit/carrot requirements entirely — doesn't run the actual
    // transition/reset/achievement side effects a real BigBang does,
    // just bumps the counter for quick testing of BigBang-gated content
    // (like Guilds' 10-BigBang unlock).
    public void GrantBigBang()
    {
        if (PrestigeManager.Instance == null) return;

        PrestigeManager.Instance.prestigeCount += 1;
        PrestigeManager.Instance.UpdateBigBangButton();

        Debug.Log($"CheatButton: granted +1 BigBang — now at {PrestigeManager.Instance.prestigeCount}.");
    }

    // Instantly maxes progress on every Cosmetic-reward achievement's
    // CURRENT tier, so it's immediately ready to claim (still requires
    // tapping Claim in the Achievements panel, same as any other
    // achievement — doesn't silently grant rewards behind your back).
    // Doesn't touch Coins/Carrots achievements.
    public void CompleteAllCosmeticObjectives()
    {
        if (AchievementsManager.Instance == null) return;

        foreach (AchievementData data in AchievementsManager.Instance.achievements)
        {
            if (data == null || data.rewardType != AchievementData.RewardType.Cosmetic) continue;
            if (data.currentTierIndex >= 4) continue; // already fully complete

            data.currentStatValue = data.TargetForTier(data.currentTierIndex);
        }

        Debug.Log("CheatButton: all Cosmetic achievement objectives maxed out — ready to claim in the Achievements panel.");
    }

    // Right-click this component's header in the Inspector (works in both
    // Edit mode and Play mode) to instantly wipe all PlayerPrefs — the
    // entire save, backup, and corrupt-save slots included. Next time the
    // game loads (or the next time you press Play), it'll start as a
    // genuinely brand-new player with no save data at all.
    [ContextMenu("DEBUG: Clear All Save Data")]
    void ClearAllSaveData()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("CheatButton: PlayerPrefs.DeleteAll() done — all save data wiped. Restart Play mode to test as a fresh player.");
    }
}
