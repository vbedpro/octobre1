using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Offers a free coin reward in exchange for watching an ad. Flow:
//   1. A small notification bubble appears (immediately on launch, once
//      gated conditions allow it).
//   2. Tapping it opens an Accept/Decline panel.
//   3. Decline -> panel closes, 120s real-time cooldown starts, no reward.
//   4. Accept -> ad plays. Watched fully -> reward granted, THEN the
//      120s cooldown starts. Abandoned/failed -> same as decline (120s
//      cooldown, no reward).
//   5. When the cooldown finishes, if a quest is currently active
//      (notification/popup/challenge) the notification goes ON HOLD
//      instead of reappearing — it waits until the quest system goes
//      idle, THEN shows again. The cooldown itself is NOT extended by
//      this wait; it only delays the moment it's allowed to reappear.
//   6. The whole timer resets back to "appears immediately" whenever the
//      game applies offline earnings (i.e. the player just came back
//      after being away) — call ResetForOfflineReturn() from SaveManager.
public class AdNotificationManager : MonoBehaviour
{
    public static AdNotificationManager Instance { get; private set; }

    enum State { WaitingToShow, NotificationVisible, PanelOpen, Cooldown, OnHold }
    private State state = State.WaitingToShow;

    [Header("Timing")]
    [Tooltip("Delay before the AdNotification can appear for the very first time after launch.")]
    public float initialLaunchDelay = 15f;
    [Tooltip("How long the notification bubble stays up before disappearing if ignored (same convention as QuestManager's own notification window).")]
    public float notificationLifespan = 30f;
    [Tooltip("Real-time cooldown after a decline OR after a completed/abandoned ad, in seconds.")]
    public float cooldownSeconds = 120f;

    [Header("Reward")]
    [Tooltip("Coins = current truck capacity x this multiplier.")]
    public float truckCapacityRewardMultiplier = 3f;
    [Tooltip("Reference to the active SellManager, used to read current truck capacity (maxSell).")]
    public SellManager sellManager;

    [Header("UI — Notification Bubble")]
    public GameObject notificationObject;
    public QuestSlidePanel notificationSlide;
    public Button notificationButton; // opens the panel when tapped

    [Header("UI — Accept/Decline Panel")]
    public GameObject panelObject;
    public QuestSlidePanel panelSlide;
    public Button acceptButton;
    public Button declineButton;
    public TextMeshProUGUI rewardPreviewText;

    // For QuestManager to check — pauses its own offer-interval timer
    // while this panel is up, so a quest notification can't silently
    // "use up" idle time while the player is busy deciding on this one.
    public bool IsPanelOpen => state == State.PanelOpen;

    // Broader check for QuestManager — true while EITHER the bubble or
    // the panel is currently showing (not just the panel). Prevents a
    // quest notification from popping up while the player still has an
    // untapped AdNotification bubble sitting on screen.
    public bool IsShowingAnything => state == State.NotificationVisible || state == State.PanelOpen;

    private float cooldownTimer = 0f;
    private float notificationTimer = 0f;
    private float launchTimer = 0f;
    private bool hasLaunchDelayPassed = false;
    private float showGraceTimer = 0f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (notificationObject != null) notificationObject.SetActive(false);
        if (panelObject != null) panelObject.SetActive(false);
        state = State.WaitingToShow; // shows immediately (after launchTimer) once gating allows it
    }

    void Update()
    {
        if (!hasLaunchDelayPassed)
        {
            launchTimer += Time.deltaTime;
            if (launchTimer < initialLaunchDelay) return;
            hasLaunchDelayPassed = true;
        }

        switch (state)
        {
            case State.WaitingToShow:
            case State.OnHold:
                // Small padding once the path actually clears (e.g. a
                // quest notification just closed) — avoids this popping
                // up the instant something else finishes, which felt
                // pushy/aggressive.
                if (CanShowRightNow())
                {
                    showGraceTimer += Time.deltaTime;
                    if (showGraceTimer >= 5f)
                    {
                        showGraceTimer = 0f;
                        Show();
                    }
                }
                else
                {
                    showGraceTimer = 0f;
                }
                break;

            case State.NotificationVisible:
                notificationTimer -= Time.deltaTime;
                if (notificationTimer <= 0f)
                {
                    // Ignored for the full lifespan — treated the same as
                    // a decline: hide it and start the cooldown.
                    HideNotification();
                    StartCooldown();
                }
                break;

            case State.Cooldown:
                cooldownTimer -= Time.deltaTime;
                if (cooldownTimer <= 0f)
                {
                    // Cooldown elapsed — but only actually reappear once
                    // quests are idle. If a quest is active right now,
                    // wait (OnHold) rather than showing over it.
                    state = CanShowRightNow() ? State.WaitingToShow : State.OnHold;
                }
                break;

            // PanelOpen: infinite lifespan, nothing to tick — waits on player input.
        }
    }

    bool CanShowRightNow()
    {
        if (TutorialManager.Instance != null && TutorialManager.Instance.CurrentStep != TutorialManager.TutorialStep.Completed)
            return false;

        if (QuestManager.Instance == null || !QuestManager.Instance.IsQuestSystemIdle)
            return false;

        if (QuestManager.Instance == null || !QuestManager.Instance.HasRespondedToFirstQuest)
            return false;

        return true;
    }

    void Show()
    {
        state = State.NotificationVisible;
        notificationTimer = notificationLifespan;
        if (notificationSlide != null) notificationSlide.Show();
        else if (notificationObject != null) notificationObject.SetActive(true); // fallback if no slide assigned
    }

    void HideNotification()
    {
        if (notificationSlide != null) notificationSlide.Hide(true);
        else if (notificationObject != null) notificationObject.SetActive(false);
    }

    // Wire to the notification bubble's OnClick().
    public void OnNotificationTapped()
    {
        if (state != State.NotificationVisible) return;

        state = State.PanelOpen;
        HideNotification();

        CloseOtherUI();

        if (rewardPreviewText != null)
            rewardPreviewText.text = GameManager.FormatFullNumber(ComputeReward());

        OpenPanel();
    }

    // Closes every other panel before the Accept/Decline panel opens —
    // same convention every other panel in this project already follows
    // for each other.
    void CloseOtherUI()
    {
        if (ShopManager.shopOpen) FindAnyObjectByType<ShopManager>()?.CloseShop();
        if (SellManager.popupOpen) FindAnyObjectByType<SellManager>()?.CloseSellPopup();
        if (HutUpgradeManager.popupOpen) FindAnyObjectByType<HutUpgradeManager>()?.ClosePopup();
        if (LeaderboardUI.leaderboardOpen) FindAnyObjectByType<LeaderboardUI>()?.CloseLeaderboard();
        if (AchievementsPanelUI.achievementsOpen) FindAnyObjectByType<AchievementsPanelUI>()?.ClosePanel();
        if (SettingsPanelController.settingsOpen) SettingsPanelController.Instance?.CloseSettings();
        if (PrestigeManager.popupOpen) FindAnyObjectByType<PrestigeManager>()?.CloseBigBangPopup();
        if (InventoryPanel.inventoryOpen) FindAnyObjectByType<InventoryPanel>(FindObjectsInactive.Include)?.ClosePanel();
        FindAnyObjectByType<PlanetColumnUI>()?.CloseColumn(); // safe no-op if already closed
        FindAnyObjectByType<QuestManager>()?.ForceCloseQuestUI(); // closes QuestPopup + ActiveQuestPanel visually only
    }

    void OpenPanel()
    {
        if (panelSlide != null) panelSlide.Show();
        else if (panelObject != null) panelObject.SetActive(true); // fallback if no slide assigned
    }

    void ClosePanel()
    {
        if (panelSlide != null) panelSlide.Hide(true);
        else if (panelObject != null) panelObject.SetActive(false);
    }

    // Wire to the panel's Decline button OnClick().
    public void OnDeclinePressed()
    {
        if (state != State.PanelOpen) return;
        ClosePanelAndStartCooldown();
    }

    // Wire to the panel's Accept button OnClick().
    public void OnAcceptPressed()
    {
        if (state != State.PanelOpen) return;
        ClosePanel();

        if (AdManager.Instance == null || !AdManager.Instance.IsRewardedAdReady())
        {
            StartCooldown(); // couldn't play — treat like a decline
            return;
        }

        AdManager.Instance.ShowRewardedAd(
            onRewarded: () =>
            {
                GameManager.Instance?.AddCoins(ComputeReward());
                AudioManager.Instance?.PlayBoostBought();
                StartCooldown();
            },
            onFailed: StartCooldown // abandoned/failed — same as decline, no reward
        );
    }

    void ClosePanelAndStartCooldown()
    {
        ClosePanel();
        StartCooldown();
    }

    void StartCooldown()
    {
        state = State.Cooldown;
        cooldownTimer = cooldownSeconds;
    }

    long ComputeReward()
    {
        long truckCapacity = sellManager != null ? sellManager.maxSell : 0;
        return (long)(truckCapacity * truckCapacityRewardMultiplier);
    }

    // Call this from SaveManager right after offline earnings are applied
    // (i.e. the player just came back after being away) — resets the
    // whole cycle back to "can appear immediately," matching how it
    // behaves on a fresh launch.
    public void ResetForOfflineReturn()
    {
        HideNotification();
        ClosePanel();
        cooldownTimer = 0f;
        state = State.WaitingToShow;
    }
}
