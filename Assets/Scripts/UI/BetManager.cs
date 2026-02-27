using UnityEngine;
using TMPro;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
public class BetManager : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────────
    [Header("References")]
    [SerializeField] private SocketIOManager socketManager;
    [SerializeField] private UiManager uiManager;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private RectTransform profileIcon;
    [SerializeField] private TMP_Text balanceLabel;
    [SerializeField] private TMP_Text totalBetLabel;

    [Header("Chip Prefab")]
    [Tooltip("Prefab with an Image + optional TMP_Text child showing chip value")]
    [SerializeField] internal GameObject chipPrefab;

    [Header("Bet Buttons")]
    [SerializeField] private GameObject RepeatBetPanel;
    [SerializeField] private Button RepeatBetButton;
    [SerializeField] internal GameObject BetButtonPanel;
    [SerializeField] private Button UndoButton;
    [SerializeField] private Button CancelButton;
    [SerializeField] private Button DoubleButton;

    [Header("Game State")]
    [SerializeField] internal string currentLevel = "casual";
    [SerializeField] internal int selectedChipIndex = 0; // set by your coin-selector UI

    // ── State ──────────────────────────────────────────────────────────────────
    internal bool isBettingOpen = false;
    internal double currentBalance = 0;
    internal double currentTotalBet = 0;
    private bool hasPlacedBet = false;

    // betOption -> chip GameObjects on that slot
    private Dictionary<string, List<GameObject>> slotChips = new Dictionary<string, List<GameObject>>();
    // betOption -> running total displayed on that slot
    private Dictionary<string, double> slotTotals = new Dictionary<string, double>();
    // betOption -> BetButton
    private Dictionary<string, BetButton> buttonMap = new Dictionary<string, BetButton>();

    // ── Lifecycle ──────────────────────────────────────────────────────────────
    private void Awake()
    {
        foreach (var bb in FindObjectsOfType<BetButton>(true))
        {
            bb.Init(this);
            if (!string.IsNullOrEmpty(bb.betOption))
                buttonMap[bb.betOption] = bb;
        }
    }

    private void Start()
    {
        RepeatBetButton.onClick.AddListener(SendRepeat);
        UndoButton.onClick.AddListener(SendUndo);
        CancelButton.onClick.AddListener(SendCancel);
        DoubleButton.onClick.AddListener(SendDouble);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Game Flow  — call these from SocketIOManager event handlers
    // ─────────────────────────────────────────────────────────────────────────

    internal void OnRoundStart()
    {
        isBettingOpen = true;
        ClearAllChips();
        currentTotalBet = 0;
        RefreshTotalLabel();
        Debug.Log("Has placed bet : " + hasPlacedBet);
        if (hasPlacedBet)
        {
            SlideInFromLeft(RepeatBetPanel);
        }
    }

    internal void OnRoundEnd()
    {
        isBettingOpen = false;
        // FIX: check before resetting, otherwise currentTotalBet is always 0 here
        if (currentTotalBet > 0)
        {
            hasPlacedBet = true;
        }
        ClearAllChips();
        currentTotalBet = 0;
        RefreshTotalLabel();
    }

    internal void SetBalance(double balance)
    {
        currentBalance = balance;
        if (balanceLabel != null)
            balanceLabel.text = FormatAmount(balance);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PlaceBet — called by BetButton on click
    // ─────────────────────────────────────────────────────────────────────────
    internal void PlaceBet(BetButton betButton)
    {
        if (!isBettingOpen) { ShowPopup("Betting is closed"); return; }
        socketManager.BetPlaced(selectedChipIndex, betButton.betType, betButton.betOption);
    }

    // Called back by SocketIOManager once server confirms the bet
    internal void OnBetPlaced(string betOption, double amount, double balance, double totalBet)
    {
        currentTotalBet = totalBet;
        SetBalance(balance);
        RefreshTotalLabel();

        if (buttonMap.TryGetValue(betOption, out BetButton bb))
            SpawnChip(bb, amount);

        if (BetButtonPanel.activeSelf == false)
        {
            SlideInFromLeft(BetButtonPanel);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Undo
    // ─────────────────────────────────────────────────────────────────────────
    internal void OnUndoDone(string betOption, double balance, double totalBet)
    {
        currentTotalBet = totalBet;
        SetBalance(balance);
        RefreshTotalLabel();
        RemoveTopChip(betOption);
        if (totalBet == 0)
        {
            SlideOutToLeft(BetButtonPanel);
        }
        // FIX: only show RepeatBetPanel between rounds, not while betting is still open
        if (hasPlacedBet && !isBettingOpen && totalBet == 0)
        {
            SlideInFromLeft(RepeatBetPanel);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Cancel
    // ─────────────────────────────────────────────────────────────────────────
    internal void OnCancelDone(double balance)
    {
        currentTotalBet = 0;
        SetBalance(balance);
        RefreshTotalLabel();
        ClearAllChips();
        SlideOutToLeft(BetButtonPanel);
        if (hasPlacedBet)
            SlideInFromLeft(RepeatBetPanel);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Double
    // ─────────────────────────────────────────────────────────────────────────
    internal void OnDoubleDone(List<Bet> bets, double balance, double totalBet)
    {
        currentTotalBet = totalBet;
        SetBalance(balance);
        RefreshTotalLabel();

        foreach (var entry in bets)
            if (buttonMap.TryGetValue(entry.betOption, out BetButton bb))
                SpawnChip(bb, entry.delta);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Repeat
    // ─────────────────────────────────────────────────────────────────────────
    internal void OnRepeatDone(List<Bet> bets, double balance, double totalBet)
    {
        ClearAllChips();
        currentTotalBet = totalBet;
        SetBalance(balance);
        RefreshTotalLabel();

        foreach (var entry in bets)
            if (buttonMap.TryGetValue(entry.betOption, out BetButton bb))
                SpawnChip(bb, entry.amount);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  UI Button hooks — wire these to your Undo / Cancel / Double / Repeat buttons
    // ─────────────────────────────────────────────────────────────────────────
    internal void SendUndo() => socketManager.SendUndo();
    internal void SendCancel() => socketManager.SendCancle();
    internal void SendDouble() => socketManager.SendDouble();
    internal void SendRepeat() => socketManager.SendRepeat();

    // ─────────────────────────────────────────────────────────────────────────
    //  Cashout — chips fly to profile on win, pop out on loss
    // ─────────────────────────────────────────────────────────────────────────
    internal void OnCashout(double winAmount, double balance)
    {
        isBettingOpen = false;
        SetBalance(balance);

        if (winAmount > 0)
            StartCoroutine(FlyChipsToProfile());
        else
            StartCoroutine(PopOutChips());
    }

    private IEnumerator FlyChipsToProfile()
    {
        float delay = 0f;
        foreach (var list in slotChips.Values)
            foreach (var chip in list)
                if (chip != null)
                {
                    chip.transform.DOMove(profileIcon.position, 0.4f)
                        .SetDelay(delay).SetEase(Ease.InBack)
                        .OnComplete(() => Destroy(chip));
                    delay += 0.05f;
                }

        yield return new WaitForSeconds(delay + 0.5f);
        slotChips.Clear();
        slotTotals.Clear();
        RefreshAllSlotLabels();
    }

    private IEnumerator PopOutChips()
    {
        foreach (var list in slotChips.Values)
            foreach (var chip in list)
                if (chip != null)
                    chip.transform.DOScale(Vector3.zero, 0.25f)
                        .SetEase(Ease.InBack)
                        .OnComplete(() => Destroy(chip));

        yield return new WaitForSeconds(0.35f);
        slotChips.Clear();
        slotTotals.Clear();
        RefreshAllSlotLabels();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Chip helpers
    // ─────────────────────────────────────────────────────────────────────────
    private void SpawnChip(BetButton bb, double amount)
    {
        if (chipPrefab == null) return;

        GameObject chip = Instantiate(chipPrefab, bb.chipParent);
        chip.transform.localScale = Vector3.zero;
        chip.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack);

        TMP_Text lbl = chip.GetComponentInChildren<TMP_Text>();
        if (lbl != null) lbl.text = FormatAmount(amount);

        string key = bb.betOption;
        if (!slotChips.ContainsKey(key)) slotChips[key] = new List<GameObject>();
        slotChips[key].Add(chip);

        slotTotals[key] = slotTotals.ContainsKey(key) ? slotTotals[key] + amount : amount;
        UpdateSlotLabel(bb, slotTotals[key]);
    }

    private void RemoveTopChip(string betOption)
    {
        if (!slotChips.ContainsKey(betOption) || slotChips[betOption].Count == 0) return;

        var list = slotChips[betOption];
        GameObject top = list[list.Count - 1];
        list.RemoveAt(list.Count - 1);

        if (top != null)
            top.transform.DOScale(Vector3.zero, 0.15f)
               .SetEase(Ease.InBack)
               .OnComplete(() => Destroy(top));

        if (list.Count == 0) slotTotals.Remove(betOption);

        if (buttonMap.TryGetValue(betOption, out BetButton bb))
            UpdateSlotLabel(bb, slotTotals.ContainsKey(betOption) ? slotTotals[betOption] : 0); // pass 0 if no more bets on this option
    }

    private void ClearAllChips()
    {
        foreach (var list in slotChips.Values)
            foreach (var chip in list)
                if (chip != null) Destroy(chip);

        slotChips.Clear();
        slotTotals.Clear();
        RefreshAllSlotLabels();
    }

    private void UpdateSlotLabel(BetButton bb, double amount)
    {
        bool hasBet = amount > 0;
        bb.totalBetLabel.text = hasBet ? FormatAmount(amount) : "";
        bb.RedBg.SetActive(hasBet);
        if (hasBet)
            bb.RedBg.GetComponent<RectTransform>().DOPunchScale(Vector3.one * 0.12f, 0.25f, 5, 0.5f);
    }


    private void RefreshAllSlotLabels()
    {
        foreach (var kvp in buttonMap) UpdateSlotLabel(kvp.Value, slotTotals.ContainsKey(kvp.Key) ? slotTotals[kvp.Key] : 0);
    }

    private void RefreshTotalLabel()
    {
        if (totalBetLabel != null)
            totalBetLabel.text = currentTotalBet > 0 ? FormatAmount(currentTotalBet) : "";
    }

    private string FormatAmount(double val)
    {
        if (val >= 1000) return $"{val / 1000:0.#}K";
        return val.ToString("0.##");
    }

    private void ShowPopup(string msg)
    {
        Debug.LogWarning("BetManager: " + msg);
        // plug into your UIManager popup here
    }

    // Repeatpanel and betbuttons panel slide in from the left coin image
    private void SlideInFromLeft(GameObject panel)
    {
        if (panel == null) return;
        panel.SetActive(true);
        RectTransform rect = panel.GetComponent<RectTransform>();
        float endX = rect.anchoredPosition.x; // Store the original X position
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x - 200, rect.anchoredPosition.y);
        rect.DOAnchorPosX(endX, 0.7f).SetEase(Ease.OutCubic);
    }

    internal void SlideOutToLeft(GameObject panel)
    {
        if (panel == null) return;
        RectTransform rect = panel.GetComponent<RectTransform>();
        float startX = rect.anchoredPosition.x;
        // FIX: was resetting anchoredPosition on the same frame, which cancelled the tween immediately
        rect.DOAnchorPosX(startX - 200, 0.5f).SetEase(Ease.InCubic)
            .OnComplete(() =>
            {
                panel.SetActive(false);
                rect.anchoredPosition = new Vector2(startX, rect.anchoredPosition.y); // reset after hiding
            });
    }
}