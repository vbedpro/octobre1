using UnityEngine;
using System.Collections.Generic;

// Central, persistent achievement tracker — owns the actual list (not
// AchievementsPanelUI, which only displays it). Every gameplay system
// reports progress here via the ReportX() methods; the panel just reads
// AchievementsManager.Instance.achievements when it opens.
//
// Indices below match the 14 achievements as specified, in order. If you
// reorder the list in the Inspector, update the constants below to match.
public class AchievementsManager : MonoBehaviour
{
    public static AchievementsManager Instance { get; private set; }

    [Header("All 14 achievements, in order")]
    public List<AchievementData> achievements = new List<AchievementData>();

#if UNITY_EDITOR
    [Header("Editor Testing Only — never affects a real build")]
    [Tooltip("Artificially adds 1 day to the login-streak comparison every time Play mode restarts, so you can test day-to-day transitions without waiting real calendar time. Does NOT affect what gets written to PlayerPrefs, so no drift accumulates — each restart simulates exactly one day passed since your ACTUAL last real check.")]
    public bool editorSimulateDayPassed = false;
#endif

    // Index constants — keep in sync with the Inspector list's actual order.
    const int IDX_GlobalRank = 0;
    const int IDX_LoginStreak = 1;
    const int IDX_HatchTotal = 2;
    const int IDX_SellTotal = 3;
    const int IDX_BuildHuts = 4;
    const int IDX_UnlockAllPlanets = 5;
    const int IDX_PlanetTravel = 6;
    const int IDX_FoeFreeStreak = 7;
    const int IDX_TapFoes = 8;
    const int IDX_BigBangs = 9;
    const int IDX_LifetimeRabbits = 10;
    const int IDX_AcceptQuests = 11;
    const int IDX_ConsecutiveQuests = 12;
    const int IDX_SpendToZero = 13;

    // ── Cosmetic achievements (all single-tier) ─────────────────────
    const int IDX_ColoredNickname   = 14; // complete N bronze tiers
    const int IDX_GoldenNickname    = 15; // fully complete N achievements
    const int IDX_ColorFadeNickname = 16; // buy N common upgrades
    const int IDX_RainbowNickname   = 17; // buy N epic upgrades
    const int IDX_StarryBgStatic    = 18; // BigBang while exactly 2 planets unlocked
    const int IDX_SkullBgStatic     = 19; // let N foes enter huts
    const int IDX_HeartsBgStatic    = 20; // reach breeding rate of N%
    const int IDX_CrownsBgStatic    = 21; // win N guild contests (system not built yet)
    const int IDX_FireBgStatic      = 22; // reach N rabbits on a single farm
    const int IDX_LightningBg       = 23; // activate N total boosts
    const int IDX_StarryBgAnimated  = 24; // BigBang while ALL planets unlocked
    const int IDX_SkullBgAnimated   = 25; // let N foes enter huts
    const int IDX_HeartsBgAnimated  = 26; // reach breeding rate of N%
    const int IDX_CrownsBgAnimated  = 27; // win N guild contests (system not built yet)
    const int IDX_FireBgAnimated    = 28; // reach N rabbits on a single farm

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        DontDestroyOnLoad(gameObject);
    }

    // NOTE: CheckLoginStreak() is deliberately NOT called from Start()
    // anymore — it needs the achievement's currentStatValue already
    // restored from the save file first (SaveManager calls it explicitly,
    // right after RestoreSaveData()). Calling it here in Start() would run
    // BEFORE that restore, always operating on a fresh default of 0
    // instead of the real previous streak.

    // ── Save / Load ──────────────────────────────────────────────────
    // Called by SaveManager on every save (autosave, pause, quit) and
    // load — same pattern as every other system in this project. Matched
    // by "id" rather than list position, so reordering the Inspector
    // list later doesn't silently scramble a returning player's progress.
    [System.Serializable]
    public class AchievementSaveEntry
    {
        public string id;
        public long currentStatValue;
        public int currentTierIndex;
        public long rankStartingReference; // only meaningful for the inverted-progress (rank) achievement
    }

    public List<AchievementSaveEntry> GetSaveData()
    {
        List<AchievementSaveEntry> result = new List<AchievementSaveEntry>();
        foreach (AchievementData a in achievements)
        {
            result.Add(new AchievementSaveEntry
            {
                id = a.id,
                currentStatValue = a.currentStatValue,
                currentTierIndex = a.currentTierIndex,
                rankStartingReference = a.rankStartingReference
            });
        }
        return result;
    }

    public void RestoreSaveData(List<AchievementSaveEntry> saved)
    {
        if (saved == null) return;

        foreach (AchievementSaveEntry entry in saved)
        {
            foreach (AchievementData a in achievements)
            {
                if (a.id != entry.id) continue;

                a.currentStatValue = entry.currentStatValue;
                a.currentTierIndex = entry.currentTierIndex;
                a.rankStartingReference = entry.rankStartingReference;
                break;
            }
        }
    }

    AchievementData Get(int index) => (index >= 0 && index < achievements.Count) ? achievements[index] : null;

    // Monotonically raises a stat to at most the new value — never lets
    // it decrease, matching how every "reach X" achievement should behave
    // (best-ever progress, not current/live value that could dip back down).
    // Keeps accumulating even past a tier's threshold while it sits
    // unclaimed — this is intentional: claiming a tier always requires an
    // individual button press regardless of how high the stat climbs, so
    // nothing can ever auto-complete multiple tiers at once. Letting the
    // raw stat keep moving is what makes the NEXT tier's gauge correctly
    // start pre-filled instead of resetting to a hard 0%.
    static void RaiseTo(AchievementData a, long newValue)
    {
        if (a == null) return;
        if (!a.isLifetimeStat && a.IsReadyToClaim()) return;
        if (newValue > a.currentStatValue) a.currentStatValue = newValue;
    }

    static void Increment(AchievementData a, long amount = 1)
    {
        if (a == null) return;
        if (!a.isLifetimeStat && a.IsReadyToClaim()) return;
        a.currentStatValue += amount;
    }

    // ── #1 Global Rank (inverted — lower is better) ─────────────────
    // Call whenever the player's own rank is known (e.g. after a
    // leaderboard fetch). Uses RaiseTo's inverse — only improves (lowers)
    // the tracked value, since a temporary worse rank shouldn't undo
    // progress toward a better one already achieved.
    public void ReportGlobalRank(int rank)
    {
        AchievementData a = Get(IDX_GlobalRank);
        if (a == null) return;
        if (!a.isLifetimeStat && a.IsReadyToClaim()) return;

        // First time ever seeing a rank — this becomes the permanent
        // "starting line" the Bronze gauge measures progress from.
        if (a.rankStartingReference == 0) a.rankStartingReference = rank;

        if (a.currentStatValue == 0 || rank < a.currentStatValue) a.currentStatValue = rank;
    }

    // ── #2 Hatch total ───────────────────────────────────────────────
    public void ReportRabbitHatched() => Increment(Get(IDX_HatchTotal));

    // ── #3 Sell total ────────────────────────────────────────────────
    public void ReportRabbitsSold(long amount) => Increment(Get(IDX_SellTotal), amount);

    // ── #4 Build huts ────────────────────────────────────────────────
    public void ReportHutBuilt() => Increment(Get(IDX_BuildHuts));

    // ── #5 Unlock all planets (single-tier) ─────────────────────────
    // Call with however many planets are currently unlocked (1-5) —
    // achievement's Bronze Target should be set to 5 in the Inspector.
    public void ReportPlanetsUnlockedCount(int count) => RaiseTo(Get(IDX_UnlockAllPlanets), count);

    // ── #6 Planet travel count ───────────────────────────────────────
    public void ReportPlanetSwitch() => Increment(Get(IDX_PlanetTravel));

    // ── #7 Foe-free streak (per-planet timer, planet needs 10 huts) ──
    // Tracked in real time via Update() below — see that section.
    private PlanetManager.Planet? foeFreeTrackedPlanet = null;
    private float foeFreeStreakSeconds = 0f;

    // Called by PlanetManager whenever the player switches planets —
    // resets which planet we're tracking the streak for.
    public void OnPlanetChanged(PlanetManager.Planet newPlanet)
    {
        foeFreeTrackedPlanet = newPlanet;
        foeFreeStreakSeconds = 0f;
    }

    // Called by FoxMover whenever a foe successfully enters a hut —
    // resets the current streak back to 0 (the whole point of this
    // achievement: an uninterrupted stretch with NO foe getting in).
    public void ReportFoeEnteredHut(PlanetManager.Planet planet)
    {
        if (foeFreeTrackedPlanet.HasValue && foeFreeTrackedPlanet.Value == planet)
            foeFreeStreakSeconds = 0f;
    }

    void Update()
    {
        // Only accumulate while actively on the tracked planet AND that
        // planet has at least 10 huts (per spec) — otherwise just hold,
        // don't reset (leaving early on a planet with <10 huts shouldn't
        // punish the player, it just doesn't count toward the streak yet).
        if (foeFreeTrackedPlanet.HasValue && UpgradeManager.Instance != null && UpgradeManager.Instance.hutCount >= 10)
        {
            foeFreeStreakSeconds += Time.deltaTime;
            float minutes = foeFreeStreakSeconds / 60f;
            RaiseTo(Get(IDX_FoeFreeStreak), (long)minutes);
        }

        CheckDayRollover();
    }

    // ── #8 Tap foes ──────────────────────────────────────────────────
    public void ReportFoeTapped() => Increment(Get(IDX_TapFoes));

    // ── #9 BigBangs ──────────────────────────────────────────────────
    public void ReportBigBang(long amount = 1) => Increment(Get(IDX_BigBangs), amount);

    // ── #10 Lifetime rabbits ─────────────────────────────────────────
    // Call with GameManager.lifetimeRabbits directly whenever it changes
    // — this stat already only ever increases, so RaiseTo is a safe fit
    // even though a direct set would also work.
    public void ReportLifetimeRabbits(long total) => RaiseTo(Get(IDX_LifetimeRabbits), total);

    // ── #11 Accept quests ────────────────────────────────────────────
    public void ReportQuestAccepted() => Increment(Get(IDX_AcceptQuests));

    // ── #12 Consecutive completed quests ─────────────────────────────
    // Increments on a successful completion; resets to 0 if an ACCEPTED
    // quest fails/times out. Declining a quest (never accepted) does NOT
    // affect this streak at all — it simply isn't counted either way.
    public void ReportQuestCompletedSuccessfully()
    {
        AchievementData a = Get(IDX_ConsecutiveQuests);
        if (a == null) return;
        if (!a.isLifetimeStat && a.IsReadyToClaim()) return;
        a.currentStatValue += 1;
    }

    public void ReportAcceptedQuestFailed()
    {
        AchievementData a = Get(IDX_ConsecutiveQuests);
        if (a == null) return;
        a.currentStatValue = 0;
    }

    // ── #13 Spend to exactly 0 (single-tier) ─────────────────────────
    // Call from GameManager.SpendCoins() right after a successful spend
    // — quest debuffs use ForceSpendCoins() instead, so they never touch
    // this by construction (per spec: debuffs don't count).
    public void ReportCoinsSpent(long resultingBalance)
    {
        if (resultingBalance == 0)
        {
            AchievementData a = Get(IDX_SpendToZero);
            if (a != null) a.currentStatValue = 1; // target should be set to 1 in the Inspector
        }
    }

    // ── #14 Login streak ─────────────────────────────────────────────
    private const string LastLoginDateKey = "RabbitUniverse_LastLoginDate";
    private System.DateTime lastCheckedDate;

    public void CheckLoginStreak()
    {
        string todayStr = System.DateTime.UtcNow.ToString("yyyy-MM-dd");
        string lastStr = PlayerPrefs.GetString(LastLoginDateKey, "");

        AchievementData a = Get(IDX_LoginStreak);

        Debug.Log($"[LoginStreakDebug] CheckLoginStreak() called. todayStr={todayStr}, lastStr(raw from PlayerPrefs)='{lastStr}', achievement found (a != null)? {a != null}, currentStatValue BEFORE this check={a?.currentStatValue}");

        if (string.IsNullOrEmpty(lastStr))
        {
            // First time ever — start the streak at 1.
            Debug.Log("[LoginStreakDebug] Branch: lastStr is empty/null -> treating as FIRST EVER login, setting currentStatValue=1.");
            if (a != null) a.currentStatValue = 1;
        }
        else if (System.DateTime.TryParse(lastStr, System.Globalization.CultureInfo.InvariantCulture,
                     System.Globalization.DateTimeStyles.None, out System.DateTime lastDate))
        {
            System.DateTime today = System.DateTime.UtcNow.Date;
            int daysSince = (today - lastDate.Date).Days;

#if UNITY_EDITOR
            if (editorSimulateDayPassed)
            {
                daysSince += 1;
                Debug.Log($"[LoginStreakDebug] Editor Simulate Day Passed is ON — daysSince bumped by 1 for testing (real value was {daysSince - 1}).");
            }
#endif

            Debug.Log($"[LoginStreakDebug] Branch: parsed lastDate={lastDate:yyyy-MM-dd}, today={today:yyyy-MM-dd}, daysSince={daysSince}");

            if (daysSince == 1)
            {
                // Consecutive day — streak continues.
                Debug.Log("[LoginStreakDebug] daysSince == 1 -> incrementing streak.");
                if (a != null) a.currentStatValue += 1;
            }
            else if (daysSince > 1)
            {
                // Missed at least one full calendar day — streak resets.
                Debug.Log("[LoginStreakDebug] daysSince > 1 -> RESETTING streak to 1.");
                if (a != null) a.currentStatValue = 1;
            }
            else
            {
                Debug.Log("[LoginStreakDebug] daysSince == 0 (or negative/clock skew) -> no change.");
            }
            // daysSince == 0 (already logged in today) — no change, don't double-count.
        }
        else
        {
            Debug.LogWarning($"[LoginStreakDebug] lastStr ('{lastStr}') failed to parse as a date entirely — falling through with NO branch taken, currentStatValue left untouched.");
        }

        Debug.Log($"[LoginStreakDebug] currentStatValue AFTER this check={a?.currentStatValue}. Writing todayStr='{todayStr}' to PlayerPrefs now.");

        PlayerPrefs.SetString(LastLoginDateKey, todayStr);
        PlayerPrefs.Save();
        lastCheckedDate = System.DateTime.UtcNow.Date;
    }

    // Catches the edge case of the app staying open across a real
    // midnight rollover without a fresh launch — re-checks periodically.
    private float dayCheckTimer = 0f;
    void CheckDayRollover()
    {
        dayCheckTimer += Time.deltaTime;
        if (dayCheckTimer < 60f) return; // check once a minute, not every frame
        dayCheckTimer = 0f;

        if (System.DateTime.UtcNow.Date != lastCheckedDate)
            CheckLoginStreak();
    }

    // ── #15 Colored Nickname — complete N bronze tiers (any achievement) ──
    // Called by AchievementRowUI whenever ANY row claims its bronze tier
    // (tierBeingClaimed == 0), including this and every other achievement.
    public void ReportBronzeTierClaimed() => Increment(Get(IDX_ColoredNickname));

    // ── #16 Golden Nickname — fully complete N achievements ─────────
    // Called by AchievementRowUI whenever ANY row reaches currentTierIndex
    // 4 (fully complete) — singleTier claims count too, same as a normal
    // achievement's Platinum claim.
    public void ReportAchievementFullyCompleted() => Increment(Get(IDX_GoldenNickname));

    // ── #17 Color Fade Nickname — buy N common upgrades ──────────────
    public void ReportCommonUpgradePurchased() => Increment(Get(IDX_ColorFadeNickname));

    // ── #18 Rainbow Nickname — buy N epic upgrades ───────────────────
    public void ReportEpicUpgradePurchased() => Increment(Get(IDX_RainbowNickname));

    // ── #19 / #24 Starry Background — BigBang with 2 / ALL planets unlocked ──
    // Called by PrestigeManager.ConfirmBigBang() with however many planets
    // are unlocked AT THE MOMENT the BigBang is confirmed. Single-tier
    // "did this exact thing ever happen" achievements — same pattern as
    // Spend-to-Zero: set currentStatValue straight to 1 (Bronze Target
    // should be 1 in the Inspector) rather than counting up.
    public void ReportBigBangWithPlanetsUnlocked(int unlockedCount, int totalPlanetCount)
    {
        if (unlockedCount == 2)
        {
            AchievementData a = Get(IDX_StarryBgStatic);
            if (a != null) a.currentStatValue = 1;
        }
        if (unlockedCount >= totalPlanetCount)
        {
            AchievementData a = Get(IDX_StarryBgAnimated);
            if (a != null) a.currentStatValue = 1;
        }
    }

    // ── #20 / #25 Skullheads Background — let N foes enter huts ──────
    // Separate CUMULATIVE counter from the Foe-Free-Streak achievement
    // (#7) above, which resets on every foe entry instead of counting up
    // — these two need the opposite behavior, so they can't share a stat.
    public void ReportFoeEnteredHutCumulative()
    {
        Increment(Get(IDX_SkullBgStatic));
        Increment(Get(IDX_SkullBgAnimated));
    }

    // ── #21 / #26 Hearts Background — reach a breeding rate of N% ────
    // Tracked live in Update() below — see that section. 100% = the
    // unmodified base rate (1x), matching how the target value ("10000%")
    // is meant to be read: 10000% = 100x the base rate.
    void UpdateBreedingRateAchievements()
    {
        if (UpgradeManager.Instance == null) return;
        float ratePercent = UpgradeManager.Instance.ComputeBaseReproductionRate() * 100f;
        RaiseTo(Get(IDX_HeartsBgStatic), (long)ratePercent);
        RaiseTo(Get(IDX_HeartsBgAnimated), (long)ratePercent);
    }

    // ── #22 / #27 Fire Background — reach N rabbits on a single farm ──
    // "Single farm" = any one planet's rabbit count, tracked as a running
    // best via RaiseTo (never decreases even if that farm later gets
    // sold down or raided) — checked against whichever planet is
    // currently active, since that's the only one with a live, accurate
    // totalRabbits at any given moment.
    void UpdateSingleFarmRabbitAchievements()
    {
        if (PlanetManager.Instance == null) return;
        long highest = PlanetManager.Instance.GetHighestSingleFarmRabbits();
        RaiseTo(Get(IDX_FireBgStatic), highest);
        RaiseTo(Get(IDX_FireBgAnimated), highest);
    }

    // ── #23 Lightning Background — activate N total boosts ───────────
    // Subscribed in Start() below to BoostActivationManager.OnBoostActivated.
    void OnAnyBoostActivated(string itemId) => Increment(Get(IDX_LightningBg));

    void Start()
    {
        if (BoostActivationManager.Instance != null)
            BoostActivationManager.Instance.OnBoostActivated += OnAnyBoostActivated;
    }

    // ── #21/#22/#26/#27 live checks, plus the existing day-rollover check ──
    void LateUpdate()
    {
        UpdateBreedingRateAchievements();
        UpdateSingleFarmRabbitAchievements();
    }

    // ── #24 / #29 Crowns Background — win N Guild Contests ───────────
    // PLACEHOLDER — Guilds don't exist yet (separate future system, needs
    // its own backend). Call this once that system tracks contest wins,
    // with the player's new TOTAL win count — same RaiseTo pattern as
    // everything else here. Nothing else needs to change when that day
    // comes; this method is already fully wired to both tiers.
    public void ReportGuildContestsWon(int totalWins)
    {
        RaiseTo(Get(IDX_CrownsBgStatic), totalWins);
        RaiseTo(Get(IDX_CrownsBgAnimated), totalWins);
    }
}
