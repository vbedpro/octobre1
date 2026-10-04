using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

// Achievements panel — now VIRTUALIZED the same way as the leaderboard:
// Content is NOT managed by Vertical Layout Group / Content Size Fitter.
// REMOVE both components from Content in the Inspector if still present —
// this script sets Content's height and each row's anchoredPosition by
// hand instead, and only instantiates/repositions enough rows to cover
// the visible viewport (+ buffer).
//
// Content's RectTransform: Anchor Preset = top-stretch, pivot = (0.5, 1).
// scrollRect/viewport/content must be assigned below (same as LeaderBoardUI).
//
// Unlike the leaderboard, this list has THREE different item heights
// (normal rows, the gap spacer, cosmetic rows) instead of one uniform row
// height, so positions are computed from a small precomputed layout array
// rather than a single "index * rowStride" formula.
public class AchievementsPanelUI : MonoBehaviour
{
    public static bool achievementsOpen = false;

    [Header("Panel Slide")]
    public RectTransform panelRect;
    public Vector2 openPosition;
    public Vector2 closedPosition;
    public float slideDuration = 0.35f;
    public GameObject panelObject;
    [Tooltip("The button that opens this panel — faded out while the panel is open, faded back in once it's closed.")]
    public GameObject openButton;
    [Tooltip("CanvasGroup on the Open Button — required for the fade.")]
    public CanvasGroup openButtonCanvasGroup;
    [Tooltip("Faded out while this panel is open, faded back in once closed — same as the Achievements button above.")]
    public CanvasGroup leaderboardButtonCanvasGroup;
    [Tooltip("Faded out while this panel is open, faded back in once closed — same as the Achievements button above.")]
    public CanvasGroup inventoryButtonCanvasGroup;
    [Tooltip("Faded out while this panel is open, faded back in once closed — same as the Achievements button above.")]
    public CanvasGroup bigBangButtonCanvasGroup;
    [Tooltip("Faded out while this panel is open, faded back in once closed — same as the Achievements button above.")]
    public CanvasGroup settingsButtonCanvasGroup;
    public float buttonFadeDuration = 0.3f;
    private Coroutine buttonFadeCoroutine;
    private Coroutine leaderboardButtonFadeCoroutine;
    private Coroutine inventoryButtonFadeCoroutine;
    private Coroutine bigBangButtonFadeCoroutine;
    private Coroutine settingsButtonFadeCoroutine;

    [Header("Virtualized Scroll")]
    public ScrollRect scrollRect;
    public RectTransform viewport;
    public RectTransform content;
    public AchievementRowUI rowPrefab;
    [Tooltip("Must match Row Prefab's actual height in pixels.")]
    public float normalRowHeight = 90f;
    public float rowSpacing = 10f;
    [Tooltip("Extra rows instantiated above/below the visible area so scrolling doesn't flash blank rows for a frame.")]
    public int bufferRows = 2;
    [Tooltip("Extra empty space above the first row — replicates whatever 'Padding > Top' your old Vertical Layout Group had, so the first row doesn't sit flush against the viewport's very top edge (which may be behind a header/tab bar).")]
    public float topPadding = 0f;
    [Tooltip("Extra empty space below the last row — same idea as Top Padding, so the last row can scroll fully into view instead of being clipped at the bottom.")]
    public float bottomPadding = 0f;
    [Tooltip("Left/right inset for every row — replicates whatever 'Padding > Left/Right' your old Vertical Layout Group had. Rows are now stretched to Content's FULL width with zero inset by default, which can make internal elements (like the gauge bar, whose fill width is computed relative to its own row-relative size) render too wide and shift off-screen.")]
    public float leftPadding = 0f;
    public float rightPadding = 0f;

    [Header("Cosmetic Achievement Rows")]
    [Tooltip("Separate prefab used for the cosmetic achievements (index 14 onward) — everything before that index still uses Row Prefab above.")]
    public AchievementRowUI cosmeticRowPrefab;
    [Tooltip("Must match Cosmetic Row Prefab's actual height in pixels.")]
    public float cosmeticRowHeight = 90f;
    [Tooltip("How many entries at the START of AchievementsManager's list are 'normal' achievements, using Row Prefab. Everything after this index uses Cosmetic Row Prefab instead. Must match your actual Inspector list order.")]
    public int normalAchievementCount = 14;
    [Tooltip("A simple empty RectTransform (with a LayoutElement setting its Preferred Height) — inserted right between the last normal row and the first cosmetic row for a bigger visual gap than the list's normal row spacing.")]
    public RectTransform gapSpacerPrefab;
    [Tooltip("Must match Gap Spacer Prefab's Preferred Height.")]
    public float gapHeight = 60f;

    // One layout item per row/gap, in display order — precomputed whenever
    // the underlying data changes (Populate()), not every scroll tick.
    private struct LayoutItem
    {
        public bool isGap;
        public int sourceIndex; // index into AchievementsManager.Instance.achievements; unused for the gap
        public float height;
    }

    private List<LayoutItem> layout = new List<LayoutItem>();
    private List<float> layoutOffsets = new List<float>(); // top Y (positive, downward) of each layout item
    private float totalContentHeight = 0f;

    private readonly List<AchievementRowUI> normalPool = new List<AchievementRowUI>();
    private readonly List<AchievementRowUI> cosmeticPool = new List<AchievementRowUI>();
    private RectTransform spawnedGapSpacer;
    private Coroutine slideCoroutine;

    [Header("Live Refresh")]
    [Tooltip("How often (seconds) the panel re-reads achievement data while open. Rebuilds the layout + repaints visible rows — cheap even every 0.5s since only ~visible rows get touched.")]
    public float refreshInterval = 0.5f;
    private float refreshTimer = 0f;

    void Update()
    {
        if (!achievementsOpen) return;

        refreshTimer += Time.deltaTime;
        if (refreshTimer >= refreshInterval)
        {
            refreshTimer = 0f;
            Populate();
        }
    }

    void Start()
    {
        if (panelObject != null) panelObject.SetActive(false);
        if (panelRect != null) panelRect.anchoredPosition = closedPosition;

        if (scrollRect != null)
            scrollRect.onValueChanged.AddListener(_ => RefreshVisibleRows());
    }

    public void OpenPanel()
    {
        CloseOtherUI();

        achievementsOpen = true;
        if (panelObject != null) panelObject.SetActive(true);
        FadeButton(0f);
        FadeLeaderboardButton(0f);
        FadeInventoryButton(0f);
        FadeBigBangButton(0f);
        FadeSettingsButton(0f);

        Populate();

        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        slideCoroutine = StartCoroutine(SlideTo(openPosition));
    }

    void CloseOtherUI()
    {
        if (ShopManager.shopOpen) FindAnyObjectByType<ShopManager>()?.CloseShop();
        if (SellManager.popupOpen) FindAnyObjectByType<SellManager>()?.CloseSellPopup();
        if (HutUpgradeManager.popupOpen) FindAnyObjectByType<HutUpgradeManager>()?.ClosePopup();
        if (LeaderboardUI.leaderboardOpen) FindAnyObjectByType<LeaderboardUI>()?.CloseLeaderboard();
        if (SettingsPanelController.settingsOpen) SettingsPanelController.Instance?.CloseSettings();
        if (InventoryPanel.inventoryOpen) FindAnyObjectByType<InventoryPanel>()?.ClosePanel();
        FindAnyObjectByType<PlanetColumnUI>()?.CloseColumn(); // safe no-op if already closed
    }

    public void ClosePanel()
    {
        achievementsOpen = false;
        FadeButton(1f);
        FadeLeaderboardButton(1f);
        FadeInventoryButton(1f);
        FadeBigBangButton(1f);
        FadeSettingsButton(1f);

        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        slideCoroutine = StartCoroutine(SlideToThenDeactivate(closedPosition));
    }

    void FadeButton(float targetAlpha)
    {
        if (openButtonCanvasGroup == null) return;
        if (buttonFadeCoroutine != null) StopCoroutine(buttonFadeCoroutine);
        buttonFadeCoroutine = StartCoroutine(FadeButtonRoutine(targetAlpha));
    }

    IEnumerator FadeButtonRoutine(float targetAlpha)
    {
        bool showing = targetAlpha > 0.5f;

        // Stop blocking clicks/raycasts immediately when fading OUT, but
        // only start allowing them once fully faded IN — avoids the
        // button being tappable while still invisible or mid-fade.
        if (!showing)
        {
            openButtonCanvasGroup.interactable = false;
            openButtonCanvasGroup.blocksRaycasts = false;
        }
        if (openButton != null) openButton.SetActive(true); // stays active for the whole fade, in or out

        float startAlpha = openButtonCanvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < buttonFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / buttonFadeDuration;
            openButtonCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }
        openButtonCanvasGroup.alpha = targetAlpha;

        if (showing)
        {
            openButtonCanvasGroup.interactable = true;
            openButtonCanvasGroup.blocksRaycasts = true;
        }
    }

    void FadeLeaderboardButton(float targetAlpha)
    {
        if (leaderboardButtonCanvasGroup == null) return;
        if (leaderboardButtonFadeCoroutine != null) StopCoroutine(leaderboardButtonFadeCoroutine);
        leaderboardButtonFadeCoroutine = StartCoroutine(FadeCanvasGroupRoutine(leaderboardButtonCanvasGroup, targetAlpha));
    }

    void FadeInventoryButton(float targetAlpha)
    {
        if (inventoryButtonCanvasGroup == null) return;
        if (inventoryButtonFadeCoroutine != null) StopCoroutine(inventoryButtonFadeCoroutine);
        inventoryButtonFadeCoroutine = StartCoroutine(FadeCanvasGroupRoutine(inventoryButtonCanvasGroup, targetAlpha));
    }

    void FadeBigBangButton(float targetAlpha)
    {
        if (bigBangButtonCanvasGroup == null) return;
        if (bigBangButtonFadeCoroutine != null) StopCoroutine(bigBangButtonFadeCoroutine);
        bigBangButtonFadeCoroutine = StartCoroutine(FadeCanvasGroupRoutine(bigBangButtonCanvasGroup, targetAlpha));
    }

    void FadeSettingsButton(float targetAlpha)
    {
        if (settingsButtonCanvasGroup == null) return;
        if (settingsButtonFadeCoroutine != null) StopCoroutine(settingsButtonFadeCoroutine);
        settingsButtonFadeCoroutine = StartCoroutine(FadeCanvasGroupRoutine(settingsButtonCanvasGroup, targetAlpha));
    }

    // Same fade shape as FadeButtonRoutine above (stop blocking raycasts
    // immediately when hiding, only start allowing them once fully shown),
    // just generalized to any CanvasGroup so the Leaderboard/Inventory/
    // BigBang/Settings buttons don't need their own near-duplicate
    // coroutine bodies.
    IEnumerator FadeCanvasGroupRoutine(CanvasGroup group, float targetAlpha)
    {
        bool showing = targetAlpha > 0.5f;

        if (!showing)
        {
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        float startAlpha = group.alpha;
        float elapsed = 0f;
        while (elapsed < buttonFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / buttonFadeDuration;
            group.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }
        group.alpha = targetAlpha;

        if (showing)
        {
            group.interactable = true;
            group.blocksRaycasts = true;
        }
    }

    // Rebuilds the layout array from current achievement data (cheap —
    // ~30 items, no GameObject work here) and Content's total height,
    // then repaints only whatever rows are currently visible.
    public void Populate()
    {
        if (rowPrefab == null || content == null) return;

        if (AchievementsManager.Instance == null)
        {
            Debug.LogWarning("AchievementsPanelUI: no AchievementsManager found in the scene — nothing to display.");
            return;
        }

        List<AchievementData> source = AchievementsManager.Instance.achievements;
        int normalCount = Mathf.Min(normalAchievementCount, source.Count);
        int cosmeticCount = source.Count - normalCount;

        layout.Clear();
        for (int i = 0; i < normalCount; i++)
            layout.Add(new LayoutItem { isGap = false, sourceIndex = i, height = normalRowHeight });

        bool hasGap = cosmeticCount > 0 && gapSpacerPrefab != null;
        if (hasGap)
            layout.Add(new LayoutItem { isGap = true, sourceIndex = -1, height = gapHeight });

        for (int i = 0; i < cosmeticCount; i++)
            layout.Add(new LayoutItem { isGap = false, sourceIndex = normalCount + i, height = cosmeticRowHeight });

        // Precompute each item's top Y offset (cumulative height + spacing).
        layoutOffsets.Clear();
        float cursor = topPadding;
        for (int i = 0; i < layout.Count; i++)
        {
            layoutOffsets.Add(cursor);
            cursor += layout[i].height + rowSpacing;
        }
        totalContentHeight = layout.Count > 0 ? cursor - rowSpacing + bottomPadding : 0f;
        content.sizeDelta = new Vector2(content.sizeDelta.x, totalContentHeight);

        RefreshVisibleRows();
    }

    // ── Virtualization core ──────────────────────────────────────────
    // Only instantiates/repositions enough rows to cover the visible
    // viewport (+ buffer), reusing pooled instances as you scroll —
    // same technique as LeaderBoardUI.RefreshVisibleRows().
    void RefreshVisibleRows()
    {
        if (content == null || viewport == null || layout.Count == 0)
        {
            foreach (AchievementRowUI r in normalPool) r.gameObject.SetActive(false);
            foreach (AchievementRowUI r in cosmeticPool) r.gameObject.SetActive(false);
            if (spawnedGapSpacer != null) spawnedGapSpacer.gameObject.SetActive(false);
            return;
        }

        float scrollOffset = Mathf.Max(0f, content.anchoredPosition.y);
        float viewportHeight = viewport.rect.height;
        float bufferPixels = bufferRows * (normalRowHeight + rowSpacing);

        int firstVisible = -1;
        int lastVisible = -1;
        for (int i = 0; i < layout.Count; i++)
        {
            float itemTop = layoutOffsets[i];
            float itemBottom = itemTop + layout[i].height;
            if (itemBottom < scrollOffset - bufferPixels) continue; // above the visible window
            if (itemTop > scrollOffset + viewportHeight + bufferPixels) break; // below it — items are in order, safe to stop

            if (firstVisible == -1) firstVisible = i;
            lastVisible = i;
        }

        if (firstVisible == -1)
        {
            foreach (AchievementRowUI r in normalPool) r.gameObject.SetActive(false);
            foreach (AchievementRowUI r in cosmeticPool) r.gameObject.SetActive(false);
            if (spawnedGapSpacer != null) spawnedGapSpacer.gameObject.SetActive(false);
            return;
        }

        List<AchievementData> source = AchievementsManager.Instance.achievements;
        int normalPoolIndex = 0;
        int cosmeticPoolIndex = 0;
        bool gapUsed = false;

        for (int i = firstVisible; i <= lastVisible; i++)
        {
            LayoutItem item = layout[i];

            if (item.isGap)
            {
                if (spawnedGapSpacer == null)
                {
                    spawnedGapSpacer = Instantiate(gapSpacerPrefab, content);
                    SetupRectTransform(spawnedGapSpacer, gapHeight);
                }
                PositionRect(spawnedGapSpacer, layoutOffsets[i]);
                spawnedGapSpacer.gameObject.SetActive(true);
                gapUsed = true;
                continue;
            }

            bool isCosmetic = item.sourceIndex >= normalAchievementCount;
            List<AchievementRowUI> pool = isCosmetic ? cosmeticPool : normalPool;
            AchievementRowUI prefab = isCosmetic ? (cosmeticRowPrefab != null ? cosmeticRowPrefab : rowPrefab) : rowPrefab;
            int poolIndex = isCosmetic ? cosmeticPoolIndex : normalPoolIndex;

            if (poolIndex >= pool.Count)
            {
                AchievementRowUI r = Instantiate(prefab, content);
                RectTransform newRt = r.GetComponent<RectTransform>();
                SetupRectTransform(newRt, item.height);

                // A freshly instantiated row may contain its own internal
                // Layout Group (e.g. sizing the gauge bar) that hasn't
                // finished computing on this exact frame yet. Force it to
                // settle immediately — otherwise GaugeBar.SetFillInstant()
                // below can read a stale/zero width and render the fill
                // way off to one side. The old (non-virtualized) system
                // never hit this because every row was created upfront in
                // Start(), giving layout plenty of time to settle first.
                LayoutRebuilder.ForceRebuildLayoutImmediate(newRt);

                pool.Add(r);
            }

            AchievementRowUI row = pool[poolIndex];
            if (isCosmetic) cosmeticPoolIndex++; else normalPoolIndex++;

            PositionRect(row.GetComponent<RectTransform>(), layoutOffsets[i]);
            row.gameObject.SetActive(true);
            row.Set(source[item.sourceIndex], this);
        }

        // Hide unused pool slots (window shrank, or scrolled past them).
        for (int i = normalPoolIndex; i < normalPool.Count; i++)
            normalPool[i].gameObject.SetActive(false);
        for (int i = cosmeticPoolIndex; i < cosmeticPool.Count; i++)
            cosmeticPool[i].gameObject.SetActive(false);
        if (!gapUsed && spawnedGapSpacer != null)
            spawnedGapSpacer.gameObject.SetActive(false);
    }

    void SetupRectTransform(RectTransform rt, float height)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        // Stretched-X sizing: sizeDelta.x is the width DELTA vs full parent
        // width, so a negative value insets from both edges. anchoredPosition.x
        // re-centers when the inset is asymmetric (pivot.x = 0.5).
        rt.sizeDelta = new Vector2(-(leftPadding + rightPadding), height);
        rt.anchoredPosition = new Vector2((leftPadding - rightPadding) / 2f, rt.anchoredPosition.y);
    }

    void PositionRect(RectTransform rt, float topOffset)
    {
        if (rt == null) return;
        // Only touch Y here — X (the left/right padding inset) was already
        // set once in SetupRectTransform and must be preserved.
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -topOffset);
    }

    // Called by a row when the player claims a tier — placeholder hook
    // for whatever should happen next (saving, analytics, etc.) once
    // this system is wired up further.
    public void NotifyAchievementClaimed(AchievementData data)
    {
        // TODO: persist achievement state once save/load for achievements exists.
    }

    IEnumerator SlideTo(Vector2 target)
    {
        if (panelRect == null) yield break;

        Vector2 start = panelRect.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / slideDuration;
            t = t * t * (3f - 2f * t);
            panelRect.anchoredPosition = Vector2.Lerp(start, target, t);
            yield return null;
        }
        panelRect.anchoredPosition = target;
    }

    IEnumerator SlideToThenDeactivate(Vector2 target)
    {
        yield return SlideTo(target);
        if (panelObject != null) panelObject.SetActive(false);
    }
}
