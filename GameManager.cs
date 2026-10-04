using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Resources")]
    public long totalRabbits = 0;
    // Cumulative all-time count — never decreases (unlike totalRabbits, which
    // drops on selling/fox damage). Used for the leaderboard lifetime stat.
    [HideInInspector] public long lifetimeRabbits = 0;
    // Same permanent, never-reset pattern as lifetimeRabbits — but ONLY
    // incremented by hut reproduction (AddRabbitsSilent + both offline
    // earnings paths in SaveManager), never by hatching or other sources.
    // Used for the "Breed Rabbits" guild contest objective.
    [HideInInspector] public long lifetimeRabbitsBred = 0;
    public long totalCoins = 90;
    public long totalCarrots = 0;

    [Header("UI")]
    public TextMeshProUGUI rabbitText;
    [Tooltip("Font size used for rabbitText once totalRabbits exceeds rabbitTextShrinkThreshold.")]
    public float rabbitTextLargeFontSize = 15f;
    [Tooltip("Normal font size for rabbitText below the threshold.")]
    public float rabbitTextNormalFontSize = 20f;
    public long rabbitTextShrinkThreshold = 999999999999L;
    public TextMeshProUGUI moneyText;
    [Tooltip("Font size used for moneyText once totalCoins exceeds moneyTextShrinkThreshold.")]
    public float moneyTextLargeFontSize = 13f;
    [Tooltip("Normal font size for moneyText below the threshold.")]
    public float moneyTextNormalFontSize = 16f;
    public long moneyTextShrinkThreshold = 10000000000L;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI carrotText;
    [Tooltip("Font size used for carrotText once totalCarrots exceeds carrotTextShrinkThreshold.")]
    public float carrotTextLargeFontSize = 13f;
    [Tooltip("Normal font size for carrotText below the threshold.")]
    public float carrotTextNormalFontSize = 16f;
    public long carrotTextShrinkThreshold = 10000L;
    public TextMeshProUGUI prestigeText;
    [Tooltip("Font size used for prestigeText once prestigeCount exceeds prestigeTextShrinkThreshold.")]
    public float prestigeTextLargeFontSize = 13f;
    [Tooltip("Normal font size for prestigeText below the threshold.")]
    public float prestigeTextNormalFontSize = 16f;
    public int prestigeTextShrinkThreshold = 100;

    [Header("Truck Timer")]
    public float truckCooldown = 30f;
    private float truckTimer = 30f;
    public bool truckAvailable = true;

    // Exposed so UpgradeManager can set it (kept for potential future use).
    [HideInInspector] public double rabbitsPerSecond = 0;
    [HideInInspector] public double coinsPerSecond = 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        Application.targetFrameRate = 60;
    }

    void Start()
    {
        // Force an initial UI refresh — without this, every text field
        // just shows whatever placeholder was typed into it in the Editor
        // (e.g. a literal "0") until the first reactive AddCoins/AddRabbits/
        // etc. call happens to fire, which could be much later (or look
        // like the starting value is wrong/lost, when it was actually
        // correct the whole time — just not yet displayed).
        UpdateUI();
        UpdateTimerUI();
    }

    void Update()
    {
        if (!truckAvailable)
        {
            truckTimer -= Time.deltaTime;
            if (truckTimer <= 0f)
            {
                truckTimer = 0f;
                truckAvailable = true;
            }
        }
        UpdateTimerUI();
    }

    public void AddRabbits(long amount)
    {
        totalRabbits += amount;
        if (amount > 0)
        {
            lifetimeRabbits += amount;
            AchievementsManager.Instance?.ReportLifetimeRabbits(lifetimeRabbits);
        }
        UpdateUI();
    }

    // Same as AddRabbits, but skips the immediate UI rebuild — used by
    // RabbitHut's passive reproduction, which can call this once PER HUT,
    // EVERY FRAME at high breeding rates. Calling the full AddRabbits()
    // (which rebuilds the TextMeshPro text mesh) that often meant a
    // separate UI rebuild per hut per frame instead of just one — this
    // marks the total as "dirty" instead, and LateUpdate() below does the
    // actual UI refresh exactly once per frame no matter how many huts
    // called this in the meantime.
    private bool rabbitsUiDirty = false;
    public void AddRabbitsSilent(long amount)
    {
        totalRabbits += amount;
        if (amount > 0)
        {
            lifetimeRabbits += amount;
            lifetimeRabbitsBred += amount; // this method is ONLY called by RabbitHut's passive reproduction
            AchievementsManager.Instance?.ReportLifetimeRabbits(lifetimeRabbits);
        }
        rabbitsUiDirty = true;
    }

    void LateUpdate()
    {
        if (rabbitsUiDirty)
        {
            rabbitsUiDirty = false;
            UpdateUI();
        }
    }

    public void AddCoins(long amount)
    {
        totalCoins += amount;
        UpdateUI();
    }

    public bool SpendCoins(long amount)
    {
        if (totalCoins >= amount)
        {
            totalCoins -= amount;

            if (QuestManager.Instance != null)
                QuestManager.Instance.ReportCoinsSpent(amount);
            AchievementsManager.Instance?.ReportCoinsSpent(totalCoins);

            UpdateUI();
            return true;
        }
        return false;
    }

    // Unlike SpendCoins above (which requires sufficient funds and is a
    // no-op otherwise — correct for PURCHASES), this ALWAYS deducts,
    // clamped at 0 — for PENALTIES (e.g. a failed quest's coin debuff),
    // which should still take whatever the player has even if it's less
    // than the full penalty amount, not silently do nothing.
    public void ForceSpendCoins(long amount)
    {
        totalCoins = System.Math.Max(0, totalCoins - amount);
        UpdateUI();
    }

    public void TruckUsed()
    {
        truckAvailable = false;

        float effectiveCooldown = truckCooldown;

        // Boost: Elite Dispatching — flat 5 seconds off the truck
        // cooldown, floored at 0 so it can't go negative on already-short
        // cooldowns.
        if (BoostActivationManager.Instance != null && BoostActivationManager.Instance.IsActive("boost_elite_dispatching"))
            effectiveCooldown = Mathf.Max(0f, effectiveCooldown - 5f);

        truckTimer = effectiveCooldown;
    }

    // Seconds remaining on the truck cooldown right now. 0 if the truck is
    // already available. Used by PlanetManager to save the EXACT remaining
    // time when switching away from a planet, so each planet's truck timer
    // keeps counting independently instead of resetting on every switch.
    public float GetTruckTimeRemaining()
    {
        return truckAvailable ? 0f : truckTimer;
    }

    // Restores a truck's exact state (used by PlanetManager when switching
    // TO a planet, to resume that planet's own truck cooldown from where it
    // was left off). timeRemaining <= 0 just means the truck is ready.
    public void SetTruckState(bool available, float timeRemaining)
    {
        if (available || timeRemaining <= 0f)
        {
            truckAvailable = true;
            truckTimer = 0f;
        }
        else
        {
            truckAvailable = false;
            truckTimer = timeRemaining;
        }
    }

    public void AddCarrots(long amount)
    {
        totalCarrots += amount;
        UpdateUI();
    }

    public bool SpendCarrots(long amount)
    {
        if (totalCarrots >= amount)
        {
            totalCarrots -= amount;

            if (QuestManager.Instance != null)
                QuestManager.Instance.ReportCarrotsSpent(amount);

            UpdateUI();
            return true;
        }
        return false;
    }

    // Same reasoning as ForceSpendCoins above — for quest carrot
    // penalties, which must always take effect (clamped at 0), never
    // silently blocked by insufficient funds.
    public void ForceSpendCarrots(long amount)
    {
        totalCarrots = System.Math.Max(0, totalCarrots - amount);
        UpdateUI();
    }

    void UpdateUI()
    {
        if (rabbitText != null)
        {
            rabbitText.text = FormatFullNumber(totalRabbits);
            rabbitText.fontSize = totalRabbits > rabbitTextShrinkThreshold ? rabbitTextLargeFontSize : rabbitTextNormalFontSize;
        }
        if (moneyText != null)
        {
            moneyText.text = FormatFullNumber(totalCoins);
            moneyText.fontSize = totalCoins > moneyTextShrinkThreshold ? moneyTextLargeFontSize : moneyTextNormalFontSize;
        }
        if (carrotText != null)
        {
            carrotText.text = FormatFullNumber(totalCarrots);
            carrotText.fontSize = totalCarrots > carrotTextShrinkThreshold ? carrotTextLargeFontSize : carrotTextNormalFontSize;
        }
        if (prestigeText != null)
        {
            int prestigeCount = PrestigeManager.Instance != null ? PrestigeManager.Instance.prestigeCount : 0;
            prestigeText.text = prestigeCount.ToString();
            prestigeText.fontSize = prestigeCount > prestigeTextShrinkThreshold ? prestigeTextLargeFontSize : prestigeTextNormalFontSize;
        }
    }

    void UpdateTimerUI()
    {
        if (timerText == null) return;
        if (truckAvailable)
        {
            timerText.text = "RDY";
        }
        else
        {
            int minutes = Mathf.FloorToInt(truckTimer / 60f);
            int seconds = Mathf.FloorToInt(truckTimer % 60f);
            timerText.text = string.Format("{0}:{1:00}", minutes, seconds);
        }
    }

    public static string FormatNumber(long number)
    {
        if (number >= 1_000_000_000_000L) return FormatThreeSig(number / 1_000_000_000_000f) + "T";
        if (number >= 1_000_000_000L)     return FormatThreeSig(number / 1_000_000_000f) + "B";
        if (number >= 1_000_000L)         return FormatThreeSig(number / 1_000_000f) + "M";
        if (number >= 1_000L)             return FormatThreeSig(number / 1_000f) + "K";
        return number.ToString();
    }

    // Returns a number formatted to at most 3 significant characters, minimum 2 digits.
    // e.g. 733.8 -> "734". 45.5 -> "45.5". 4.0 -> "4.0". 1.25 -> "1.25".
    static string FormatThreeSig(float value)
    {
        if (value >= 100f) return Mathf.RoundToInt(value).ToString();   // 100–999 → "734"
        if (value >= 10f)  return value.ToString("0.#");                 // 10–99  → "45.5"
        return value.ToString("0.0#");                                   // 1–9    → "4.0" or "1.25"
    }

    public static string FormatFullNumber(long number)
    {
        return number.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string FormatNumberThreshold(long number, long threshold = 100000)
    {
        if (number < threshold) return number.ToString();
        if (number >= 1_000_000_000_000L) return (number / 1_000_000_000_000f).ToString("0.#") + "T";
        if (number >= 1_000_000_000L)     return (number / 1_000_000_000f).ToString("0.#") + "B";
        if (number >= 1_000_000L)         return (number / 1_000_000f).ToString("0.#") + "M";
        return (number / 1000) + "K";
    }

    public static string FormatCapacity(long number)
    {
        if (number < 1_000L)             return number.ToString();
        if (number < 1_000_000L)         return (number / 1_000f).ToString("0.#") + "K";
        if (number < 1_000_000_000L)     return (number / 1_000_000f).ToString("0.#") + "M";
        if (number < 1_000_000_000_000L) return (number / 1_000_000_000f).ToString("0.#") + "B";
        return (number / 1_000_000_000_000f).ToString("0.#") + "T";
    }
}
