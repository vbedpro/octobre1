using UnityEngine;
using UnityEngine.UI;
using TMPro;

[ExecuteAlways]
public class DynamicCounter : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI counterText;
    public RectTransform iconRect;

    [Header("Settings")]
    public float iconSpacing = 10f;

    private RectTransform textRect;

    void Start()
    {
        if (counterText != null)
            textRect = counterText.GetComponent<RectTransform>();
    }

    void LateUpdate()
    {
        if (counterText == null || iconRect == null) return;
        if (textRect == null) textRect = counterText.GetComponent<RectTransform>();

        counterText.ForceMeshUpdate();

        float textWidth = counterText.preferredWidth;
        float iconWidth = iconRect.sizeDelta.x;
        float totalWidth = iconWidth + iconSpacing + textWidth;
        float startX = -totalWidth / 2f;

        textRect.sizeDelta = new Vector2(textWidth, textRect.sizeDelta.y);

        iconRect.anchoredPosition = new Vector2(
            startX + iconWidth / 2f,
            iconRect.anchoredPosition.y
        );

        textRect.anchoredPosition = new Vector2(
            startX + iconWidth + iconSpacing + textWidth / 2f,
            textRect.anchoredPosition.y
        );
    }
}