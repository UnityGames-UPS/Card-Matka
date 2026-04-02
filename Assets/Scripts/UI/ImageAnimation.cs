using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ImageAnimation : MonoBehaviour
{
    public enum ImageState
    {
        NONE,
        PLAYING,
        PAUSED
    }

    public static ImageAnimation Instance;

    public List<Sprite> textureArray;

    public Image rendererDelegate;

    public bool useSharedMaterial = true;

    public bool doLoopAnimation = true;
    [SerializeField] private bool StartOnAwake;
    [SerializeField] private bool StartonEnable;

    [HideInInspector]
    public ImageState currentAnimationState;

    private int indexOfTexture;

    private float idealFrameRate = 0.0416666679f;

    private float delayBetweenAnimation;

    public float AnimationSpeed = 5f;

    public float delayBetweenLoop;
    internal bool isAnimationDone = false;

    // ── Coroutine handle so we can stop it cleanly ───────────────────────────
    private Coroutine _animCoroutine;

    // NOTE: OnApplicationFocus and OnApplicationPause have been intentionally
    // removed. "Run In Background" is enabled in Player Settings, so Unity
    // continues ticking in background tabs. Those callbacks were pausing the
    // coroutine on tab-switch and resuming on return, which caused all the
    // queued WaitForSecondsRealtime delays to fire at once — producing the
    // "animation burst" on tab focus. With them gone the animation simply
    // keeps running continuously regardless of tab focus, which is correct.

    private void Awake()
    {
        if (Instance == null)
            Instance = this;

        if (StartOnAwake)
            StartAnimation();
    }

    private void OnEnable()
    {
        if (StartonEnable)
            StartAnimation();
    }

    private void OnDisable()
    {
        StopAnimation();
    }

    // ── Core animation coroutine ─────────────────────────────────────────────
    // Uses WaitForSeconds (scaled time) instead of WaitForSecondsRealtime.
    // WaitForSecondsRealtime accumulates real-world debt while the browser
    // throttles the tab, so when you return it fires in a rapid burst to
    // "catch up". WaitForSeconds is driven by Unity's Time.deltaTime which
    // does NOT accumulate during throttled frames, so playback resumes at
    // the normal rate with no burst.
    private IEnumerator AnimationCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(delayBetweenAnimation);

            SetTextureOfIndex();
            indexOfTexture++;

            if (indexOfTexture >= textureArray.Count)
            {
                indexOfTexture = 0;
                if (doLoopAnimation)
                {
                    if (delayBetweenLoop > 0f)
                        yield return new WaitForSeconds(delayBetweenLoop);
                    // continue looping
                }
                else
                {
                    isAnimationDone = true;
                    currentAnimationState = ImageState.NONE;
                    _animCoroutine = null;
                    yield break;
                }
            }
        }
    }

    public void StartAnimation()
    {
        indexOfTexture = 0;
        isAnimationDone = false;

        if (currentAnimationState == ImageState.NONE)
        {
            RevertToInitialState();
            delayBetweenAnimation = idealFrameRate * (float)textureArray.Count / AnimationSpeed;
            currentAnimationState = ImageState.PLAYING;

            if (_animCoroutine != null)
                StopCoroutine(_animCoroutine);
            _animCoroutine = StartCoroutine(AnimationCoroutine());
        }
    }

    public void PauseAnimation()
    {
        if (currentAnimationState == ImageState.PLAYING)
        {
            if (_animCoroutine != null)
            {
                StopCoroutine(_animCoroutine);
                _animCoroutine = null;
            }
            currentAnimationState = ImageState.PAUSED;
        }
    }

    public void ResumeAnimation()
    {
        if (currentAnimationState == ImageState.PAUSED)
        {
            currentAnimationState = ImageState.PLAYING;
            if (_animCoroutine != null)
                StopCoroutine(_animCoroutine);
            _animCoroutine = StartCoroutine(AnimationCoroutine());
        }
    }

    public void StopAnimation()
    {
        if (currentAnimationState != ImageState.NONE)
        {
            if (_animCoroutine != null)
            {
                StopCoroutine(_animCoroutine);
                _animCoroutine = null;
            }
            if (textureArray != null && textureArray.Count > 0 && rendererDelegate != null)
                rendererDelegate.sprite = textureArray[0];
            currentAnimationState = ImageState.NONE;
        }
    }

    public void RevertToInitialState()
    {
        indexOfTexture = 0;
        SetTextureOfIndex();
    }

    private void SetTextureOfIndex()
    {
        if (rendererDelegate != null && textureArray != null && indexOfTexture < textureArray.Count)
            rendererDelegate.sprite = textureArray[indexOfTexture];
    }

    internal void ResetAnimationState()
    {
        if (_animCoroutine != null)
        {
            StopCoroutine(_animCoroutine);
            _animCoroutine = null;
        }
        RevertToInitialState();
        currentAnimationState = ImageState.NONE;
        isAnimationDone = false;
    }
}