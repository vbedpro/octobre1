using UnityEngine;
using System.Collections;

[RequireComponent(typeof(RectTransform))]
public class GaugeBar : MonoBehaviour
{
    [Tooltip("The green fill bar. Its anchor + pivot must be on the LEFT.")]
    public RectTransform fill;

    [Tooltip("Width at 100%. Leave 0 to use this object's own width.")]
    public float fullWidth = 0f;

    public float animDuration = 0.3f;

    private Coroutine anim;

    private float FullWidth => fullWidth > 0f ? fullWidth : ((RectTransform)transform).rect.width;

    // Snap immediately (used when opening the popup).
    public void SetFillInstant(float fraction)
    {
        fraction = Mathf.Clamp01(fraction);
        if (anim != null) { StopCoroutine(anim); anim = null; }
        if (fill != null)
            fill.sizeDelta = new Vector2(FullWidth * fraction, fill.sizeDelta.y);
    }

    // Animate the grow (used when a hut is purchased).
    public void SetFill(float fraction)
    {
        fraction = Mathf.Clamp01(fraction);
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(Animate(fraction));
    }

    private IEnumerator Animate(float target)
    {
        float startW = fill != null ? fill.sizeDelta.x : 0f;
        float endW = FullWidth * target;
        float t = 0f;
        while (t < animDuration)
        {
            t += Time.deltaTime;
            float p = t / animDuration;
            p = p * p * (3f - 2f * p);   // ease in-out
            if (fill != null)
                fill.sizeDelta = new Vector2(Mathf.Lerp(startW, endW, p), fill.sizeDelta.y);
            yield return null;
        }
        if (fill != null) fill.sizeDelta = new Vector2(endW, fill.sizeDelta.y);
        anim = null;
    }
}
