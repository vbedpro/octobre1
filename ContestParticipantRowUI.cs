using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Smaller row shown during an ongoing contest, for a single participant
// (friendly OR rival side): country icon, player name, objective-type
// icon (same icon on every row — all participants share one contest-wide
// objective), and this player's OWN individual progress toward it.
public class ContestParticipantRowUI : MonoBehaviour
{
    public Image countryIcon;
    public TextMeshProUGUI nameText;
    public Image objectiveTypeIcon;
    public TextMeshProUGUI progressText;

    [Tooltip("Sprites in the SAME ORDER as the objective type list below.")]
    public Sprite[] objectiveIcons;

    // Fixed order matching objectiveIcons — must stay in sync with
    // GuildManager.ContestObjectiveTargets' keys.
    private static readonly string[] ObjectiveTypeOrder =
    {
        "InitiateBigBangs", "ClimbRanks", "HatchRabbits", "CompleteQuests", "SendTrucks", "BreedRabbits"
    };

    public void Set(LeaderboardUI.RowData row)
    {
        if (countryIcon != null) countryIcon.sprite = CountryFlagDatabase.GetFlag(row.country);

        if (nameText != null)
        {
            // Player names are stored as "username#12345" — strip the
            // hashtag suffix for this row specifically, then add " : "
            // as a separator before whatever comes next (progress text).
            string displayName = row.playerName ?? "";
            int hashIndex = displayName.IndexOf('#');
            if (hashIndex >= 0) displayName = displayName.Substring(0, hashIndex);

            nameText.text = displayName + " : ";
        }

        string objectiveType = GuildManager.Instance != null ? GuildManager.Instance.MyContestObjectiveType : null;
        if (objectiveTypeIcon != null && !string.IsNullOrEmpty(objectiveType))
        {
            int index = System.Array.IndexOf(ObjectiveTypeOrder, objectiveType);
            if (objectiveIcons != null && index >= 0 && index < objectiveIcons.Length)
                objectiveTypeIcon.sprite = objectiveIcons[index];
        }

        // Assumption: plain formatted number for THIS player's own
        // contribution (not "X / target" — that format is used for the
        // guild-wide totals on ContestObjectiveUI instead). Let me know
        // if you actually wanted "X / target" here too.
        if (progressText != null) progressText.text = GameManager.FormatNumber(row.contestProgress);
    }
}
