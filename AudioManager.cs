using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Central audio system — one singleton handles both background music
// (looping, cross-fadeable between tracks) and sound effects (pooled
// one-shot playback). Volume/mute preferences persist via PlayerPrefs.
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Music")]
    [Tooltip("4 sources total: two pairs. Each pair ping-pongs internally to loop its own track gaplessly (never double-scheduling on a single source, which can cause Unity to cut playback short on some platforms). The two PAIRS cross-fade between each other when switching tracks.")]
    public AudioSource musicSourceA1;
    public AudioSource musicSourceA2;
    public AudioSource musicSourceB1;
    public AudioSource musicSourceB2;
    [Range(0f, 1f)] public float musicVolume = 0.6f;
    public float musicCrossfadeDuration = 1.5f;
    [Tooltip("How far ahead of the actual loop point to queue the next repeat on the idle source. Doesn't affect timing precision — just needs to comfortably exceed one frame.")]
    public double loopScheduleAheadSeconds = 0.5;
    [Tooltip("The next loop iteration starts this many seconds BEFORE the previous one would naturally end — both briefly play at once, masking any residual gap. Try 0.03-0.08 (30-80ms). The loop's overall timing/cadence stays accurate regardless — only each individual trigger point shifts slightly earlier.")]
    public double loopOverlapSeconds = 0.05;

    [Tooltip("One looping track per planet, in enum order: Earth, Moon, Venus, Mars, Pluto.")]
    public AudioClip earthMusic;
    public AudioClip moonMusic;
    public AudioClip venusMusic;
    public AudioClip marsMusic;
    public AudioClip plutoMusic;

    [Tooltip("Per-planet volume multiplier (1 = normal, lower = quieter) — use this to balance tracks that were mixed/recorded louder than the others, without needing to re-export the audio file.")]
    [Range(0f, 1f)] public float earthMusicVolumeMultiplier = 1f;
    [Range(0f, 1f)] public float moonMusicVolumeMultiplier = 1f;
    [Range(0f, 1f)] public float venusMusicVolumeMultiplier = 1f;
    [Range(0f, 1f)] public float marsMusicVolumeMultiplier = 1f;
    [Range(0f, 1f)] public float plutoMusicVolumeMultiplier = 1f;

    [Header("SFX Clips")]
    public AudioClip foeEntersHut;
    [Tooltip("Alternates with Tapped V2 every time a foe is tapped.")]
    public AudioClip foeTappedV1;
    [Tooltip("Alternates with Tapped V1 every time a foe is tapped.")]
    public AudioClip foeTappedV2;
    public AudioClip epicUpgradeBought;
    public AudioClip commonUpgradeBought;
    public AudioClip boostBought;
    public AudioClip sellRabbitsClicked;
    public AudioClip bigBangInitiate;
    public AudioClip bigBangNumberSwitch;
    [Tooltip("Generic click — any UI button, hut, or garage. NOT upgrade buy buttons (those use their own dedicated clips above).")]
    public AudioClip uiElementClick;
    public AudioClip rabbitHatched;
    public AudioClip questCountdown15Sec;
    public AudioClip planetSwitch;
    public AudioClip hutLevelUp;

    private bool foeTapAlternateFlag = false; // toggles between the two tap variants

    [Header("SFX")]
    [Tooltip("Pool of AudioSources used for overlapping one-shot sound effects — lets multiple SFX play simultaneously (e.g. several coins/rabbits at once) without cutting each other off.")]
    public int sfxPoolSize = 8;
    public AudioSource sfxSourcePrefabTemplate; // any AudioSource to clone settings from, doesn't need to be a real prefab
    [Range(0f, 1f)] public float sfxVolume = 1f;

    private List<AudioSource> sfxPool = new List<AudioSource>();
    private int sfxPoolIndex = 0;

    // Handles gapless looping of ONE track via 2 ping-ponging AudioSources.
    // Never calls PlayScheduled a second time on a source that's already
    // playing — always targets whichever of the pair is currently silent/
    // idle. This is the reliable pattern; double-scheduling on a single
    // source can cause Unity to cut the current playback short on some
    // platforms/versions instead of cleanly queuing the next one.
    private class MusicSlot
    {
        public AudioSource sourceOdd;
        public AudioSource sourceEven;
        private AudioSource currentSource; // whichever is audible/playing right now
        private double nextLoopDspTime;
        private bool schedulingActive;
        private float currentVolume = 0f;

        public void Setup(AudioSource a, AudioSource b)
        {
            sourceOdd = a;
            sourceEven = b;
            foreach (AudioSource s in new[] { a, b })
            {
                if (s == null) continue;
                s.loop = false;
                s.playOnAwake = false;
                s.volume = 0f;
            }
        }

        public void StartClip(AudioClip clip)
        {
            schedulingActive = false;
            if (sourceOdd == null || sourceEven == null || clip == null) return;

            sourceOdd.clip = clip;
            sourceEven.clip = clip;

            double startTime = AudioSettings.dspTime + 0.1;
            sourceOdd.volume = currentVolume;
            sourceOdd.PlayScheduled(startTime);
            currentSource = sourceOdd;

            nextLoopDspTime = startTime + ExactClipDuration(clip);
            schedulingActive = true;
        }

        public void Tick(double loopScheduleAheadSeconds, double loopOverlapSeconds)
        {
            if (!schedulingActive || currentSource == null || currentSource.clip == null) return;

            double actualTriggerTime = nextLoopDspTime - loopOverlapSeconds;

            if (AudioSettings.dspTime > actualTriggerTime - loopScheduleAheadSeconds)
            {
                AudioSource idleSource = (currentSource == sourceOdd) ? sourceEven : sourceOdd;
                idleSource.volume = currentVolume;
                idleSource.PlayScheduled(actualTriggerTime); // scheduling on the IDLE source only — never double-scheduling the one that's still playing
                currentSource = idleSource;
                // Cadence tracked from the TRUE clip length, not the overlapped
                // trigger time — otherwise the overlap would compound and the
                // loop would drift progressively earlier over many repeats.
                nextLoopDspTime += ExactClipDuration(currentSource.clip);
            }
        }

        // Applies to whichever source is/will be audible — keeps both in
        // sync so a mid-loop-transition doesn't cause a volume jump.
        public void SetVolume(float v)
        {
            currentVolume = v;
            if (sourceOdd != null) sourceOdd.volume = v;
            if (sourceEven != null) sourceEven.volume = v;
        }

        public void Stop()
        {
            schedulingActive = false;
            if (sourceOdd != null) sourceOdd.Stop();
            if (sourceEven != null) sourceEven.Stop();
            currentVolume = 0f;
        }
    }

    // Precise clip duration from its actual sample count and frequency —
    // more accurate than clip.length (a float, prone to tiny rounding
    // error that would itself reintroduce a small gap over many loops).
    static double ExactClipDuration(AudioClip clip) => (double)clip.samples / clip.frequency;

    private MusicSlot slotA = new MusicSlot();
    private MusicSlot slotB = new MusicSlot();
    private MusicSlot activeSlot;
    private MusicSlot inactiveSlot;
    private Coroutine crossfadeCoroutine;
    private AudioClip currentMusicClip;
    private float activeTrackVolumeMultiplier = 1f;  // whatever's CURRENTLY in the active slot
    private float pendingTrackVolumeMultiplier = 1f; // set right before PlayMusic() for whatever's about to become active

    void Update()
    {
        slotA.Tick(loopScheduleAheadSeconds, loopOverlapSeconds);
        slotB.Tick(loopScheduleAheadSeconds, loopOverlapSeconds);
    }

    private const string MusicVolumeKey = "RabbitUniverse_MusicVolume";
    private const string SfxVolumeKey = "RabbitUniverse_SfxVolume";
    private const string MusicMutedKey = "RabbitUniverse_MusicMuted";
    private const string SfxMutedKey = "RabbitUniverse_SfxMuted";

    private bool musicMuted = false;
    private bool sfxMuted = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        DontDestroyOnLoad(gameObject);

        musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, musicVolume);
        sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, sfxVolume);
        musicMuted = PlayerPrefs.GetInt(MusicMutedKey, 0) == 1;
        sfxMuted = PlayerPrefs.GetInt(SfxMutedKey, 0) == 1;

        slotA.Setup(musicSourceA1, musicSourceA2);
        slotB.Setup(musicSourceB1, musicSourceB2);
        activeSlot = slotA;
        inactiveSlot = slotB;

        BuildSfxPool();
    }

    void BuildSfxPool()
    {
        for (int i = 0; i < sfxPoolSize; i++)
        {
            GameObject go = new GameObject($"SFX_Pool_{i}");
            go.transform.SetParent(transform);
            AudioSource src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            if (sfxSourcePrefabTemplate != null)
            {
                src.outputAudioMixerGroup = sfxSourcePrefabTemplate.outputAudioMixerGroup;
                src.spatialBlend = sfxSourcePrefabTemplate.spatialBlend;
            }
            sfxPool.Add(src);
        }
    }

    // ── Music ────────────────────────────────────────────────────────

    // Plays a track immediately if nothing's currently playing, or
    // cross-fades smoothly into it if something already is. Calling this
    // with the SAME clip that's already playing does nothing (avoids
    // restarting/refading a track that's already correct).
    public void PlayMusic(AudioClip clip)
    {
        if (clip == null || clip == currentMusicClip) return;
        currentMusicClip = clip;

        if (crossfadeCoroutine != null) StopCoroutine(crossfadeCoroutine);
        crossfadeCoroutine = StartCoroutine(CrossfadeToClip(clip));
    }

    public void StopMusic()
    {
        currentMusicClip = null;
        if (crossfadeCoroutine != null) StopCoroutine(crossfadeCoroutine);
        crossfadeCoroutine = StartCoroutine(FadeOutAndStop(activeSlot));
    }

    IEnumerator CrossfadeToClip(AudioClip clip)
    {
        inactiveSlot.StartClip(clip);

        float startVolumeOld = musicMuted ? 0f : musicVolume * activeTrackVolumeMultiplier;
        float targetVolumeNew = musicMuted ? 0f : musicVolume * pendingTrackVolumeMultiplier;

        float elapsed = 0f;
        while (elapsed < musicCrossfadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / musicCrossfadeDuration);
            activeSlot.SetVolume(Mathf.Lerp(startVolumeOld, 0f, t));
            inactiveSlot.SetVolume(Mathf.Lerp(0f, targetVolumeNew, t));
            yield return null;
        }

        activeSlot.Stop();
        activeTrackVolumeMultiplier = pendingTrackVolumeMultiplier; // the slot we're about to swap to becomes active

        // Swap roles — what was inactive is now active.
        MusicSlot temp = activeSlot;
        activeSlot = inactiveSlot;
        inactiveSlot = temp;
    }

    IEnumerator FadeOutAndStop(MusicSlot slot)
    {
        float elapsed = 0f;
        while (elapsed < musicCrossfadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / musicCrossfadeDuration;
            slot.SetVolume(Mathf.Lerp(musicMuted ? 0f : musicVolume * activeTrackVolumeMultiplier, 0f, t));
            yield return null;
        }
        slot.Stop();
    }

    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicVolumeKey, musicVolume);
        if (!musicMuted) activeSlot?.SetVolume(musicVolume * activeTrackVolumeMultiplier);
    }

    public void SetMusicMuted(bool muted)
    {
        musicMuted = muted;
        PlayerPrefs.SetInt(MusicMutedKey, muted ? 1 : 0);
        activeSlot?.SetVolume(muted ? 0f : musicVolume * activeTrackVolumeMultiplier);
    }

    public bool IsMusicMuted() => musicMuted;
    public float GetMusicVolume() => musicVolume;

    // ── SFX ──────────────────────────────────────────────────────────

    // Plays a one-shot sound effect using the next available pooled
    // source (round-robin) — multiple SFX can overlap freely without
    // cutting each other off, up to sfxPoolSize simultaneous sounds.
    public void PlaySfx(AudioClip clip, float volumeMultiplier = 1f)
    {
        if (clip == null || sfxMuted || sfxPool.Count == 0) return;

        AudioSource source = sfxPool[sfxPoolIndex];
        sfxPoolIndex = (sfxPoolIndex + 1) % sfxPool.Count;

        source.pitch = 1f;
        source.PlayOneShot(clip, sfxVolume * volumeMultiplier);
    }

    // Same as PlaySfx, but with slight random pitch variation — useful for
    // sounds that repeat often (hatching, tapping foes, coin gain) so they
    // don't sound robotically identical every single time.
    public void PlaySfxVaried(AudioClip clip, float pitchVariance = 0.08f, float volumeMultiplier = 1f)
    {
        if (clip == null || sfxMuted || sfxPool.Count == 0) return;

        AudioSource source = sfxPool[sfxPoolIndex];
        sfxPoolIndex = (sfxPoolIndex + 1) % sfxPool.Count;

        source.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
        source.PlayOneShot(clip, sfxVolume * volumeMultiplier);
    }

    public void SetSfxVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume);
    }

    public void SetSfxMuted(bool muted)
    {
        sfxMuted = muted;
        PlayerPrefs.SetInt(SfxMutedKey, muted ? 1 : 0);
    }

    public bool IsSfxMuted() => sfxMuted;
    public float GetSfxVolume() => sfxVolume;

    // ── Per-planet music ─────────────────────────────────────────────

    public void PlayMusicForPlanet(PlanetManager.Planet planet)
    {
        AudioClip clip = planet switch
        {
            PlanetManager.Planet.Earth => earthMusic,
            PlanetManager.Planet.Moon => moonMusic,
            PlanetManager.Planet.Venus => venusMusic,
            PlanetManager.Planet.Mars => marsMusic,
            PlanetManager.Planet.Pluto => plutoMusic,
            _ => earthMusic
        };
        pendingTrackVolumeMultiplier = planet switch
        {
            PlanetManager.Planet.Earth => earthMusicVolumeMultiplier,
            PlanetManager.Planet.Moon => moonMusicVolumeMultiplier,
            PlanetManager.Planet.Venus => venusMusicVolumeMultiplier,
            PlanetManager.Planet.Mars => marsMusicVolumeMultiplier,
            PlanetManager.Planet.Pluto => plutoMusicVolumeMultiplier,
            _ => 1f
        };
        PlayMusic(clip);
    }

    // ── Named SFX shortcuts ──────────────────────────────────────────

    public void PlayFoeEntersHut() => PlaySfx(foeEntersHut);

    // Alternates between the two variants every call, so repeated foe
    // taps don't sound identical back-to-back.
    public void PlayFoeTapped()
    {
        AudioClip clip = foeTapAlternateFlag ? foeTappedV2 : foeTappedV1;
        foeTapAlternateFlag = !foeTapAlternateFlag;
        PlaySfx(clip);
    }

    public void PlayEpicUpgradeBought() => PlaySfx(epicUpgradeBought);
    public void PlayCommonUpgradeBought() => PlaySfx(commonUpgradeBought);
    public void PlayBoostBought() => PlaySfx(boostBought);
    public void PlaySellRabbitsClicked() => PlaySfx(sellRabbitsClicked);
    public void PlayBigBangInitiate() => PlaySfx(bigBangInitiate);
    public void PlayBigBangNumberSwitch() => PlaySfx(bigBangNumberSwitch);
    public void PlayUiElementClick() => PlaySfx(uiElementClick);

    // Fires potentially very rapidly at high hatch rates — uses the
    // varied-pitch pooled variant so a flurry of hatches doesn't sound
    // like a single sound stuttering/machine-gunning identically.
    public void PlayRabbitHatched() => PlaySfxVaried(rabbitHatched, 0.05f);

    public void PlayHutLevelUp() => PlaySfx(hutLevelUp);

    // ── Quest countdown — needs to be interruptible (Accept/Decline) ──
    private AudioSource questCountdownSource;

    public void PlayQuestCountdown()
    {
        StopQuestCountdown();
        if (questCountdown15Sec == null || sfxMuted) return;

        GameObject go = new GameObject("QuestCountdown_Temp");
        go.transform.SetParent(transform);
        questCountdownSource = go.AddComponent<AudioSource>();
        questCountdownSource.clip = questCountdown15Sec;
        questCountdownSource.volume = sfxVolume;
        questCountdownSource.loop = false;
        questCountdownSource.Play();
        Destroy(go, questCountdown15Sec.length);
    }

    // Called when the player presses Accept or Decline on the quest popup.
    public void StopQuestCountdown()
    {
        if (questCountdownSource != null)
        {
            Destroy(questCountdownSource.gameObject);
            questCountdownSource = null;
        }
    }

    // Guards against playing when tapping the planet you're ALREADY on —
    // caller (PlanetManager) should only invoke this on an actual switch.
    public void PlayPlanetSwitch() => PlaySfx(planetSwitch);
}
