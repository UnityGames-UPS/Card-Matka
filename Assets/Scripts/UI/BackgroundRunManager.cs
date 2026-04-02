using System.Collections;
using UnityEngine;
using System.Runtime.InteropServices;

/// <summary>
/// Attach to any persistent GameObject (e.g. the one holding GameManager).
///
/// Drives the three-layer background-run system:
///   1. Time.maximumDeltaTime cap — prevents WaitForSeconds burst catch-up
///      if the engine does stall for any reason.
///   2. JS Web Worker + MessageChannel pump — keeps Unity's Emscripten main
///      loop ticking at full speed in a background tab by bypassing rAF.
///   3. Web Lock — belt-and-suspenders hint to browsers that the tab is active.
///
/// The old AcquireWakeLock / ReleaseWakeLock / HeartbeatWakeLock entry points
/// have been replaced by StartBackgroundRun / StopBackgroundRun which do all
/// three layers in one call.
/// </summary>
public class BackgroundRunManager : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void StartBackgroundRun();

    [DllImport("__Internal")]
    private static extern void StopBackgroundRun();
#endif

    [Tooltip("Maximum seconds Unity treats a single frame as taking.\n" +
             "Prevents WaitForSeconds coroutines from firing in a burst if\n" +
             "the engine stalls. 0.05 = one frame at 20 fps max catch-up.")]
    [SerializeField] private float maxDeltaTimeCap = 0.05f;

    private void Awake()
    {
        // Cap deltaTime — safety net for any residual stall
        Time.maximumDeltaTime = maxDeltaTimeCap;

#if UNITY_WEBGL && !UNITY_EDITOR
        StartBackgroundRun();
#endif
    }

    private void OnDestroy()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        StopBackgroundRun();
#endif
    }
}