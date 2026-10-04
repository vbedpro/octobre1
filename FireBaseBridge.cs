using UnityEngine;
using Unity.Services.CloudCode;
using Firebase;
using Firebase.Auth;
using System.Collections.Generic;
using System.Threading.Tasks;

// Bridges Unity Authentication to Firebase Authentication. Call
// SignInToFirebase() once, right after Unity Authentication succeeds —
// this signs the player into Firebase with a UID that's IDENTICAL to
// their existing Unity Player ID, so both systems agree on who's who.
public class FirebaseBridge : MonoBehaviour
{
    public static FirebaseBridge Instance;

    [System.Serializable]
    private class FirebaseTokenResponse
    {
        public string token;
    }

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }
    }

    // Call this once, right after Unity Authentication finishes signing
    // the player in — wherever that currently happens in your startup flow.
    public async Task<bool> SignInToFirebase()
    {
        // Required Firebase boilerplate: checks/fixes Google Play Services
        // dependencies on Android before ANY Firebase API is safe to use.
        // Skipping this is a common source of silent failures on device.
        var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (dependencyStatus != DependencyStatus.Available)
        {
            Debug.LogWarning($"FirebaseBridge: Firebase dependencies not available — {dependencyStatus}");
            return false;
        }

        FirebaseTokenResponse response;
        try
        {
            response = await CloudCodeService.Instance.CallEndpointAsync<FirebaseTokenResponse>(
                "GetFirebaseToken", new Dictionary<string, object>());
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"FirebaseBridge: GetFirebaseToken call failed — {e.Message}");
            return false;
        }

        if (response == null || string.IsNullOrEmpty(response.token))
        {
            Debug.LogWarning("FirebaseBridge: received an empty token.");
            return false;
        }

        try
        {
            AuthResult result = await FirebaseAuth.DefaultInstance.SignInWithCustomTokenAsync(response.token);
            Debug.Log($"FirebaseBridge: signed into Firebase as {result.User.UserId}");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"FirebaseBridge: Firebase sign-in failed — {e.Message}");
            return false;
        }
    }

    public bool IsSignedIn => FirebaseAuth.DefaultInstance.CurrentUser != null;
}
