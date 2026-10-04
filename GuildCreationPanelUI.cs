using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;

// The slide-in panel for creating a new guild — name input, a cyclable
// icon shape + color combo, and a Create button. Same slide pattern as
// GuildInfoPanelUI/GuildMemberInfoPanel.
public class GuildCreationPanelUI : MonoBehaviour
{
    // Only one of these ever exists (unlike GuildInfoPanel/GuildMemberInfoPanel),
    // so a plain singleton is safe here — no collision risk.
    public static GuildCreationPanelUI Instance { get; private set; }
    public bool IsOpen { get; private set; } = false;

    [Header("Panel Slide")]
    public RectTransform panelRect;
    public Vector2 openPosition;
    public Vector2 closedPosition;
    public float slideDuration = 0.35f;
    public GameObject panelObject;
    public Button backButton;
    [Tooltip("Every one of these is hidden while this panel is open, and restored when closed IF still on the Guilds tab. E.g. the columns header, GuildStatus, etc.")]
    public List<GameObject> objectsToHideWhileOpen = new List<GameObject>();

    [Header("Guild Name")]
    public TMP_InputField guildNameInput;
    public int maxNameLength = 14;
    [Tooltip("Updates live as the player types, mirroring the input field exactly.")]
    public TextMeshProUGUI guildNamePreviewText;
    [Tooltip("Optional — shown briefly if Create is pressed with a blocked name. Leave unassigned to just silently block + log a warning instead.")]
    public TextMeshProUGUI errorText;

    [Tooltip("Button covering the panel's empty background area (same role as AuthenticationPopUp's dimOverlayButton) — lets the player tap away from guildNameInput to dismiss the mobile keyboard without closing this panel. Needs a Graphic to raycast against; leave unassigned if this panel has no such background button yet.")]
    public Button backgroundTapButton;

    [Header("Cost")]
    public long createGuildCost = 500;

    [Header("Guild Logo")]
    public Button iconSelectorButton;
    public Button colorSelectorButton;
    [Tooltip("Flat-color background image sitting BEHIND the shape icon — tinted with the selected color.")]
    public Image guildLogoBackgroundPreview;
    [Tooltip("The shape icon on top of the background — sprite changes as you cycle, but is NOT tinted by the color.")]
    public Image guildLogoPreview;
    [Tooltip("Sprites in the SAME ORDER as GuildManager.GuildLogoShape (Circle, Square, Triangle, Star, Diamond).")]
    public Sprite[] shapeSprites;
    [Tooltip("Sprites in the SAME ORDER as GuildManager.GuildLogoBackground \u2014 cyclable background image options.")]
    public Sprite[] backgroundSprites;

    [Header("Selector Preview Images (the image sitting above/on each selector button, showing the currently selected icon/color \u2014 same sprite+tint logic as guildLogoPreview/guildLogoBackgroundPreview above)")]
    [Tooltip("Shows the currently selected shape sprite, tinted the same as guildLogoPreview. Updates every time iconSelectorButton is clicked.")]
    public Image iconSelectorPreview;
    [Tooltip("Shows the currently selected background sprite, same as guildLogoBackgroundPreview. Updates every time colorSelectorButton is clicked.")]
    public Image colorSelectorPreview;

    [Header("Icon Tint (8 direct-selection buttons, NOT cycled)")]
    [Tooltip("All 8 buttons, in the same order as Tint Colors below. Element 0 should be your white/no-tint button.")]
    public Button[] tintButtons;
    [Tooltip("Matching color for each button above. Defaults to the 8 colors you specified \u2014 edit freely.")]
    public Color[] tintColors = new Color[]
    {
        Color.white,                                    // no tint
        new Color32(0x42, 0x71, 0xFF, 0xFF),             // blue
        new Color32(0xFF, 0x4F, 0x00, 0xFF),             // orange
        new Color32(0xFF, 0x00, 0x00, 0xFF),             // red
        new Color32(0xFF, 0x00, 0xE6, 0xFF),             // purple
        new Color32(0xFF, 0xDB, 0x42, 0xFF),             // yellow
        new Color32(0x28, 0xE0, 0x25, 0xFF),             // green
        new Color32(0x1D, 0x1D, 0x1D, 0xFF),             // dark
    };

    [Header("Create Button")]
    public Button createGuildButton;
    [Tooltip("Newly created guilds get added to this list immediately, so they show up in the browse list right away.")]
    public GuildsListUI guildsListUI;

    private int shapeIndex = 0;
    private int colorIndex = 0;
    private int selectedTintIndex = 0; // defaults to white/no-tint
    private Coroutine slideCoroutine;

    // Safeguard against a well-known TMP_InputField mobile quirk: tapping
    // AWAY from the field to dismiss the keyboard (instead of pressing
    // Return/Done) gets reported as the keyboard being "Canceled" rather
    // than "Done" — and TMP_InputField's own internal polling then reverts
    // the field's text back to whatever it was BEFORE the player started
    // typing, repeating every time until Return/Done is actually pressed.
    // This caches the last real value the player typed and restores it if
    // that revert is detected right after a deselect. The real fix, if not
    // already set, is "Should Hide Mobile Input" checked on guildNameInput's
    // TMP_InputField component in the Inspector — that avoids the
    // native-overlay keyboard mode where this Canceled-revert quirk lives
    // in the first place; this is a backstop in case that alone doesn't
    // fully cover every device/OS version.
    private string lastNonEmptyGuildName = "";
    private Coroutine nameRevertGuardCoroutine;

    void Awake()
    {
        Instance = this;

        if (guildNameInput != null)
        {
            guildNameInput.characterLimit = maxNameLength;
            // Force Standard content type at runtime regardless of what's
            // set in the Inspector — Standard is the one TMP content
            // type guaranteed to allow spaces while still respecting
            // maxNameLength above (some presets, like Name, silently strip
            // or reshape spaces).
            guildNameInput.contentType = TMP_InputField.ContentType.Standard;
            guildNameInput.onValueChanged.AddListener(UpdateNamePreview);
            guildNameInput.onValueChanged.AddListener(_ => RefreshCreateButtonInteractable());
            guildNameInput.onValueChanged.AddListener(CacheNonEmptyGuildName);
            guildNameInput.onDeselect.AddListener(OnGuildNameFieldDeselected);
        }

        if (backgroundTapButton != null) backgroundTapButton.onClick.AddListener(OnBackgroundTapped);

        if (backButton != null) backButton.onClick.AddListener(Close);
        if (iconSelectorButton != null) iconSelectorButton.onClick.AddListener(CycleShape);
        if (colorSelectorButton != null) colorSelectorButton.onClick.AddListener(CycleColor);
        if (createGuildButton != null) createGuildButton.onClick.AddListener(OnCreatePressed);

        if (tintButtons != null)
        {
            for (int i = 0; i < tintButtons.Length; i++)
            {
                int capturedIndex = i; // avoid the classic closure-over-loop-variable bug
                if (tintButtons[i] != null)
                    tintButtons[i].onClick.AddListener(() => SelectTint(capturedIndex));
            }
        }

        if (panelObject != null) panelObject.SetActive(false);
        if (panelRect != null) panelRect.anchoredPosition = closedPosition;
    }

    // Wire this directly to your "Create Guild" button on
    // GuildStatus_NoGuild's OnClick() — no extra script needed there.
    public void Open()
    {
        shapeIndex = 0;
        colorIndex = 0;
        selectedTintIndex = 0; // white/no-tint by default
        lastNonEmptyGuildName = ""; // fresh session — don't carry over a stale cached name
        if (guildNameInput != null) guildNameInput.text = "";
        if (guildNamePreviewText != null) guildNamePreviewText.text = "";
        // Clear any leftover "name not allowed" (or other) error from a
        // previous guild-creation attempt — otherwise it stays on screen
        // across Close()/Open() (e.g. leaving a guild and creating a new
        // one) even though it no longer applies to anything the player's
        // currently typing.
        if (errorText != null) errorText.gameObject.SetActive(false);
        UpdatePreview();
        RefreshCreateButtonInteractable();

        if (panelObject != null) panelObject.SetActive(true);
        foreach (GameObject obj in objectsToHideWhileOpen)
            if (obj != null) obj.SetActive(false);
        IsOpen = true;

        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        slideCoroutine = StartCoroutine(SlideTo(openPosition));
    }

    public void Close()
    {
        IsOpen = false;

        if (LeaderboardTabController.guildsActive)
        {
            foreach (GameObject obj in objectsToHideWhileOpen)
                if (obj != null) obj.SetActive(true);

            GuildStatusController.Instance?.Refresh();
        }

        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        slideCoroutine = StartCoroutine(SlideToThenDeactivate(closedPosition));
    }

    // Called when switching tabs, or closing the whole leaderboard panel,
    // while this panel happens to be open — snaps it shut immediately
    // instead of playing the slide animation. Safe to call even if the
    // panel isn't currently open (no-op).
    public void CloseInstant()
    {
        if (!IsOpen) return;

        if (slideCoroutine != null) StopCoroutine(slideCoroutine);

        if (panelRect != null) panelRect.anchoredPosition = closedPosition;
        if (panelObject != null) panelObject.SetActive(false);

        IsOpen = false;
    }

    void OnEnable()
    {
        // Carrots can change while this panel is sitting open (passive
        // income, ad rewards, etc.) — keep the button's affordability
        // check honest the whole time it's visible, not just at the
        // moment it was opened.
        if (refreshInteractableCoroutine != null) StopCoroutine(refreshInteractableCoroutine);
        refreshInteractableCoroutine = StartCoroutine(RefreshInteractableLoop());
    }

    void OnDisable()
    {
        if (refreshInteractableCoroutine != null) { StopCoroutine(refreshInteractableCoroutine); refreshInteractableCoroutine = null; }
    }

    private Coroutine refreshInteractableCoroutine;
    IEnumerator RefreshInteractableLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.5f);
            if (IsOpen) RefreshCreateButtonInteractable();
        }
    }

    // Grey out / disable Create Guild whenever the name is empty OR the
    // player can't afford createGuildCost — checked live, not just once
    // on open, so it stays correct as the player types or as carrots
    // change (e.g. an ad reward lands while this panel is open).
    void RefreshCreateButtonInteractable()
    {
        if (createGuildButton == null) return;

        bool hasName = guildNameInput != null && !string.IsNullOrWhiteSpace(guildNameInput.text);
        bool canAfford = GameManager.Instance != null && GameManager.Instance.totalCarrots >= createGuildCost;

        createGuildButton.interactable = hasName && canAfford;
    }

    void ShowError(string message)
    {
        if (errorText == null) return;
        errorText.text = message;
        errorText.gameObject.SetActive(true);
    }

    void UpdateNamePreview(string currentText)
    {
        if (guildNamePreviewText != null) guildNamePreviewText.text = currentText;
    }

    // Wired to backgroundTapButton's OnClick — same role as
    // AuthenticationPopUp.OnDimOverlayTapped: dismiss the mobile keyboard
    // when the player taps the empty area around guildNameInput, without
    // closing this panel.
    void OnBackgroundTapped()
    {
        if (guildNameInput != null)
        {
            guildNameInput.DeactivateInputField(true);
            guildNameInput.ReleaseSelection();
        }

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    void CacheNonEmptyGuildName(string value)
    {
        if (!string.IsNullOrEmpty(value))
            lastNonEmptyGuildName = value;
    }

    // Wired to guildNameInput.onDeselect — fires the moment the field loses
    // focus (tapping away OR pressing Return/Done). See the field comments
    // above for why a short delayed check is needed rather than checking
    // immediately: the "revert to pre-typing text" quirk this guards
    // against happens in TMP_InputField's own LateUpdate, which runs AFTER
    // this callback, not before.
    void OnGuildNameFieldDeselected(string value)
    {
        if (nameRevertGuardCoroutine != null) StopCoroutine(nameRevertGuardCoroutine);
        nameRevertGuardCoroutine = StartCoroutine(RestoreGuildNameIfReverted());
    }

    IEnumerator RestoreGuildNameIfReverted()
    {
        yield return null;
        yield return null;

        if (guildNameInput == null) yield break;

        if (string.IsNullOrEmpty(guildNameInput.text) && !string.IsNullOrEmpty(lastNonEmptyGuildName))
        {
            guildNameInput.text = lastNonEmptyGuildName;
            UpdateNamePreview(lastNonEmptyGuildName);
            RefreshCreateButtonInteractable();
        }
    }

    void CycleShape()
    {
        if (shapeSprites == null || shapeSprites.Length == 0) return;
        shapeIndex = (shapeIndex + 1) % shapeSprites.Length;
        UpdatePreview();
    }

    void CycleColor()
    {
        if (backgroundSprites == null || backgroundSprites.Length == 0) return;
        colorIndex = (colorIndex + 1) % backgroundSprites.Length;
        UpdatePreview();
    }

    // Direct selection (not cycled) — called by whichever of the 8 tint
    // buttons was pressed.
    void SelectTint(int index)
    {
        if (tintColors == null || index < 0 || index >= tintColors.Length) return;
        selectedTintIndex = index;
        UpdatePreview();
    }

    void UpdatePreview()
    {
        if (shapeSprites != null && shapeIndex >= 0 && shapeIndex < shapeSprites.Length)
        {
            Sprite shapeSprite = shapeSprites[shapeIndex];
            Color shapeTint = (tintColors != null && selectedTintIndex >= 0 && selectedTintIndex < tintColors.Length)
                ? tintColors[selectedTintIndex]
                : Color.white;

            if (guildLogoPreview != null)
            {
                guildLogoPreview.sprite = shapeSprite;
                guildLogoPreview.color = shapeTint;
            }

            // Icon selector's own image mirrors guildLogoPreview exactly —
            // same sprite, same tint — so the selector itself always shows
            // whatever icon is currently chosen, just like the big preview.
            if (iconSelectorPreview != null)
            {
                iconSelectorPreview.sprite = shapeSprite;
                iconSelectorPreview.color = shapeTint;
            }
        }

        if (backgroundSprites != null && colorIndex >= 0 && colorIndex < backgroundSprites.Length)
        {
            Sprite backgroundSprite = backgroundSprites[colorIndex];

            if (guildLogoBackgroundPreview != null)
            {
                guildLogoBackgroundPreview.sprite = backgroundSprite;
                guildLogoBackgroundPreview.color = Color.white; // background is never tinted, only the icon is
            }

            // Color selector's own image mirrors guildLogoBackgroundPreview
            // exactly, same as iconSelectorPreview does for the icon side.
            if (colorSelectorPreview != null)
            {
                colorSelectorPreview.sprite = backgroundSprite;
                colorSelectorPreview.color = Color.white;
            }
        }
    }

    async void OnCreatePressed()
    {
        // Disabled as the literal first action, before any other check —
        // closes the window a rapid double/triple tap could otherwise slip
        // through in (the real, authoritative guard against duplicate guild
        // creation now lives in GuildManager.CreateGuild itself via
        // _createGuildInFlight, but disabling the button immediately here
        // too means a mashed tap never even gets this far to begin with).
        if (createGuildButton != null) createGuildButton.interactable = false;

        if (GuildManager.Instance == null)
        {
            RefreshCreateButtonInteractable();
            return;
        }

        string guildName = guildNameInput != null ? guildNameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(guildName))
        {
            Debug.LogWarning("GuildCreationPanelUI: guild name can't be empty.");
            RefreshCreateButtonInteractable(); // undo the disable above — this early-return isn't an actual creation attempt
            return;
        }

        if (ProfanityFilter.ContainsBlockedWord(guildName))
        {
            Debug.LogWarning("GuildCreationPanelUI: guild name blocked by profanity filter.");
            ShowError("That guild name isn't allowed.");
            RefreshCreateButtonInteractable(); // undo the disable above — this early-return isn't an actual creation attempt
            return;
        }

        // Final live check right before spending/creating — the button's
        // interactable state only refreshes every 0.5s (RefreshInteractableLoop),
        // so this closes that small window instead of trusting stale UI state.
        if (GameManager.Instance == null || GameManager.Instance.totalCarrots < createGuildCost)
        {
            Debug.LogWarning("GuildCreationPanelUI: not enough carrots to create a guild.");
            RefreshCreateButtonInteractable();
            return;
        }

        string shapeName = ((GuildManager.GuildLogoShape)shapeIndex).ToString();
        string colorHex = ((GuildManager.GuildLogoBackground)colorIndex).ToString(); // field name kept for compatibility, actually a background enum name now
        string iconTintHex = "#" + ColorUtility.ToHtmlStringRGB(
            (tintColors != null && selectedTintIndex >= 0 && selectedTintIndex < tintColors.Length) ? tintColors[selectedTintIndex] : Color.white
        );

        // Optimistic: close the panel and disable the button immediately
        // on press, instead of leaving the player staring at it for
        // however long the Cloud Function's cold start takes. Nothing is
        // spent yet — carrots are only taken below, once creation actually
        // succeeds.
        if (createGuildButton != null) createGuildButton.interactable = false;
        Close();

        var guild = await GuildManager.Instance.CreateGuild(guildName, shapeName, colorHex, iconTintHex);
        if (guild != null)
        {
            // Only spend on actual success — a failed creation (name
            // taken, network error, etc.) shouldn't cost the player
            // anything.
            GameManager.Instance.SpendCarrots(createGuildCost);

            guildsListUI?.AddOrUpdateGuild(guild);

            // GuildManager.CreateGuild already sets IsGuildLeader = true and
            // fires OnMyGuildChanged — GuildStatusController picks that up
            // automatically and switches to GuildStatus_GuildMember.
        }
        else
        {
            // Rollback: creation failed server-side. Nothing was spent, so
            // just reopen the panel WITHOUT resetting the name/logo/tint
            // the player already picked, so they can just hit Create again.
            Debug.LogWarning("GuildCreationPanelUI: guild creation failed — no carrots were spent.");
            ReopenAfterFailedCreate();
        }
    }

    // Same as Open(), minus the reset of name/shape/color/tint — used only
    // to roll back a failed optimistic create, so the player doesn't lose
    // what they'd already typed/picked.
    void ReopenAfterFailedCreate()
    {
        if (panelObject != null) panelObject.SetActive(true);
        foreach (GameObject obj in objectsToHideWhileOpen)
            if (obj != null) obj.SetActive(false);
        IsOpen = true;
        RefreshCreateButtonInteractable();

        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        slideCoroutine = StartCoroutine(SlideTo(openPosition));
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
            t = t * t * (3f - 2f * t); // smoothstep, matches other panels' ease
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
