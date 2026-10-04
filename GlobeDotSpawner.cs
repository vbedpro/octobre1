using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Spawns player dots onto the globe's surface, positioned by their real
// IP-derived lat/long (see LocationService), with country flags looked up
// via CountryFlagDatabase.
//
// Dot SIZE is based purely on RANK POSITION within the currently fetched
// list (rank 1 = biggest, the last entry actually returned = smallest —
// linear in between), never on the actual score value. If the real
// leaderboard has fewer than 999 players, the "smallest" dot is simply
// whatever the last real entry is, not a hardcoded rank 999.
//
// Dots are NEVER hidden/shown based on zoom or anything else — nothing
// here ever calls SetActive(false) on a spawned dot.
//
// Decluttering: recalculated FRESH every refresh (not baked once) — if
// two players' true locations would place their dots too close together,
// they're nudged apart. If one of those dots later disappears (dropped
// out of the list), the remaining one automatically settles back to its
// true position on the next refresh, since positions are never persisted
// between refreshes.
public class GlobeDotSpawner : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The sphere's own transform — dots parent to this so they rotate along with it.")]
    public Transform globeSurface;
    public GlobeDot dotPrefab;
    [Tooltip("Must match your sphere's actual radius. Unity's default primitive sphere at scale 1 has radius 0.5.")]
    public float globeRadius = 0.5f;
    [Tooltip("Dots spawn slightly ABOVE the sphere's surface (radius * this factor) so raycasts reliably hit the dot instead of ambiguously hitting the sphere collider at the same point.")]
    public float dotSurfaceOffset = 1.02f;
    [Tooltip("Flip if dots are landing at the wrong north/south position relative to your world texture — this is a known mismatch between how equirectangular textures are authored (row 0 = north pole) and how Unity samples texture V coordinates (V=0 = bottom).")]
    public bool invertLatitude = false;

    [Header("Dot Size (overrides the prefab's own min/max scale on every spawn)")]
    public float minDotScale = 0.005f;
    public float maxDotScale = 0.02f;

    [Header("Decluttering (recalculated every refresh, never persisted)")]
    [Tooltip("Minimum allowed 3D distance between any two dots. Same units as globeRadius/localPosition.")]
    public float minDotDistance = 0.02f;
    [Tooltip("More iterations untangle dense clusters better, at higher cost. 999 dots with a few iterations is still cheap since this only runs once per refresh, not per frame.")]
    public int declutterIterations = 4;

    [Header("Data Source")]
    [Tooltip("Dots mirror whatever this is currently displaying — real fetched data during normal play, or whatever a fake-test tool populated it with during testing. No separate fetch here.")]
    public LeaderboardUI leaderboardUI;

    private readonly List<GlobeDot> spawnedDots = new List<GlobeDot>();
    private readonly Dictionary<string, GlobeDot> dotsByPlayerId = new Dictionary<string, GlobeDot>();

    // Used only by TestSpawn999EvenlyDistributedGrowingDots below.
    private class GrowingBotEntry
    {
        public GlobeDot dot;
        public double initialScore;
        public double growthPerSecond;
        public float startTime;
    }
    private List<GrowingBotEntry> growingBots;
    private Coroutine growingDotsCoroutine;

    // Standard equirectangular lat/long -> 3D sphere-surface conversion.
    // Matches Unity's default primitive sphere UV layout, so a standard
    // equirectangular world map texture applied directly to that sphere
    // will line up correctly with these positions.
    public static Vector3 LatLongToLocalPosition(float latDegrees, float lonDegrees, float radius)
    {
        float lat = latDegrees * Mathf.Deg2Rad;
        float lon = lonDegrees * Mathf.Deg2Rad;
        float x = radius * Mathf.Cos(lat) * Mathf.Cos(lon);
        float y = radius * Mathf.Sin(lat);
        float z = radius * Mathf.Cos(lat) * Mathf.Sin(lon);
        return new Vector3(x, y, z);
    }

    // Called whenever the Overview tab opens (and periodically while it
    // stays open — see GlobeTabController). Reads whatever LeaderboardUI
    // currently has loaded, computes each player's TRUE position from
    // their real lat/long, runs decluttering, then spawns/updates dots.
    public void PopulateFromLeaderboard()
    {
        if (leaderboardUI == null) return;

        List<LeaderboardUI.RowData> entries = leaderboardUI.GetCurrentEntries();
        if (entries == null || entries.Count == 0) return;

        int total = entries.Count;
        float placementRadius = globeRadius * dotSurfaceOffset;

        Vector3[] positions = new Vector3[total];
        for (int i = 0; i < total; i++)
        {
            float lat = invertLatitude ? -entries[i].latitude : entries[i].latitude;
            positions[i] = LatLongToLocalPosition(lat, entries[i].longitude, placementRadius);
        }

        // Decluttering temporarily disabled — dots always use their true
        // real-world position for now. Uncomment to re-enable:
        // DeclutterPositions(positions, placementRadius);

        HashSet<string> seenThisRefresh = new HashSet<string>();

        for (int i = 0; i < total; i++)
        {
            LeaderboardUI.RowData entry = entries[i];
            int rank = i + 1;
            seenThisRefresh.Add(entry.playerId);

            if (dotsByPlayerId.TryGetValue(entry.playerId, out GlobeDot dot) && dot != null)
            {
                // Detect a BigBang since the last refresh — compared BEFORE
                // overwriting bigBangCount below. Not real-time; only ever
                // as fresh as the leaderboard's own refresh interval.
                bool justBigBanged = entry.bigBangCount > dot.bigBangCount;

                dot.playerName = entry.playerName;
                dot.country = entry.country;
                dot.lifetimeRabbits = entry.lifetimeRabbits;
                dot.bigBangCount = entry.bigBangCount;
                dot.rank = rank;
                dot.equippedNickname = entry.equippedNickname;
                dot.nicknameColorIndex = entry.nicknameColorIndex;
                dot.equippedBackground = entry.equippedBackground;
                dot.minScale = minDotScale;
                dot.maxScale = maxDotScale;
                dot.ApplyScaleByRank(rank, total);
                ApplyPosition(dot, positions[i]);

                if (justBigBanged)
                {
                    GlobeDotCelebration celebration = dot.GetComponent<GlobeDotCelebration>();
                    if (celebration != null) celebration.PlayCelebration();
                }
            }
            else
            {
                GlobeDot newDot = SpawnDotAtPosition(positions[i], entry.playerId, entry.playerName, entry.country, entry.lifetimeRabbits, entry.bigBangCount, rank, total, entry.equippedNickname, entry.equippedBackground, entry.nicknameColorIndex);
                if (newDot != null) dotsByPlayerId[entry.playerId] = newDot;
            }
        }

        List<string> toRemove = new List<string>();
        foreach (KeyValuePair<string, GlobeDot> kvp in dotsByPlayerId)
            if (!seenThisRefresh.Contains(kvp.Key)) toRemove.Add(kvp.Key);

        foreach (string id in toRemove)
        {
            GlobeDot dot = dotsByPlayerId[id];
            if (dot != null)
            {
                spawnedDots.Remove(dot);
                Destroy(dot.gameObject);
            }
            dotsByPlayerId.Remove(id);
        }
    }

    // Simple iterative repulsion: any pair closer than minDotDistance gets
    // pushed apart along the line between them, then re-projected back
    // onto the sphere surface. A handful of passes is enough to untangle
    // most local clusters. Runs once per refresh, not per frame, so the
    // O(n^2) cost is acceptable even at ~999 entries.
    void DeclutterPositions(Vector3[] positions, float radius)
    {
        if (positions.Length < 2) return;

        for (int iter = 0; iter < declutterIterations; iter++)
        {
            bool anyMoved = false;

            for (int i = 0; i < positions.Length; i++)
            {
                for (int j = i + 1; j < positions.Length; j++)
                {
                    Vector3 diff = positions[i] - positions[j];
                    float distSq = diff.sqrMagnitude;

                    if (distSq <= 0.0000001f)
                    {
                        // Exact same spot (identical city) — deterministic
                        // tiny nudge so they don't divide-by-zero on direction.
                        Vector3 arbitraryOffset = new Vector3(0.0001f * (i + 1), 0.0001f * (j + 1), 0.0001f * (i - j));
                        positions[j] = (positions[j] + arbitraryOffset).normalized * radius;
                        anyMoved = true;
                        continue;
                    }

                    if (distSq < minDotDistance * minDotDistance)
                    {
                        float dist = Mathf.Sqrt(distSq);
                        float pushAmount = (minDotDistance - dist) * 0.5f;
                        Vector3 pushDir = diff / dist;

                        positions[i] = (positions[i] + pushDir * pushAmount).normalized * radius;
                        positions[j] = (positions[j] - pushDir * pushAmount).normalized * radius;
                        anyMoved = true;
                    }
                }
            }

            if (!anyMoved) break; // converged early, no need for remaining iterations
        }
    }

    void ApplyPosition(GlobeDot dot, Vector3 localPos)
    {
        dot.transform.localPosition = localPos;
        // Face outward from the globe's center, like a marker printed on
        // the surface rather than a free-floating object. If it looks
        // backwards (facing inward) once you see it, flip the sign here.
        dot.transform.localRotation = Quaternion.LookRotation(-localPos.normalized);
    }

    GlobeDot SpawnDotAtPosition(Vector3 localPos, string playerId, string playerName, string country, long lifetimeRabbits, int bigBangCount, int rank, int totalCount, string equippedNickname = null, string equippedBackground = null, int nicknameColorIndex = 0)
    {
        if (dotPrefab == null || globeSurface == null) return null;

        GlobeDot dot = Instantiate(dotPrefab, globeSurface);
        ApplyPosition(dot, localPos);

        dot.playerId = playerId;
        dot.playerName = playerName;
        dot.country = country;
        dot.lifetimeRabbits = lifetimeRabbits;
        dot.bigBangCount = bigBangCount;
        dot.rank = rank;
        dot.equippedNickname = equippedNickname;
        dot.nicknameColorIndex = nicknameColorIndex;
        dot.equippedBackground = equippedBackground;
        dot.minScale = minDotScale;
        dot.maxScale = maxDotScale;
        dot.ApplyScaleByRank(rank, totalCount);

        spawnedDots.Add(dot);
        return dot;
    }

    // ── Local-only testing tools (unrelated to real location/declutter) ──

    // 30 real-world cities, including two deliberate duplicate pairs
    // (Paris x2, Tokyo x2) so you can actually see decluttering push
    // apart two players who'd otherwise land on the exact same spot.
    // Routes through the SAME DeclutterPositions pass real data uses.
    [ContextMenu("TEST: 30 Real-World City Dots")]
    public void TestSpawn30RealCities()
    {
        ClearDots();

        var cities = new (string name, string country, float lat, float lon)[]
        {
            ("New York", "US", 40.7128f, -74.0060f),
            ("Los Angeles", "US", 34.0522f, -118.2437f),
            ("Chicago", "US", 41.8781f, -87.6298f),
            ("Toronto", "CA", 43.6532f, -79.3832f),
            ("Mexico City", "MX", 19.4326f, -99.1332f),
            ("Sao Paulo", "BR", -23.5505f, -46.6333f),
            ("Buenos Aires", "AR", -34.6037f, -58.3816f),
            ("London", "GB", 51.5074f, -0.1278f),
            ("Paris", "FR", 48.8566f, 2.3522f),
            ("Paris (2nd player)", "FR", 48.8566f, 2.3522f), // deliberate duplicate
            ("Berlin", "DE", 52.5200f, 13.4050f),
            ("Madrid", "ES", 40.4168f, -3.7038f),
            ("Rome", "IT", 41.9028f, 12.4964f),
            ("Amsterdam", "NL", 52.3676f, 4.9041f),
            ("Moscow", "RU", 55.7558f, 37.6173f),
            ("Istanbul", "TR", 41.0082f, 28.9784f),
            ("Cairo", "EG", 30.0444f, 31.2357f),
            ("Lagos", "NG", 6.5244f, 3.3792f),
            ("Johannesburg", "ZA", -26.2041f, 28.0473f),
            ("Dubai", "AE", 25.2048f, 55.2708f),
            ("Mumbai", "IN", 19.0760f, 72.8777f),
            ("New Delhi", "IN", 28.6139f, 77.2090f),
            ("Bangkok", "TH", 13.7563f, 100.5018f),
            ("Singapore", "SG", 1.3521f, 103.8198f),
            ("Jakarta", "ID", -6.2088f, 106.8456f),
            ("Tokyo", "JP", 35.6762f, 139.6503f),
            ("Tokyo (2nd player)", "JP", 35.6762f, 139.6503f), // deliberate duplicate
            ("Seoul", "KR", 37.5665f, 126.9780f),
            ("Shanghai", "CN", 31.2304f, 121.4737f),
            ("Sydney", "AU", -33.8688f, 151.2093f)
        };

        System.Random rng = new System.Random();
        int total = cities.Length;
        float placementRadius = globeRadius * dotSurfaceOffset;

        Vector3[] positions = new Vector3[total];
        for (int i = 0; i < total; i++)
        {
            float lat = invertLatitude ? -cities[i].lat : cities[i].lat;
            positions[i] = LatLongToLocalPosition(lat, cities[i].lon, placementRadius);
        }

        // Decluttering temporarily disabled — see PopulateFromLeaderboard for the matching switch.
        // DeclutterPositions(positions, placementRadius);

        // Random scores, sorted descending so rank correctly reflects them.
        long logMinSeed = 10000;
        long logMaxSeed = 250000000;
        float logMin = Mathf.Log10(logMinSeed);
        float logMax = Mathf.Log10(logMaxSeed);

        List<(int cityIndex, long score)> scored = new List<(int, long)>();
        for (int i = 0; i < total; i++)
        {
            float logScore = logMin + (float)rng.NextDouble() * (logMax - logMin);
            scored.Add((i, (long)Mathf.Pow(10f, logScore)));
        }
        scored.Sort((a, b) => b.score.CompareTo(a.score));

        for (int rankIndex = 0; rankIndex < scored.Count; rankIndex++)
        {
            int cityIndex = scored[rankIndex].cityIndex;
            int rank = rankIndex + 1;
            var city = cities[cityIndex];
            SpawnDotAtPosition(positions[cityIndex], "CITYBOT_" + cityIndex, city.name, city.country, scored[rankIndex].score, rng.Next(0, 200), rank, total);
        }
    }

    // Spawns dots at KNOWN reference points so we can precisely diagnose
    // the coordinate mismatch instead of guessing. Report back exactly
    // where each one visually lands.
    [ContextMenu("TEST: Spawn Calibration Points (Poles + Equator)")]
    public void TestSpawnCalibrationPoints()
    {
        ClearDots();
        float r = globeRadius * dotSurfaceOffset;

        SpawnDotAtPosition(LatLongToLocalPosition(90f, 0f, r), "CAL_1", "NORTH POLE (lat 90, lon 0)", "??", 1, 0, 1, 4);
        SpawnDotAtPosition(LatLongToLocalPosition(-90f, 0f, r), "CAL_2", "SOUTH POLE (lat -90, lon 0)", "??", 1, 0, 2, 4);
        SpawnDotAtPosition(LatLongToLocalPosition(0f, 0f, r), "CAL_3", "EQUATOR / PRIME MERIDIAN (lat 0, lon 0)", "??", 1, 0, 3, 4);
        SpawnDotAtPosition(LatLongToLocalPosition(0f, 90f, r), "CAL_4", "EQUATOR, 90E (lat 0, lon 90)", "??", 1, 0, 4, 4);
    }

    [ContextMenu("TEST: Spawn 30 Random Dots")]
    public void TestSpawnRandomDots()
    {
        ClearDots();

        System.Random rng = new System.Random();
        long logMinSeed = 10000;
        long logMaxSeed = 250000000;
        float logMin = Mathf.Log10(logMinSeed);
        float logMax = Mathf.Log10(logMaxSeed);

        int total = 30;
        List<(float lat, float lon, long score, int bigBang)> generated = new List<(float, float, long, int)>();
        for (int i = 0; i < total; i++)
        {
            float lat = (float)(rng.NextDouble() * 180.0 - 90.0);
            float lon = (float)(rng.NextDouble() * 360.0 - 180.0);
            float logScore = logMin + (float)rng.NextDouble() * (logMax - logMin);
            long score = (long)Mathf.Pow(10f, logScore);
            generated.Add((lat, lon, score, rng.Next(0, 200)));
        }

        generated.Sort((a, b) => b.score.CompareTo(a.score));

        for (int i = 0; i < generated.Count; i++)
        {
            var g = generated[i];
            int rank = i + 1;
            float lat = invertLatitude ? -g.lat : g.lat;
            Vector3 pos = LatLongToLocalPosition(lat, g.lon, globeRadius * dotSurfaceOffset);
            SpawnDotAtPosition(pos, "FAKE_" + rank, "TestPlayer" + rank, "??", g.score, g.bigBang, rank, total);
        }
    }

    [ContextMenu("TEST: Clear Dots")]
    public void ClearDots()
    {
        if (growingDotsCoroutine != null) { StopCoroutine(growingDotsCoroutine); growingDotsCoroutine = null; }
        growingBots = null;

        foreach (GlobeDot d in spawnedDots)
            if (d != null) Destroy(d.gameObject);
        spawnedDots.Clear();
        dotsByPlayerId.Clear();
    }

    [ContextMenu("TEST: 999 Evenly-Distributed Growing Dots")]
    public void TestSpawn999EvenlyDistributedGrowingDots()
    {
        ClearDots();

        System.Random rng = new System.Random();
        long lowScore = 1000;
        long highScore = 9500000000;
        float logLow = Mathf.Log10(lowScore);
        float logHigh = Mathf.Log10(highScore);

        int total = 999;
        growingBots = new List<GrowingBotEntry>(total);

        for (int i = 0; i < total; i++)
        {
            float t = (float)i / (total - 1);
            float logScore = logLow + (logHigh - logLow) * t;
            long startScore = (long)Mathf.Pow(10f, logScore);
            int rank = total - i;

            float lat = (float)(rng.NextDouble() * 180.0 - 90.0);
            float lon = (float)(rng.NextDouble() * 360.0 - 180.0);
            double growthPerSecond = startScore * (rng.NextDouble() * 0.02);

            Vector3 pos = LatLongToLocalPosition(invertLatitude ? -lat : lat, lon, globeRadius * dotSurfaceOffset);
            GlobeDot dot = SpawnDotAtPosition(pos, "GROWBOT_" + i, "GrowBot" + i, "??", startScore, rng.Next(0, 1000), rank, total);

            growingBots.Add(new GrowingBotEntry
            {
                dot = dot,
                initialScore = startScore,
                growthPerSecond = growthPerSecond,
                startTime = Time.time
            });
        }

        growingDotsCoroutine = StartCoroutine(GrowingDotsLoop());
    }

    IEnumerator GrowingDotsLoop()
    {
        while (true)
        {
            float now = Time.time;
            foreach (GrowingBotEntry entry in growingBots)
            {
                if (entry.dot == null) continue;
                float elapsed = now - entry.startTime;
                long currentScore = (long)(entry.initialScore + entry.growthPerSecond * elapsed);
                entry.dot.lifetimeRabbits = currentScore;
            }
            yield return new WaitForSeconds(1f);
        }
    }
}
