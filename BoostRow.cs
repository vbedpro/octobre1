using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

// A single boost, purchasable with carrots. Unlike ShopUpgradeRow, this is
// NOT a leveled upgrade with scaling cost — it's a flat-cost, one-shot
// purchase. What the boost actually DOES is left to whoever wires
// onPurchased in the Inspector (e.g. call into a BoostEffects script that
// applies a temporary multiplier, or an instant carrot/coin reward) — this
// script only handles "can the player afford it, and did they buy it."
public class BoostRow : MonoBehaviour
{
    [Header("Cost")]
    public long carrotCost = 50;

    [Header("Item")]
    [Tooltip("Unique ID for this boost, e.g. 'Boost_DoubleCoins10Min'. Used as the InventoryManager key.")]
    public string itemId;

    [Header("UI")]
    public TextMeshProUGUI costText;
    public Button buyButton;
    public Image buyButtonImage;
    public Color affordableColor    = new Color(0.2f, 0.8f, 0.2f);
    public Color notAffordableColor = new Color(0.5f, 0.5f, 0.5f);

    [Header("On Purchased (optional — most rows won't need this)")]
    [Tooltip("Purchasing already adds the boost to InventoryManager automatically. Only wire this for extra one-off effects beyond that, if ever needed.")]
    public UnityEvent onPurchased;

    void Start()
    {
        RefreshUI();
    }

    void Update()
    {
        bool atMax = IsAtMax();
        bool affordable = !atMax && CanAfford();

        if (buyButton != null) buyButton.interactable = affordable;
        if (buyButtonImage != null) buyButtonImage.color = affordable ? affordableColor : notAffordableColor;

        if (costText != null)
            costText.text = atMax ? "MAX" : carrotCost.ToString();
    }

    bool IsAtMax()
    {
        return !string.IsNullOrEmpty(itemId) && InventoryManager.Instance != null
            && InventoryManager.Instance.GetCount(itemId) >= InventoryManager.MaxStackPerItem;
    }

    bool CanAfford()
    {
        return GameManager.Instance != null && GameManager.Instance.totalCarrots >= carrotCost;
    }

    void RefreshUI()
    {
        if (costText != null) costText.text = IsAtMax() ? "MAX" : carrotCost.ToString();
    }

    // Wire this to the row's Buy button OnClick().
    public void OnBuyPressed()
    {
        if (IsAtMax()) return;
        if (!CanAfford()) return;
        if (!GameManager.Instance.SpendCarrots(carrotCost)) return;

        AudioManager.Instance?.PlayBoostBought();

        if (!string.IsNullOrEmpty(itemId))
            InventoryManager.Instance?.AddItem(itemId, 1);

        onPurchased?.Invoke();
    }
}
