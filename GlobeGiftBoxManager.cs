using UnityEngine;
using System;

// Every intervalSeconds (default 3 real-life minutes), a box icon spawns
// on the globe above a random country. It stays there — persisting
// through BigBangs, app close, backgrounding, anything — until tapped.
// Tapping it grants a flat 15 carrots, shows the same floating carrot
// icon used elsewhere, removes the box, and restarts the interval.
//
// Timing is real wall-clock time (epoch-based), same pattern as boosts —
// survives the app being fully closed. Since nothing in PrestigeManager
// touches this manager, it automatically survives BigBang too, with zero
// extra wiring needed there.
public class GlobeGiftBoxManager : MonoBehaviour
{
    public static GlobeGiftBoxManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("Used to read globeSurface/globeRadius/dotSurfaceOffset/invertLatitude so the box lines up exactly like a player dot would.")]
    public GlobeDotSpawner dotSpawner;
    public GlobeGiftBoxIcon boxIconPrefab;

    [Header("Visual")]
    [Tooltip("Extra distance ABOVE the normal dot placement radius, in world units — pushes the box clearly above the actual rendered globe surface (land geometry is opaque and will hide it otherwise). Increase this if the box still gets hidden behind land.")]
    public float extraSurfaceOffset = 0.5f;

    [Header("Timing")]
    public float intervalSeconds = 180f; // 3 minutes

    [Header("Reward")]
    public long carrotReward = 15;

    // A representative spread of countries to spawn above — reuses the
    // same style of list GlobeDotSpawner's test tools already use.
    private static readonly (string country, float lat, float lon)[] countries = new[]
    {
        ("US", 39.8283f, -98.5795f),
        ("CA", 56.1304f, -106.3468f),
        ("MX", 23.6345f, -102.5528f),
        ("BR", -14.2350f, -51.9253f),
        ("AR", -38.4161f, -63.6167f),
        ("GB", 55.3781f, -3.4360f),
        ("FR", 46.2276f, 2.2137f),
        ("DE", 51.1657f, 10.4515f),
        ("ES", 40.4637f, -3.7492f),
        ("IT", 41.8719f, 12.5674f),
        ("NL", 52.1326f, 5.2913f),
        ("RU", 61.5240f, 105.3188f),
        ("TR", 38.9637f, 35.2433f),
        ("EG", 26.8206f, 30.8025f),
        ("NG", 9.0820f, 8.6753f),
        ("ZA", -30.5595f, 22.9375f),
        ("AE", 23.4241f, 53.8478f),
        ("IN", 20.5937f, 78.9629f),
        ("TH", 15.8700f, 100.9925f),
        ("SG", 1.3521f, 103.8198f),
        ("ID", -0.7893f, 113.9213f),
        ("JP", 36.2048f, 138.2529f),
        ("KR", 35.9078f, 127.7669f),
        ("CN", 35.8617f, 104.1954f),
        ("AU", -25.2744f, 133.7751f),
    };

    private bool isSpawned = false;
    private string spawnedCountry;
    private float spawnedLat, spawnedLon;
    private long nextSpawnEpoch = 0;
    private bool hasBeenRestored = false;

    private GlobeGiftBoxIcon currentIconInstance;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // NOTE: actual restore + icon spawning happens in
        // RestoreSaveData() below, not here — SaveManager's load sequence
        // can call that either before or after this Start() runs
        // (depends on frame timing), so this Start() must not assume
        // restored data has already arrived. If RestoreSaveData() is
        // NEVER called at all (e.g. this manager wasn't wired into
        // SaveManager yet), fall back to starting a fresh interval so the
        // system still works rather than staying permanently stuck at 0.
        if (nextSpawnEpoch == 0 && !isSpawned && !hasBeenRestored)
            nextSpawnEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)intervalSeconds;
    }

    void Update()
    {
        if (isSpawned) return;

        long nowEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (nowEpoch < nextSpawnEpoch) return;

        Debug.Log("[GiftBox DEBUG] Interval elapsed, attempting to spawn...");
        PickNewLocationAndSpawn();
    }

    void PickNewLocationAndSpawn()
    {
        var pick = countries[UnityEngine.Random.Range(0, countries.Length)];
        spawnedCountry = pick.country;
        spawnedLat = pick.lat;
        spawnedLon = pick.lon;
        isSpawned = true;

        SpawnIconAt(spawnedCountry, spawnedLat, spawnedLon);
    }

    void SpawnIconAt(string country, float lat, float lon)
    {
        if (dotSpawner == null) { Debug.LogWarning("[GiftBox DEBUG] dotSpawner is NULL — assign it in the Inspector."); return; }
        if (boxIconPrefab == null) { Debug.LogWarning("[GiftBox DEBUG] boxIconPrefab is NULL — assign it in the Inspector."); return; }
        if (dotSpawner.globeSurface == null) { Debug.LogWarning("[GiftBox DEBUG] dotSpawner.globeSurface is NULL — assign it on GlobeDotSpawner itself."); return; }

        float placementRadius = (dotSpawner.globeRadius * dotSpawner.dotSurfaceOffset) + extraSurfaceOffset;
        float effectiveLat = dotSpawner.invertLatitude ? -lat : lat;
        Vector3 localPos = GlobeDotSpawner.LatLongToLocalPosition(effectiveLat, lon, placementRadius);

        GlobeGiftBoxIcon icon = Instantiate(boxIconPrefab, dotSpawner.globeSurface);
        icon.transform.localPosition = localPos;

        // Lie FLAT on the sphere surface like a decal — the sprite's
        // forward axis points straight out along the radial direction
        // from the sphere's center, regardless of where on the globe it
        // spawned. This is NOT a camera-facing billboard (that would tilt
        // as the globe rotates); it's a fixed orientation relative to the
        // globe itself, exactly like GlobeDot's own dots.
        Vector3 outwardNormal = localPos.normalized;
        icon.transform.localRotation = Quaternion.LookRotation(-outwardNormal, Vector3.up);

        currentIconInstance = icon;

        Debug.Log($"[GiftBox DEBUG] Spawned at {country} (lat={lat}, lon={lon}) -> localPos={localPos}, placementRadius={placementRadius}");
    }

    // Called by GlobeUIDragHandler when a raycast hits the box icon.
    // screenPos is the tap's screen-space position, used to place the
    // floating carrot reward correctly (the globe has its own camera —
    // see FloatingIconManager.ShowCarrotGainAtScreenPos for why this
    // can't just reuse the box's 3D globe-local position).
    public void OnBoxTapped(Vector2 screenPos)
    {
        if (!isSpawned) return;

        GameManager.Instance?.AddCarrots(carrotReward);
        FloatingIconManager.Instance?.ShowCarrotGainAtScreenPos(screenPos, carrotReward);
        AudioManager.Instance?.PlaySellRabbitsClicked();

        if (currentIconInstance != null)
        {
            Destroy(currentIconInstance.gameObject);
            currentIconInstance = null;
        }

        isSpawned = false;
        nextSpawnEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)intervalSeconds;
    }

    // ── Save/Load ──────────────────────────────────────────────────

    public bool GetSaveIsSpawned() => isSpawned;
    public string GetSaveCountry() => spawnedCountry;
    public float GetSaveLat() => spawnedLat;
    public float GetSaveLon() => spawnedLon;
    public long GetSaveNextSpawnEpoch() => nextSpawnEpoch;

    public void RestoreSaveData(bool savedIsSpawned, string savedCountry, float savedLat, float savedLon, long savedNextSpawnEpoch)
    {
        hasBeenRestored = true;
        isSpawned = savedIsSpawned;
        spawnedCountry = savedCountry;
        spawnedLat = savedLat;
        spawnedLon = savedLon;
        nextSpawnEpoch = savedNextSpawnEpoch;

        // Brand-new save (never had this system before) — start a fresh
        // interval instead of leaving nextSpawnEpoch at 0 forever.
        if (!isSpawned && nextSpawnEpoch == 0)
            nextSpawnEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)intervalSeconds;

        // Spawn the icon immediately if one was already out there when
        // the player left — don't wait for Update() to notice.
        if (isSpawned && !string.IsNullOrEmpty(spawnedCountry))
            SpawnIconAt(spawnedCountry, spawnedLat, spawnedLon);
    }
}
