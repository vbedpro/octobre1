using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

// The single ad-only boost row. No cost, no carrots — just watch a
// rewarded ad. Mirrors BoostRow's onPurchased hook so the actual boost
// effect gets wired per-row in the Inspector, same as every other row.
public class AdBoostRow : MonoBehaviour
{
    [Header("UI")]
    public Button watchAdButton;
    public Image watchAdButtonImage;
    public Color readyColor    = new Color(0.2f, 0.8f, 0.2f);
    public Color notReadyColor = new Color(0.5f, 0.5f, 0.5f);

    [Header("Item")]
    [Tooltip("Unique ID for this boost, e.g. 'Boost_FreeCarrots'. Used as the InventoryManager key.")]
    public string itemId;

    [Header("On Rewarded (optional — most rows won't need this)")]
    [Tooltip("Watching the ad already adds the boost to InventoryManager automatically. Only wire this for extra one-off effects beyond that, if ever needed.")]
    public UnityEvent onRewarded;

    void Update()
    {
        bool ready = !IsAtMax() && AdManager.Instance != null && AdManager.Instance.IsRewardedAdReady();
        if (watchAdButton != null) watchAdButton.interactable = ready;
        if (watchAdButtonImage != null) watchAdButtonImage.color = ready ? readyColor : notReadyColor;
    }

    bool IsAtMax()
    {
        return !string.IsNullOrEmpty(itemId) && InventoryManager.Instance != null
            && InventoryManager.Instance.GetCount(itemId) >= InventoryManager.MaxStackPerItem;
    }

    // Wire this to the row's Watch Ad button OnClick().
    public void OnWatchAdPressed()
    {
        if (IsAtMax()) return;
        if (AdManager.Instance == null || !AdManager.Instance.IsRewardedAdReady()) return;

        AdManager.Instance.ShowRewardedAd(
            onRewarded: () =>
            {
                AudioManager.Instance?.PlayBoostBought();

                if (!string.IsNullOrEmpty(itemId))
                    InventoryManager.Instance?.AddItem(itemId, 1);

                onRewarded?.Invoke();
            },
            onFailed: null
        );
    }
}
