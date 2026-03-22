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
    [SerializeField] private UiManager uiManager;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private BetManager betManager;
    [SerializeField] private WinHistoryController winHistoryController;
    [SerializeField] private AudioManager audioManager;

    private static readonly string[] OuterLayout = { "Q", "J", "K", "Q", "J", "K", "Q", "J", "K", "Q", "J", "K" };
    private static readonly string[] InnerLayout = { "diamonds", "spades", "hearts", "clubs", "spades", "hearts", "diamonds", "clubs", "spades", "diamonds", "hearts", "clubs" };

    private const float SegmentDeg = 30f; // 360 / 12 segments
    private const float ExtraSpins = 3f;  // full rotations added before settling

    [Header("Spin Settings")]
    [SerializeField] private float SpinDuration = 6f;


    internal void ShowBonusCards(string betOption, int bonusMultiplier)
    {
        Debug.Log("Bet locked! Reveal bonus cards.");
        foreach (var cardObject in CardObjects)
        {
            Button cardButton = cardObject.GetComponent<Button>();
            if (betOption == cardButton.GetComponent<BetButton>().betOption)
            {
                GameObject bonusAnimationObject = cardObject.transform.GetChild(6).gameObject;
                bonusAnimationObject.GetComponentInChildren<TMP_Text>().text = $"{bonusMultiplier}x";
                bonusAnimationObject.SetActive(true);
                bonusAnimationObject.transform.localScale = Vector3.one * 3f;
                bonusAnimationObject.transform.DOScale(1f, 0.3f);
                // ImageAnimation bonusAnimation = bonusAnimationObject.GetComponent<ImageAnimation>();
                // bonusAnimation.AnimationSpeed = 35f;
                // bonusAnimation.rendererDelegate = bonusAnimationObject.GetComponent<Image>();
                // bonusAnimation.textureArray = new List<Sprite>(BonusAnimationSprites);
                // bonusAnimation.ResetAnimationState();
                // bonusAnimation.StartAnimation();
            }
        }
    }

    internal void WheelAnimation(string card, string suit)
    {
        ResetAnimationUI();
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

        yield return new WaitForSeconds(0.7f);

        OuterWheel_Rect.DOKill();
        InnerWheel_Rect.DOKill();

        // NO windback — reference video goes straight into spin
        // Reset to identity so rotation math is clean
        OuterWheel_Rect.localRotation = Quaternion.identity;
        InnerWheel_Rect.localRotation = Quaternion.identity;

        float outerFinalAngle = -(ExtraSpins * 360f + outerIndex * SegmentDeg);
        float innerFinalAngle = (ExtraSpins * 360f + innerIndex * SegmentDeg);

        float outerSpinDuration = SpinDuration + 2f;
        float innerSpinDuration = SpinDuration + 4f;

        // OutSine = cosine curve — starts fast, bleeds speed naturally,
        // arrives gently. Closest mathematical match to real friction decay.
        audioManager.PlayWheelSpin();
        OuterWheel_Rect.DORotate(
            new Vector3(0f, 0f, outerFinalAngle),
            outerSpinDuration,
            RotateMode.FastBeyond360
        ).SetEase(Ease.OutSine);

        InnerWheel_Rect.DORotate(
            new Vector3(0f, 0f, innerFinalAngle),
            innerSpinDuration,
            RotateMode.FastBeyond360
        ).SetEase(Ease.OutSine);

        // Wait for outer to land and do a tiny settle
        yield return new WaitForSeconds(outerSpinDuration);
        OuterWheel_Rect.DORotate(
            new Vector3(0f, 0f, -(outerIndex * SegmentDeg)), 0.15f
        ).SetEase(Ease.OutSine);

        // Wait for inner to land
        yield return new WaitForSeconds(2f);
        InnerWheel_Rect.DORotate(
            new Vector3(0f, 0f, (innerIndex * SegmentDeg)), 0.15f
        ).SetEase(Ease.OutSine);

        yield return new WaitForSeconds(0.2f);
        audioManager.StopGameAudio();
        ResetBonusImage();

        yield return new WaitForSeconds(0.2f);
        WheelResultEffects(card, suit);
        ResultCardAnimation(card, suit);
        winHistoryController.OnResult(card, suit);
    }

    private void WheelResultEffects(string card, string suit)
    {
        Sprite textSprite = card switch
        {
            "K" => K_Text,
            "Q" => Q_Text,
            "J" => J_Text,
            _ => null
        };
        Sprite symbolSprite = suit switch
        {
            "diamonds" => Diamonds_Symbol,
            "spades" => Spades_Symbol,
            "hearts" => Hearts_Symbol,
            "clubs" => Clubs_Symbol,
            _ => null
        };

        if (textSprite != null && symbolSprite != null)
        {
            SymbolGreenObject.GetComponent<Image>().sprite = symbolSprite;
            TextGreenObject.GetComponent<Image>().sprite = textSprite;
        }
        GreenObject.SetActive(true);
    }

    private void ResultCardAnimation(string card, string suit)
    {
        Debug.Log($"Result: {card} of {suit}");


        string opBetOption = $"{card}_{suit}";
        string mainBetOption = card;
        string suitCapitalised = char.ToUpper(suit[0]) + suit.Substring(1);
        string sideBetOption = $"specific_{suitCapitalised}";
        bool isWinner = false;

        if (betManager.slotTotals.ContainsKey(opBetOption) || betManager.slotTotals.ContainsKey(sideBetOption) || betManager.slotTotals.ContainsKey(mainBetOption))
        {
            isWinner = true;
        }

        foreach (var cardObject in CardObjects)
        {
            BetButton betButton = cardObject.GetComponent<Button>().GetComponent<BetButton>();
            bool isCardWin = betButton.betOption == opBetOption || betButton.betOption == mainBetOption || betButton.betOption == sideBetOption;

            if (!isCardWin) continue;

            if (isWinner && betButton.betOption == opBetOption)
            {
                betButton.WinAnimationObject.SetActive(true);
                ImageAnimation winAnimation = betButton.WinAnimationObject.GetComponent<ImageAnimation>();
                winAnimation.doLoopAnimation = false;
                winAnimation.AnimationSpeed = 45f;
                winAnimation.rendererDelegate = betButton.WinAnimationObject.GetComponent<Image>();
                winAnimation.textureArray = new List<Sprite>(RayEffectAnimationSprites);
                winAnimation.ResetAnimationState();
                winAnimation.StartAnimation();

                GameObject GreenImage = cardObject.transform.GetChild(0).gameObject;
                GreenImage.SetActive(true);

            }
            else
            {
                GameObject winAnimationObject = cardObject.transform.GetChild(4).gameObject;
                winAnimationObject.SetActive(true);
                ImageAnimation winAnimation = winAnimationObject.GetComponent<ImageAnimation>();
                winAnimation.AnimationSpeed = 45f;
                winAnimation.rendererDelegate = winAnimationObject.GetComponent<Image>();
                winAnimation.textureArray = new List<Sprite>(CardWinCoinAnimationSprites);
                winAnimation.ResetAnimationState();
                GameObject BlackBg = cardObject.transform.GetChild(5).gameObject;
                BlackBg.SetActive(false);
                winAnimation.StartAnimation();

                GameObject GreenImage = cardObject.transform.GetChild(0).gameObject;
                GreenImage.SetActive(true);
            }

        }
    }


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


        OuterWheel_Rect.DOKill(true);
        InnerWheel_Rect.DOKill(true);

        foreach (var cardObject in CardObjects)
        {
            cardObject.transform.GetChild(0).gameObject.SetActive(false);
            //cardObject.transform.GetChild(6).gameObject.SetActive(false); // Bonus animation
            cardObject.transform.GetChild(4).gameObject.SetActive(false); // Win animation
            cardObject.transform.GetChild(5).gameObject.SetActive(true);  // BlackBg — re-enable for next round
        }
    }

    private void ResetBonusImage()
    {
        foreach (var cardObject in CardObjects)
        {
            cardObject.transform.GetChild(6).gameObject.SetActive(false); // Bonus animation
        }
    }

    internal void ResetAnimations()
    {
        foreach (var cardObject in CardObjects)
        {
            cardObject.transform.GetChild(0).gameObject.SetActive(false);
            cardObject.transform.GetChild(6).gameObject.SetActive(false); // Bonus animation
            cardObject.transform.GetChild(4).gameObject.SetActive(false); // Win animation

            BetButton betButton = cardObject.GetComponent<Button>().GetComponent<BetButton>();
            betButton.WinAnimationObject.SetActive(false);
        }
    }
}




// Angular Speed
// │
// │      ____
// │    /      \
// │   /         \
// │  /            \
// │ /                \
// │/                   \____.__
// └──────────────────────────────────────────── Time
// 0%   15%  20%  65%  82%  93% 100%