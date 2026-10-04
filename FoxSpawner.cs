using UnityEngine;

public class FoxSpawner : MonoBehaviour
{
    [Header("Enemies")]
    public GameObject foxPrefab;
    [Tooltip("Used instead of foxPrefab whenever the current planet is Moon. Falls back to foxPrefab if left unassigned.")]
    public GameObject spiderPrefab;
    [Tooltip("Used instead of foxPrefab whenever the current planet is Venus. Falls back to foxPrefab if left unassigned.")]
    public GameObject snakePrefab;
    [Tooltip("Used instead of foxPrefab whenever the current planet is Mars. Falls back to foxPrefab if left unassigned.")]
    public GameObject martianPrefab;
    [Tooltip("Used instead of foxPrefab whenever the current planet is Pluto. Falls back to foxPrefab if left unassigned.")]
    public GameObject alienPrefab;

    [Header("Paths")]
    [Tooltip("Each element is one possible fox path. Fox is spawned at path[0] and follows to the end.")]
    public FoxPath[] paths;

    [Header("Spawn Timing")]
    public float minInterval = 5f;
    public float maxInterval = 20f;
    [Tooltip("If checked, Venus uses its own spawn interval below instead of the shared Min/Max Interval — every other planet is unaffected.")]
    public bool overrideVenusInterval = false;
    public float venusMinInterval = 2f;
    public float venusMaxInterval = 8f;

    private float timer = 0f;
    private float nextSpawnTime;

    void Start()
    {
        ResetSpawner();
    }

    public void ResetSpawner()
    {
        // Destroy every fox by component — reliable regardless of tag.
        foreach (FoxMover fox in FindObjectsByType<FoxMover>(FindObjectsInactive.Include))
            Destroy(fox.gameObject);

        // Reset all path occupations.
        foreach (FoxPath path in paths)
            if (path != null) path.occupied = false;

        // Also clear any reserved fox damage on huts.
        foreach (RabbitHut hut in FindObjectsByType<RabbitHut>(FindObjectsInactive.Include))
            hut.ClearFoxDamage();

        // Reset timer.
        timer         = 0f;
        var (min0, max0) = GetSpawnIntervalForCurrentPlanet();
        nextSpawnTime = Random.Range(min0, max0);
    }

    void Update()
    {
        if (PrestigeManager.Instance != null && PrestigeManager.Instance.isTransitioning) return;

        timer += Time.deltaTime;
        if (timer >= nextSpawnTime)
        {
            SpawnFox();
            timer = 0f;
            var (min1, max1) = GetSpawnIntervalForCurrentPlanet();
            nextSpawnTime = Random.Range(min1, max1);
        }
    }

    // Earth spawns foxes, Moon spawns spiders, Venus spawns snakes, Mars
    // spawns martians, Pluto spawns aliens — each falls back to foxPrefab
    // if its own prefab hasn't been assigned yet.
    // Returns (min, max) spawn interval for whichever planet is currently
    // active. Only Venus can differ from the shared default right now —
    // every other planet always uses minInterval/maxInterval unchanged.
    (float min, float max) GetSpawnIntervalForCurrentPlanet()
    {
        if (overrideVenusInterval && PlanetManager.Instance != null && PlanetManager.Instance.currentPlanet == PlanetManager.Planet.Venus)
            return (venusMinInterval, venusMaxInterval);
        return (minInterval, maxInterval);
    }

    GameObject GetEnemyPrefabForCurrentPlanet()
    {
        if (PlanetManager.Instance == null) return foxPrefab;

        switch (PlanetManager.Instance.currentPlanet)
        {
            case PlanetManager.Planet.Moon:
                return spiderPrefab != null ? spiderPrefab : foxPrefab;
            case PlanetManager.Planet.Venus:
                return snakePrefab != null ? snakePrefab : foxPrefab;
            case PlanetManager.Planet.Mars:
                return martianPrefab != null ? martianPrefab : foxPrefab;
            case PlanetManager.Planet.Pluto:
                return alienPrefab != null ? alienPrefab : foxPrefab;
            default:
                return foxPrefab; // Earth defaults to fox
        }
    }

    void SpawnFox()
    {
        GameObject enemyPrefab = GetEnemyPrefabForCurrentPlanet();
        if (enemyPrefab == null || paths == null || paths.Length == 0) return;

        // Only consider paths whose target hut is unlocked and has rabbits
        // left after accounting for already-spawned foxes heading there.
        System.Collections.Generic.List<FoxPath> validPaths = new System.Collections.Generic.List<FoxPath>();
        foreach (FoxPath path in paths)
        {
            if (path == null || path.waypoints == null || path.waypoints.Length == 0) continue;
            if (path.targetHut == null) continue;
            if (!path.targetHut.gameObject.activeInHierarchy) continue;
            if (path.targetHut.GetEffectiveRabbits() <= 0) continue;
            if (path.occupied) continue;
            if (path.IsDisabledOnCurrentPlanet()) continue;
            validPaths.Add(path);
        }

        if (validPaths.Count == 0) return;

        FoxPath chosen = validPaths[Random.Range(0, validPaths.Count)];

        // Pre-calculate damage this fox will deal and reserve it on the hut.
        long effective = chosen.targetHut.GetEffectiveRabbits();
        long damage = effective <= 100 ? 5 : (long)(effective * 0.3f);
        damage = System.Math.Min(damage, effective);
        chosen.targetHut.ReserveFoxDamage(damage);

        chosen.occupied = true;

        Vector3 spawnPos = chosen.waypoints[0].position;
        GameObject fox = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);

        FoxMover mover = fox.GetComponent<FoxMover>();
        if (mover != null)
        {
            mover.waypoints      = chosen.waypoints;
            mover.targetHut      = chosen.targetHut;
            mover.reservedDamage = damage;
            mover.path           = chosen;
        }
    }
}
