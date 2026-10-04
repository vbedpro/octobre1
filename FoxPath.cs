using UnityEngine;

// Place this on an empty GameObject for each fox path.
// Assign the waypoints and which hut this path leads to.
public class FoxPath : MonoBehaviour
{
    public Transform[] waypoints;
    public RabbitHut targetHut;
    [HideInInspector] public bool occupied = false;

    [Tooltip("This path is never used for spawning while the player is on any of these planets. Leave empty to allow it everywhere.")]
    public PlanetManager.Planet[] disabledOnPlanets;

    public bool IsDisabledOnCurrentPlanet()
    {
        if (disabledOnPlanets == null || disabledOnPlanets.Length == 0) return false;
        if (PlanetManager.Instance == null) return false;

        PlanetManager.Planet current = PlanetManager.Instance.currentPlanet;
        foreach (PlanetManager.Planet p in disabledOnPlanets)
            if (p == current) return true;
        return false;
    }
}
