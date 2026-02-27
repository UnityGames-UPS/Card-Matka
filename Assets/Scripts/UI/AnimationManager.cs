using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using TMPro;

public class AnimationManager : MonoBehaviour
{
    [Header("Wheel Objects")]
    [SerializeField] private RectTransform OuterWheel_Rect;  // J/Q/K wheel
    [SerializeField] private RectTransform InnerWheel_Rect;  // Suits wheel
    [SerializeField] private GameObject GreenObject;
    [SerializeField] private GameObject SymbolGreenObject;
    [SerializeField] private GameObject TextGreenObject;

    [Header("Symbol & Text Objects")]
    [SerializeField] private Sprite K_Text;
    [SerializeField] private Sprite J_Text;
    [SerializeField] private Sprite Q_Text;
    [SerializeField] private Sprite Diamonds_Symbol;
    [SerializeField] private Sprite Spades_Symbol;
    [SerializeField] private Sprite Hearts_Symbol;
    [SerializeField] private Sprite Clubs_Symbol;

    [Header("Animation Sprites")]
    [SerializeField] private Sprite[] BonusAnimationSprites;
    [SerializeField] private Sprite[] RayEffectAnimationSprites;
    [SerializeField] private Sprite[] WinCoinAnimationSprites;
    [SerializeField] private Sprite[] CardWinCoinAnimationSprites;

    [Header("Card Objects")]
    [SerializeField] private List<GameObject> CardObjects;

    [Header("Manager References")]
    [SerializeField] private UiManager   uiManager;
    [SerializeField] private GameManager gameManager;

    // ── Wheel Layout ───────────────────────────────────────────────────────────
    // 12 segments × 30° each. Pointer is fixed at TOP.
    // Outer wheel spins CLOCKWISE  → negative Z rotation
    // Inner wheel spins COUNTER-CLOCKWISE → positive Z rotation
    // To land segment[N] at the top: outer rotates -(N × 30°), inner rotates +(N × 30°)

    private static readonly string[] OuterLayout = { "K","J","Q","K","J","Q","K","J","Q","K","J","Q" };
    private static readonly string[] InnerLayout  = { "diamonds","spades","hearts","clubs","spades","hearts","diamonds","clubs","spades","diamonds","hearts","clubs" };

    private const float SegmentDeg = 30f; // 360 / 12 segments
    private const float ExtraSpins = 5f;  // full rotations added before settling

    [Header("Spin Settings")]
    [SerializeField] private float SpinDuration = 3.5f;


    internal void ShowBonusCards(string betOption, int bonusMultiplier)
    {
        Debug.Log("Bet locked! Reveal bonus cards.");
        foreach (var cardObject in CardObjects)
        {
            Button cardButton = cardObject.GetComponent<Button>();
            if(betOption == cardButton.GetComponent<BetButton>().betOption)
            {
                GameObject bonusAnimationObject = cardObject.transform.GetChild(4).gameObject;
                bonusAnimationObject.GetComponentInChildren<TextMeshPro>().text = $"{bonusMultiplier}x";
                bonusAnimationObject.SetActive(true);
                ImageAnimation bonusAnimation = bonusAnimationObject.GetComponent<ImageAnimation>();
                bonusAnimation.rendererDelegate = bonusAnimationObject.GetComponent<Image>();
                bonusAnimation.textureArray = new List<Sprite>(BonusAnimationSprites);
                bonusAnimation.StartAnimation();
            }
        }
    }

    internal void WheelAnimation(string card, string suit)
    {
        ResetAnimationUI(); // clean up all leftovers from the previous round before spinning
        StartCoroutine(SpinWheels(card, suit));
    }

    private IEnumerator SpinWheels(string card, string suit)
    {
        int outerIndex = GetSegmentIndex(OuterLayout, card);
        int innerIndex = GetSegmentIndex(InnerLayout, suit);

        if (outerIndex < 0 || innerIndex < 0)
        {
            Debug.LogError($"AnimationManager: No segment found for card={card} suit={suit}");
            yield break;
        }

        OuterWheel_Rect.DOKill();
        InnerWheel_Rect.DOKill();
        OuterWheel_Rect.localRotation = Quaternion.identity;
        InnerWheel_Rect.localRotation = Quaternion.identity;


        float outerTarget = -(ExtraSpins * 360f + outerIndex * SegmentDeg);
        float innerTarget =  (ExtraSpins * 360f + innerIndex * SegmentDeg);

        OuterWheel_Rect.DORotate(new Vector3(0f, 0f, outerTarget), SpinDuration, RotateMode.FastBeyond360)
                       .SetEase(Ease.OutCubic);

        InnerWheel_Rect.DORotate(new Vector3(0f, 0f, innerTarget), SpinDuration, RotateMode.FastBeyond360)
                       .SetEase(Ease.OutCubic);

        yield return new WaitForSeconds(SpinDuration + 0.1f);

        OuterWheel_Rect.DORotate(new Vector3(0f, 0f, -(outerIndex * SegmentDeg)), 0.15f).SetEase(Ease.OutSine);
        InnerWheel_Rect.DORotate(new Vector3(0f, 0f,  (innerIndex * SegmentDeg)), 0.15f).SetEase(Ease.OutSine);

        yield return new WaitForSeconds(0.2f);

        // OuterWheel_Rect.DOPunchScale(Vector3.one * 0.07f, 0.35f, 5, 0.5f);
        // InnerWheel_Rect.DOPunchScale(Vector3.one * 0.07f, 0.35f, 5, 0.5f);

        yield return new WaitForSeconds(0.35f);

        WheelResultEffects(card, suit);

        ResultCardAnimation(card, suit);
    }

    private void WheelResultEffects(string card, string suit)
    {
        // Green flash
        //GreenObject.GetComponent<Image>().DOFade(0f, 0.5f).From(1f).OnComplete(() => GreenObject.SetActive(false));

        // Symbol & Text flash
        Sprite textSprite = card switch
        {
            "K" => K_Text,
            "Q" => Q_Text,
            "J" => J_Text,
            _   => null
        };
        Sprite symbolSprite = suit switch
        {
            "diamonds" => Diamonds_Symbol,
            "spades"   => Spades_Symbol,
            "hearts"   => Hearts_Symbol,
            "clubs"    => Clubs_Symbol,
            _          => null
        };

        if (textSprite != null && symbolSprite != null)
        {
            SymbolGreenObject.GetComponent<Image>().sprite = symbolSprite;
            TextGreenObject.GetComponent<Image>().sprite = textSprite;

            // Sequence flashSeq = DOTween.Sequence();
            // flashSeq.Append(SymbolGreenObject.GetComponent<Image>().DOFade(1f, 0.25f).From(0f));
            // flashSeq.Join(TextGreenObject.GetComponent<Image>().DOFade(1f, 0.25f).From(0f));
            // flashSeq.AppendInterval(0.5f);
            // flashSeq.Append(SymbolGreenObject.GetComponent<Image>().DOFade(0f, 0.25f));
            // flashSeq.Join(TextGreenObject.GetComponent<Image>().DOFade(0f, 0.25f));
        }
        GreenObject.SetActive(true);
    }

    private void ResultCardAnimation(string card, string suit)
    {
        Debug.Log($"Result: {card} of {suit}");
        foreach (var cardObject in CardObjects)
        {
            BetButton betButton = cardObject.GetComponent<Button>().GetComponent<BetButton>();
            if(betButton.betOption == $"{card}_{suit}")
            {
                GameObject winAnimationObject = cardObject.transform.GetChild(5).gameObject;
                winAnimationObject.SetActive(true);
                ImageAnimation winAnimation = winAnimationObject.GetComponent<ImageAnimation>();
                winAnimation.AnimationSpeed = 45f;
                winAnimation.rendererDelegate = winAnimationObject.GetComponent<Image>();
                winAnimation.textureArray = new List<Sprite>(CardWinCoinAnimationSprites);
                winAnimation.ResetAnimationState();
                GameObject BlackBg = cardObject.transform.GetChild(6).gameObject;
                BlackBg.SetActive(false);
                winAnimation.StartAnimation();
            }
        }
    }

    //  Helpers

    private int GetSegmentIndex(string[] layout, string value)
    {
        for (int i = 0; i < layout.Length; i++)
            if (layout[i] == value) return i;
        return -1;
    }
    
    private void ResetAnimationUI()
    {
        GreenObject.SetActive(false);
        SymbolGreenObject.GetComponent<Image>().sprite = null;
        TextGreenObject.GetComponent<Image>().sprite = null;

        // Kill any in-progress punch tweens on the wheels and reset their scale,
        // otherwise DOKill mid-punch leaves them at a non-one scale next round
        OuterWheel_Rect.DOKill(true);
        InnerWheel_Rect.DOKill(true);
        // OuterWheel_Rect.localScale = Vector3.one;
        // InnerWheel_Rect.localScale = Vector3.one;

        foreach (var cardObject in CardObjects)
        {
            cardObject.transform.GetChild(4).gameObject.SetActive(false); // Bonus animation
            cardObject.transform.GetChild(5).gameObject.SetActive(false); // Win animation
            cardObject.transform.GetChild(6).gameObject.SetActive(true);  // BlackBg — re-enable for next round
        }
    }
}