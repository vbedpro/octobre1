using UnityEngine;
using UnityEngine.UI;
using TMPro;

// One row in the Achievements panel — displays a 4-tier (Bronze/Silver/
// Gold/Platinum) achievement's current state.
public class AchievementRowUI : MonoBehaviour
{
    [Header("Medal — always shows the correct tier badge, never a checkmark")]
    public Image medalIcon;
    public Sprite medalLockedSprite;
    public Sprite medalBronzeSprite;
    public Sprite medalSilverSprite;
    public Sprite medalGoldSprite;
    public Sprite medalPlatinumSprite;

    [Header("Description")]
    public TextMeshProUGUI descriptionText;

    [Header("Progress")]
    [Tooltip("Same GaugeBar component used everywhere else in the project (resizes a Fill RectTransform directly, not Image.fillAmount).")]
    public GaugeBar progressGauge;
    [Tooltip("Optional — shows raw progress as text, e.g. '35 / 50'. Leave empty if not wanted.")]
    public TextMeshProUGUI progressText;

    [Header("Reward Row (Claim button + icon + quantity) — visible while INCOMPLETE, hidden once fully complete")]
    [Tooltip("The parent container holding the claim button, reward icon, and reward quantity together — this whole row disappears once the achievement is fully complete (Platinum claimed).")]
    public GameObject rewardRow;
    public Image rewardIcon;
    public TextMeshProUGUI rewardQuantityText;
    public Button claimButton;
    public Image claimButtonBackground;
    public Color claimButtonNormalColor = Color.gray;
    public Color claimButtonReadyColor = Color.green;

    [Header("Reward Row Horizontal Offset (shifts left as the reward number gets longer)")]
    [Tooltip("Reward Row's local X position when the reward amount is 1 digit (or unset/0 offset baseline).")]
    public float oneDigitOffsetX = 0f;
    public float twoDigitOffsetX = -10f;
    public float threeDigitOffsetX = -20f;
    [Tooltip("Used for 4 digits AND anything longer.")]
    public float fourDigitOffsetX = -30f;
    [Tooltip("The icon+text sub-group specifically (NOT the whole Reward Row — this is just the icon and quantity text together, separate from the Claim button) — this is what actually shifts based on digit count.")]
    public RectTransform rewardIconTextRect;

    [Header("Checkmark — takes the Reward Row's place once FULLY complete")]
    public GameObject checkmarkIcon;

    [Header("Row Background")]
    [Tooltip("Turns green once this achievement is FULLY complete (Platinum claimed).")]
    public Image rowBackground;
    public Color rowBackgroundNormalColor = Color.white;
    public Color rowBackgroundCompleteColor = new Color(0.6f, 1f, 0.6f);

    private AchievementData currentData;
    private AchievementsPanelUI panel;

    public virtual void Set(AchievementData data, AchievementsPanelUI ownerPanel)
    {
        currentData = data;
        panel = ownerPanel;

        bool complete = data.currentTierIndex >= 4;
        bool readyToClaim = data.IsReadyToClaim();

        if (descriptionText != null)
        {
            long displayTarget = complete
                ? (data.singleTier ? data.bronzeTarget : data.platinumTarget)
                : data.TargetForTier(data.currentTierIndex);
            descriptionText.text = data.description.Replace("X", displayTarget.ToString("N0"));
        }

        // ── Medal — always the correct tier badge, no checkmark here ──
        if (medalIcon != null)
        {
            medalIcon.sprite = data.currentTierIndex switch
            {
                0 => medalLockedSprite,
                1 => medalBronzeSprite,
                2 => medalSilverSprite,
                3 => medalGoldSprite,
                _ => medalPlatinumSprite // 4 = fully complete
            };
        }

        // ── Gauge ──
        float fraction = data.GetDisplayedFraction();
        if (data.currentTierIndex >= 1 && data.currentTierIndex < 4)
        {
            long thisT = data.TargetForTier(data.currentTierIndex);
            long prevT = data.TargetForTier(data.currentTierIndex - 1);
            Debug.Log($"[GaugeDebug] '{data.id}': tierIndex={data.currentTierIndex}, currentStatValue={data.currentStatValue}, prevTarget={prevT}, thisTarget={thisT}, fraction={fraction}");
        }
        if (progressGauge != null)
        {
            progressGauge.SetFillInstant(fraction);
            // Softer refresh attempts (ForceUpdateCanvases, etc.) didn't
            // reliably force this to redraw while the panel stays open —
            // a genuine enable/disable cycle is what actually works
            // (that's effectively what closing/reopening the panel does),
            // so force one directly on the gauge's own GameObject.
            progressGauge.gameObject.SetActive(false);
            progressGauge.gameObject.SetActive(true);
        }

        if (progressText != null)
        {
            long thisTarget = complete ? (data.singleTier ? data.bronzeTarget : data.platinumTarget) : data.TargetForTier(data.currentTierIndex);

            if (data.isInvertedProgress)
            {
                progressText.text = $"#{data.currentStatValue:N0} (target #{thisTarget:N0})";
            }
            else if (data.isLifetimeStat)
            {
                long previousTarget = data.currentTierIndex == 0 ? 0 : data.TargetForTier(data.currentTierIndex - 1);
                long shown = complete ? thisTarget : Mathf.Clamp((int)(data.currentStatValue), (int)previousTarget, (int)thisTarget);
                progressText.text = $"{shown:N0} / {thisTarget:N0}";
            }
            else
            {
                // Normal achievement — currentStatValue is already
                // relative to the current tier (resets to 0 on claim).
                long shown = complete ? thisTarget : Mathf.Clamp((int)data.currentStatValue, 0, (int)thisTarget);
                progressText.text = $"{shown:N0} / {thisTarget:N0}";
            }
        }

        // ── Reward Row vs Checkmark — mutually exclusive ──
        if (rewardRow != null) rewardRow.SetActive(!complete);

        if (checkmarkIcon != null)
            checkmarkIcon.SetActive(complete);
        else if (complete)
            Debug.LogWarning($"AchievementRowUI ('{data.id}'): achievement is fully complete but 'Checkmark Icon' isn't assigned on this row's prefab — nothing to show in the Reward Row's place.");

        if (!complete)
        {
            Sprite pendingRewardIcon = data.currentTierIndex switch
            {
                0 => data.bronzeRewardIcon,
                1 => data.silverRewardIcon,
                2 => data.goldRewardIcon,
                _ => data.platinumRewardIcon
            };
            long pendingRewardQty = data.currentTierIndex switch
            {
                0 => data.bronzeRewardQuantity,
                1 => data.silverRewardQuantity,
                2 => data.goldRewardQuantity,
                _ => data.platinumRewardQuantity
            };

            if (rewardIcon != null && pendingRewardIcon != null)
                rewardIcon.sprite = pendingRewardIcon;
            if (rewardQuantityText != null)
            {
                rewardQuantityText.text = pendingRewardQty.ToString("N0");
                Color c = readyToClaim ? Color.white : Color.black;
                rewardQuantityText.color = c;
                rewardQuantityText.fontMaterial.SetColor(ShaderUtilities.ID_FaceColor, c);
                rewardQuantityText.ForceMeshUpdate();
                rewardQuantityText.gameObject.SetActive(false);
                rewardQuantityText.gameObject.SetActive(true);
            }

            ApplyRewardRowOffset(pendingRewardQty);

            if (claimButton != null)
            {
                claimButton.interactable = readyToClaim;
                claimButton.onClick.RemoveAllListeners();
                claimButton.onClick.AddListener(OnClaimPressed);
            }
            if (claimButtonBackground != null)
                claimButtonBackground.color = readyToClaim ? claimButtonReadyColor : claimButtonNormalColor;
        }

        // ── Row background ──
        if (rowBackground != null)
            rowBackground.color = complete ? rowBackgroundCompleteColor : rowBackgroundNormalColor;
    }

    void ApplyRewardRowOffset(long rewardQuantity)
    {
        if (rewardIconTextRect == null) return;

        int digits = System.Math.Abs(rewardQuantity).ToString().Length;
        float offsetX = digits switch
        {
            <= 1 => oneDigitOffsetX,
            2 => twoDigitOffsetX,
            3 => threeDigitOffsetX,
            _ => fourDigitOffsetX // 4 or more
        };

        Vector2 pos = rewardIconTextRect.anchoredPosition;
        rewardIconTextRect.anchoredPosition = new Vector2(offsetX, pos.y);
    }

    void OnClaimPressed()
    {
        if (currentData == null || !currentData.IsReadyToClaim()) return;

        AudioManager.Instance?.PlaySellRabbitsClicked();

        // Capture which tier is being claimed BEFORE advancing — the
        // reward granted always corresponds to whichever tier was JUST
        // completed, not whatever tier we're about to move into.
        int tierBeingClaimed = currentData.currentTierIndex;

        if (currentData.rewardType == AchievementData.RewardType.Cosmetic)
        {
            string cosmeticId = tierBeingClaimed switch
            {
                0 => currentData.bronzeCosmeticId,
                1 => currentData.silverCosmeticId,
                2 => currentData.goldCosmeticId,
                _ => currentData.platinumCosmeticId
            };

            if (!string.IsNullOrEmpty(cosmeticId) && InventoryManager.Instance != null)
            {
                InventoryManager.Instance.AddItem(cosmeticId, 1);
                Debug.Log($"[AchievementRowUI] Granted cosmetic '{cosmeticId}', new count = {InventoryManager.Instance.GetCount(cosmeticId)}");
            }
            else
            {
                Debug.LogWarning($"[AchievementRowUI] Cosmetic claim skipped — cosmeticId empty or InventoryManager null. cosmeticId='{cosmeticId}'");
            }
        }
        else
        {
            long rewardQty = tierBeingClaimed switch
            {
                0 => currentData.bronzeRewardQuantity,
                1 => currentData.silverRewardQuantity,
                2 => currentData.goldRewardQuantity,
                _ => currentData.platinumRewardQuantity
            };

            if (GameManager.Instance != null && rewardQty > 0)
            {
                if (currentData.rewardType == AchievementData.RewardType.Coins)
                    GameManager.Instance.AddCoins(rewardQty);
                else
                    GameManager.Instance.AddCarrots(rewardQty);
            }
        }

        currentData.ClaimCurrentTier();

        // #15 Colored Nickname: any bronze tier claimed, anywhere.
        // #16 Golden Nickname: this achievement just became fully complete.
        // Deliberately excludes cosmetic-reward achievements themselves —
        // otherwise claiming a cosmetic achievement would count toward
        // its own (or another cosmetic's) progress requirement, wildly
        // inflating these two counters from unrelated claims.
        if (currentData.rewardType != AchievementData.RewardType.Cosmetic)
        {
            if (tierBeingClaimed == 0)
                AchievementsManager.Instance?.ReportBronzeTierClaimed();
            if (currentData.currentTierIndex >= 4)
                AchievementsManager.Instance?.ReportAchievementFullyCompleted();
        }

        Set(currentData, panel);
        panel?.NotifyAchievementClaimed(currentData);
    }
}
