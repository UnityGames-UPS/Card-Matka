using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Newtonsoft.Json;

// ─────────────────────────────────────────────────────────────────────────────
//  GAME STATE ENUM
// ─────────────────────────────────────────────────────────────────────────────
public enum GameState
{
    Idle,
    WaitingForRound,
    BettingOpen,
    BettingClosed,
    BonusReveal,
    CardReveal,
    Cashout,
    RoundEnd
}

// ─────────────────────────────────────────────────────────────────────────────
//  SERVER PAYLOAD MODELS  (JSON ↔ C#)
// ─────────────────────────────────────────────────────────────────────────────
[Serializable]
public class RoundStartPayload
{
    public string roundId;
    public long   startedAt;
    public long   bettingEndTime;
    public long   serverTime;
    public int    playerCount;
}

[Serializable]
public class BettingTimerPayload
{
    public string roundId;
    public long   serverTime;
    public long   bettingEndTime;
    public long   timeRemaining;
}

[Serializable]
public class BetPlacedPayload
{
    public bool    success;
    public BetPlacedInner payload;
}
[Serializable]
public class BetPlacedInner
{
    public string betId;
    public string betType;
    public string betOption;
    public double amount;
    public double balance;
    public double totalBet;
    public string message;
}

[Serializable]
public class BetCancelPayload
{
    public bool success;
    public BetCancelInner payload;
}
[Serializable]
public class BetCancelInner
{
    public int    cancelledCount;
    public double totalRefund;
    public double balance;
    public List<CancelledBet> bets;
}
[Serializable]
public class CancelledBet
{
    public string betId;
    public string betType;
    public string betOption;
    public double amount;
}

[Serializable]
public class BonusPayload
{
    public string roundId;
    public int    bonusPosition;
    public double bonusMultiplier;
}

[Serializable]
public class CardResultPayload
{
    public string roundId;
    public string resultCard;   // "J" | "Q" | "K"
    public string resultSuit;   // "spades" | "clubs" | "diamonds" | "hearts"
    public string combination;  // e.g. "K_hearts"
}

[Serializable]
public class CashoutLeaderEntry
{
    public string username;
    public double balance;
    public double totalWins;
    public int    rank;
}
[Serializable]
public class CashoutPayout
{
    public string userId;
    public string username;
    public double win;
    public double balance;
}
[Serializable]
public class CashoutLeaderboards
{
    public List<CashoutLeaderEntry> richest;
    public List<CashoutLeaderEntry> winners;
}
[Serializable]
public class CashoutPayload
{
    public CashoutLeaderboards leaderboards;
    public List<CashoutPayout> payouts;
}

// ─────────────────────────────────────────────────────────────────────────────
//  BET RECORD  (tracks what this player has bet this round)
// ─────────────────────────────────────────────────────────────────────────────
[Serializable]
public class BetRecord
{
    public string betId;
    public string betType;
    public string betOption;
    public double amount;
    public GameObject chipOnBoard;   // instantiated chip GO shown on bet slot
}

// ─────────────────────────────────────────────────────────────────────────────
//  GAME MANAGER
// ─────────────────────────────────────────────────────────────────────────────
public class Gamemanager : MonoBehaviour
{
    // ── References ────────────────────────────────────────────────────────────
    [Header("Manager References")]
    [SerializeField] internal SocketIOManager socketManager;
    [SerializeField] internal UiManager       uiManager;

    // ── Game State ─────────────────────────────────────────────────────────────
    public GameState CurrentState { get; private set; } = GameState.Idle;
    private string   currentRoundId;
    private string   currentLevel = "casual";

    // ── Balance & Bet Tracking ─────────────────────────────────────────────────
    [Header("Balance & Bet")]
    [SerializeField] private TMP_Text   balanceText;
    [SerializeField] private TMP_Text   totalBetText;
    [SerializeField] private TMP_Text   timerText;
    [SerializeField] private GameObject betLockedBanner;     // "Bet Locked!" overlay

    private double currentBalance   = 0;
    public  double currentTotalBet  = 0;

    // ── Bet Slots ──────────────────────────────────────────────────────────────
    // Assign in Inspector: one BetButton component per betting cell
    [Header("Main Bet Slots  (K / Q / J)")]
    [SerializeField] private BetButton slotK;
    [SerializeField] private BetButton slotQ;
    [SerializeField] private BetButton slotJ;

    [Header("Side Bet Slots  (suits)")]
    [SerializeField] private BetButton slotSpades;
    [SerializeField] private BetButton slotClubs;
    [SerializeField] private BetButton slotDiamonds;
    [SerializeField] private BetButton slotHearts;

    [Header("OP Bet Slots  (card+suit combos)")]
    [SerializeField] private BetButton slotKSpades;
    [SerializeField] private BetButton slotKClubs;
    [SerializeField] private BetButton slotKDiamonds;
    [SerializeField] private BetButton slotKHearts;
    [SerializeField] private BetButton slotQSpades;
    [SerializeField] private BetButton slotQClubs;
    [SerializeField] private BetButton slotQDiamonds;
    [SerializeField] private BetButton slotQHearts;
    [SerializeField] private BetButton slotJSpades;
    [SerializeField] private BetButton slotJClubs;
    [SerializeField] private BetButton slotJDiamonds;
    [SerializeField] private BetButton slotJHearts;

    // Quick lookup table built at Start
    private Dictionary<string, BetButton> slotByOption = new();

    // ── Action Buttons ─────────────────────────────────────────────────────────
    [Header("Action Buttons")]
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button undoButton;
    [SerializeField] private Button doubleButton;
    [SerializeField] private Button repeatButton;

    // ── Wheel ──────────────────────────────────────────────────────────────────
    [Header("Spinning Wheel")]
    [SerializeField] private WheelController wheelController;

    // ── Chip Prefab ────────────────────────────────────────────────────────────
    [Header("Chip")]
    [SerializeField] private GameObject chipPrefab;          // prefab with Chip component

    // ── Win / Bonus Animations ─────────────────────────────────────────────────
    [Header("Win Coin Animation")]
    [SerializeField] private ImageAnimation winCoinAnimation;
    [SerializeField] private ImageAnimation bonusAnimation;

    // ── Leaderboard UI ─────────────────────────────────────────────────────────
    [Header("Leaderboard")]
    [SerializeField] private TMP_Text richestPlayer1Text;
    [SerializeField] private TMP_Text richestPlayer2Text;
    [SerializeField] private TMP_Text richestPlayer3Text;
    [SerializeField] private TMP_Text biggestWinnerText;

    // ── Countdown ─────────────────────────────────────────────────────────────
    private Coroutine timerCoroutine;
    private long      serverBettingEndTime;   // epoch ms

    // ── Round-level bets list ──────────────────────────────────────────────────
    private List<BetRecord> currentBets = new();

    // ── Coin selector ─────────────────────────────────────────────────────────
    // Set by UiManager when the player picks a chip denomination
    public  double SelectedChipValue { get; set; } = 10;
    public  int    SelectedChipIndex { get; set; } = 0;   // index into bets array

    // ── Popup helper ──────────────────────────────────────────────────────────
    [Header("Message Popup")]
    [SerializeField] private GameObject messagePopup;
    [SerializeField] private TMP_Text   messagePopupText;

    // ═════════════════════════════════════════════════════════════════════════
    //  UNITY LIFECYCLE
    // ═════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        BuildSlotLookup();
    }

    private void Start()
    {
        // Wire up action buttons
        if (cancelButton) cancelButton.onClick.AddListener(OnCancelClicked);
        if (undoButton)   undoButton.onClick.AddListener(OnUndoClicked);
        if (doubleButton) doubleButton.onClick.AddListener(OnDoubleClicked);
        if (repeatButton) repeatButton.onClick.AddListener(OnRepeatClicked);

        SetActionButtonsInteractable(false);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  INIT DATA  (called by SocketIOManager after game:init)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Called by SocketIOManager once game:init arrives.</summary>
    public void SetInitialData()
    {
        if (socketManager.initialData == null || socketManager.playerdata == null)
        {
            Debug.LogError("[GameManager] SetInitialData: missing init data.");
            return;
        }

        currentBalance = socketManager.playerdata.balance;
        UpdateBalanceUI();

        Debug.Log($"[GameManager] Init done. Balance={currentBalance}, Level={currentLevel}");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  LEVEL SELECTION  (called from UiManager lobby buttons)
    // ═════════════════════════════════════════════════════════════════════════

    public void JoinLevel(string level)
    {
        currentLevel = level;
        socketManager.SendRoomSelection(level);
        Debug.Log($"[GameManager] Joining level: {level}");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  SERVER EVENT HANDLERS  (called by SocketIOManager)
    // ═════════════════════════════════════════════════════════════════════════

    // ── game:round_start ──────────────────────────────────────────────────────
    public void OnRoundStart(string json)
    {
        RoundStartPayload data = null;
        try { data = JsonConvert.DeserializeObject<RoundStartPayload>(json); }
        catch (Exception e) { Debug.LogError("[GameManager] OnRoundStart parse error: " + e.Message); return; }

        currentRoundId        = data.roundId;
        serverBettingEndTime  = data.bettingEndTime;

        TransitionToState(GameState.BettingOpen);

        // Start countdown timer
        if (timerCoroutine != null) StopCoroutine(timerCoroutine);
        timerCoroutine = StartCoroutine(CountdownTimer(data.bettingEndTime, data.serverTime));

        // Enable bet slots & action buttons
        SetAllSlotsInteractable(true);
        SetActionButtonsInteractable(false); // enabled only once first bet placed
        if (betLockedBanner) betLockedBanner.SetActive(false);

        Debug.Log($"[GameManager] Round started: {currentRoundId}");
    }

    // ── game:betting_timer ────────────────────────────────────────────────────
    public void SyncBettingTimer(string json)
    {
        BettingTimerPayload data = null;
        try { data = JsonConvert.DeserializeObject<BettingTimerPayload>(json); }
        catch { return; }

        // Resync our local countdown to the server's authoritative time
        serverBettingEndTime = data.bettingEndTime;
        float remainingSec = Mathf.Max(0, (data.bettingEndTime - data.serverTime) / 1000f);
        UpdateTimerUI(remainingSec);
    }

    // ── game:bet_placed  (broadcast - any player, including us) ───────────────
    public void OnBetPlacedBroadcast(string json)
    {
        BetPlacedPayload data = null;
        try { data = JsonConvert.DeserializeObject<BetPlacedPayload>(json); }
        catch { return; }

        // This broadcast is used for OTHER players' bets shown on wheel animation etc.
        // Our own balance/bet is updated via the acknowledgement callback (OnBetAcknowledged).
        Debug.Log($"[GameManager] Bet placed broadcast: {data?.payload?.betOption}");
    }

    // ── Acknowledgement of OUR bet ────────────────────────────────────────────
    public void OnOwnBetAcknowledged(BetPlacedInner payload)
    {
        if (payload == null) return;

        currentBalance  = payload.balance;
        currentTotalBet = payload.totalBet;

        UpdateBalanceUI();
        UpdateTotalBetUI();
        SetActionButtonsInteractable(true);
    }

    // ── game:bet_cancel ───────────────────────────────────────────────────────
    public void OnBetCancelBroadcast(string json)
    {
        Debug.Log("[GameManager] Bet cancel: " + json);
    }

    public void OnOwnCancelAcknowledged(BetCancelInner payload)
    {
        if (payload == null) return;
        currentBalance  = payload.balance;
        currentTotalBet = 0;
        ClearAllBetChips();
        UpdateBalanceUI();
        UpdateTotalBetUI();
        SetActionButtonsInteractable(false);
    }

    // ── game:bonus ────────────────────────────────────────────────────────────
    public void OnBonus(string json)
    {
        BonusPayload data = null;
        try { data = JsonConvert.DeserializeObject<BonusPayload>(json); }
        catch { return; }

        TransitionToState(GameState.BonusReveal);

        // Lock betting
        SetAllSlotsInteractable(false);
        if (betLockedBanner) betLockedBanner.SetActive(true);

        // Stop timer
        if (timerCoroutine != null) { StopCoroutine(timerCoroutine); timerCoroutine = null; }

        // Play bonus animation and hand off to wheel
        if (bonusAnimation) bonusAnimation.StartAnimation();
        if (wheelController) wheelController.ShowBonus(data.bonusPosition, data.bonusMultiplier);

        Debug.Log($"[GameManager] Bonus: pos={data.bonusPosition}, mult={data.bonusMultiplier}");
    }

    // ── game:card_result ──────────────────────────────────────────────────────
    public void OnCardResult(string json)
    {
        CardResultPayload data = null;
        try { data = JsonConvert.DeserializeObject<CardResultPayload>(json); }
        catch { return; }

        TransitionToState(GameState.CardReveal);

        if (wheelController) wheelController.SpinToResult(data.resultCard, data.resultSuit);

        Debug.Log($"[GameManager] Card result: {data.combination}");
    }

    // ── game:cashout ──────────────────────────────────────────────────────────
    public void OnCashout(string json)
    {
        CashoutPayload data = null;
        try { data = JsonConvert.DeserializeObject<CashoutPayload>(json); }
        catch { return; }

        TransitionToState(GameState.Cashout);

        ManagePayouts(data);
        UpdateLeaderboard(data.leaderboards);

        Debug.Log("[GameManager] Cashout received.");
    }

    // ── game:round_end ────────────────────────────────────────────────────────
    public void OnRoundEnd(string json)
    {
        TransitionToState(GameState.RoundEnd);
        StartCoroutine(PrepareNextRound());
        Debug.Log("[GameManager] Round ended.");
    }

    // ── game:lobby_count ──────────────────────────────────────────────────────
    public void OnLobbyCount(string json)
    {
        // Forward to UI if needed
        Debug.Log("[GameManager] Lobby count: " + json);
    }

    // ── game:leaderboard_update ───────────────────────────────────────────────
    public void OnLeaderboardUpdate(string json)
    {
        CashoutLeaderboards data = null;
        try { data = JsonConvert.DeserializeObject<CashoutLeaderboards>(json); }
        catch { return; }
        UpdateLeaderboard(data);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  BET PLACEMENT (called by BetButton on tap)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Called by BetButton when the player taps a cell.
    /// Places the chip visually, then emits PLACE_BET to the server.
    /// </summary>
    public void PlaceBet(string betType, string betOption, BetButton targetSlot)
    {
        if (CurrentState != GameState.BettingOpen)
        {
            PlayPopup("Betting is closed");
            return;
        }

        if (currentBalance <= 0 || currentBalance < SelectedChipValue)
        {
            PlayPopup("Low Balance");
            return;
        }

        // Optimistic local placement
        SpawnChipOnSlot(targetSlot, SelectedChipValue);

        // Emit to server
        socketManager.BetPlaced(SelectedChipIndex, betType, betOption);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  CHIP SPAWNING ON BOARD
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Instantiates a chip on the target bet slot and animates it flying there.
    /// </summary>
    private void SpawnChipOnSlot(BetButton targetSlot, double value)
    {
        // if (chipPrefab == null || targetSlot == null) return;

        // // Find the chip selector's world position as spawn origin
        // Transform origin = uiManager.CoinSelectorTransform;
        // if (origin == null) return;

        // GameObject chipGO = Instantiate(chipPrefab, origin.position, Quaternion.identity,
        //                                 targetSlot.ChipParent);

        // Chip chip = chipGO.GetComponent<Chip>();
        // if (chip != null)
        // {
        //     // Update chip sprite & text to match selected denomination
        //     //chip.SetData(uiManager.SelectedCoinSprite, FormatValue(value));
        // }

        // // Fly from coin selector to slot center
        // RectTransform rt = chipGO.GetComponent<RectTransform>();
        // Vector3 targetLocalPos = Vector3.zero; // center of parent
        // rt.anchoredPosition = targetSlot.ChipParent.InverseTransformPoint(origin.position);

        // rt.DOAnchorPos(Vector2.zero, 0.35f)
        //   .SetEase(Ease.OutCubic);

        // // Scale pop effect
        // chipGO.transform.localScale = Vector3.zero;
        // chipGO.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);

        // // Notify slot so it can stack / update its total label
        // targetSlot.AddChip(chipGO, value);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  ACTION BUTTON HANDLERS
    // ═════════════════════════════════════════════════════════════════════════

    private void OnCancelClicked()
    {
        if (CurrentState != GameState.BettingOpen) return;
        socketManager.SendCancle();
    }

    private void OnUndoClicked()
    {
        if (CurrentState != GameState.BettingOpen) return;
        socketManager.SendUndo();
    }

    private void OnDoubleClicked()
    {
        if (CurrentState != GameState.BettingOpen) return;
        if (currentTotalBet * 2 > currentBalance)
        {
            PlayPopup("Low Balance");
            return;
        }
        socketManager.SendDouble();
    }

    private void OnRepeatClicked()
    {
        if (CurrentState != GameState.BettingOpen) return;
        socketManager.SendRepeat();
    }

    // ── Undo callback ─────────────────────────────────────────────────────────
    public void OnUndoAcknowledged(BetPlacedInner payload)
    {
        if (payload == null) return;

        // Remove the most recent chip from its slot
        UndoLastChip(payload.betOption);

        currentBalance  = payload.balance;
        currentTotalBet = payload.totalBet;
        UpdateBalanceUI();
        UpdateTotalBetUI();

        if (currentTotalBet <= 0) SetActionButtonsInteractable(false);
    }

    // ── Double callback ───────────────────────────────────────────────────────
    public void DoubleBets(List<BetPlacedInner> bets)
    {
        if (bets == null) return;
        foreach (var b in bets)
        {
            if (slotByOption.TryGetValue(b.betOption, out BetButton slot))
                SpawnChipOnSlot(slot, b.amount - (b.amount / 2)); // delta = original amount
        }
    }

    // ── Repeat callback ───────────────────────────────────────────────────────
    public void RepeatBets(List<BetPlacedInner> bets)
    {
        if (bets == null) return;
        ClearAllBetChips();
        foreach (var b in bets)
        {
            if (slotByOption.TryGetValue(b.betOption, out BetButton slot))
                SpawnChipOnSlot(slot, b.amount);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  PAYOUT / WIN ANIMATIONS
    // ═════════════════════════════════════════════════════════════════════════

    private void ManagePayouts(CashoutPayload data)
    {
        if (data?.payouts == null) return;

        string myUsername = socketManager.playerdata?.username;
        foreach (var payout in data.payouts)
        {
            if (payout.username == myUsername)
            {
                currentBalance  = payout.balance;
                double winAmount = payout.win;

                UpdateBalanceUI();

                if (winAmount > 0)
                {
                    // Play coin burst animation
                    if (winCoinAnimation) winCoinAnimation.StartAnimation();
                    PlayPopup($"+{FormatValue(winAmount)}");
                }
                break;
            }
        }

        // Clear board after showing result
        StartCoroutine(ClearBoardAfterDelay(2.5f));
    }

    private IEnumerator ClearBoardAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ClearAllBetChips();
        currentTotalBet = 0;
        UpdateTotalBetUI();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  LEADERBOARD
    // ═════════════════════════════════════════════════════════════════════════

    private void UpdateLeaderboard(CashoutLeaderboards boards)
    {
        if (boards == null) return;

        if (boards.richest != null && boards.richest.Count > 0)
        {
            if (richestPlayer1Text && boards.richest.Count > 0)
                richestPlayer1Text.text = $"{boards.richest[0].username}\n{FormatValue(boards.richest[0].balance)}";
            if (richestPlayer2Text && boards.richest.Count > 1)
                richestPlayer2Text.text = $"{boards.richest[1].username}\n{FormatValue(boards.richest[1].balance)}";
            if (richestPlayer3Text && boards.richest.Count > 2)
                richestPlayer3Text.text = $"{boards.richest[2].username}\n{FormatValue(boards.richest[2].balance)}";
        }

        if (boards.winners != null && boards.winners.Count > 0)
        {
            if (biggestWinnerText)
                biggestWinnerText.text = $"{boards.winners[0].username}\n{FormatValue(boards.winners[0].totalWins)}";
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  TIMER COROUTINE
    // ═════════════════════════════════════════════════════════════════════════

    private IEnumerator CountdownTimer(long bettingEndTimeMs, long serverTimeMs)
    {
        long clientStartMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long serverOffset  = serverTimeMs - clientStartMs;   // ms to add to local time to get server time

        while (true)
        {
            long nowServerMs     = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + serverOffset;
            long remainingMs     = bettingEndTimeMs - nowServerMs;

            if (remainingMs <= 0)
            {
                UpdateTimerUI(0);
                yield break;
            }

            UpdateTimerUI(remainingMs / 1000f);
            yield return new WaitForSeconds(0.25f);
        }
    }

    private void UpdateTimerUI(float seconds)
    {
        if (timerText) timerText.text = Mathf.CeilToInt(seconds).ToString();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  ROUND LIFECYCLE HELPERS
    // ═════════════════════════════════════════════════════════════════════════

    private IEnumerator PrepareNextRound()
    {
        yield return new WaitForSeconds(1f);
        TransitionToState(GameState.WaitingForRound);
        SetAllSlotsInteractable(false);
        SetActionButtonsInteractable(false);
        if (betLockedBanner) betLockedBanner.SetActive(false);
        if (wheelController) wheelController.ResetWheel();
    }

    private void TransitionToState(GameState newState)
    {
        CurrentState = newState;
        Debug.Log($"[GameManager] State → {newState}");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  CHIP / BET SLOT HELPERS
    // ═════════════════════════════════════════════════════════════════════════

    private void BuildSlotLookup()
    {
        slotByOption = new Dictionary<string, BetButton>
        {
            // Main
            { "K", slotK }, { "Q", slotQ }, { "J", slotJ },
            // Side
            { "specific_Spades",   slotSpades },
            { "specific_Clubs",    slotClubs },
            { "specific_Diamonds", slotDiamonds },
            { "specific_Hearts",   slotHearts },
            // OP
            { "K_spades",   slotKSpades },  { "K_clubs",   slotKClubs },
            { "K_diamonds", slotKDiamonds },{ "K_hearts",  slotKHearts },
            { "Q_spades",   slotQSpades },  { "Q_clubs",   slotQClubs },
            { "Q_diamonds", slotQDiamonds },{ "Q_hearts",  slotQHearts },
            { "J_spades",   slotJSpades },  { "J_clubs",   slotJClubs },
            { "J_diamonds", slotJDiamonds },{ "J_hearts",  slotJHearts },
        };
    }

    private void SetAllSlotsInteractable(bool state)
    {
        // foreach (var kvp in slotByOption)
        //     if (kvp.Value != null) kvp.Value.SetInteractable(state);
    }

    private void SetActionButtonsInteractable(bool state)
    {
        if (cancelButton) cancelButton.interactable = state;
        if (undoButton)   undoButton.interactable   = state;
        if (doubleButton) doubleButton.interactable = state;
        if (repeatButton) repeatButton.interactable = state;
    }

    public void ClearAllBetChips()
    {
        // foreach (var kvp in slotByOption)
        //     kvp.Value?.ClearChips();
        // currentBets.Clear();
    }

    private void UndoLastChip(string betOption)
    {
        // if (slotByOption.TryGetValue(betOption, out BetButton slot))
        //     slot?.RemoveLastChip();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  UI HELPERS
    // ═════════════════════════════════════════════════════════════════════════

    public void UpdatePlayerbalance(string val)
    {
        if (double.TryParse(val, out double b)) currentBalance = b;
        UpdateBalanceUI();
    }

    private void UpdateBalanceUI()
    {
        if (balanceText) balanceText.text = FormatValue(currentBalance);
    }

    private void UpdateTotalBetUI()
    {
        if (totalBetText) totalBetText.text = FormatValue(currentTotalBet);
    }

    public void PlayPopup(string message)
    {
        if (messagePopup == null || messagePopupText == null) return;

        messagePopupText.text = message;
        messagePopup.SetActive(true);
        messagePopup.transform.DOKill();
        messagePopup.transform.localScale = Vector3.zero;
        messagePopup.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack);

        DOVirtual.DelayedCall(2f, () =>
        {
            if (messagePopup)
                messagePopup.transform.DOScale(Vector3.zero, 0.2f)
                    .OnComplete(() => messagePopup.SetActive(false));
        });
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  UTILITY
    // ═════════════════════════════════════════════════════════════════════════

    public static string FormatValue(double val)
    {
        if (val >= 1_000_000) return $"{val / 1_000_000:0.##}M";
        if (val >= 1_000)     return $"{val / 1_000:0.##}K";
        return val.ToString("0.##");
    }
}