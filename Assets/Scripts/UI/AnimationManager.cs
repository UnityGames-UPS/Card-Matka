using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using TMPro;
using NUnit.Framework;

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
    internal bool isCardWinAnimationComplete = false;

    [Header("Spin Settings")]
    [SerializeField] private float SpinDuration = 6f;

    private string bonusposition;

    // ── Active spin coroutine handle so it can be cancelled on round reset ──
    private Coroutine _spinCoroutine;

    internal void ShowBonusCards(string betOption, int bonusMultiplier)
    {
        Debug.Log("Bet locked! Reveal bonus cards.");
        bonusposition = betOption;
        foreach (var cardObject in CardObjects)
        {
            Button cardButton = cardObject.GetComponent<Button>();
            if (betOption == cardButton.GetComponent<BetButton>().betOption)
            {
                GameObject bonusAnimationObject = cardObject.transform.GetChild(7).gameObject;
                bonusAnimationObject.GetComponentInChildren<TMP_Text>().text = $"{bonusMultiplier}x";
                bonusAnimationObject.SetActive(true);
                bonusAnimationObject.transform.localScale = Vector3.one * 5f;
                bonusAnimationObject.transform.DOScale(1f, 0.6f);
            }
        }
    }

    internal void WheelAnimation(string card, string suit)
    {
        ResetAnimationUI();

        if (_spinCoroutine != null)
        {
            StopCoroutine(_spinCoroutine);
            _spinCoroutine = null;
        }
        _spinCoroutine = StartCoroutine(SpinWheels(card, suit));
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

        OuterWheel_Rect.localRotation = Quaternion.identity;
        InnerWheel_Rect.localRotation = Quaternion.identity;

        float outerFinalAngle = -(ExtraSpins * 360f + outerIndex * SegmentDeg);
        float innerFinalAngle = (ExtraSpins * 360f + innerIndex * SegmentDeg);

        float outerSpinDuration = SpinDuration + 2f;
        float innerSpinDuration = SpinDuration + 4f;

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

        yield return new WaitForSeconds(outerSpinDuration);
        OuterWheel_Rect.DORotate(
            new Vector3(0f, 0f, -(outerIndex * SegmentDeg)), 0.15f
        ).SetEase(Ease.OutSine);

        yield return new WaitForSeconds(2f);
        InnerWheel_Rect.DORotate(
            new Vector3(0f, 0f, (innerIndex * SegmentDeg)), 0.15f
        ).SetEase(Ease.OutSine);

        yield return new WaitForSeconds(0.2f);
        audioManager.StopGameAudio();

        yield return new WaitForSeconds(0.2f);
        WheelResultEffects(card, suit);
        ResultCardAnimation(card, suit);
        winHistoryController.OnResult(card, suit);

        _spinCoroutine = null;
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
        PlayResultAudio(card, suit);
        StartCoroutine(ResultCardAnimationCoroutine(card, suit));
    }

    private void PlayResultAudio(string card, string suit)
    {
        if (suit == "spades")
        {
            switch (card)
            {
                case "K": audioManager.PlayKingSpade(); break;
                case "Q": audioManager.PlayQueenSpade(); break;
                case "J": audioManager.PlayJackSpade(); break;
                default: return;
            }
        }
        if (suit == "hearts")
        {
            switch (card)
            {
                case "K": audioManager.PlayKingHeart(); break;
                case "Q": audioManager.PlayQueenHeart(); break;
                case "J": audioManager.PlayJackHeart(); break;
                default: return;
            }
        }
        if (suit == "clubs")
        {
            switch (card)
            {
                case "K": audioManager.PlayKingClub(); break;
                case "Q": audioManager.PlayQueenClub(); break;
                case "J": audioManager.PlayJackClub(); break;
                default: return;
            }
        }
        if (suit == "diamonds")
        {
            switch (card)
            {
                case "K": audioManager.PlayKingDiamond(); break;
                case "Q": audioManager.PlayQueenDiamond(); break;
                case "J": audioManager.PlayJackDiamond(); break;
                default: return;
            }
        }
    }

    private IEnumerator ResultCardAnimationCoroutine(string card, string suit)
    {
        isCardWinAnimationComplete = false;

        string opBetOption   = $"{card}_{suit}";
        string mainBetOption = card;
        string suitCapitalised = char.ToUpper(suit[0]) + suit.Substring(1);
        string sideBetOption = $"specific_{suitCapitalised}";

        var playerWonSlots = new HashSet<string>();
        if (betManager.slotTotals.ContainsKey(opBetOption))   playerWonSlots.Add(opBetOption);
        if (betManager.slotTotals.ContainsKey(mainBetOption)) playerWonSlots.Add(mainBetOption);
        if (betManager.slotTotals.ContainsKey(sideBetOption)) playerWonSlots.Add(sideBetOption);

        bool isWinner = playerWonSlots.Count > 0;

        foreach (var cardObject in CardObjects)
        {
            BetButton betButton = cardObject.GetComponent<Button>().GetComponent<BetButton>();

            bool isCardWin = betButton.betOption == opBetOption
                          || betButton.betOption == mainBetOption
                          || betButton.betOption == sideBetOption;

            if (!isCardWin) continue;

            bool playerBetOnThisSlot = playerWonSlots.Contains(betButton.betOption);

            if (isWinner && playerBetOnThisSlot)
            {
                betButton.WinAnimationObject.SetActive(true);
                ImageAnimation winAnimation = betButton.WinAnimationObject.GetComponent<ImageAnimation>();
                winAnimation.doLoopAnimation = false;
                winAnimation.AnimationSpeed = 95f;
                winAnimation.rendererDelegate = betButton.WinAnimationObject.GetComponent<Image>();
                winAnimation.textureArray = new List<Sprite>(RayEffectAnimationSprites);
                winAnimation.ResetAnimationState();
                winAnimation.StartAnimation();

                GameObject GreenImage = cardObject.transform.GetChild(0).gameObject;
                GreenImage.SetActive(true);

                yield return null;
            }
            else
            {
                GameObject winAnimationObject = cardObject.transform.GetChild(4).gameObject;
                winAnimationObject.SetActive(true);
                ImageAnimation winAnimation = winAnimationObject.GetComponent<ImageAnimation>();
                winAnimation.AnimationSpeed = 95f;
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

        yield return new WaitForSeconds(0.7f);
        isCardWinAnimationComplete = true;

        if (!string.IsNullOrEmpty(bonusposition))
        {
            TriggerBonusOnWinningSlots(opBetOption, mainBetOption, sideBetOption);
        }
    }

    private void TriggerBonusOnWinningSlots(string opBetOption, string mainBetOption, string sideBetOption)
    {
        var winningBetOptions = new HashSet<string>();
        if (betManager.slotTotals.ContainsKey(opBetOption))   winningBetOptions.Add(opBetOption);
        if (betManager.slotTotals.ContainsKey(mainBetOption)) winningBetOptions.Add(mainBetOption);
        if (betManager.slotTotals.ContainsKey(sideBetOption)) winningBetOptions.Add(sideBetOption);

        if (winningBetOptions.Count == 0)
        {
            Debug.Log("AnimationManager: No winning bets — clearing bonus symbols.");
            ResetBonusImage();
            return;
        }

        bool anyBonusTriggered = false;

        foreach (var cardObject in CardObjects)
        {
            BetButton betButton = cardObject.GetComponent<Button>().GetComponent<BetButton>();
            bool isWinningSlot = winningBetOptions.Contains(betButton.betOption);
            bool isBonusSlot   = betButton.betOption == bonusposition;

            if (isWinningSlot && isBonusSlot)
            {
                Debug.Log($"AnimationManager: Triggering bonus animation on slot [{betButton.betOption}]");
                BonusAnimation(betButton.betOption);
                anyBonusTriggered = true;
            }
        }

        if (!anyBonusTriggered)
        {
            Debug.Log("AnimationManager: Player did not bet on the bonus slot — clearing bonus symbols.");
            ResetBonusImage();
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
            cardObject.transform.GetChild(4).gameObject.SetActive(false);
        }
    }

    internal void BonusAnimation(string betOption)
    {
        foreach (var cardObject in CardObjects)
        {
            Button cardButton = cardObject.GetComponent<Button>();
            if (betOption == cardButton.GetComponent<BetButton>().betOption)
            {
                GameObject bonusAnimationObject = cardObject.transform.GetChild(7).gameObject;
                ImageAnimation bonusAnimation = bonusAnimationObject.GetComponent<ImageAnimation>();
                bonusAnimation.AnimationSpeed = 45f;
                bonusAnimation.rendererDelegate = bonusAnimationObject.GetComponent<Image>();
                bonusAnimation.textureArray = new List<Sprite>(BonusAnimationSprites);
                bonusAnimation.ResetAnimationState();
                bonusAnimation.StartAnimation();
            }
        }
    }

    internal void ResetBonusImage()
    {
        foreach (var cardObject in CardObjects)
        {
            cardObject.transform.GetChild(7).gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Called at the start of every new round.
    /// Stops the wheel spin coroutine, kills all DOTween tweens, stops every
    /// ImageAnimation component on every card slot, and hides all result/bonus
    /// overlays so the board is completely clean for the next betting phase.
    /// </summary>
    internal void ResetAnimations()
    {
        // ── 1. Kill any in-flight spin coroutine ─────────────────────────────
        if (_spinCoroutine != null)
        {
            StopCoroutine(_spinCoroutine);
            _spinCoroutine = null;
        }

        // ── 2. Reset wheel positions ──────────────────────────────────────────
        OuterWheel_Rect.DOKill(true);
        InnerWheel_Rect.DOKill(true);

        // ── 3. Reset result overlay ───────────────────────────────────────────
        GreenObject.SetActive(false);
        SymbolGreenObject.GetComponent<Image>().sprite = null;
        TextGreenObject.GetComponent<Image>().sprite = null;

        // ── 4. Reset every card slot ──────────────────────────────────────────
        foreach (var cardObject in CardObjects)
        {
            // Green highlight
            cardObject.transform.GetChild(0).gameObject.SetActive(false);

            // Win-coin animation (child 4) — stop ImageAnimation then hide
            GameObject winCoinObj = cardObject.transform.GetChild(4).gameObject;
            StopAndHideImageAnimation(winCoinObj);

            // Bonus animation (child 7) — stop ImageAnimation then hide
            GameObject bonusObj = cardObject.transform.GetChild(7).gameObject;
            StopAndHideImageAnimation(bonusObj);

            // WinAnimationObject on the BetButton — stop ImageAnimation then hide
            BetButton betButton = cardObject.GetComponent<Button>().GetComponent<BetButton>();
            if (betButton != null && betButton.WinAnimationObject != null)
                StopAndHideImageAnimation(betButton.WinAnimationObject);
        }

        // ── 5. Clear bonus state ──────────────────────────────────────────────
        bonusposition = null;
        isCardWinAnimationComplete = false;
    }

    /// <summary>
    /// Stops the ImageAnimation on <paramref name="obj"/> (if present) and
    /// deactivates the GameObject.  Safe to call on already-inactive objects.
    /// </summary>
    private void StopAndHideImageAnimation(GameObject obj)
    {
        if (obj == null) return;

        ImageAnimation anim = obj.GetComponent<ImageAnimation>();
        if (anim != null)
            anim.ResetAnimationState();   // stops coroutine + resets to frame 0

        obj.SetActive(false);
    }
}