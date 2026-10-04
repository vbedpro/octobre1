using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

// Attached to the ContestObjective prefab. GuildSearchContestUI calls
// Refresh() on this whenever contest state updates, on the same cadence
// as everything else (rival members, friendly participants) — this
// component itself doesn't poll anything on its own.
public class ContestObjectiveUI : MonoBehaviour
{
    public GaugeBar friendlyGuildOverallProgress;
    public GaugeBar rivalGuildOverallProgress;
    [Tooltip("Also attach a PermanentGoldenShineText component to this same text object for the golden_nickname visual effect.")]
    public TextMeshProUGUI contestRewardAmountText;
    public TextMeshProUGUI friendlyProgressText;
    public TextMeshProUGUI rivalProgressText;

    [Tooltip("Shown underneath the ContestReward icon, e.g. \"Objective: Hatch rabbits.\" — updates automatically from the current objective type, no numbers.")]
    public TextMeshProUGUI objectiveDescriptionText;

    // Plain action description per objective type — no numbers/targets
    // (those already show via friendlyProgressText/rivalProgressText).
    // ClimbRanks is kept here but currently unreachable — it's excluded
    // from the server-side contest objective draw pool for now.
    private static readonly System.Collections.Generic.Dictionary<string, string> ObjectiveDescriptions = new System.Collections.Generic.Dictionary<string, string>
    {
        { "InitiateBigBangs", "Objective: Initiate Big Bangs." },
        { "ClimbRanks",       "Objective: Climb the leaderboard ranks." },
        { "HatchRabbits",     "Objective: Hatch rabbits." },
        { "CompleteQuests",   "Objective: Complete quests." },
        { "SendTrucks",       "Objective: Send rabbit trucks." },
        { "BreedRabbits",     "Objective: Breed rabbits." },
    };

    [Header("Ongoing / Ended Labels")]
    [Tooltip("Visible while the contest is actively ongoing (not yet in the 24-hour Results phase).")]
    public GameObject ongoingContestLabel;
    [Tooltip("Visible during the 24-hour Results phase, in place of ongoingContestLabel.")]
    public GameObject contestEndedLabel;

    [Header("Winner / Loser Labels (Results phase only)")]
    public GameObject winnersLabel;
    public GameObject losersLabel;

    [Header("Contest Reward Claim (Results phase only)")]
    [Tooltip("The normal reward icon. For the WINNING guild's members (before claiming), its background turns green and it becomes clickable. Hidden for the losing guild, and hidden again (replaced by checkmarkImage) once claimed.")]
    public Image contestRewardImage;
    [Tooltip("Button component on contestRewardImage (or a child of it) — clicking it claims the reward.")]
    public Button contestRewardButton;
    [Tooltip("Background color for contestRewardImage while the WINNING guild's reward is unclaimed and clickable.")]
    public Color contestRewardClaimableColor = new Color32(0x28, 0xE0, 0x25, 0xFF);
    [Tooltip("Background color for contestRewardImage the rest of the time (ongoing contest) — its normal, non-claimable look.")]
    public Color contestRewardDefaultColor = Color.white;
    [Tooltip("Shown in place of contestRewardImage, for the WINNING guild, once the reward has been claimed — your checkmark sprite on a white background.")]
    public GameObject checkmarkImage;
    [Tooltip("Shown in place of contestRewardImage, for the LOSING guild, for the whole Results phase — your red X sprite.")]
    public GameObject xMarkImage;

    // Fired when the player taps the reward while it's clickable. The
    // actual claim (calling GuildManager, crediting carrots) is handled by
    // GuildSearchContestUI — this component only displays state and raises
    // the click, keeping the same "dumb display" pattern as Refresh() below.
    public event Action OnClaimRewardClicked;

    void Awake()
    {
        if (contestRewardButton != null)
            contestRewardButton.onClick.AddListener(() => OnClaimRewardClicked?.Invoke());
    }

    public void Refresh(long friendlyProgress, long rivalProgress, string objectiveType, long rewardAmount, long objectiveTarget)
    {
        // Skip entirely if this UI isn't currently visible — e.g. the
        // player closed the panel or switched tabs while a background
        // fetch (PopulateContestObjective's two async lookups) was still
        // in flight. Attempting to animate a hidden GaugeBar throws a
        // "coroutine couldn't be started, inactive" warning. Harmless,
        // but noisy — and pointless, since it'll correctly repopulate
        // fresh the next time this becomes visible anyway.
        if (!gameObject.activeInHierarchy) return;

        // The target is no longer a flat per-objective-type constant —
        // it's decided server-side once at match time (scaled from the
        // matched member count, same one the reward pool uses) and passed
        // straight through from GuildManager.MyContestObjectiveTarget.
        long target = objectiveTarget > 0 ? objectiveTarget : 1;

        // Never DISPLAY beyond the target, even if actual progress has
        // overshot it (e.g. a burst of offline earnings pushed it past
        // the finish line right before the win got detected/processed).
        long displayFriendlyProgress = Math.Min(friendlyProgress, target);
        long displayRivalProgress = Math.Min(rivalProgress, target);

        float friendlyFraction = (float)((double)displayFriendlyProgress / target);
        float rivalFraction = (float)((double)displayRivalProgress / target);

        if (friendlyGuildOverallProgress != null) friendlyGuildOverallProgress.SetFill(friendlyFraction);
        if (rivalGuildOverallProgress != null) rivalGuildOverallProgress.SetFill(rivalFraction);

        if (friendlyProgressText != null) friendlyProgressText.text = $"{GameManager.FormatNumber(displayFriendlyProgress)} / {GameManager.FormatNumber(target)}";
        if (rivalProgressText != null) rivalProgressText.text = $"{GameManager.FormatNumber(displayRivalProgress)} / {GameManager.FormatNumber(target)}";

        // Whatever value the caller passes here is just displayed as-is —
        // GuildSearchContestUI decides whether that's the total contest
        // reward (ongoing/losing) or this player's own per-member share
        // (winning, during Results).
        if (contestRewardAmountText != null) contestRewardAmountText.text = rewardAmount.ToString();

        if (objectiveDescriptionText != null)
        {
            objectiveDescriptionText.text = (!string.IsNullOrEmpty(objectiveType) && ObjectiveDescriptions.TryGetValue(objectiveType, out string desc))
                ? desc
                : "";
        }
    }

    // Called by GuildSearchContestUI alongside Refresh() above, once per
    // update — drives everything that depends on ongoing-vs-Results and
    // win/loss, rather than cramming it into Refresh()'s numeric signature.
    public void RefreshResultsState(bool inResultsPhase, bool won, bool lost, bool rewardClaimed)
    {
        // No activeInHierarchy guard here (unlike Refresh() above, which
        // skips work to avoid animating a hidden GaugeBar coroutine) — this
        // method only does plain SetActive/color/interactable assignments,
        // which are harmless on an inactive object. GuildSearchContestUI
        // deliberately calls this AFTER deactivating contestObjectiveUI's
        // GameObject (to clear the labels/reward visuals once a contest's
        // Results phase ends), so guarding on activeInHierarchy would skip
        // that call entirely and leave the labels stuck on — which is
        // exactly the bug this is fixing.

        if (ongoingContestLabel != null) ongoingContestLabel.SetActive(!inResultsPhase);
        if (contestEndedLabel != null) contestEndedLabel.SetActive(inResultsPhase);

        // Objective description only makes sense while the contest is still
        // ongoing — hidden the instant Results phase starts, same as
        // ongoingContestLabel above.
        if (objectiveDescriptionText != null) objectiveDescriptionText.gameObject.SetActive(!inResultsPhase);

        if (winnersLabel != null) winnersLabel.SetActive(inResultsPhase && won);
        if (losersLabel != null) losersLabel.SetActive(inResultsPhase && lost);

        bool showRewardImage = !inResultsPhase || (won && !rewardClaimed);
        if (contestRewardImage != null)
        {
            contestRewardImage.gameObject.SetActive(showRewardImage);
            contestRewardImage.color = (inResultsPhase && won && !rewardClaimed) ? contestRewardClaimableColor : contestRewardDefaultColor;
        }
        if (contestRewardButton != null)
            contestRewardButton.interactable = inResultsPhase && won && !rewardClaimed;

        if (checkmarkImage != null) checkmarkImage.SetActive(inResultsPhase && won && rewardClaimed);
        if (xMarkImage != null) xMarkImage.SetActive(inResultsPhase && lost);
    }

    // Called by GuildSearchContestUI once the panel returns to passive (no
    // contest, no Results phase) — turns EVERY results/ongoing visual off,
    // including ongoingContestLabel. RefreshResultsState(false, ...) is NOT
    // the right call for this: its inResultsPhase=false path means "ongoing
    // contest" and deliberately turns ongoingContestLabel ON, which is
    // wrong once there's no contest at all.
    public void HideAllResultsUI()
    {
        if (ongoingContestLabel != null) ongoingContestLabel.SetActive(false);
        if (contestEndedLabel != null) contestEndedLabel.SetActive(false);
        if (objectiveDescriptionText != null) objectiveDescriptionText.gameObject.SetActive(false);
        if (winnersLabel != null) winnersLabel.SetActive(false);
        if (losersLabel != null) losersLabel.SetActive(false);
        if (contestRewardImage != null) contestRewardImage.gameObject.SetActive(false);
        if (contestRewardButton != null) contestRewardButton.interactable = false;
        if (checkmarkImage != null) checkmarkImage.SetActive(false);
        if (xMarkImage != null) xMarkImage.SetActive(false);
    }
}
