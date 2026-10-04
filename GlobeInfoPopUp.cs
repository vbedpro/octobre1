using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Services.Authentication;

// Shown when a player taps a dot on the Overview globe. Reuses the fixed
// "personal row" slot at the bottom of the panel, swapping between two
// pre-placed scene instances stacked at that position:
//   - regularRow — plain style, shown for any OTHER player's dot.
//   - personalizedRow — highlighted style, shown only for your own dot.
//
// Once shown, independently re-queries LeaderboardUI's live list by
// playerId every liveUpdateInterval seconds — this is what makes the
// popup track real-time changes (score growth, rank shifting) rather than
// showing a frozen snapshot from the moment you tapped. This is
// deliberately decoupled from whatever the globe's dots are doing
// visually — it doesn't matter whether/how often dots get respawned.
public class GlobeInfoPopup : MonoBehaviour
{
    [Header("Regular Row (shown for other players)")]
    public LeaderboardRowUI regularRow;
    public GameObject regularRowObject;

    [Header("Personalized Row (shown only for your own dot)")]
    public LeaderboardRowUI personalizedRow;
    public GameObject personalizedRowObject;

    [Header("Auto-Close On Rotate")]
    public GlobeController globeController;

    [Header("Live Update")]
    [Tooltip("Source of live data to re-query while a dot's info is showing.")]
    public LeaderboardUI leaderboardUI;
    public float liveUpdateInterval = 2f;

    private string currentlyShownPlayerId;
    private bool showingSelf;
    private Coroutine liveUpdateCoroutine;

    void Awake()
    {
        HideBoth();
    }

    void OnEnable()
    {
        if (globeController != null) globeController.OnRotationStarted += Hide;
    }

    void OnDisable()
    {
        if (globeController != null) globeController.OnRotationStarted -= Hide;
    }

    public void Show(GlobeDot dot)
    {
        if (dot == null) return;

        bool isOwnRow = LeaderboardManager.Instance != null && LeaderboardManager.Instance.IsReady
            && !string.IsNullOrEmpty(dot.playerId)
            && dot.playerId == AuthenticationService.Instance.PlayerId;

        currentlyShownPlayerId = dot.playerId;
        showingSelf = isOwnRow;

        ApplyDisplay(dot.rank, dot.playerName, dot.country, dot.bigBangCount, dot.lifetimeRabbits, isOwnRow, dot.equippedNickname, dot.equippedBackground, dot.nicknameColorIndex);

        if (liveUpdateCoroutine != null) StopCoroutine(liveUpdateCoroutine);
        liveUpdateCoroutine = StartCoroutine(LiveUpdateLoop());
    }

    void ApplyDisplay(int rank, string playerName, string country, int bigBangCount, long lifetimeRabbits, bool isOwnRow, string equippedNickname = null, string equippedBackground = null, int nicknameColorIndex = 0)
    {
        Sprite flag = CountryFlagDatabase.GetFlag(country);

        if (isOwnRow)
        {
            if (regularRowObject != null) regularRowObject.SetActive(false);
            if (personalizedRowObject != null) personalizedRowObject.SetActive(true);
            if (personalizedRow != null)
                personalizedRow.Set(rank, flag, playerName, bigBangCount, lifetimeRabbits, true, equippedNickname, equippedBackground, nicknameColorIndex);
        }
        else
        {
            if (personalizedRowObject != null) personalizedRowObject.SetActive(false);
            if (regularRowObject != null) regularRowObject.SetActive(true);
            if (regularRow != null)
                regularRow.Set(rank, flag, playerName, bigBangCount, lifetimeRabbits, false, equippedNickname, equippedBackground, nicknameColorIndex);
        }
    }

    IEnumerator LiveUpdateLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(liveUpdateInterval);

            if (string.IsNullOrEmpty(currentlyShownPlayerId) || leaderboardUI == null) continue;

            List<LeaderboardUI.RowData> entries = leaderboardUI.GetCurrentEntries();
            if (entries == null) continue;

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].playerId == currentlyShownPlayerId)
                {
                    ApplyDisplay(i + 1, entries[i].playerName, entries[i].country, entries[i].bigBangCount, entries[i].lifetimeRabbits, showingSelf, entries[i].equippedNickname, entries[i].equippedBackground, entries[i].nicknameColorIndex);
                    break;
                }
            }
        }
    }

    public void Hide()
    {
        HideBoth();
        currentlyShownPlayerId = null;
        if (liveUpdateCoroutine != null) { StopCoroutine(liveUpdateCoroutine); liveUpdateCoroutine = null; }
    }

    void HideBoth()
    {
        if (regularRowObject != null) regularRowObject.SetActive(false);
        if (personalizedRowObject != null) personalizedRowObject.SetActive(false);
    }
}
