using System;
using System.Collections.Generic;
using UnityEngine;
using GoogleMobileAds.Api;

// Real AdMob rewarded-ad integration. Same public interface as the old
// placeholder (IsRewardedAdReady / ShowRewardedAd) — nothing calling into
// this needed to change, only the implementation underneath.
public class AdManager : MonoBehaviour
{
    public static AdManager Instance { get; private set; }

#if UNITY_ANDROID
    private const string rewardedAdUnitId = "ca-app-pub-5954769894602846/5847120362";
#elif UNITY_IOS
    private const string rewardedAdUnitId = "ca-app-pub-5954769894602846/2557898033";
#else
    private const string rewardedAdUnitId = "unused";
#endif

    private RewardedAd rewardedAd;
    private bool isLoadingAd = false;

    // Tracks whether THIS specific show actually earned its reward, so
    // HandleAdClosed (which fires for both "watched fully" and "closed
    // early") can tell the two apart.
    private bool pendingEarnedReward = false;
    private Action pendingOnRewarded;
    private Action pendingOnFailed;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // Test device setup — without this, MobileAds.Initialize() below
        // requests REAL production ads even in development, which risks
        // your AdMob account getting flagged for invalid traffic if you
        // click your own ads while testing. Replace the string below with
        // YOUR device's test ID (Console will print it the first time you
        // run without this configured).
        List<string> testDeviceIds = new List<string> { "504F5F354129F80C1CFC97E352B0D6B1" };
        RequestConfiguration requestConfig = new RequestConfiguration
        {
            TestDeviceIds = testDeviceIds
        };
        MobileAds.SetRequestConfiguration(requestConfig);

        MobileAds.Initialize(initStatus =>
        {
            Debug.Log("AdManager: Mobile Ads SDK initialized.");
            LoadAd();
        });
    }

    void LoadAd()
    {
        if (isLoadingAd) return;
        isLoadingAd = true;

        if (rewardedAd != null)
        {
            rewardedAd.Destroy();
            rewardedAd = null;
        }

        AdRequest request = new AdRequest();
        RewardedAd.Load(rewardedAdUnitId, request, (RewardedAd ad, LoadAdError error) =>
        {
            isLoadingAd = false;

            if (error != null || ad == null)
            {
                Debug.LogWarning($"AdManager: rewarded ad failed to load — {error}");
                return;
            }

            rewardedAd = ad;
            RegisterEventHandlers(rewardedAd);
            Debug.Log("AdManager: rewarded ad loaded and ready.");
        });
    }

    void RegisterEventHandlers(RewardedAd ad)
    {
        ad.OnAdFullScreenContentClosed += HandleAdClosed;
        ad.OnAdFullScreenContentFailed += HandleAdFailedToShow;
    }

    public bool IsRewardedAdReady()
    {
        return rewardedAd != null && rewardedAd.CanShowAd();
    }

    public void ShowRewardedAd(Action onRewarded, Action onFailed)
    {
        if (!IsRewardedAdReady())
        {
            Debug.LogWarning("AdManager: no rewarded ad ready to show.");
            onFailed?.Invoke();
            LoadAd(); // try to have one ready for next time
            return;
        }

        pendingEarnedReward = false;
        pendingOnRewarded = onRewarded;
        pendingOnFailed = onFailed;

        rewardedAd.Show((Reward reward) =>
        {
            // Fires only if the ad was watched to completion.
            pendingEarnedReward = true;
        });
    }

    // Fires when the ad closes, REGARDLESS of whether the reward was
    // earned — this is what lets us detect "player closed the ad early"
    // (pendingEarnedReward still false here) vs. "watched it fully"
    // (userRewardEarnedCallback above already flipped it to true).
    //
    // IMPORTANT: this callback can fire from a native background thread,
    // not Unity's main thread — Google's SDK doesn't guarantee otherwise.
    // Everything here is routed through MainThreadDispatcher so none of
    // our game code (which touches Unity APIs — GameObjects, UI, etc.)
    // ever runs off the main thread. Running Unity API calls off-thread
    // is undefined behavior: it can work fine most of the time and then
    // intermittently crash the whole native process with no catchable
    // C# exception — which is exactly what was happening before this fix.
    void HandleAdClosed()
    {
        MainThreadDispatcher.Instance?.Enqueue(() =>
        {
            if (pendingEarnedReward)
                pendingOnRewarded?.Invoke();
            else
                pendingOnFailed?.Invoke(); // closed early — no reward

            pendingOnRewarded = null;
            pendingOnFailed = null;

            LoadAd(); // preload the next one immediately
        });
    }

    void HandleAdFailedToShow(AdError error)
    {
        MainThreadDispatcher.Instance?.Enqueue(() =>
        {
            Debug.LogWarning($"AdManager: rewarded ad failed to show — {error}");
            pendingOnFailed?.Invoke();
            pendingOnRewarded = null;
            pendingOnFailed = null;
            LoadAd();
        });
    }
}
