using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.EventSystems;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

[RequireComponent(typeof(Collider))]
public class FoxMover : MonoBehaviour
{
    [Header("Movement")]
    public float minMoveSpeed = 1.5f;
    public float maxMoveSpeed = 3.5f;
    public float turnSpeed    = 180f;
    public float arrivalDistance = 0.3f;
    [Tooltip("Extra Y-axis rotation (degrees) applied on top of the normal face-movement-direction turning — use this if the model's own 'front' doesn't match Unity's +Z forward (e.g. the snake prefab moving butt-first). Try 180 first for an exact backwards flip. The object still turns correctly to follow path curves; this just corrects which way the MODEL considers 'forward'.")]
    public float rotationOffsetY = 0f;

    [Header("Animation")]
    [Tooltip("Multiplies playback speed of the creature's animation (walk/slither clip) — works with BOTH the Animator component AND the Legacy Animation component, so it doesn't matter which one the model actually uses. Completely independent of Move Speed above — this only affects how fast the animation LOOKS, not how fast the creature actually travels.")]
    public float animationSpeedMultiplier = 1f;

    private float moveSpeed;
    private float laneOffset;
    private float wigglePhase;
    private float freqMul;

    [Header("Path Randomness")]
    public float maxLaneOffset    = 0.4f;
    public float wiggleAmplitude  = 0.1f;
    public float wiggleFrequency  = 1.5f;

    [Header("Path")]
    public Transform[] waypoints;
    public RabbitHut targetHut;
    [HideInInspector] public long reservedDamage = 0;
    [HideInInspector] public FoxPath path = null;

    [Header("Tap")]
    public float tapMoveToleranceInches       = 0.15f;
    public float tapMoveToleranceFallbackPixels = 40f;

    public Camera cam;

    private int currentWaypoint = 0;
    private bool enteredHut     = false;

    // Tap detection state.
    private Vector2 pressPos;
    private int     pressFingerId = -1;
    private bool    pressedOnFox  = false;

    void OnEnable()  { EnhancedTouchSupport.Enable(); }
    void OnDisable() { EnhancedTouchSupport.Disable(); }

    void Start()
    {
        if (cam == null) cam = Camera.main;
        moveSpeed   = Random.Range(minMoveSpeed, maxMoveSpeed);
        laneOffset  = Random.Range(-maxLaneOffset, maxLaneOffset);
        wigglePhase = Random.Range(0f, Mathf.PI * 2f);
        freqMul     = Random.Range(0.8f, 1.2f);

        ApplyAnimationSpeedMultiplier();
    }

    // Speeds up (or slows down) the creature's animation playback without
    // touching moveSpeed at all — works whether the model uses a Mecanim
    // Animator (with or without a Controller — animator.speed is a global
    // multiplier applied on top of any per-state Speed) or the older
    // Legacy Animation component (common when a model imports with
    // Animation Type set to "Legacy" instead of "Generic"/"Humanoid").
    void ApplyAnimationSpeedMultiplier()
    {
        if (Mathf.Approximately(animationSpeedMultiplier, 1f)) return;

        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.speed = animationSpeedMultiplier;
            return;
        }

        Animation legacyAnim = GetComponentInChildren<Animation>();
        if (legacyAnim != null)
        {
            foreach (AnimationState state in legacyAnim)
                state.speed = animationSpeedMultiplier;
        }
    }

    void Update()
    {
        if (enteredHut) return;

        Move();
        DetectTap();
    }

    void Move()
    {
        if (waypoints == null || currentWaypoint >= waypoints.Length) return;

        Transform target = waypoints[currentWaypoint];

        // Final segment — go straight to the door, no wiggle.
        bool finalSegment = currentWaypoint >= waypoints.Length - 1;
        if (finalSegment)
        {
            Vector3 doorTarget = new Vector3(target.position.x, transform.position.y, target.position.z);
            Vector3 toDoor = doorTarget - transform.position;
            if (toDoor.sqrMagnitude > 0.0001f)
            {
                Quaternion rot = Quaternion.LookRotation(toDoor) * Quaternion.Euler(0f, rotationOffsetY, 0f);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, rot, turnSpeed * 2f * Time.deltaTime);
            }
            transform.position = Vector3.MoveTowards(transform.position, doorTarget, moveSpeed * Time.deltaTime);
            if (Vector3.Distance(transform.position, doorTarget) < arrivalDistance)
            {
                currentWaypoint++;
                if (currentWaypoint >= waypoints.Length) EnterHut();
            }
            return;
        }

        // Wiggle and lane offset for natural movement.
        float wiggle = Mathf.Sin(Time.time * wiggleFrequency * freqMul + wigglePhase) * wiggleAmplitude;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        Vector3 flatDir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : transform.forward;
        Vector3 side = Vector3.Cross(Vector3.up, flatDir);
        Vector3 aim = target.position + side * (laneOffset + wiggle);

        Vector3 dir = aim - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, rotationOffsetY, 0f);
            transform.rotation   = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);

        }

        // Move along the actual computed direction toward the target — NOT
        // transform.forward, which would include rotationOffsetY and make
        // the object physically walk in the wrong direction whenever that
        // offset is nonzero. This keeps the offset purely visual.
        Vector3 moveDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : flatDir;
        transform.position += moveDir * moveSpeed * Time.deltaTime;

        // Advance when close enough AND the fox has passed or reached the waypoint.
        // The 'passed' check requires being within 2 units first to prevent skipping waypoints.
        Vector3 toWaypoint = target.position - transform.position;
        toWaypoint.y = 0f;
        float distToWaypoint = toWaypoint.magnitude;
        bool close  = distToWaypoint < arrivalDistance + Mathf.Abs(laneOffset);
        bool passed = distToWaypoint < 2f && Vector3.Dot(moveDir, toWaypoint.normalized) < 0f;

        if (close || passed)
        {
            currentWaypoint++;
            if (currentWaypoint >= waypoints.Length) EnterHut();
        }
    }

    void ClearPath()
    {
        if (path != null) path.occupied = false;
    }

    void EnterHut()
    {
        enteredHut = true;
        ClearPath();
        AudioManager.Instance?.PlayFoeEntersHut();
        if (PlanetManager.Instance != null)
            AchievementsManager.Instance?.ReportFoeEnteredHut(PlanetManager.Instance.currentPlanet);
            AchievementsManager.Instance?.ReportFoeEnteredHutCumulative(); // #20 / #25 Skullheads — separate cumulative counter
        if (targetHut != null)
        {
            targetHut.ReleaseFoxDamage(reservedDamage);

            // Either a carrot theft OR a rabbit loss happens on this
            // entry — never both. Carrot-steal chance is rolled first;
            // if it hits, that's the ONLY thing this fox does.
            if (!TryStealCarrots())
            {
                long current = targetHut.GetCurrentRabbits();
                long toRemove;
                if (current <= 100)
                    toRemove = 5;
                else
                    toRemove = (long)(current * 0.3f);

                toRemove = System.Math.Min(toRemove, current);
                if (toRemove > 0)
                {
                    targetHut.RemoveRabbits(toRemove);
                    GameManager.Instance.AddRabbits(-toRemove);
                    if (FloatingIconManager.Instance != null)
                        FloatingIconManager.Instance.ShowRabbitLoss(targetHut.transform.position + Vector3.up * 2f, toRemove);
                }
            }
        }
        Destroy(gameObject);
    }

    // Carrot-theft odds scale with how populated THIS PLANET currently is
    // (totalRabbits is per-planet, swapped in/out on planet switch —
    // unlike carrots, which are global). Bigger, more established farms
    // get raided more often. Returns true if a carrot was actually stolen
    // (so the caller skips the rabbit-loss branch entirely — a fox either
    // steals carrots or takes rabbits, never both on the same entry).
    bool TryStealCarrots()
    {
        if (GameManager.Instance == null) return false;

        long currentPlanetRabbits = GameManager.Instance.totalRabbits;

        float stealChance;
        if (currentPlanetRabbits >= 50_000_000) stealChance = 0.80f;
        else if (currentPlanetRabbits >= 20_000_000) stealChance = 0.60f;
        else if (currentPlanetRabbits >= 10_000_000) stealChance = 0.40f;
        else if (currentPlanetRabbits >= 5_000_000) stealChance = 0.30f;
        else if (currentPlanetRabbits >= 1_000_000) stealChance = 0.20f;
        else stealChance = 0.10f;

        if (Random.value >= stealChance) return false;

        long currentCarrots = GameManager.Instance.totalCarrots;
        long stealAmount;
        if (currentCarrots <= 100) stealAmount = 1;
        else if (currentCarrots <= 200) stealAmount = 2;
        else stealAmount = 3;

        GameManager.Instance.ForceSpendCarrots(stealAmount);

        if (FloatingIconManager.Instance != null && targetHut != null)
            FloatingIconManager.Instance.ShowCarrotLoss(targetHut.transform.position + Vector3.up * 2.5f, stealAmount);

        return true;
    }

    // ── Tap detection ────────────────────────────────────────────────
    void DetectTap()
    {
        if (Touchscreen.current != null) { DetectTouch(); return; }
        DetectMouse();
    }

    void DetectTouch()
    {
        var touches = ETouch.activeTouches;

        if (pressFingerId == -1)
        {
            for (int i = 0; i < touches.Count; i++)
            {
                var t = touches[i];
                if (t.phase != UnityEngine.InputSystem.TouchPhase.Began) continue;
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.touchId)) continue;
                pressFingerId = t.touchId;
                pressPos      = t.screenPosition;
                pressedOnFox  = RaycastSelf(pressPos);
                return;
            }
            return;
        }

        for (int i = 0; i < touches.Count; i++)
        {
            var t = touches[i];
            if (t.touchId != pressFingerId) continue;

            if (t.phase == UnityEngine.InputSystem.TouchPhase.Ended)
            {
                TryKill(t.screenPosition);
                pressFingerId = -1;
                pressedOnFox  = false;
            }
            else if (t.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                pressFingerId = -1;
                pressedOnFox  = false;
            }
            return;
        }

        pressFingerId = -1;
        pressedOnFox  = false;
    }

    void DetectMouse()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            pressedOnFox = false;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            pressPos     = mouse.position.ReadValue();
            pressedOnFox = RaycastSelf(pressPos);
        }
        else if (mouse.leftButton.wasReleasedThisFrame)
        {
            TryKill(mouse.position.ReadValue());
            pressedOnFox = false;
        }
    }

    void TryKill(Vector2 releasePos)
    {
        if (!pressedOnFox) return;
        if (Vector2.Distance(pressPos, releasePos) > TapTolerancePixels()) return;
        if (!RaycastSelf(releasePos)) return;

        if (targetHut != null)
            targetHut.ReleaseFoxDamage(reservedDamage);
        ClearPath();
        GiveReward();
        AudioManager.Instance?.PlayFoeTapped();
        AchievementsManager.Instance?.ReportFoeTapped();

        if (QuestManager.Instance != null)
            QuestManager.Instance.ReportFoxTapped();

        Destroy(gameObject);
    }

    void GiveReward()
    {
        int lucrativeHuntingMultiplier = (BoostActivationManager.Instance != null && BoostActivationManager.Instance.IsActive("boost_lucrative_hunting"))
            ? 10 : 1;

        if (Random.value < 0.25f)
        {
            // Carrot reward is always exactly 1 — Sharp Reflexes never affects this.
            long carrotAmount = 1 * lucrativeHuntingMultiplier;
            GameManager.Instance.AddCarrots(carrotAmount);
            if (FloatingIconManager.Instance != null)
                FloatingIconManager.Instance.ShowCarrotGain(transform.position, carrotAmount);
        }
        else
        {
            int prestigeCount = PrestigeManager.Instance != null ? PrestigeManager.Instance.prestigeCount : 0;
            // Scaled to 75% of the original formula's strength.
            long coins = (long)((50 + (long)(GameManager.Instance.totalRabbits * 0.05f) + (25 * prestigeCount)) * 0.75f);

            // Epic upgrade: Sharp Reflexes — +10% coin reward per level. Coins only.
            if (EpicUpgradeManager.Instance != null)
            {
                int sharpReflexesLevel = EpicUpgradeManager.Instance.GetLevel(EpicUpgradeManager.EpicUpgradeType.SharpReflexes);
                if (sharpReflexesLevel > 0)
                    coins = (long)(coins * (1f + sharpReflexesLevel * 0.1f));
            }

            coins *= lucrativeHuntingMultiplier;

            GameManager.Instance.AddCoins(coins);
            if (FloatingIconManager.Instance != null)
                FloatingIconManager.Instance.ShowCoinGain(transform.position, coins);
        }
    }

    float TapTolerancePixels()
    {
        float dpi = Screen.dpi;
        return dpi > 0f ? tapMoveToleranceInches * dpi : tapMoveToleranceFallbackPixels;
    }

    bool RaycastSelf(Vector2 screenPos)
    {
        if (cam == null) return false;
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            return hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform);
        return false;
    }
}
