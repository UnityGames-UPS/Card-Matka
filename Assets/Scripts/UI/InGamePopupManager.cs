using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using TMPro;
using System;

public class InGamePopupManager : MonoBehaviour
{

    [Header("Managers")]
    [SerializeField] private SocketIOManager socketIOManager;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private UiManager uiManager;
    [SerializeField] private AudioManager audioManager;

    [Header("Level Selection Popup")]
    [SerializeField] private Button LevelSelectionButton;
    [SerializeField] private TMP_Text MinBetText;
    [SerializeField] private TMP_Text MaxBetText;
    [SerializeField] private GameObject LevelSelectionPopup;
    [SerializeField] private Button LevelSelectionCloseButton;
    //[SerializeField] private List<GameObject> BetLimitObjects;


    [SerializeField] private Button CasualButton;
    [SerializeField] private Button NoviceButton;
    [SerializeField] private Button ExpertButton;
    [SerializeField] private Button High_RollerButton;


    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private CanvasGroup scrollbarCG;
    [SerializeField] private float fadeDelay = 1.5f;
    [SerializeField] private float fadeDuration = 0.7f;
    private Tween fadeTween;
    private float lastScrollTime;


    [SerializeField] private BetLimit K;
    [SerializeField] private BetLimit Q;
    [SerializeField] private BetLimit J;
    [SerializeField] private BetLimit Specific_Clubs;
    [SerializeField] private BetLimit Specific_Diamonds;
    [SerializeField] private BetLimit Specific_Hearts;
    [SerializeField] private BetLimit Specific_Spades;
    [SerializeField] private BetLimit K_spades;
    [SerializeField] private BetLimit K_hearts;
    [SerializeField] private BetLimit K_clubs;
    [SerializeField] private BetLimit K_diamonds;
    [SerializeField] private BetLimit Q_spades;
    [SerializeField] private BetLimit Q_hearts;
    [SerializeField] private BetLimit Q_clubs;
    [SerializeField] private BetLimit Q_diamonds;
    [SerializeField] private BetLimit J_spades;
    [SerializeField] private BetLimit J_hearts;
    [SerializeField] private BetLimit J_clubs;
    [SerializeField] private BetLimit J_diamonds;
    [SerializeField] private Button ConfirmButton;

    [Header("Max Bet Popup")]
    [SerializeField] private GameObject MaxBetPopup;

    private GameData gamedata;
    internal bool isHome = false;
    private string currentLevel = "casual";
    private string SelectedButton = "casual";

    private void Start()
    {
        LevelSelectionButton.onClick.AddListener(OpenLevelPopup);
        LevelSelectionCloseButton.onClick.AddListener(CloseLevelPopup);
        CasualButton.onClick.AddListener(() => LevelButtonClicked("casual"));
        NoviceButton.onClick.AddListener(() => LevelButtonClicked("novice"));
        ExpertButton.onClick.AddListener(() => LevelButtonClicked("expert"));
        High_RollerButton.onClick.AddListener(() => LevelButtonClicked("high_roller"));
        ConfirmButton.onClick.AddListener(ConfirmLevelSelection);

        scrollbarCG.alpha = 0f;
        scrollRect.onValueChanged.AddListener(OnScroll);
    }

    private void OpenLevelPopup()
    {
        if (LevelSelectionPopup == null) return;

        LevelSelectionPopup.SetActive(true);
        LevelSelectionPopup.transform.DOKill(false);
        LevelSelectionPopup.transform.localScale = Vector3.zero;
        LevelSelectionPopup.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);

        // Reset scroll to top
        //scrollRect.verticalNormalizedPosition = 1f;

        // Kill any running fade tween and reset scrollbar visibility
        scrollbarCG.DOKill();
        scrollbarCG.alpha = 0f;          // already hidden — no flash
        StartFadeOutRoutine();            // starts the delayed fade (which fades from 0→0, harmless)
    }

    private void CloseLevelPopup()
    {
        if (LevelSelectionPopup == null) return;
        LevelSelectionPopup.transform.DOScale(Vector3.zero, 0.2f)
            .SetEase(Ease.InBack)
            .OnComplete(() => LevelSelectionPopup.SetActive(false));
        SelectedButton = currentLevel;
    }

    private void LevelButtonClicked(string level)
    {
        audioManager.PlayUiButton();
        List<Button> levelButtons = new List<Button> { CasualButton, NoviceButton, ExpertButton, High_RollerButton };
        foreach (Button btn in levelButtons)
        {
            btn.transform.GetChild(0).gameObject.SetActive(false); // Deactivate all highlights
        }

        switch (level)
        {
            case "casual":
                CasualButton.transform.GetChild(0).gameObject.SetActive(true); // Highlight casual button
                SelectedButton = "casual";
                break;
            case "novice":
                NoviceButton.transform.GetChild(0).gameObject.SetActive(true); // Highlight novice button
                SelectedButton = "novice";
                break;
            case "expert":
                ExpertButton.transform.GetChild(0).gameObject.SetActive(true); // Highlight expert button
                SelectedButton = "expert";
                break;
            case "high_roller":
                High_RollerButton.transform.GetChild(0).gameObject.SetActive(true); // Highlight high roller button
                SelectedButton = "high_roller";
                break;
        }
        RefreshBetLimits();

    }

    private void RefreshBetLimits()
    {
        Wagers wagers = gameManager.GetWagers();
        if (wagers == null) return;

        // Get the min_bet_limit for the current level (shared across all bet types)
        double minBet = GetMinBetForLevel(SelectedButton);

        // ── Main Bets ─────
        SetBetLimitUI(K, minBet, wagers.main_bets.K.max_bet_limit);
        SetBetLimitUI(Q, minBet, wagers.main_bets.Q.max_bet_limit);
        SetBetLimitUI(J, minBet, wagers.main_bets.J.max_bet_limit);

        // ── Side Bets ───────
        SetBetLimitUI(Specific_Clubs, minBet, wagers.side_bets.specific_Clubs.max_bet_limit);
        SetBetLimitUI(Specific_Diamonds, minBet, wagers.side_bets.specific_Diamonds.max_bet_limit);
        SetBetLimitUI(Specific_Hearts, minBet, wagers.side_bets.specific_Hearts.max_bet_limit);
        SetBetLimitUI(Specific_Spades, minBet, wagers.side_bets.specific_Spades.max_bet_limit);

        // ── Op Bets ─────────
        SetBetLimitUI(K_spades, minBet, wagers.op_bets.K_spades.max_bet_limit);
        SetBetLimitUI(K_hearts, minBet, wagers.op_bets.K_hearts.max_bet_limit);
        SetBetLimitUI(K_clubs, minBet, wagers.op_bets.K_clubs.max_bet_limit);
        SetBetLimitUI(K_diamonds, minBet, wagers.op_bets.K_diamonds.max_bet_limit);
        SetBetLimitUI(Q_spades, minBet, wagers.op_bets.Q_spades.max_bet_limit);
        SetBetLimitUI(Q_hearts, minBet, wagers.op_bets.Q_hearts.max_bet_limit);
        SetBetLimitUI(Q_clubs, minBet, wagers.op_bets.Q_clubs.max_bet_limit);
        SetBetLimitUI(Q_diamonds, minBet, wagers.op_bets.Q_diamonds.max_bet_limit);
        SetBetLimitUI(J_spades, minBet, wagers.op_bets.J_spades.max_bet_limit);
        SetBetLimitUI(J_hearts, minBet, wagers.op_bets.J_hearts.max_bet_limit);
        SetBetLimitUI(J_clubs, minBet, wagers.op_bets.J_clubs.max_bet_limit);
        SetBetLimitUI(J_diamonds, minBet, wagers.op_bets.J_diamonds.max_bet_limit);
    }

    private double GetMinBetForLevel(string level)
    {
        LevelBetLimit lbl = gameManager.GetLevelBetLimit(); // expose this from GameManager (see below)
        switch (level)
        {
            case "casual": return lbl.casual.min_bet_limit;
            case "novice": return lbl.novice.min_bet_limit;
            case "expert": return lbl.expert.min_bet_limit;
            case "high_roller": return lbl.high_roller.min_bet_limit;
            default: return 0;
        }
    }

    private int GetMaxBetForLevel(MaxBetLimit limit, string level)
    {
        switch (level)
        {
            case "casual": return limit.casual;
            case "novice": return limit.novice;
            case "expert": return limit.expert;
            case "high_roller": return limit.high_roller;
            default: return 0;
        }
    }

    private void SetBetLimitUI(BetLimit betLimit, double minBet, MaxBetLimit maxBetLimit)
    {
        betLimit.MinBetText.text = minBet.ToString();
        float maxbet = GetMaxBetForLevel(maxBetLimit, SelectedButton);
        if (maxbet >= 10000)
        {
            betLimit.MaxBetText.text = (maxbet / 1000f).ToString() + "k";
        }
        else
        {
            betLimit.MaxBetText.text = GetMaxBetForLevel(maxBetLimit, SelectedButton).ToString();
        }
    }

    private void ConfirmLevelSelection()
    {
        audioManager.PlayUiButton();
        //isHome = true;
        currentLevel = SelectedButton;
        StartCoroutine(SwitchLevel());
    }

    private IEnumerator SwitchLevel()
    {
        uiManager.LoadingScreen_Object.GetComponentInChildren<ImageAnimation>().ResetAnimationState();
        uiManager.LoadingScreen_Object.SetActive(true);
        uiManager.LoadingScreen_Object.GetComponentInChildren<ImageAnimation>().StartAnimation();
        CloseLevelPopup();
        SetBetLimit();

        yield return new WaitForSeconds(1f);
        socketIOManager.SendHome();
        yield return new WaitUntil(() => isHome);

        yield return new WaitForSeconds(1f);
        gameManager.SelectLevel(currentLevel);
        yield return new WaitUntil(() => !isHome);


        yield return new WaitUntil(() => gameManager.currentPhase == GameManager.GamePhase.Betting);


        yield return null;

        uiManager.LoadingScreen_Object.SetActive(false);
    }

    internal void SlideInFromLeft(GameObject popup)
    {
        if (popup == null) return;
        popup.SetActive(true);
        RectTransform rect = popup.GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(-Screen.width, rect.anchoredPosition.y);
        rect.DOAnchorPosX(0, 0.5f).SetEase(Ease.OutCubic);
    }

    internal void SetInitialGameData(GameData gameData)
    {
        gamedata = gameData;
        RefreshBetLimits();
        List<Button> levelButtons = new List<Button> { CasualButton, NoviceButton, ExpertButton, High_RollerButton };

        levelButtons[0].transform.GetChild(1).GetComponent<TMP_Text>().text = gameData.levelBetLimit.casual.min_bet_limit.ToString();
        levelButtons[0].transform.GetChild(2).GetComponent<TMP_Text>().text = gameData.levelBetLimit.casual.max_bet_limit.ToString();

        levelButtons[1].transform.GetChild(1).GetComponent<TMP_Text>().text = gameData.levelBetLimit.novice.min_bet_limit.ToString();
        levelButtons[1].transform.GetChild(2).GetComponent<TMP_Text>().text = gameData.levelBetLimit.novice.max_bet_limit.ToString();

        levelButtons[2].transform.GetChild(1).GetComponent<TMP_Text>().text = gameData.levelBetLimit.expert.min_bet_limit.ToString();
        levelButtons[2].transform.GetChild(2).GetComponent<TMP_Text>().text = gameData.levelBetLimit.expert.max_bet_limit.ToString();

        levelButtons[3].transform.GetChild(1).GetComponent<TMP_Text>().text = gameData.levelBetLimit.high_roller.min_bet_limit.ToString();
        float maxlimit = gameData.levelBetLimit.high_roller.max_bet_limit / 1000f;
        Debug.Log(gameData.levelBetLimit.high_roller.max_bet_limit);
        Debug.Log("yo - yo " + maxlimit);
        levelButtons[3].transform.GetChild(2).GetComponent<TMP_Text>().text = maxlimit.ToString() + "k";

        BetLinmitUI(gameData.levelBetLimit.casual.min_bet_limit, gameData.levelBetLimit.casual.max_bet_limit);
    }

    private void SetBetLimit()
    {
        switch (currentLevel)
        {
            case "casual":
                BetLinmitUI(gamedata.levelBetLimit.casual.min_bet_limit, gamedata.levelBetLimit.casual.max_bet_limit);
                break;

            case "novice":
                BetLinmitUI(gamedata.levelBetLimit.novice.min_bet_limit, gamedata.levelBetLimit.novice.max_bet_limit);
                break;

            case "expert":
                BetLinmitUI(gamedata.levelBetLimit.expert.min_bet_limit, gamedata.levelBetLimit.expert.max_bet_limit);
                break;

            case "high_roller":
                BetLinmitUI(gamedata.levelBetLimit.high_roller.min_bet_limit, gamedata.levelBetLimit.high_roller.max_bet_limit);
                break;
        }
    }

    private void BetLinmitUI(float minBet, float maxBet)
    {
        MinBetText.text = minBet.ToString();
        if (maxBet > 10000)
        {
            float maxbet = maxBet / 1000f;
            MaxBetText.text = maxbet.ToString() + "k";
        }
        else
        {
            MaxBetText.text = maxBet.ToString();
        }
    }

    void OnScroll(Vector2 value)
    {
        lastScrollTime = Time.time;

        // Show immediately
        scrollbarCG.DOKill();
        scrollbarCG.alpha = 1f;

        // Restart fade routine
        StartFadeOutRoutine();
    }

    private void StartFadeOutRoutine()
    {
        if (fadeTween != null && fadeTween.IsActive())
            fadeTween.Kill();

        fadeTween = DOVirtual.DelayedCall(fadeDelay, () =>
        {
            scrollbarCG.DOFade(0f, fadeDuration);
        });
    }

}

[Serializable]
public class BetLimit
{
    [SerializeField] internal TMP_Text MinBetText;
    [SerializeField] internal TMP_Text MaxBetText;
}