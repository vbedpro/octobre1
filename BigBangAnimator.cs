using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class BigBangAnimator : MonoBehaviour
{
    [Header("References")]
    public RectTransform bigBangButton;   // the circular button
    public Image bigBangButtonImage;      // image on the button to morph color/shape
    public RectTransform popupReady;
    public RectTransform popupNotReady;

    [Header("Morph Settings")]
    public float morphDuration = 0.3f;

    // Saved original button state so we can restore it on close.
    private Vector2 buttonOriginalSize;
    private Vector2 buttonOriginalPos;
    private Vector2 buttonOriginalPivot;
    private Vector2 buttonOriginalAnchorMin;
    private Vector2 buttonOriginalAnchorMax;

    private Coroutine currentMorph;
    private RectTransform activePopup;
    private bool isOpen = false;

    void Start()
    {
        // Save button's original state.
        buttonOriginalSize      = bigBangButton.sizeDelta;
        buttonOriginalPos       = bigBangButton.anchoredPosition;
        buttonOriginalPivot     = bigBangButton.pivot;
        buttonOriginalAnchorMin = bigBangButton.anchorMin;
        buttonOriginalAnchorMax = bigBangButton.anchorMax;

        if (popupReady    != null) popupReady.gameObject.SetActive(false);
        if (popupNotReady != null) popupNotReady.gameObject.SetActive(false);
    }

    public void OpenReady()    => Open(popupReady);
    public void OpenNotReady() => Open(popupNotReady);

    void Open(RectTransform target)
    {
        if (currentMorph != null) StopCoroutine(currentMorph);
        currentMorph = StartCoroutine(MorphOpen(target));
    }

    public void Close()
    {
        if (!isOpen) return;
        if (currentMorph != null) StopCoroutine(currentMorph);
        currentMorph = StartCoroutine(MorphClose());
    }

    IEnumerator MorphOpen(RectTransform targetPopup)
    {
        isOpen = true;

        // Hide both popups — button does all the visual work during morph.
        if (popupReady    != null) popupReady.gameObject.SetActive(false);
        if (popupNotReady != null) popupNotReady.gameObject.SetActive(false);
        activePopup = targetPopup;

        // Match the popup's anchor/pivot so we can interpolate to its position.
        // We temporarily move the button to match popup's anchor system.
        Vector2 startSize = bigBangButton.sizeDelta;
        Vector2 startPos  = bigBangButton.anchoredPosition;

        // Get target popup's layout values.
        Vector2 endSize = targetPopup.sizeDelta;
        Vector2 endPos  = targetPopup.anchoredPosition;

        // Get or add CanvasGroup on popup for content fade-in.
        CanvasGroup cg = targetPopup.GetComponent<CanvasGroup>();
        if (cg == null) cg = targetPopup.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        // Animate the BUTTON stretching into popup shape.
        float elapsed = 0f;
        while (elapsed < morphDuration)
        {
            elapsed += Time.deltaTime;
            float t    = Mathf.Clamp01(elapsed / morphDuration);
            // Elastic-like ease: fast start, slight overshoot, settle.
            float ease = EaseOutBack(t);

            bigBangButton.sizeDelta        = Vector2.Lerp(startSize, endSize, ease);
            bigBangButton.anchoredPosition = Vector2.Lerp(startPos,  endPos,  ease);

            yield return null;
        }

        bigBangButton.sizeDelta        = endSize;
        bigBangButton.anchoredPosition = endPos;

        // Snap: show real popup on top of the now-matching button shape,
        // and fade it in over a very short time.
        targetPopup.gameObject.SetActive(true);
        cg.alpha = 0f;

        float fadeElapsed = 0f;
        float fadeDuration = 0.08f;
        while (fadeElapsed < fadeDuration)
        {
            fadeElapsed += Time.deltaTime;
            cg.alpha = Mathf.Clamp01(fadeElapsed / fadeDuration);
            yield return null;
        }
        cg.alpha = 1f;
    }

    IEnumerator MorphClose()
    {
        // Fade out popup content first.
        if (activePopup != null)
        {
            CanvasGroup cg = activePopup.GetComponent<CanvasGroup>();
            if (cg == null) cg = activePopup.gameObject.AddComponent<CanvasGroup>();

            float fadeElapsed = 0f;
            float fadeDuration = 0.06f;
            while (fadeElapsed < fadeDuration)
            {
                fadeElapsed += Time.deltaTime;
                cg.alpha = Mathf.Lerp(1f, 0f, fadeElapsed / fadeDuration);
                yield return null;
            }
            activePopup.gameObject.SetActive(false);
            cg.alpha = 1f;
        }

        // Now morph button back to its original shape.
        Vector2 startSize = bigBangButton.sizeDelta;
        Vector2 startPos  = bigBangButton.anchoredPosition;

        float elapsed = 0f;
        while (elapsed < morphDuration)
        {
            elapsed += Time.deltaTime;
            float t    = Mathf.Clamp01(elapsed / morphDuration);
            float ease = EaseOutBack(t);

            bigBangButton.sizeDelta        = Vector2.Lerp(startSize, buttonOriginalSize, ease);
            bigBangButton.anchoredPosition = Vector2.Lerp(startPos,  buttonOriginalPos,  ease);

            yield return null;
        }

        bigBangButton.sizeDelta        = buttonOriginalSize;
        bigBangButton.anchoredPosition = buttonOriginalPos;
        isOpen = false;
    }

    // Ease out with a slight elastic overshoot — gives the liquid feel.
    float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
