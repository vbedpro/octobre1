using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

// The single "Free Ad Reward" button: plays 1 rewarded ad, then deposits ONE random boost (picked from possibleRewards, only
// counting boosts the player owns 98 or fewer of, so it can never push
// past the 99 cap) into the inventory. Only goes on its 15-minute
// cooldown if the ad was watched to completion — abandoning it
// early leaves the button fully available again immediately, no cooldown
// applied.
public class FreeAdRewardButton : MonoBehaviour
{
    public static FreeAdRewardButton Instance { get; private set; }

    [Tooltip("All 7 boosts this button can reward. Same assets as your InventoryPanel/BoostActivationManager lists.")]
    public List<BoostDefinition> possibleRewards;

    [Tooltip("Cooldown length in seconds after a FULLY completed reward.")]
    public float cooldownSeconds = 900f; // 15 minutes

    [Header("UI")]
    public Button button;
    public Image buttonImage;
    public TextMeshProUGUI cooldownText;
    public GameObject playIcon;
    public Color readyColor = new Color(0.2f, 0.8f, 0.2f);
    public Color cooldownColor = new Color(0.5f, 0.5f, 0.5f);

    // Real wall-clock deadline (Unix epoch seconds) — survives the app
    // being closed and reopened, same reasoning as the boost timers.
    private long cooldownEndEpoch = 0;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        long nowEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        bool onCooldown = nowEpoch < cooldownEndEpoch;

        if (button != null) button.interactable = !onCooldown;
        if (buttonImage != null) buttonImage.color = onCooldown ? cooldownColor : readyColor;
        if (playIcon != null) playIcon.SetActive(!onCooldown);

        if (cooldownText != null)
        {
            if (onCooldown)
            {
                long remaining = cooldownEndEpoch - nowEpoch;
                int minutes = (int)(remaining / 60);
                int seconds = (int)(remaining % 60);
                cooldownText.text = $"{minutes:00}:{seconds:00}";
                cooldownText.gameObject.SetActive(true);
            }
            else
            {
                cooldownText.gameObject.SetActive(false);
            }
        }
    }

    // Wire this to the button's OnClick().
    public void OnButtonPressed()
    {
        Debug.Log("[FreeAdReward DEBUG] OnButtonPressed() called.");

        if (!NetworkStatusManager.RequireWifiOrShowMessage())
        {
            Debug.Log("[FreeAdReward DEBUG] Blocked — no Wi-Fi.");
            return;
        }

        long nowEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (nowEpoch < cooldownEndEpoch)
        {
            Debug.Log("[FreeAdReward DEBUG] Still on cooldown, ignoring.");
            return;
        }

        if (AdManager.Instance == null)
        {
            Debug.LogWarning("[FreeAdReward DEBUG] AdManager.Instance is NULL.");
            return;
        }

        if (!AdManager.Instance.IsRewardedAdReady())
        {
            Debug.LogWarning("[FreeAdReward DEBUG] AdManager says ad is NOT ready.");
            return;
        }

        Debug.Log("[FreeAdReward DEBUG] Playing ad...");
        PlayAd();
    }

    void PlayAd()
    {
        AdManager.Instance.ShowRewardedAd(
            onRewarded: () =>
            {
                Debug.Log("[FreeAdReward DEBUG] Ad rewarded — granting reward.");
                // Wrapped defensively: this callback runs from inside a
                // NATIVE AdMob SDK callback (the ad's close event). An
                // unhandled C# exception thrown from here doesn't behave
                // like a normal caught exception on Android/IL2CPP — it
                // can crash the entire native process instead of logging
                // a readable stack trace, which is exactly what appears
                // to have been happening (crash right after this exact
                // log line, no catchable exception in Logcat). Catching
                // it here guarantees we get a real, readable error
                // instead of a silent native crash.
                try
                {
                    GrantReward();
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[FreeAdReward DEBUG] Exception in GrantReward(): {e}");
                }
            },
            onFailed: () => { Debug.Log("[FreeAdReward DEBUG] Ad failed/abandoned."); OnAbandoned(); }
        );
    }

    // Player closed the ad before it finished, or it failed to
    // load — no reward, no cooldown, button stays fully available.
    void OnAbandoned()
    {
        Debug.Log("[FreeAdReward DEBUG] Ad sequence abandoned/failed — no reward, no cooldown.");
    }

    void GrantReward()
    {
        BoostDefinition chosen = PickRandomEligibleReward();
        if (chosen == null)
        {
            // Every boost is already at/near the 99 cap — extremely rare,
            // but don't silently eat the player's 2 watched ads for
            // nothing: still start the cooldown (they DID complete both
            // ads), just skip the grant.
            Debug.LogWarning("FreeAdRewardButton: no eligible reward (all boosts at cap) — cooldown still applied.");
        }
        else if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AddItem(chosen.itemId, 1);
            AudioManager.Instance?.PlayBoostBought();
            Debug.Log($"FreeAdRewardButton: granted 1x '{chosen.itemId}'.");

            // Automatically open the Inventory panel so the player can
            // immediately see what they got. FindObjectsInactive.Include
            // is required here — the panel starts inactive in the scene,
            // and FindAnyObjectByType skips inactive objects by default,
            // which would otherwise make this silently do nothing.
            FindAnyObjectByType<InventoryPanel>(FindObjectsInactive.Include)?.OpenPanel();
        }

        cooldownEndEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)cooldownSeconds;
    }

    BoostDefinition PickRandomEligibleReward()
    {
        if (possibleRewards == null || InventoryManager.Instance == null) return null;

        List<BoostDefinition> eligible = new List<BoostDefinition>();
        foreach (BoostDefinition def in possibleRewards)
        {
            if (def == null || string.IsNullOrEmpty(def.itemId)) continue;
            if (InventoryManager.Instance.GetCount(def.itemId) <= 98)
                eligible.Add(def);
        }

        if (eligible.Count == 0) return null;
        return eligible[UnityEngine.Random.Range(0, eligible.Count)];
    }

    // ── Save/Load ──────────────────────────────────────────────────
    public long GetSaveCooldownEndEpoch() => cooldownEndEpoch;

    public void RestoreSaveData(long savedCooldownEndEpoch)
    {
        cooldownEndEpoch = savedCooldownEndEpoch;
    }
}
