using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Controls BOTH spinning wheels visible in the game UI.
///
/// Symbol Wheel  – outer ring showing card suits (♣ ♦ ♠ ♥)
/// Letter Wheel  – inner ring showing card faces (J Q K)
///
/// The wheel uses Unity's RectTransform rotation on Z-axis.
/// Each wheel has 12 equal segments (30° each).
///
/// Segment order clockwise from top (0°):
///   Letter wheel : K  J  Q  K  J  Q  K  J  Q  K  J  Q   (4 of each)
///   Symbol wheel : ♦  ♠  ♥  ♣  ♦  ♠  ♥  ♣  ♦  ♠  ♥  ♣  (3 of each)
///
/// To land on a segment, we rotate so that segment aligns with the pointer
/// (top/12-o'clock position). The pointer arrow sits outside the wheel.
/// </summary>
public class WheelController : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────────
    [Header("Wheel Transforms")]
    [SerializeField] private RectTransform letterWheelRT;   // J/Q/K wheel image
    [SerializeField] private RectTransform symbolWheelRT;   // suit symbol wheel image

    [Header("Result Indicator")]
    [SerializeField] private GameObject pointerArrow;       // Arrow at top of wheel

    [Header("Bonus UI")]
    [SerializeField] private GameObject      bonusContainer;
    [SerializeField] private TMP_Text        bonusPositionText;
    [SerializeField] private TMP_Text        bonusMultiplierText;
    [SerializeField] private ImageAnimation  bonusAnimComponent;

    [Header("Spin Config")]
    [SerializeField] private float minSpinDuration  = 2.5f;
    [SerializeField] private float maxSpinDuration  = 4.0f;
    [SerializeField] private int   extraFullRotations = 5;    // extra 360° spins before landing

    [Header("Audio (optional)")]
    [SerializeField] private AudioSource spinAudio;
    [SerializeField] private AudioSource resultAudio;

    // ── Segment maps ──────────────────────────────────────────────────────────
    // 12 segments, each 30°. Index 0 = top (12 o'clock), going clockwise.
    // These arrays define what each segment shows.

    private static readonly string[] LetterSegments = new string[12]
    {
        "K", "J", "Q",
        "K", "J", "Q",
        "K", "J", "Q",
        "K", "J", "Q"
    };

    private static readonly string[] SymbolSegments = new string[12]
    {
        "diamonds", "spades", "hearts", "clubs",
        "diamonds", "spades", "hearts", "clubs",
        "diamonds", "spades", "hearts", "clubs"
    };

    // Segment degree per index (clockwise from top = 0)
    private const float SegmentDeg = 30f;  // 360 / 12

    // ── State ─────────────────────────────────────────────────────────────────
    private bool    isSpinning = false;
    private float   currentLetterAngle = 0f;
    private float   currentSymbolAngle = 0f;

    // Bonus position (1-12) from server
    private int    bonusPosition = -1;

    // ── Coroutine handles ─────────────────────────────────────────────────────
    private Coroutine spinCoroutine;

    // ═════════════════════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ═════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (bonusContainer) bonusContainer.SetActive(false);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  PUBLIC API  (called by GameManager)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Show bonus position highlight before card reveal.</summary>
    public void ShowBonus(int position, double multiplier)
    {
        bonusPosition = position;

        if (bonusContainer)
        {
            bonusContainer.SetActive(true);
            if (bonusPositionText)   bonusPositionText.text   = $"Pos {position}";
            if (bonusMultiplierText) bonusMultiplierText.text = $"x{multiplier}";
            if (bonusAnimComponent)  bonusAnimComponent.StartAnimation();

            // Highlight the wheel segment at bonusPosition
            HighlightBonusSegment(position);
        }
    }

    /// <summary>
    /// Spin both wheels to land on the winning card + suit combination.
    /// Called when game:card_result arrives.
    /// </summary>
    public void SpinToResult(string resultCard, string resultSuit)
    {
        if (isSpinning) return;

        int letterIndex = FindSegmentIndex(LetterSegments, resultCard);
        int symbolIndex = FindSegmentIndex(SymbolSegments, resultSuit);

        if (letterIndex < 0 || symbolIndex < 0)
        {
            Debug.LogError($"[WheelController] Could not find segment: card={resultCard}, suit={resultSuit}");
            return;
        }

        float targetLetterAngle = SegmentIndexToAngle(letterIndex);
        float targetSymbolAngle = SegmentIndexToAngle(symbolIndex);

        float spinDuration = UnityEngine.Random.Range(minSpinDuration, maxSpinDuration);

        if (spinCoroutine != null) StopCoroutine(spinCoroutine);
        spinCoroutine = StartCoroutine(SpinBothWheels(
            targetLetterAngle, targetSymbolAngle, spinDuration, resultCard, resultSuit));
    }

    /// <summary>Reset wheels to starting position between rounds.</summary>
    public void ResetWheel()
    {
        if (isSpinning)
        {
            if (spinCoroutine != null) StopCoroutine(spinCoroutine);
            isSpinning = false;
        }

        if (letterWheelRT) letterWheelRT.DORotate(Vector3.zero, 0.4f);
        if (symbolWheelRT) symbolWheelRT.DORotate(Vector3.zero, 0.4f);

        currentLetterAngle = 0f;
        currentSymbolAngle = 0f;

        if (bonusContainer) bonusContainer.SetActive(false);
        bonusPosition = -1;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  SPIN COROUTINE
    // ═════════════════════════════════════════════════════════════════════════

    private IEnumerator SpinBothWheels(float targetLetterAngle, float targetSymbolAngle,
                                       float duration, string resultCard, string resultSuit)
    {
        isSpinning = true;
        if (spinAudio) spinAudio.Play();

        // Each wheel adds extra_full_rotations * 360 degrees of spin before landing
        float extraDeg = extraFullRotations * 360f;

        // Current rotation to start from
        float fromLetter = currentLetterAngle;
        float fromSymbol = currentSymbolAngle;

        // Normalize current angles
        fromLetter = fromLetter % 360f;
        fromSymbol = fromSymbol % 360f;

        // Target = extra full spins PLUS the precise landing angle
        // We always spin clockwise (negative Z in Unity UI)
        // We need to go PAST current angle by extraDeg, landing on targetAngle
        float toLetter = CalculateSpinTarget(fromLetter, targetLetterAngle, extraDeg);
        float toSymbol = CalculateSpinTarget(fromSymbol, targetSymbolAngle, extraDeg);

        float elapsed = 0f;

        // We use DOTween for smooth easing on both wheels simultaneously
        bool letterDone = false;
        bool symbolDone = false;

        if (letterWheelRT)
        {
            letterWheelRT.DORotate(new Vector3(0, 0, -toLetter), duration, RotateMode.FastBeyond360)
                .SetEase(Ease.InOutCubic)
                .OnComplete(() =>
                {
                    currentLetterAngle = targetLetterAngle;
                    letterDone = true;
                });
        }
        else letterDone = true;

        if (symbolWheelRT)
        {
            symbolWheelRT.DORotate(new Vector3(0, 0, -toSymbol), duration, RotateMode.FastBeyond360)
                .SetEase(Ease.InOutCubic)
                .OnComplete(() =>
                {
                    currentSymbolAngle = targetSymbolAngle;
                    symbolDone = true;
                });
        }
        else symbolDone = true;

        // Wait for both to finish
        while (!letterDone || !symbolDone)
            yield return null;

        isSpinning = false;

        if (spinAudio)  spinAudio.Stop();
        if (resultAudio) resultAudio.Play();

        // Flash result on pointer
        FlashResultPointer(resultCard, resultSuit);

        Debug.Log($"[WheelController] Landed on: {resultCard} of {resultSuit}");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  HIGHLIGHT BONUS SEGMENT
    // ═════════════════════════════════════════════════════════════════════════

    private void HighlightBonusSegment(int position)
    {
        // position is 1-based from server; convert to 0-based segment index
        int idx = (position - 1) % 12;
        float angleDeg = idx * SegmentDeg;

        // Gently rotate the letter wheel so the bonus segment faces the pointer
        if (letterWheelRT)
        {
            letterWheelRT.DORotate(new Vector3(0, 0, -angleDeg), 1.0f)
                .SetEase(Ease.OutCubic);
            currentLetterAngle = angleDeg;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  POINTER FLASH
    // ═════════════════════════════════════════════════════════════════════════

    private void FlashResultPointer(string card, string suit)
    {
        if (pointerArrow == null) return;

        pointerArrow.transform.DOKill();
        pointerArrow.transform.localScale = Vector3.one;
        pointerArrow.transform.DOPunchScale(Vector3.one * 0.3f, 0.4f, 5, 0.5f);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  MATH HELPERS
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Given the segment index (0-11), return the rotation angle (degrees)
    /// that puts that segment at the top (pointer position).
    /// Segment 0 starts at 0°, segment 1 at 30°, etc.
    /// </summary>
    private float SegmentIndexToAngle(int index)
    {
        return index * SegmentDeg;
    }

    /// <summary>
    /// Return first index in the array where entry matches value (case-insensitive).
    /// Returns -1 if not found.
    /// </summary>
    private int FindSegmentIndex(string[] array, string value)
    {
        for (int i = 0; i < array.Length; i++)
            if (string.Equals(array[i], value, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    /// <summary>
    /// Calculate the total degrees to rotate (from current) so we land on target
    /// after the extra full spins, always spinning in the positive (clockwise) direction.
    /// </summary>
    private float CalculateSpinTarget(float currentAngle, float targetAngle, float extraDeg)
    {
        float diff = targetAngle - currentAngle;
        if (diff < 0) diff += 360f;   // ensure we spin forward
        return currentAngle + extraDeg + diff;
    }
}