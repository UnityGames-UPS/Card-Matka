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

    // ── Tracks whether we were playing when focus was lost ───────────────────
    private bool _wasPlayingOnFocusLost = false;

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

    // ── Tab-switch / app-pause handlers ─────────────────────────────────────
    // OnApplicationFocus fires on both mobile and desktop browser tab switches.
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            // Lost focus — pause if playing so frames don't queue up
            if (currentAnimationState == ImageState.PLAYING)
            {
                _wasPlayingOnFocusLost = true;
                PauseAnimationInternal();
            }
            else
            {
                _wasPlayingOnFocusLost = false;
            }
        }
        else
        {
            // Regained focus — resume only if we were playing before
            if (_wasPlayingOnFocusLost)
            {
                _wasPlayingOnFocusLost = false;
                ResumeAnimation();
            }
        }
    }

    // OnApplicationPause covers mobile home-button presses etc.
    private void OnApplicationPause(bool isPaused)
    {
        OnApplicationFocus(!isPaused);
    }

    // ── Internal pause that doesn't reset _wasPlayingOnFocusLost ────────────
    private void PauseAnimationInternal()
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

    // ── Core animation coroutine (replaces Invoke chain) ────────────────────
    private IEnumerator AnimationCoroutine()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(delayBetweenAnimation);

            SetTextureOfIndex();
            indexOfTexture++;

            if (indexOfTexture >= textureArray.Count)
            {
                indexOfTexture = 0;
                if (doLoopAnimation)
                {
                    if (delayBetweenLoop > 0f)
                        yield return new WaitForSecondsRealtime(delayBetweenLoop);
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
        PauseAnimationInternal();
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