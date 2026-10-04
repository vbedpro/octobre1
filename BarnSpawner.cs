using UnityEngine;
using System.Collections.Generic;

public class BarnSpawner : MonoBehaviour
{
    [Header("Spawning")]
    public GameObject rabbitPrefab;
    public Transform spawnPoint;
    public float spawnInterval = 0.5f;

    [Header("Huts")]
    public RabbitHut[] rabbitHuts;

    private float timer = 0f;
    private bool isHolding = false;

    void Update()
    {
        if (isHolding && RoboticHatchingBoost.IsManualHatchingBlocked())
        {
            // Boost turned on mid-hold — stop the manual hold-to-spawn
            // immediately instead of letting it keep firing until release.
            isHolding = false;
        }

        if (isHolding)
        {
            timer += Time.deltaTime;
            if (timer >= spawnInterval)
            {
                SpawnRabbit();
                timer = 0f;
            }
        }
        else
        {
            timer = 0f;
        }
    }

    public void OnButtonDown()
    {
        // Robotic Hatching boost disables manual hatching entirely for
        // its duration — rabbits only come from the automatic barn rate
        // while it's active.
        if (RoboticHatchingBoost.IsManualHatchingBlocked()) return;

        isHolding = true;
        // Fire the first spawn immediately instead of waiting a full
        // spawnInterval, then let Update() continue at the normal rate.
        SpawnRabbit();
        timer = 0f;
    }
    public void OnButtonUp() { isHolding = false; }

    // Entry point for RoboticHatchingBoost — spawns exactly one rabbit,
    // same as a single manual tap, without going through the hold-to-spawn
    // timer/button state at all.
    public void AutoHatchOne()
    {
        SpawnRabbit(playSound: false);
    }

    void SpawnRabbit(bool playSound = true)
    {
        if (rabbitPrefab == null) return;

        RabbitHut targetHut = PickTargetHut();
        if (targetHut == null) return;

        targetHut.ReserveSpot();

        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
        spawnPos += new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f));

        GameObject rabbit = Instantiate(rabbitPrefab, spawnPos, Quaternion.identity);
        if (playSound) AudioManager.Instance?.PlayRabbitHatched();
        AchievementsManager.Instance?.ReportRabbitHatched();
        QuestManager.Instance?.ReportRabbitsHatched(1);
        _ = GuildManager.Instance?.ReportContestProgressAsync("HatchRabbits", 1);

        if (TutorialManager.Instance != null)
            TutorialManager.Instance.ReportRabbitHatched();

        RabbitMover mover = rabbit.GetComponent<RabbitMover>();
        if (mover != null)
        {
            mover.waypoints = targetHut.waypoints;
            mover.targetHut = targetHut;
        }

        // Counted at the moment of hatching, not on arrival at the hut —
        // quest timers are short, so waiting for travel time isn't fair.
        if (QuestManager.Instance != null)
            QuestManager.Instance.ReportRabbitsHatched(1);
    }

    RabbitHut PickTargetHut()
    {
        List<RabbitHut> available = new List<RabbitHut>();
        foreach (RabbitHut hut in rabbitHuts)
        {
            if (hut != null && hut.gameObject.activeInHierarchy && !hut.IsFull())
                available.Add(hut);
        }

        if (available.Count == 0) return null;

        return available[Random.Range(0, available.Count)];
    }
}
