using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using Unity.Services.Authentication;

// Shown once, the very first time a new player launches the game — asks
// for a nickname (max 15 characters) before anything leaderboard-related
// can meaningfully identify them. Never shown again after a name is set
// successfully.
//
// "New player" is tracked locally via PlayerPrefs rather than by checking
// AuthenticationService.Instance.PlayerName, since UGS auto-assigns every
// anonymous player a default name (e.g. "Player-a3f9") on sign-in — that
// field is never actually empty, so it can't tell us whether the player
// has deliberately chosen a name yet.
public class AuthenticationPopUp : MonoBehaviour
{
    private const string HasSetNicknameKey = "RabbitUniverse_HasSetNickname";
    private const int MaxNameLength = 15;

    public static bool popupOpen = false;

    [Header("Panel")]
    public GameObject panel;

    [Header("Dim Overlay")]
    public CanvasGroup dimOverlay;
    public float dimAlpha = 0.6f;
    public float dimFadeDuration = 0.4f;
    private Coroutine dimCoroutine;
    [Tooltip("Button component on the SAME GameObject as dimOverlay (needs an Image/Graphic to raycast against — the dim overlay's own image works fine as the target graphic). Tapping it dismisses the on-screen keyboard by deselecting the input field, WITHOUT closing this panel or reaching anything behind it — dimOverlay already blocks raycasts to everything else while this panel is open, so this only ever intercepts taps that land on the empty dimmed area, never on the input field or Confirm button sitting in front of it.")]
    public Button dimOverlayButton;

    [Header("Input")]
    public TMP_InputField nicknameInput;
    public TextMeshProUGUI characterCountText;
    public TextMeshProUGUI errorText;

    [Header("Confirm")]
    public Button confirmButton;
    public Image confirmButtonImage;
    public Color readyColor = new Color(0.2f, 0.8f, 0.2f);
    public Color notReadyColor = new Color(0.5f, 0.5f, 0.5f);

    // Right-click-able on the component in the Inspector — clears the
    // local "has this player set a nickname" flag so you can re-test the
    // popup without it needing to match your game save's reset state.
    // This is a SEPARATE PlayerPrefs key from SaveManager's save data —
    // deleting one does not delete the other.
    [ContextMenu("Reset Nickname Flag (Testing)")]
    public void ResetNicknameFlag()
    {
        PlayerPrefs.DeleteKey(HasSetNicknameKey);
    }

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (errorText != null) errorText.gameObject.SetActive(false);
    }

    void Start()
    {
        if (nicknameInput != null)
        {
            nicknameInput.characterLimit = MaxNameLength;
            // Force Standard content type at runtime regardless of what's
            // set in the Inspector — Standard is the one TMP content type
            // guaranteed to allow spaces while still respecting
            // characterLimit above (some of the other presets, like Name,
            // silently strip or reshape spaces).
            nicknameInput.contentType = TMP_InputField.ContentType.Standard;
            nicknameInput.onValueChanged.AddListener(OnInputChanged);
        }
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmPressed);
        if (dimOverlayButton != null)
            dimOverlayButton.onClick.AddListener(OnDimOverlayTapped);

        UpdateCharacterCount();
        StartCoroutine(WaitThenCheck());
    }

    IEnumerator WaitThenCheck()
    {
        // Wait until UGS auth is actually ready — submitting a name before
        // sign-in completes would fail.
        while (LeaderboardManager.Instance == null || !LeaderboardManager.Instance.IsReady)
            yield return null;

        if (!PlayerPrefs.HasKey(HasSetNicknameKey))
            Show();
    }

    public void Show()
    {
        popupOpen = true;
        if (panel != null) panel.SetActive(true);
        if (errorText != null) errorText.gameObject.SetActive(false);
        if (nicknameInput != null)
        {
            nicknameInput.text = "";
            nicknameInput.Select();
            nicknameInput.ActivateInputField();
        }
        UpdateCharacterCount();
        UpdateConfirmButtonState();

        if (dimOverlay != null)
        {
            dimOverlay.blocksRaycasts = true;
            dimOverlay.interactable = true;
        }
        FadeDim(dimAlpha);
    }

    void OnInputChanged(string value)
    {
        UpdateCharacterCount();
        UpdateConfirmButtonState();
    }

    // Wired to dimOverlayButton's OnClick — lets the player tap away from
    // the nickname field to dismiss their phone's on-screen keyboard (which
    // can otherwise cover the Confirm button), without closing this panel
    // or letting the tap reach anything behind it. Only deselects/closes
    // the keyboard; nothing else changes.
    void OnDimOverlayTapped()
    {
        if (nicknameInput != null)
        {
            // true = also clear the text selection highlight, not just
            // focus — this is also what actually tells the native mobile
            // keyboard to close/minimize, not just DeactivateInputField()
            // with no argument.
            nicknameInput.DeactivateInputField(true);
            nicknameInput.ReleaseSelection();
        }

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    void UpdateConfirmButtonState()
    {
        bool hasInput = nicknameInput != null && !string.IsNullOrWhiteSpace(nicknameInput.text);
        if (confirmButton != null) confirmButton.interactable = hasInput;
        if (confirmButtonImage != null) confirmButtonImage.color = hasInput ? readyColor : notReadyColor;
    }

    void UpdateCharacterCount()
    {
        if (characterCountText == null) return;
        characterCountText.text = "Enter name here...";
    }

    public async void OnConfirmPressed()
    {
        if (nicknameInput == null) return;

        string name = nicknameInput.text.Trim();

        if (string.IsNullOrEmpty(name))
        {
            ShowError("Please enter a nickname.");
            return;
        }
        if (ProfanityFilter.ContainsBlockedWord(name))
        {
            ShowError("That nickname isn't allowed.");
            return;
        }
        if (name.Length > MaxNameLength)
            name = name.Substring(0, MaxNameLength);

        if (confirmButton != null) confirmButton.interactable = false;

        try
        {
            await AuthenticationService.Instance.UpdatePlayerNameAsync(name);
            PlayerPrefs.SetInt(HasSetNicknameKey, 1);
            PlayerPrefs.Save();
            popupOpen = false;
            if (panel != null) panel.SetActive(false);
            if (dimOverlay != null)
            {
                dimOverlay.blocksRaycasts = false;
                dimOverlay.interactable = false;
            }
            FadeDim(0f);

            if (TutorialManager.Instance != null)
                TutorialManager.Instance.OnAuthenticationSuccess();
        }
        catch (System.Exception e)
        {
            ShowError("Invalid nickname.");
            Debug.LogWarning("AuthenticationPopUp: UpdatePlayerNameAsync failed — " + e.Message);
        }
        finally
        {
            if (confirmButton != null) confirmButton.interactable = true;
        }
    }

    void ShowError(string message)
    {
        if (errorText == null) return;
        errorText.text = message;
        errorText.gameObject.SetActive(true);
    }

    void FadeDim(float targetAlpha)
    {
        if (dimOverlay == null) return;
        if (dimCoroutine != null) StopCoroutine(dimCoroutine);
        dimCoroutine = StartCoroutine(FadeDimRoutine(targetAlpha));
    }

    IEnumerator FadeDimRoutine(float targetAlpha)
    {
        float startAlpha = dimOverlay.alpha;
        float elapsed = 0f;
        while (elapsed < dimFadeDuration)
        {
            elapsed += Time.deltaTime;
            dimOverlay.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / dimFadeDuration));
            yield return null;
        }
        dimOverlay.alpha = targetAlpha;
    }
}
