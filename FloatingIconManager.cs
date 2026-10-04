using UnityEngine;
using UnityEngine.UI;

public class FloatingIconManager : MonoBehaviour
{
    public static FloatingIconManager Instance;

    [Header("Prefab")]
    public GameObject floatingIconPrefab;

    [Header("Icons")]
    public Sprite rabbitIcon;
    public Sprite carrotIcon;
    public Sprite coinIcon;

    [Header("Canvas")]
    public Canvas canvas; // assign your main Canvas
    [Tooltip("Optional — if assigned, floating icons spawn here instead of directly on the Canvas. Place this object as an EARLY sibling in the Hierarchy (above Shop/Sell/Hut/Leaderboard panels) so icons render BEHIND any open UI instead of always on top.")]
    public Transform floatingIconLayer;
    [Tooltip("Renders above EVERY panel (Leaderboard included) — used specifically for the Globe gift box reward, not normal gameplay icons.")]
    public Transform topmostLayer;

    public Camera cam;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (cam == null) cam = Camera.main;
    }

    public void ShowRabbitLoss(Vector3 worldPos, long amount)
    {
        Spawn(worldPos, rabbitIcon, "- " + GameManager.FormatFullNumber(amount), Color.red);
    }

    public void ShowCarrotGain(Vector3 worldPos, long amount = 1)
    {
        Spawn(worldPos, carrotIcon, amount.ToString(), Color.white);
    }

    public void ShowCarrotLoss(Vector3 worldPos, long amount)
    {
        Spawn(worldPos, carrotIcon, "- " + amount, Color.red);
    }

    public void ShowCoinGain(Vector3 worldPos, long amount)
    {
        Spawn(worldPos, coinIcon, GameManager.FormatNumber(amount), Color.white);
    }

    void Spawn(Vector3 worldPos, Sprite icon, string text, Color color)
    {
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
        SpawnAtScreenPos(screenPos, icon, text, color, realWorldPos: worldPos);
    }

    // For effects triggered from a DIFFERENT camera/scene than the main
    // farm camera (e.g. a tap on the Globe, which is rendered by its own
    // camera into a RenderTexture) — the tap's screen position is already
    // correct in main-canvas space, so skip the WorldToScreenPoint
    // conversion entirely rather than feeding it the wrong camera.
    // Always renders on the topmost layer (above every panel, including
    // the Leaderboard) — unlike the normal gameplay floating icons, this
    // one needs to be visible no matter what's currently open.
    public void ShowCarrotGainAtScreenPos(Vector2 screenPos, long amount = 1)
    {
        SpawnAtScreenPos(screenPos, carrotIcon, amount.ToString(), Color.black, forceTopmost: true);
    }

    void SpawnAtScreenPos(Vector3 screenPos, Sprite icon, string text, Color color, bool forceTopmost = false, Vector3? realWorldPos = null)
    {
        if (floatingIconPrefab == null) return;

        // Spawn under the dedicated low-order layer if assigned — this is
        // what keeps floating icons visually BEHIND any currently-open UI
        // panel, since Unity draws later Hierarchy siblings on top, and
        // this layer sits earlier than Shop/Sell/Hut/Leaderboard panels.
        // Falls back to the Canvas directly if not set up yet.
        // forceTopmost bypasses this entirely — used for the Globe gift
        // box reward, which needs to render above every panel instead.
        Transform parent;
        if (forceTopmost && topmostLayer != null)
            parent = topmostLayer;
        else
            parent = floatingIconLayer != null ? floatingIconLayer : canvas.transform;

        GameObject go = Instantiate(floatingIconPrefab, parent);
        FloatingIcon fi = go.GetComponent<FloatingIcon>();
        fi.icon.sprite = icon;
        fi.label.text  = text;
        fi.label.color = color;
        fi.cam         = cam;
        go.GetComponent<RectTransform>().position = screenPos;

        if (forceTopmost)
            fi.SetScreenSpacePos(screenPos); // no real 3D world position to anchor to on the Globe
        else if (realWorldPos.HasValue)
            fi.SetWorldPos(realWorldPos.Value); // the ACTUAL world position (garage, hut, fox, etc.) — not a reconstructed synthetic one
    }
}
