using UnityEngine;
using UnityEngine.UI;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
#endif

// Shows and continuously loops a PNG sprite-sequence animation for as
// long as the Multi-Timeline BigBangs boost is active — hidden by default,
// starts looping the instant the boost activates, stops and hides again
// the instant it expires. Unlike BigBangIconAnimator (which flashes the
// animation occasionally while idle-available, then reverts to a static
// icon), this one has no static icon at all: it's either fully hidden, or
// fully animating, matching "invisible by default, appears while the
// boost runs" exactly.
public class BoostPngSequenceIcon : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Image whose sprite gets swapped frame-by-frame, and whose visibility this script controls.")]
    public Image icon;
    [Tooltip("The animation's individual frames, in playback order.")]
    public Sprite[] animationFrames;

    [Header("Boost")]
    public string itemId = "";

#if UNITY_EDITOR
    [Header("Auto-Populate (Editor only)")]
    [Tooltip("Drag the FOLDER containing all your frame sprites here, then right-click this component's header and choose 'Auto-Populate Frames From Folder' — loads and naturally sorts every sprite inside (frame_2 correctly comes before frame_10, regardless of zero-padding).")]
    public UnityEngine.Object framesFolder;
#endif

    [Header("Timing")]
    [Tooltip("How fast the frame sequence plays, in frames per second.")]
    public float framesPerSecond = 12f;

    private Coroutine loopCoroutine;
    private bool wasActive = false;

    void Start()
    {
        // IMPORTANT: hides via Image.enabled, NOT GameObject.SetActive().
        // This script lives on the same GameObject as the icon — disabling
        // the whole GameObject would also stop THIS script's own Update()
        // from ever running again, permanently freezing it right after the
        // first hide. Disabling just the Image component leaves every
        // other component (including this one) running normally.
        if (icon != null) icon.enabled = false;
    }

    void Update()
    {
        bool isActive = BoostActivationManager.Instance != null && BoostActivationManager.Instance.IsActive(itemId);

        if (isActive && !wasActive)
        {
            // Just activated — show and start looping.
            if (icon != null) icon.enabled = true;
            if (loopCoroutine != null) StopCoroutine(loopCoroutine);
            loopCoroutine = StartCoroutine(AnimationLoop());
        }
        else if (!isActive && wasActive)
        {
            // Just expired — stop and hide immediately.
            if (loopCoroutine != null) { StopCoroutine(loopCoroutine); loopCoroutine = null; }
            if (icon != null) icon.enabled = false;
        }

        wasActive = isActive;
    }

    IEnumerator AnimationLoop()
    {
        if (icon == null || animationFrames == null || animationFrames.Length == 0) yield break;

        float frameDuration = 1f / Mathf.Max(1f, framesPerSecond);
        int i = 0;
        while (true)
        {
            icon.sprite = animationFrames[i];
            i = (i + 1) % animationFrames.Length;
            yield return new WaitForSeconds(frameDuration);
        }
    }

#if UNITY_EDITOR
    // Right-click this component's header in the Inspector to run this —
    // loads every Sprite inside Frames Folder and sorts them using a
    // NATURAL sort (splits filenames into text/number chunks and compares
    // numbers numerically), so "frame_2" correctly sorts before "frame_10"
    // even without zero-padding — plain alphabetical sort would put
    // "frame_10" before "frame_2" and scramble the animation order.
    [ContextMenu("Auto-Populate Frames From Folder")]
    void AutoPopulateFromFolder()
    {
        if (framesFolder == null)
        {
            Debug.LogWarning("BoostPngSequenceIcon: assign Frames Folder first, then run this again.");
            return;
        }

        string folderPath = AssetDatabase.GetAssetPath(framesFolder);
        if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
        {
            Debug.LogWarning("BoostPngSequenceIcon: Frames Folder isn't a valid folder — drag an actual Project window FOLDER into that field, not a sprite.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });
        List<Sprite> sprites = new List<Sprite>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) sprites.Add(sprite);
        }

        sprites = sprites.OrderBy(s => s.name, new NaturalSortComparer()).ToList();
        animationFrames = sprites.ToArray();

        Debug.Log($"BoostPngSequenceIcon: loaded {animationFrames.Length} frames from '{folderPath}', naturally sorted.");
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
