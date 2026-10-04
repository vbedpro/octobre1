using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class EpicUpgradeRow : MonoBehaviour
{
    [Header("Which Epic Upgrade")]
    public EpicUpgradeManager.EpicUpgradeType upgradeType;

    [Header("Level Settings")]
    public int maxLevel = 5;

    [Header("Placeholder Pricing (in Golden Carrots)")]
    public int basePrice = 5;
    public float priceGrowth = 1.15f;

    [Header("UI")]
    public TextMeshProUGUI costText;
    public TextMeshProUGUI levelText;
    public Button buyButton;
    public Image buyButtonImage;
    public VerticalGaugeFill levelGauge;
    public Color affordableColor    = new Color(0.2f, 0.8f, 0.2f);
    public Color notAffordableColor = new Color(0.5f, 0.5f, 0.5f);

    [Header("Cost Row Offset (shifts left per extra digit in the price)")]
    public RectTransform costRow;
    public float offsetPerExtraDigit = 8f;
    private float costRowBaseX;
    private bool costRowBaseCached = false;

    [Header("Hold To Buy")]
    public float holdDelay    = 0.25f;
    public float holdInterval = 0.075f;

    public int currentLevel = 0;

    private Coroutine holdCoroutine;

    void Start()
    {
        RefreshUI();
        if (levelGauge != null)
            levelGauge.SetFillInstant((float)currentLevel / maxLevel);
    }

    void Update()
    {
        bool affordable = !IsMaxed() && CanAfford();
        if (buyButton      != null) buyButton.interactable = affordable;
        if (buyButtonImage != null) buyButtonImage.color    = affordable ? affordableColor : notAffordableColor;
    }

    public bool IsMaxed() => currentLevel >= maxLevel;

    int PriceForNextLevel()
    {
        return Mathf.RoundToInt(basePrice * Mathf.Pow(priceGrowth, currentLevel));
    }

    bool CanAfford()
    {
        if (IsMaxed()) return false;
        return GameManager.Instance != null && GameManager.Instance.totalCarrots >= PriceForNextLevel();
    }

    public void StartHolding()
    {
        StopHolding();
        holdCoroutine = StartCoroutine(HoldRoutine());
    }

    public void StopHolding()
    {
        if (holdCoroutine != null)
        {
            StopCoroutine(holdCoroutine);
            holdCoroutine = null;
        }
    }

    IEnumerator HoldRoutine()
    {
        TryBuy();
        yield return new WaitForSeconds(holdDelay);
        while (true)
        {
            TryBuy();
            yield return new WaitForSeconds(holdInterval);
        }
    }

    public void TryBuy()
    {
        if (IsMaxed()) return;
        int price = PriceForNextLevel();
        if (GameManager.Instance == null || !GameManager.Instance.SpendCarrots(price)) return;

        currentLevel++;
        AudioManager.Instance?.PlayEpicUpgradeBought();
        RefreshUI();

        if (levelGauge != null)
            levelGauge.SetFill((float)currentLevel / maxLevel);

        if (EpicUpgradeManager.Instance != null)
            EpicUpgradeManager.Instance.OnEpicUpgradeChanged(upgradeType, currentLevel);

        // #18 Rainbow Nickname
        AchievementsManager.Instance?.ReportEpicUpgradePurchased();
    }

    void RefreshUI()
    {
        if (levelText != null) levelText.text = currentLevel + " / " + maxLevel;

        int price = PriceForNextLevel();
        if (costText != null) costText.text = IsMaxed() ? "MAX" : price.ToString();

        ApplyCostRowOffset(IsMaxed() ? 3 : price.ToString().Length);
    }

    // Called by SaveManager after loading currentLevel from disk, so cost
    // text / level text / gauge all reflect the restored level.
    public void RefreshUIPublic()
    {
        RefreshUI();
        if (levelGauge != null)
            levelGauge.SetFillInstant((float)currentLevel / maxLevel);
    }

    // Shifts CostRow left by (digitCount - 1) * offsetPerExtraDigit, so a
    // 1-digit price sits at its normal spot and each extra digit nudges it
    // further left (same idea as the quest coin text sizing).
    void ApplyCostRowOffset(int digitCount)
    {
        if (costRow == null) return;

        if (!costRowBaseCached)
        {
            costRowBaseX = costRow.anchoredPosition.x;
            costRowBaseCached = true;
        }

        int extraDigits = Mathf.Max(0, digitCount - 1);
        Vector2 pos = costRow.anchoredPosition;
        pos.x = costRowBaseX - (extraDigits * offsetPerExtraDigit);
        costRow.anchoredPosition = pos;
    }
}
