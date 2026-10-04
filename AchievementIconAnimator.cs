using UnityEngine;
using UnityEngine.UI;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
#endif

// Swaps the AchievementsButton's icon between its normal static sprite and
// a brief sprite-SEQUENCE animation — plays once, holds static, then
// repeats every replayInterval seconds, for as long as AT LEAST ONE
// achievement has a reward ready to claim. Reverts to static immediately
// once nothing is claimable, and the repeat timer resets — so it won't
// play again until a reward becomes claimable again, not on some stale
// countdown. Exact same mechanism as BigBangIconAnimator, just checking a
// different availability condition and a different default interval (5s).
public class AchievementIconAnimator : MonoBehaviour
{
    [Header("References")]
    public Image icon;
    public Sprite staticIcon;
    [Tooltip("The animation's individual frames, in playback order.")]
    public Sprite[] animationFrames;

#if UNITY_EDITOR
    [Header("Auto-Populate (Editor only)")]
    [Tooltip("Drag the FOLDER containing all your frame sprites here, then right-click this component's header and choose 'Auto-Populate Frames From Folder' — loads and naturally sorts every sprite inside (frame_2 correctly comes before frame_10, regardless of zero-padding).")]
    public UnityEngine.Object framesFolder;
#endif

    [Header("Timing")]
    [Tooltip("How fast the frame sequence plays, in frames per second.")]
    public float framesPerSecond = 12f;
    [Tooltip("How often the animation repeats while at least one reward stays claimable.")]
    public float replayInterval = 5f;

    private Coroutine loopCoroutine;
    private bool wasAvailable = false;

    void Start()
    {
        if (icon != null && staticIcon != null) icon.sprite = staticIcon;
    }

    void Update()
    {
        bool isAvailable = AnyRewardClaimable();

        if (isAvailable && !wasAvailable)
        {
            // Just became available — start the repeating animation loop.
            if (loopCoroutine != null) StopCoroutine(loopCoroutine);
            loopCoroutine = StartCoroutine(AnimationLoop());
        }
        else if (!isAvailable && wasAvailable)
        {
            // No longer available (every reward claimed) — stop
            // immediately, revert to static. Next time something becomes
            // claimable again, the loop restarts fresh (plays right
            // away), not mid-countdown.
            if (loopCoroutine != null) { StopCoroutine(loopCoroutine); loopCoroutine = null; }
            if (icon != null && staticIcon != null) icon.sprite = staticIcon;
        }

        wasAvailable = isAvailable;
    }

    bool AnyRewardClaimable()
    {
        if (AchievementsManager.Instance == null) return false;
        foreach (AchievementData a in AchievementsManager.Instance.achievements)
            if (a.IsReadyToClaim()) return true;
        return false;
    }

    IEnumerator AnimationLoop()
    {
        while (true)
        {
            yield return PlayAnimationOnce();
            yield return new WaitForSeconds(replayInterval);
        }
    }

    IEnumerator PlayAnimationOnce()
    {
        if (icon == null || animationFrames == null || animationFrames.Length == 0) yield break;

        float frameDuration = 1f / Mathf.Max(1f, framesPerSecond);
        foreach (Sprite frame in animationFrames)
        {
            icon.sprite = frame;
            yield return new WaitForSeconds(frameDuration);
        }

        if (staticIcon != null) icon.sprite = staticIcon;
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
            Debug.LogWarning("AchievementIconAnimator: assign Frames Folder first, then run this again.");
            return;
        }

        string folderPath = AssetDatabase.GetAssetPath(framesFolder);
        if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
        {
            Debug.LogWarning("AchievementIconAnimator: Frames Folder isn't a valid folder — drag an actual Project window FOLDER into that field, not a sprite.");
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

        Debug.Log($"AchievementIconAnimator: loaded {animationFrames.Length} frames from '{folderPath}', naturally sorted.");
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
