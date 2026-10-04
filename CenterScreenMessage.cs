using UnityEngine;
using TMPro;
using System.Collections;

// Small reusable "toast" — a line of white text that fades in centered on
// screen, holds for a moment, then fades out. Used right now for the
// Wi-Fi-blocked buttons ("Please connect to Wi-Fi"), but ShowMessage() is
// generic — anything else that wants a brief centered callout can reuse
// this same instance instead of building its own fade logic.
//
// EDITOR SETUP: put this on a GameObject with a CanvasGroup, sitting on
// your top-most UI canvas (so it renders above every panel), containing a
// single TextMeshProUGUI child. Assign both fields below. Start the
// CanvasGroup's alpha at 0 / inactive — this script controls visibility
// entirely at runtime.
public class CenterScreenMessage : MonoBehaviour
{
    public static CenterScreenMessage Instance { get; private set; }

    [Header("UI")]
    public CanvasGroup canvasGroup;
    public TextMeshProUGUI messageText;

    [Header("Timing")]
    public float fadeInDuration = 0.2f;
    public float holdDuration = 1.5f;
    public float fadeOutDuration = 0.5f;

    private Coroutine activeCoroutine;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    // Call from anywhere: CenterScreenMessage.Instance?.ShowMessage("...").
    // Safe to call again while a message is already showing — it just
    // restarts the fade with the new text instead of stacking/queuing.
    public void ShowMessage(string text)
    {
        if (canvasGroup == null || messageText == null) return;

        messageText.text = text;

        if (activeCoroutine != null) StopCoroutine(activeCoroutine);
        activeCoroutine = StartCoroutine(FadeRoutine());
    }

    IEnumerator FadeRoutine()
    {
        yield return Fade(0f, 1f, fadeInDuration);
        yield return new WaitForSeconds(holdDuration);
        yield return Fade(1f, 0f, fadeOutDuration);
        activeCoroutine = null;
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;
        canvasGroup.alpha = from;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        canvasGroup.alpha = to;
    }
}
