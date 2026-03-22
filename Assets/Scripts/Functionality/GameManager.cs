using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;

public class GameManager : MonoBehaviour
{
    [Header("Manager References")]
    [SerializeField] internal UiManager uiManager;
    [SerializeField] internal BetManager betManager;
    [SerializeField] internal SocketIOManager socketManager;
    [SerializeField] private AnimationManager animationManager;
    [SerializeField] private HistoryController historyController;
    [SerializeField] private LeaderBoardController leaderBoardController;
    [SerializeField] private WinHistoryController winHistoryController;
    [SerializeField] private AudioManager audioManager;

    [Header("Card UI")]
    [SerializeField] private List<GameObject> CardBlackBg;

    [Header("Lobby Count UI")]
    [SerializeField] private TMP_Text LobbyCount_Text;

    [Header("Popups")]
    [SerializeField] private GameObject GeneralPopup;

    internal GamePhase currentPhase = GamePhase.Waiting;
    private string currentRoundId = "";

    internal string lastResultCard = "";
    internal string lastResultSuit = "";


    internal enum GamePhase
    {
        Waiting,
        Betting,
        Bonus,
        CardReveal,
        Cashout
    }

    // void Start()
    // {
    //     StartCoroutine(ShowPopup("Hello"));
    // }

    internal void OnInitData(GameData gameData, Player player)
    {
        Debug.Log("GameManager: Init data received");
        betManager.SetBalance(player.balance);
        uiManager.UpdateBalance(player.balance);
        uiManager.SetInitialGameData(gameData);
        SetPhase(GamePhase.Waiting);
        uiManager.SetPhase();
    }

    /// <summary>
    /// Called when the server sends ON ROOM ENTER payload.
    /// Pass the stats list from the payload so history is pre-populated.
    /// </summary>
    internal void OnRoomEnter(List<string> stats, Leaderboards leaderboards)
    {
        winHistoryController.ResyncInfoFadeAnimation();
        Debug.Log("GameManager: Room entered, loading stats history");

        if (winHistoryController != null && stats != null)
            winHistoryController.LoadStats(stats);

        if (leaderboards != null)
            UpdateLeaderboardUI(leaderboards);
    }

    internal void OnRoundStart(string roundId)
    {
        Debug.Log($"GameManager: Round started → {roundId}");
        currentRoundId = roundId;

        lastResultCard = "";
        lastResultSuit = "";

        uiManager.coinSelector.interactable = true;
        betManager.OnRoundStart();
        uiManager.RoundStart();
        SetPhase(GamePhase.Betting);
        audioManager.PlayPlaceBetNow();
        uiManager.SetPhase();
        animationManager.ResetAnimations();
        ToggleCardBlackBg(false);
    }


    internal void OnTimerTick(int timeRemainingMs)
    {
        // float seconds = timeRemainingMs / 1000f;
        // int displaySeconds = Mathf.RoundToInt(seconds);
        uiManager.UpdateTimer(timeRemainingMs);
        audioManager.PlayTimer();
    }


    internal void OnBonus(Bonus bonusData)
    {
        uiManager.RetractCoins();
        uiManager.coinSelector.interactable = false;
        betManager.SlideOutToLeft(betManager.BetButtonPanel);
        SetPhase(GamePhase.Bonus);
        audioManager.PlayBetLocked();
        uiManager.SetPhase();
        foreach (var d in bonusData.bonus)
        {
            Debug.Log($"GameManager: Bonus → key:{d.Key} value:{d.Value}");
            string bonusPosition = d.Key;
            int bonusMultiplier = d.Value;
            uiManager.BetLocked(bonusPosition, bonusMultiplier);
        }
        ToggleCardBlackBg(true);
    }


    internal void OnCardResult(string resultCard, string resultSuit, string combination)
    {
        lastResultCard = resultCard;
        lastResultSuit = resultSuit;

        SetPhase(GamePhase.CardReveal);
        uiManager.SetPhase();
        animationManager.WheelAnimation(resultCard, resultSuit);

        // if (winHistoryController != null)
        //     winHistoryController.OnResult(resultCard, resultSuit);

    }

    internal void OnCashout(double winAmount, double balance, List<Payout> payouts, Leaderboards leaderboards)
    {
        Debug.Log($"GameManager: Cashout → win:{winAmount} balance:{balance}");

        SetPhase(GamePhase.Cashout);
        uiManager.SetPhase();

        // Handle current player's win animation
        betManager.OnCashout(winAmount, balance);
        uiManager.UpdateBalance(balance);
        //uiManager.ShowCashoutUI(winAmount);

        // Handle other players' chips
        if (payouts != null)
        {
            // Build a set of usernames who have a win entry (win > 0)
            foreach (var payout in payouts)
            {
                // Skip current player — already handled above
                if (socketManager != null && payout.username == socketManager.playerdata?.username)
                    continue;

                RectTransform destination = leaderBoardController.GetOriginForUsername(payout.username);
                betManager.OnOtherPlayerCashout(payout.username, payout.win, destination);
            }

            // Any other player whose chip is still tracked but had NO payout entry → they lost
            betManager.PopUnpaidOtherPlayerChips(payouts, socketManager?.playerdata?.username);
        }

        if (leaderboards != null)
            UpdateLeaderboardUI(leaderboards);
    }


    internal void OnRoundEnd(string roundId)
    {
        Debug.Log($"GameManager: Round ended → {roundId}");

        betManager.OnRoundEnd();
        SetPhase(GamePhase.Waiting);
        uiManager.SetPhase();

        ResetResultUI();
    }

    private void ResetResultUI()
    {

    }

    internal void OnLobbyCount(int count)
    {
        if (LobbyCount_Text != null)
            LobbyCount_Text.text = $"{count}";
    }


    internal void OnLeaderboardUpdate(Leaderboards leaderboards)
    {
        Debug.Log("GameManager: Leaderboard updated");
        UpdateLeaderboardUI(leaderboards);
    }

    private void UpdateLeaderboardUI(Leaderboards leaderboards)
    {
        leaderBoardController.OnLeaderBoardDataReceived(leaderboards);
    }


    internal void OnOtherPlayerBet(string betOption, string username, float amount)
    {
        // Look up the leaderboard row for this player — null if they're not on the board
        RectTransform origin = leaderBoardController.GetOriginForUsername(username);
        betManager.SpawnOtherPlayerBetChip(betOption, origin, amount, username);
    }


    private void SetPhase(GamePhase phase)
    {
        currentPhase = phase;
        Debug.Log($"GameManager: Phase → {phase}");
    }


    internal void SelectLevel(string level)
    {
        betManager.currentLevel = level;
        socketManager.SendRoomSelection(level);
        Debug.Log($"GameManager: Joined level → {level}");
    }


    internal void OnHistoryReceived(System.Collections.Generic.List<HistoryRound> history, HistoryMeta meta)
    {
        historyController.OnDataReceived(history, meta);
    }

    internal void RequestHistory(int page)
    {
        socketManager.SendHistory(page);
    }

    internal void OnUndoButton() => betManager.SendUndo();
    internal void OnCancelButton() => betManager.SendCancel();
    internal void OnDoubleButton() => betManager.SendDouble();
    internal void OnRepeatButton() => betManager.SendRepeat();

    private void ToggleCardBlackBg(bool show)
    {
        foreach (var bg in CardBlackBg)
        {
            if (bg != null)
                bg.SetActive(show);
        }
    }

    internal Wagers GetWagers()
    {
        return socketManager.initialData?.wagers;
    }

    internal LevelBetLimit GetLevelBetLimit()
    {
        return socketManager.initialData?.levelBetLimit;
    }

    private Coroutine _popupCoroutine;

    internal void ShowPopupMessage(string message)
    {
        if (_popupCoroutine != null)
        {
            StopCoroutine(_popupCoroutine);
            _popupCoroutine = null;

            // Kill tweens and reset immediately
            RectTransform rect = GeneralPopup.GetComponent<RectTransform>();
            rect.DOKill(true); // true = complete immediately, snaps to end position
            GeneralPopup.SetActive(false);
        }

        _popupCoroutine = StartCoroutine(ShowPopup(message));
    }

    internal IEnumerator ShowPopup(string message)
    {
        TMP_Text messageText = GeneralPopup.GetComponentInChildren<TMP_Text>();
        messageText.text = message;

        yield return StartCoroutine(SlideInFromLeft(GeneralPopup));
        yield return new WaitForSeconds(1.7f);
        yield return StartCoroutine(SlideOutToRight(GeneralPopup));

        _popupCoroutine = null;
    }

    private IEnumerator SlideInFromLeft(GameObject panel)
    {
        if (panel == null) yield break;

        panel.SetActive(true);

        RectTransform rect = panel.GetComponent<RectTransform>();

        // Kill ALL tweens including any scale tweens
        rect.DOKill();
        DOTween.Kill(rect, true);

        // Hard reset scale and lock it
        rect.localScale = Vector3.one;

        float endX = rect.anchoredPosition.x;
        rect.anchoredPosition = new Vector2(endX - 1080, rect.anchoredPosition.y);

        bool done = false;
        rect.DOAnchorPosX(endX, 0.8f)
            .SetEase(Ease.OutCubic)
            .OnComplete(() =>
            {
                rect.localScale = Vector3.one; // enforce again after tween completes
                done = true;
            });

        yield return new WaitUntil(() => done);
    }

    internal IEnumerator SlideOutToRight(GameObject panel)
    {
        if (panel == null) yield break;

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.localScale = Vector3.one; // lock scale before animating

        float startX = rect.anchoredPosition.x;

        bool done = false;
        rect.DOAnchorPosX(startX + 1080, 0.8f)
            .SetEase(Ease.InCubic)
            .OnComplete(() =>
            {
                panel.SetActive(false);
                rect.anchoredPosition = new Vector2(startX, rect.anchoredPosition.y);
                // NO scale changes here
                done = true;
            });

        yield return new WaitUntil(() => done);
    }
}