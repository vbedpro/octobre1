using UnityEngine;

// Sits on each player-dot marker spawned onto the globe. Stores the data
// needed for the info popup and handles size scaling by lifetime rabbits.
public class GlobeDot : MonoBehaviour
{
    public string playerId;
    public string playerName;
    public string country;
    public long lifetimeRabbits;
    public int bigBangCount;
    public int rank;
    public string equippedNickname;
    public int nicknameColorIndex;
    public string equippedBackground;

    [Header("Size Range")]
    public float minScale = 0.005f;
    public float maxScale = 0.02f;

    // Size is purely a function of RANK POSITION within the currently
    // fetched list, never the actual score. rank 1 = maxScale, the last
    // entry in the fetched list (totalCount) = minScale, linear between.
    public void ApplyScaleByRank(int rank, int totalCount)
    {
        float t;
        if (totalCount <= 1) t = 1f;
        else t = 1f - (float)(rank - 1) / (totalCount - 1);
        t = Mathf.Clamp01(t);

        transform.localScale = Vector3.one * Mathf.Lerp(minScale, maxScale, t);
    }
}
