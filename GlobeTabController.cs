using UnityEngine;
using System.Collections;

// Controls visibility of the Overview tab's 3D globe scene. The globe and
// its own dedicated camera stay fully inactive until this tab is selected,
// so it never interferes with the farm view or other leaderboard tabs.
public class GlobeTabController : MonoBehaviour
{
    [Header("Globe Scene Objects")]
    [Tooltip("The globe sphere itself (and its dots, if parented under it).")]
    public GameObject globeRoot;
    [Tooltip("Dedicated camera framing the globe — kept separate from your main farm camera so switching tabs doesn't disturb farm gameplay view.")]
    public GameObject globeCamera;
    [Tooltip("Optional — force-closes the info popup whenever you leave this tab.")]
    public GlobeInfoPopup infoPopup;
    [Tooltip("Fetches the real top-999 and (re)spawns dots every time this tab opens.")]
    public GlobeDotSpawner dotSpawner;
    [Tooltip("How often (seconds) dots re-fetch and re-spawn while this tab stays open — needed to see live rank/size changes, not just a one-time snapshot from when the tab was opened.")]
    public float dotRefreshInterval = 3f;

    private Coroutine liveDotRefreshCoroutine;

    void Awake()
    {
        HideGlobe();
    }

    // Hook this to the Overview tab button's OnClick.
    public void ShowGlobe()
    {
        if (globeRoot != null) globeRoot.SetActive(true);
        if (globeCamera != null) globeCamera.SetActive(true);
        if (dotSpawner != null) dotSpawner.PopulateFromLeaderboard();

        if (liveDotRefreshCoroutine != null) StopCoroutine(liveDotRefreshCoroutine);
        liveDotRefreshCoroutine = StartCoroutine(LiveDotRefreshLoop());

        StartCoroutine(ForceRenderNextFrame());
    }

    IEnumerator LiveDotRefreshLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(dotRefreshInterval);
            if (dotSpawner != null) dotSpawner.PopulateFromLeaderboard();
        }
    }

    // Waiting a frame before forcing the render gives Unity's engine loop
    // a chance to fully initialize the just-activated Camera component —
    // calling Render() synchronously in the same call as SetActive(true)
    // can fire before the camera is actually ready, which is what made
    // the globe only appear starting from the second activation onward.
    IEnumerator ForceRenderNextFrame()
    {
        yield return null;

        if (globeCamera != null)
        {
            Camera cam = globeCamera.GetComponent<Camera>();
            if (cam != null) cam.Render();
        }
    }

    // Hook this to the Global/Local tab buttons' OnClick, AND to your
    // CancelButton_Leaderboard's OnClick (as an extra entry alongside
    // LeaderboardUI.CloseLeaderboard()) so the globe resets whenever you
    // leave this tab or close the panel entirely.
    public void HideGlobe()
    {
        if (liveDotRefreshCoroutine != null) { StopCoroutine(liveDotRefreshCoroutine); liveDotRefreshCoroutine = null; }
        if (globeRoot != null) globeRoot.SetActive(false);
        if (globeCamera != null) globeCamera.SetActive(false);
        if (infoPopup != null) infoPopup.Hide();
    }
}
