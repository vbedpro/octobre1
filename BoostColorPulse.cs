using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Pulses a color back and forth between its normal color and a target
// color (default yellow) in a smooth loop, for as long as the given boost
// is active — instantly reverts to the original color the moment the
// boost ends. Works on an Image, a TextMeshProUGUI, or both at once (fill
// in whichever ones apply, leave the other empty).
public class BoostColorPulse : MonoBehaviour
{
    [Tooltip("Must exactly match the boost's Item Id, e.g. 'boost_robotic_hatching'.")]
    public string itemId;

    [Header("What to pulse (fill in either or both)")]
    public Image targetImage;
    public TextMeshProUGUI targetText;

    [Tooltip("Color to pulse toward while the boost is active.")]
    public Color pulseColor = Color.yellow;

    [Tooltip("How fast the fade back-and-forth cycles, in seconds per full cycle.")]
    public float pulseSpeed = 1.5f;

    [Tooltip("Check this for boosts locked to whichever planet they were activated on (currently just Explosive Reproduction) — the pulse will only show while the player is actually standing on that same planet, pausing/resuming as they travel, even though the boost itself keeps running the whole time.")]
    public bool onlyWhileOnBoostedPlanet = false;

    private Color originalImageColor;
    private Color originalTextColor;

    void Awake()
    {
        if (targetImage != null) originalImageColor = targetImage.color;
        if (targetText != null) originalTextColor = targetText.color;
    }

    void Update()
    {
        if (string.IsNullOrEmpty(itemId)) return;

        bool active = BoostActivationManager.Instance != null && BoostActivationManager.Instance.IsActive(itemId);

        if (active && onlyWhileOnBoostedPlanet && PlanetManager.Instance != null)
        {
            PlanetManager.Planet? boostedPlanet = BoostActivationManager.Instance.GetActivePlanetFor(itemId);
            active = boostedPlanet.HasValue && boostedPlanet.Value == PlanetManager.Instance.currentPlanet;
        }

        if (active)
        {
            // Smooth 0->1->0 ping-pong using a sine wave, so it eases at
            // both ends instead of linearly snapping back and forth.
            float t = (Mathf.Sin(Time.time * (2f * Mathf.PI / pulseSpeed)) + 1f) * 0.5f;

            if (targetImage != null) targetImage.color = Color.Lerp(originalImageColor, pulseColor, t);
            if (targetText != null) targetText.color = Color.Lerp(originalTextColor, pulseColor, t);
        }
        else
        {
            if (targetImage != null && targetImage.color != originalImageColor) targetImage.color = originalImageColor;
            if (targetText != null && targetText.color != originalTextColor) targetText.color = originalTextColor;
        }
    }
}
