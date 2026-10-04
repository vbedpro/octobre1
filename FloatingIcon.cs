using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class FloatingIcon : MonoBehaviour
{
    [Header("References")]
    public Image icon;
    public TextMeshProUGUI label;

    [Header("Settings")]
    public float visibleDuration = 2f;
    public float fadeDuration    = 0.5f;
    public float floatSpeed      = 0.8f;

    [HideInInspector] public Camera cam;
    private Vector3 worldPos;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private bool useScreenSpaceOnly = false;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup   = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void SetWorldPos(Vector3 pos)
    {
        worldPos = pos;
        useScreenSpaceOnly = false;
        StartCoroutine(Animate());
    }

    // For effects with no real 3D world position to anchor to (e.g. a tap
    // on the Globe, rendered by its own separate camera) — moves purely
    // in screen-space pixels instead of round-tripping through
    // WorldToScreenPoint every frame. That round-trip is what caused the
    // "flies off screen instantly" bug: a synthetic world position placed
    // near the camera gets wildly exaggerated on-screen for even a tiny
    // world-space movement, due to normal perspective foreshortening.
    // Screen-space movement has no such distortion — it's just pixels.
    public void SetScreenSpacePos(Vector2 screenPos)
    {
        rectTransform.position = screenPos;
        useScreenSpaceOnly = true;
        StartCoroutine(Animate());
    }

    void Update()
    {
        if (useScreenSpaceOnly)
        {
            rectTransform.position += Vector3.up * floatSpeed * 100f * Time.deltaTime; // *100 keeps it visually comparable to the world-space version's on-screen speed
            return;
        }

        worldPos += Vector3.up * floatSpeed * Time.deltaTime;
        if (cam != null)
            rectTransform.position = cam.WorldToScreenPoint(worldPos);
    }

    IEnumerator Animate()
    {
        canvasGroup.alpha = 1f;
        yield return new WaitForSeconds(visibleDuration);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }

        Destroy(gameObject);
    }
}
