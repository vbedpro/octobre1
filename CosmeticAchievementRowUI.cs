using UnityEngine;
using TMPro;

// Same as AchievementRowUI, except the medal icon reflects the granted
// cosmetic's ACTUAL icon, resolved live via CosmeticDatabase from the
// achievement's current-tier cosmeticId:
//   - LOCKED (not yet claimed): shows CosmeticDefinition.achievementIconLocked
//     (a single static "silhouette/preview" sprite you assign per cosmetic).
//   - UNLOCKED (claimed): shows the SAME icon the Inventory panel uses —
//     CosmeticDefinition.icon (static) or .iconAnimationFrames (animated) —
//     so there's nothing extra to assign here once Inventory's icon is set.
//
// Also hides the normal reward icon/quantity display entirely (cosmetics
// don't have a meaningful "quantity" the way coins/carrots do) and shows
// a plain "Claim" text label instead.
public class CosmeticAchievementRowUI : AchievementRowUI
{
    [Header("Cosmetic-specific Claim Button")]
    [Tooltip("Shows just this text — replaces the normal reward icon + quantity display entirely.")]
    public TextMeshProUGUI claimButtonLabel;
    public string claimButtonText = "Claim";

    private CosmeticDefinition activeCosmetic;
    private bool activeComplete;
    private Vector2 defaultIconSizeDelta;
    private bool defaultIconSizeCaptured = false;

    public override void Set(AchievementData data, AchievementsPanelUI ownerPanel)
    {
        base.Set(data, ownerPanel);

        CaptureDefaultIconSizeOnce();

        activeComplete = data.currentTierIndex >= 4;

        string cosmeticId = data.CosmeticIdForCurrentTier();
        activeCosmetic = CosmeticDatabase.Instance != null ? CosmeticDatabase.Instance.GetById(cosmeticId) : null;
        ApplyIconFrame(); // paint immediately instead of waiting for next render

        // Cosmetics have no reward icon/quantity to show — hide whatever
        // the base Set() just populated (or left stale) for those.
        if (rewardIcon != null) rewardIcon.gameObject.SetActive(false);
        if (rewardQuantityText != null) rewardQuantityText.gameObject.SetActive(false);

        if (claimButtonLabel != null)
        {
            claimButtonLabel.gameObject.SetActive(!activeComplete);
            claimButtonLabel.text = claimButtonText;
        }
    }

    // Only actually animates anything while UNLOCKED and the cosmetic has
    // animation frames — the locked state is always a single static sprite,
    // so this is a cheap no-op most of the time.
    void OnEnable()
    {
        Canvas.willRenderCanvases += ApplyIconFrame;
    }

    void OnDisable()
    {
        Canvas.willRenderCanvases -= ApplyIconFrame;
    }

    void CaptureDefaultIconSizeOnce()
    {
        if (defaultIconSizeCaptured || medalIcon == null) return;
        defaultIconSizeDelta = medalIcon.rectTransform.sizeDelta;
        defaultIconSizeCaptured = true;
    }

    void ApplyIconFrame()
    {
        if (medalIcon == null || activeCosmetic == null) return;

        if (!activeComplete)
        {
            // Locked state: single static sprite, scaled per-cosmetic to
            // compensate for a different native resolution than the
            // unlocked icon, no animation.
            if (activeCosmetic.achievementIconLocked != null && medalIcon.sprite != activeCosmetic.achievementIconLocked)
                medalIcon.sprite = activeCosmetic.achievementIconLocked;

            medalIcon.rectTransform.sizeDelta = Vector2.Scale(defaultIconSizeDelta, activeCosmetic.lockedIconScale);
            return;
        }

        // Unlocked state: back to the row's normal icon size, no scaling.
        medalIcon.rectTransform.sizeDelta = defaultIconSizeDelta;

        // Reuse the exact same icon fields as the Inventory panel — static
        // Icon, or a cycling Icon Animation Frames sequence if this
        // cosmetic has one.
        if (activeCosmetic.iconAnimationFrames != null && activeCosmetic.iconAnimationFrames.Length > 0)
        {
            float fps = Mathf.Max(activeCosmetic.iconFrameRate, 0.01f);
            int frameIndex = Mathf.FloorToInt(Time.time * fps) % activeCosmetic.iconAnimationFrames.Length;
            Sprite frame = activeCosmetic.iconAnimationFrames[frameIndex];
            if (frame != null && medalIcon.sprite != frame)
                medalIcon.sprite = frame;
        }
        else if (activeCosmetic.icon != null)
        {
            if (medalIcon.sprite != activeCosmetic.icon)
                medalIcon.sprite = activeCosmetic.icon;
        }
    }
}
