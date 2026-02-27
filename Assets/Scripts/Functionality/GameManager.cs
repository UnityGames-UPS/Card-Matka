using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class GameManager : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────────
    [Header("Manager References")]
    [SerializeField] internal UiManager uiManager;
    [SerializeField] internal BetManager betManager;
    [SerializeField] internal SocketIOManager socketManager;
    [SerializeField] private AnimationManager animationManager;
    [SerializeField] private HistoryController historyController;

    // [Header("Game Phase UI")]
    // [SerializeField] private GameObject BettingPhase_Object;
    // [SerializeField] private GameObject BonusPhase_Object;
    // [SerializeField] private GameObject CardReveal_Object;
    // [SerializeField] private GameObject CashoutPhase_Object;
    // [SerializeField] private GameObject WaitingPhase_Object;

    [Header("Card UI")]
    [SerializeField] private List<GameObject> CardBlackBg;

    [Header("Bonus UI")]
    [SerializeField] private TMP_Text BonusPosition_Text;
    [SerializeField] private TMP_Text BonusMultiplier_Text;

    [Header("Cashout UI")]
    [SerializeField] private TMP_Text WinAmount_Text;
    [SerializeField] private GameObject WinEffect_Object;
    [SerializeField] private GameObject LoseEffect_Object;

    [Header("Leaderboard UI")]
    [SerializeField] private List<TMP_Text> Richest_Texts;
    [SerializeField] private List<TMP_Text> Winners_Texts;

    [Header("Lobby Count UI")]
    [SerializeField] private TMP_Text LobbyCount_Text;


    // ── State ──────────────────────────────────────────────────────────────────
    internal GamePhase currentPhase = GamePhase.Waiting;
    private string currentRoundId = "";

    // ─────────────────────────────────────────────────────────────────────────
    //  Game Phases Enum
    // ─────────────────────────────────────────────────────────────────────────
    internal enum GamePhase
    {
        Waiting,
        Betting,
        Bonus,
        CardReveal,
        Cashout
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Init — called by SocketIOManager after game:init
    // ─────────────────────────────────────────────────────────────────────────
    internal void OnInitData(GameData gameData, Player player)
    {
        Debug.Log("GameManager: Init data received");
        betManager.SetBalance(player.balance);
        uiManager.UpdateBalance(player.balance);
        uiManager.SetInitialGameData(gameData);
        SetPhase(GamePhase.Waiting);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Phase 1 — BETTING
    //  Triggered by: game:round_start
    // ─────────────────────────────────────────────────────────────────────────
    internal void OnRoundStart(string roundId)
    {
        Debug.Log($"GameManager: Round started → {roundId}");
        currentRoundId = roundId;

        betManager.OnRoundStart();
        uiManager.RoundStart();
        SetPhase(GamePhase.Betting);
        ToggleCardBlackBg(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Timer sync — called every game:betting_timer tick
    // ─────────────────────────────────────────────────────────────────────────
    internal void OnTimerTick(float timeRemainingMs)
    {
        float seconds = timeRemainingMs / 1000f;
        int displaySeconds = Mathf.CeilToInt(seconds);
        uiManager.UpdateTimer(displaySeconds);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Phase 2 — BONUS REVEAL
    //  Triggered by: game:bonus
    // ─────────────────────────────────────────────────────────────────────────
    internal void OnBonus(int bonusPosition, int bonusMultiplier)
    {
        Debug.Log($"GameManager: Bonus → position:{bonusPosition} multiplier:{bonusMultiplier}x");
        betManager.SlideOutToLeft(betManager.BetButtonPanel);
        SetPhase(GamePhase.Bonus);
        uiManager.BetLocked(bonusPosition, bonusMultiplier);
        ToggleCardBlackBg(true);
        //ShowBonusUI(bonusPosition, bonusMultiplier);
    }

    private void ShowBonusUI(int position, int multiplier)
    {
        if (BonusPosition_Text != null)
            BonusPosition_Text.text = $"Position: {position}";

        if (BonusMultiplier_Text != null)
            BonusMultiplier_Text.text = $"{multiplier}x";

        // Punch-scale animation on bonus panel
        // if (BonusPhase_Object != null)
        //     BonusPhase_Object.transform.DOPunchScale(Vector3.one * 0.15f, 0.4f, 5, 0.5f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Phase 3 — CARD RESULT
    //  Triggered by: game:card_result
    // ─────────────────────────────────────────────────────────────────────────
    internal void OnCardResult(string resultCard, string resultSuit, string combination)
    {
        //Debug.Log($"GameManager: Card result → {combination}");

        SetPhase(GamePhase.CardReveal);
        animationManager.WheelAnimation(resultCard, resultSuit);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Phase 4 — CASHOUT
    //  Triggered by: game:cashout
    // ─────────────────────────────────────────────────────────────────────────
    internal void OnCashout(double winAmount, double balance, List<Payout> payouts, Leaderboards leaderboards)
    {
        Debug.Log($"GameManager: Cashout → win:{winAmount} balance:{balance}");

        SetPhase(GamePhase.Cashout);

        // Update balance via betManager (also animates chips)
        betManager.OnCashout(winAmount, balance);
        uiManager.UpdateBalance(balance);

        // Show win/lose
        uiManager.ShowCashoutUI(winAmount);

        // Update leaderboard
        if (leaderboards != null)
            UpdateLeaderboardUI(leaderboards);
    }


    // ─────────────────────────────────────────────────────────────────────────
    //  Phase 5 — ROUND END / WAITING
    //  Triggered by: game:round_end
    // ─────────────────────────────────────────────────────────────────────────
    internal void OnRoundEnd(string roundId)
    {
        Debug.Log($"GameManager: Round ended → {roundId}");

        betManager.OnRoundEnd();
        SetPhase(GamePhase.Waiting);

        // Clean up result UI
        ResetResultUI();
    }

    private void ResetResultUI()
    {

    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Lobby Count
    //  Triggered by: game:lobby_count
    // ─────────────────────────────────────────────────────────────────────────
    internal void OnLobbyCount(string level, int count)
    {
        Debug.Log($"GameManager: Lobby count → {level}: {count}");

        // switch (level)
        // {
        //     case "casual": if (CasualCount_Text != null) CasualCount_Text.text = count.ToString(); break;
        //     case "novice": if (NoviceCount_Text != null) NoviceCount_Text.text = count.ToString(); break;
        //     case "expert": if (ExpertCount_Text != null) ExpertCount_Text.text = count.ToString(); break;
        //     case "high_roller": if (HighRollerCount_Text != null) HighRollerCount_Text.text = count.ToString(); break;
        // }
        if (LobbyCount_Text != null)
            LobbyCount_Text.text = $"{count}";
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Leaderboard Update
    //  Triggered by: game:leaderboard_update
    // ─────────────────────────────────────────────────────────────────────────
    internal void OnLeaderboardUpdate(Leaderboards leaderboards)
    {
        Debug.Log("GameManager: Leaderboard updated");
        UpdateLeaderboardUI(leaderboards);
    }

    private void UpdateLeaderboardUI(Leaderboards leaderboards)
    {
        // if (leaderboards?.richest != null)
        // {
        //     for (int i = 0; i < Richest_Texts.Count && i < leaderboards.richest.Count; i++)
        //     {
        //         var entry = leaderboards.richest[i];
        //         Richest_Texts[i].text = $"{entry.rank}. {entry.username}  {FormatAmount(entry.balance)}";
        //     }
        // }

        // if (leaderboards?.winners != null)
        // {
        //     for (int i = 0; i < Winners_Texts.Count && i < leaderboards.winners.Count; i++)
        //     {
        //         var entry = leaderboards.winners[i];
        //         Winners_Texts[i].text = $"{entry.rank}. {entry.username}  {entry.totalWins} wins";
        //     }
        // }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Phase switcher — controls which screen is visible
    // ─────────────────────────────────────────────────────────────────────────
    private void SetPhase(GamePhase phase)
    {
        currentPhase = phase;

        // if (BettingPhase_Object != null) BettingPhase_Object.SetActive(phase == GamePhase.Betting);
        // if (BonusPhase_Object != null) BonusPhase_Object.SetActive(phase == GamePhase.Bonus);
        // if (CardReveal_Object != null) CardReveal_Object.SetActive(phase == GamePhase.CardReveal);
        // if (CashoutPhase_Object != null) CashoutPhase_Object.SetActive(phase == GamePhase.Cashout);
        // if (WaitingPhase_Object != null) WaitingPhase_Object.SetActive(phase == GamePhase.Waiting);

        Debug.Log($"GameManager: Phase → {phase}");
        
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Level Selection — called from UI buttons
    // ─────────────────────────────────────────────────────────────────────────
    internal void SelectLevel(string level)
    {
        betManager.currentLevel = level;
        socketManager.SendRoomSelection(level);
        Debug.Log($"GameManager: Joined level → {level}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Bet Action Hooks — wire UI buttons to these
    // ─────────────────────────────────────────────────────────────────────────
    // ─────────────────────────────────────────────────────────────────────────
    //  History — called by SocketIOManager after BET_HISTORY ack
    // ─────────────────────────────────────────────────────────────────────────
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
}