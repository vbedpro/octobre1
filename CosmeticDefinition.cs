using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
#endif

// One slot per type — only one cosmetic per type can be equipped at once.
// Add new entries here as you add new cosmetic categories.
public enum CosmeticType
{
    Nickname,
    Background
}

public enum NicknameStyleType
{
    Static,
    GoldenShine,
    ColorFade,
    Rainbow
}

// One asset per cosmetic type — same pattern as BoostDefinition, but
// cosmetics have no duration/activation, just an icon + description.
// itemId MUST exactly match the cosmeticId set on the achievement that
// grants it (AchievementData.bronzeCosmeticId / silverCosmeticId / etc.).
[CreateAssetMenu(fileName = "NewCosmeticDefinition", menuName = "Rabbit Universe/Cosmetic Definition")]
public class CosmeticDefinition : ScriptableObject
{
    [Tooltip("Must exactly match the cosmeticId set on the achievement that grants this cosmetic.")]
    public string itemId;

    [Tooltip("Lower numbers appear further LEFT in the inventory panel, regardless of the order the player actually unlocked them in.")]
    public int displayOrder = 0;

    [Tooltip("Only one cosmetic of the same type can be equipped at once. Equipping another of the same type auto-unequips the previous one.")]
    public CosmeticType cosmeticType;

    public Sprite icon;
    [Tooltip("Leave empty for a static icon (just use Icon above). Fill in for an animated icon (PNG sequence) — used by the Inventory panel and anywhere else a small animated preview is shown (NOT the achievement row — see Achievement Row Icons below for that).")]
    public Sprite[] iconAnimationFrames;
    [Tooltip("Playback speed for Icon Animation Frames, in frames per second.")]
    public float iconFrameRate = 12f;
    [TextArea] public string description;

    [Header("Achievement Row Icon (locked state only — the unlocked/claimed state reuses Icon / Icon Animation Frames above, same as the Inventory panel)")]
    public Sprite achievementIconLocked;
    [Tooltip("Scales just the LOCKED icon relative to the row's normal icon size — X and Y adjustable independently (1,1 = no change, 0.5 = half size on that axis, 2 = double). Lets you compensate for Achievement Icon Locked being a different native resolution/aspect ratio than the unlocked icon. Doesn't affect the unlocked state.")]
    public Vector2 lockedIconScale = Vector2.one;

    [Header("Nickname Style (only used when Cosmetic Type = Nickname)")]
    public NicknameStyleType nicknameStyle = NicknameStyleType.Static;

    [Tooltip("Static: flat color. Golden Shine: base gold color.")]
    public Color nicknameTopColor = Color.white;
    [Tooltip("Static only: bottom vertex color (set equal to top for a flat color).")]
    public Color nicknameBottomColor = Color.white;
    [Tooltip("If set (non-empty), this Static nickname cycles through these colors each time it's re-activated (deactivated then reactivated) — first activation uses index 0, next reactivation index 1, etc, looping. Overrides Nickname Top/Bottom Color while active. Leave empty to just use Nickname Top/Bottom Color as normal, with no cycling.")]
    public Color[] cyclingColors;

    [Header("Golden Shine params")]
    [Tooltip("Bright color of the shine band that sweeps across.")]
    public Color shineColor = new Color(1f, 1f, 0.85f);
    [Tooltip("Seconds between each shine sweep.")]
    public float shineIntervalSeconds = 3f;
    [Tooltip("Seconds the sweep itself takes to cross the whole name.")]
    public float shineDurationSeconds = 0.6f;
    [Tooltip("Optional — a font material with Bevel/Lighting configured (Distance Field SSD shader) for a metallic gold-bar look. Leave empty to use the name text's normal material.")]
    public Material goldMaterial;

    [Header("Color Fade params")]
    [Tooltip("Seconds for one full loop through the fade color sequence (blue-purple-red-orange-yellow-green-blue).")]
    public float fadeLoopSeconds = 6f;

    [Header("Rainbow params")]
    [Tooltip("Seconds for the rainbow pattern to fully traverse the name once (higher = slower crawl).")]
    public float rainbowTravelSeconds = 3f;

    [Header("Background (only used when Cosmetic Type = Background)")]
    [Tooltip("Used when this background is Static (leave Animation Frames empty).")]
    public Sprite backgroundStaticSprite;
    [Tooltip("Used when this background is Animated — every frame in order. Leave empty for a Static background.")]
    public Sprite[] backgroundAnimationFrames;
    [Tooltip("Playback speed for Background Animation Frames, in frames per second.")]
    public float backgroundFrameRate = 12f;

#if UNITY_EDITOR
    [Header("Auto-Populate (Editor only)")]
    [Tooltip("Drag the FOLDER containing this cosmetic's BACKGROUND animation frame sprites here, then right-click this component's header (or the ⋮ menu in the Inspector) and choose 'Auto-Populate Background Frames From Folder' — loads and naturally sorts every sprite inside (frame_2 correctly comes before frame_10, regardless of zero-padding).")]
    public UnityEngine.Object backgroundFramesFolder;
    [Tooltip("Same idea, but for the ACHIEVEMENT ICON animation frames — right-click and choose 'Auto-Populate Icon Frames From Folder'.")]
    public UnityEngine.Object iconFramesFolder;

    // Right-click this asset's header in the Inspector (or use the ⋮ menu)
    // to run this — loads every Sprite inside Background Frames Folder and
    // sorts them using a NATURAL sort (splits filenames into text/number
    // chunks and compares numbers numerically), so "frame_2" correctly
    // sorts before "frame_10" even without zero-padding — plain
    // alphabetical sort would put "frame_10" before "frame_2" and scramble
    // the animation order.
    [ContextMenu("Auto-Populate Background Frames From Folder")]
    void AutoPopulateBackgroundFromFolder()
    {
        Sprite[] sprites = LoadSortedSpritesFromFolder(backgroundFramesFolder);
        if (sprites == null) return;

        backgroundAnimationFrames = sprites;
        Debug.Log($"CosmeticDefinition: loaded {sprites.Length} BACKGROUND frames, naturally sorted.");
    }

    // Same as above, but populates Icon Animation Frames instead.
    [ContextMenu("Auto-Populate Icon Frames From Folder")]
    void AutoPopulateIconFromFolder()
    {
        Sprite[] sprites = LoadSortedSpritesFromFolder(iconFramesFolder);
        if (sprites == null) return;

        iconAnimationFrames = sprites;
        Debug.Log($"CosmeticDefinition: loaded {sprites.Length} ICON frames, naturally sorted.");
    }

    // Shared by both auto-populate buttons above. Returns null (having
    // already logged why) if the folder field is empty/invalid.
    Sprite[] LoadSortedSpritesFromFolder(UnityEngine.Object folderRef)
    {
        if (folderRef == null)
        {
            Debug.LogWarning("CosmeticDefinition: assign the folder field first, then run this again.");
            return null;
        }

        string folderPath = AssetDatabase.GetAssetPath(folderRef);
        if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
        {
            Debug.LogWarning("CosmeticDefinition: that field isn't a valid folder — drag an actual Project window FOLDER into it, not a sprite.");
            return null;
        }

        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });
        List<Sprite> sprites = new List<Sprite>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) sprites.Add(sprite);
        }

        return sprites.OrderBy(s => s.name, new NaturalSortComparer()).ToArray();
    }

    // Splits each name into alternating text/number chunks and compares
    // number chunks as actual integers instead of character-by-character —
    // this is what makes "frame_2" sort before "frame_10".
    class NaturalSortComparer : IComparer<string>
    {
        public int Compare(string a, string b)
        {
            var aParts = Regex.Matches(a, @"\d+|\D+").Cast<Match>().Select(m => m.Value).ToArray();
            var bParts = Regex.Matches(b, @"\d+|\D+").Cast<Match>().Select(m => m.Value).ToArray();

            int len = Mathf.Min(aParts.Length, bParts.Length);
            for (int i = 0; i < len; i++)
            {
                string ap = aParts[i], bp = bParts[i];
                bool aIsNum = char.IsDigit(ap[0]);
                bool bIsNum = char.IsDigit(bp[0]);

                int cmp;
                if (aIsNum && bIsNum)
                    cmp = long.Parse(ap).CompareTo(long.Parse(bp));
                else
                    cmp = string.Compare(ap, bp, System.StringComparison.OrdinalIgnoreCase);

                if (cmp != 0) return cmp;
            }
            return aParts.Length.CompareTo(bParts.Length);
        }
    }
#endif
}
