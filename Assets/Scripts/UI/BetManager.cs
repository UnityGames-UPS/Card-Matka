using UnityEngine;
using TMPro;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

public class BetManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SocketIOManager socketManager;
    [SerializeField] private UiManager uiManager;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private AudioManager audioManager;
    [SerializeField] private LeaderBoardController leaderBoardController;
    [SerializeField] private RectTransform profileIcon;
    [SerializeField] private TMP_Text balanceLabel;
    [SerializeField] private TMP_Text totalBetLabel;

    [Header("Chip Prefab")]
    [Tooltip("Prefab with an Image + optional TMP_Text child showing chip value")]
    [SerializeField] internal GameObject chipPrefab;
    [Header("Chip Spawn Animation")]
    [Tooltip("How far above the parent the chip appears before falling (UI units)")]
    [SerializeField] private float spawnYOffset = 80f;

    [Tooltip("Radius of the random landing area inside the bet button parent (UI units)")]
    [SerializeField] private float landingScatterRadius = 27f;

    [Tooltip("Duration of the fall / arc tween")]
    [SerializeField] private float chipDropDuration = 0.22f;

    [Header("Chip Sprites")]
    [Tooltip("Sprites matching each chip denomination, in the same order as your chip selector (e.g. 1, 5, 10, 25, 100)")]
    [SerializeField] internal Sprite[] chipSprites;
    [Tooltip("Denomination value for each sprite slot, matching chipSprites order (e.g. 1, 5, 10, 25, 100)")]
    [SerializeField] internal double[] chipDenominations;

    [Header("Other Player Chip")]
    [Tooltip("Prefab used to show other players' bets flying to their button")]
    [SerializeField] internal GameObject otherPlayerChipPrefab;

    [Header("Other Player Icons")]
    [Tooltip("Fallback icon used when the betting player is not found in the leaderboard")]
    [SerializeField] internal RectTransform defaultOtherPlayerIcon;
    [Tooltip("Position from which winning chips fly TO the bet button on other-player win")]
    [SerializeField] private RectTransform winChipSpawnOrigin;

    [Header("Bet Buttons")]
    [SerializeField] internal GameObject RepeatBetPanel;
    [SerializeField] private Button RepeatBetButton;
    [SerializeField] internal GameObject BetButtonPanel;
    [SerializeField] private Button UndoButton;
    [SerializeField] private Button CancelButton;
    [SerializeField] private Button DoubleButton;

    [Header("Game State")]
    [SerializeField] internal string currentLevel = "casual";
    [SerializeField] internal int selectedChipIndex = 0; // set by your coin-selector UI

    // Add these fields to store the "resting" anchoredPosition.x for each panel
    private float repeatBetPanelHomeX;
    private float betButtonPanelHomeX;
    private bool homesRecorded = false;

    internal bool isBettingOpen = false;
    internal double currentBalance = 0;
    internal double currentTotalBet = 0;
    private bool hasPlacedBet = false;

    private Dictionary<string, List<GameObject>> slotChips = new Dictionary<string, List<GameObject>>();
    internal Dictionary<string, double> slotTotals = new Dictionary<string, double>();
    private Dictionary<string, BetButton> buttonMap = new Dictionary<string, BetButton>();

    // Tracks other-player chips: username → betOption → list of chip GameObjects
    private Dictionary<string, Dictionary<string, List<GameObject>>> otherPlayerChips = new Dictionary<string, Dictionary<string, List<GameObject>>>();

    private void Start()
    {
        foreach (var bb in FindObjectsOfType<BetButton>(true))
        {
            bb.Init(this);
            if (!string.IsNullOrEmpty(bb.betOption))
                buttonMap[bb.betOption] = bb;
        }
        RepeatBetButton.onClick.AddListener(SendRepeat);
        UndoButton.onClick.AddListener(SendUndo);
        CancelButton.onClick.AddListener(SendCancel);
        DoubleButton.onClick.AddListener(SendDouble);

        RecordPanelHomes();
    }

    internal void OnRoundStart()
    {
        isBettingOpen = true;
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
        if (currentTotalBet > 0)
        {
            hasPlacedBet = true;
        }
        ClearAllChips();
        ClearAllOtherPlayerChips();
        currentTotalBet = 0;
        RefreshTotalLabel();
    }

    internal void OnBettingClose()
    {
        if (currentTotalBet > 0)
        {
            hasPlacedBet = true;
        }
    }

    internal void OnHome()
    {
        isBettingOpen = false;
        hasPlacedBet = false;
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

    internal void PlaceBet(BetButton betButton)
    {
        if (!isBettingOpen) { ShowPopup("Betting is closed"); return; }
        socketManager.BetPlaced(selectedChipIndex, betButton.betType, betButton.betOption);
    }

    internal void OnBetPlaced(string betOption, double amount, double balance, double totalBet)
    {
        currentTotalBet = totalBet;
        audioManager.PlayChipSound();
        SetBalance(balance);
        RefreshTotalLabel();

        if (buttonMap.TryGetValue(betOption, out BetButton bb))
            SpawnChipsForAmount(bb, amount);

        if (RepeatBetPanel.activeSelf == true)
        {
            //SlideOutToLeft(RepeatBetPanel);
        }

        if (BetButtonPanel.activeSelf == false)
            SlideInFromLeft(BetButtonPanel);
    }

    internal void OnUndoDone(string betOption, double amount, double balance, double totalBet)
    {
        currentTotalBet = totalBet;
        SetBalance(balance);
        RefreshTotalLabel();

        // Remove chips from this slot whose values sum to the undone amount.
        RemoveChipsByAmount(betOption, amount);

        if (totalBet == 0)
            SlideOutToLeft(BetButtonPanel);

        if (hasPlacedBet && !isBettingOpen && totalBet == 0)
            SlideInFromLeft(RepeatBetPanel);
    }


    internal void OnCancelDone(double balance)
    {
        currentTotalBet = 0;
        SetBalance(balance);
        RefreshTotalLabel();
        StartCoroutine(CancelFlyOutAnimation());
        if (hasPlacedBet)
            SlideInFromLeft(RepeatBetPanel);
        SlideOutToLeft(BetButtonPanel);
    }

    /// <summary>
    /// Chips fly upward (reverse of spawn), then arc toward the profile icon and destroy on arrival.
    /// </summary>
    private IEnumerator CancelFlyOutAnimation()
    {
        var allChips = new List<GameObject>();
        foreach (var list in slotChips.Values)
            foreach (var chip in list)
                if (chip != null) allChips.Add(chip);

        slotChips.Clear();
        slotTotals.Clear();
        RefreshAllSlotLabels();

        Vector3 iconPos = profileIcon.position;
        float stagger = 0f;
        bool pulseScheduled = false;

        foreach (var chip in allChips)
        {
            if (chip == null) continue;
            GameObject captured = chip;
            float capturedStagger = stagger;
            bool isFirst = !pulseScheduled;
            if (isFirst) pulseScheduled = true;

            captured.transform.DOKill();

            Vector3 risePos = captured.transform.position + new Vector3(0f, 10f, 0f);

            Sequence seq = DOTween.Sequence();
            seq.SetDelay(capturedStagger);
            // Phase 1: rise straight up
            seq.Append(captured.transform.DOMove(risePos, 0.25f).SetEase(Ease.OutQuad));
            // Phase 2: move to profile icon while scaling down
            seq.AppendCallback(() =>
            {
                captured.transform.DOMove(iconPos, 0.8f).SetEase(Ease.InOutQuad)
                    .OnComplete(() =>
                    {
                        if (isFirst)
                            profileIcon.DOPunchScale(Vector3.one * 0.25f, 0.3f, 5, 0.5f);
                        Destroy(captured);
                    });
                captured.transform.DOScale(Vector3.zero, 0.8f).SetEase(Ease.InQuad);
            });

            stagger += 0.05f;
        }

        yield return new WaitForSeconds(stagger + 0.65f);
    }


    internal void OnDoubleDone(List<Bet> bets, double balance, double totalBet)
    {
        currentTotalBet = totalBet;
        SetBalance(balance);
        RefreshTotalLabel();

        foreach (var entry in bets)
        {
            if (entry.delta <= 0) continue;
            if (buttonMap.TryGetValue(entry.betOption, out BetButton bb))
                SpawnChipsForAmount(bb, entry.delta);
        }
    }

    internal void OnRepeatDone(List<Bet> bets, double balance, double totalBet)
    {
        ClearAllChips();
        currentTotalBet = totalBet;
        SetBalance(balance);
        RefreshTotalLabel();

        //SlideOutToLeft(RepeatBetPanel);

        SlideInFromLeft(BetButtonPanel);

        foreach (var entry in bets)
            if (buttonMap.TryGetValue(entry.betOption, out BetButton bb))
                SpawnChipsForAmount(bb, entry.amount);
    }


    internal void SendUndo() => socketManager.SendUndo();
    internal void SendCancel() => socketManager.SendCancle();
    internal void SendDouble() => socketManager.SendDouble();
    internal void SendRepeat() => socketManager.SendRepeat();

    internal void OnCashout(double winAmount, double balance)
    {
        isBettingOpen = false;
        SetBalance(balance);

        // Net = what the player receives back minus what they staked this round.
        // Positive  → profit   e.g. staked 100, won 250  → net = +150
        // Zero      → push     e.g. staked 100, won 100  → net =   0
        // Negative  → loss     e.g. staked 100, won 0    → net = -100
        double netAmount = winAmount - currentTotalBet;

        if (winAmount > 0)
            StartCoroutine(CashoutWinAnimation(winAmount, netAmount));
        else
            StartCoroutine(PopOutChips(netAmount));
    }


    private IEnumerator CashoutWinAnimation(double winAmount, double netAmount)
    {
        yield return new WaitForSeconds(0.1f);
        audioManager.PlayChips();

        string resultCard = gameManager.lastResultCard;
        string resultSuit = gameManager.lastResultSuit;

        var winningOptions = new HashSet<string>();
        if (!string.IsNullOrEmpty(resultCard) && !string.IsNullOrEmpty(resultSuit))
        {
            winningOptions.Add($"{resultCard}_{resultSuit}");
            winningOptions.Add(resultCard);
            string suitCap = char.ToUpper(resultSuit[0]) + resultSuit.Substring(1);
            winningOptions.Add($"specific_{suitCap}");
        }

        var winningSlots = new List<string>();
        var losingSlots = new List<string>();

        foreach (var key in slotChips.Keys)
        {
            if (winningOptions.Contains(key)) winningSlots.Add(key);
            else losingSlots.Add(key);
        }

        // ── Losing chips: pop out ─────────────────────────────────────────────
        foreach (var key in losingSlots)
            foreach (var chip in slotChips[key])
                if (chip != null)
                    chip.transform.DOScale(Vector3.zero, 0.25f)
                        .SetEase(Ease.InBack)
                        .OnComplete(() => Destroy(chip));

        foreach (var chips in winningSlots)
        {
            if (slotChips.ContainsKey(chips))
            {
                foreach (var chip in slotChips[chips])
                {
                    buttonMap.TryGetValue(chips, out BetButton bb);
                    chip.transform.SetParent(bb.WinAnimationObject.transform.GetComponentInParent<RectTransform>(), false);
                }
            }
        }

        yield return new WaitForSeconds(0.9f);

        // ── Phase 1: spawn profit chips from winChipSpawnOrigin → bet button ─
        double totalBetOnWinners = 0;
        foreach (var key in winningSlots)
            if (slotTotals.ContainsKey(key))
                totalBetOnWinners += slotTotals[key];

        double profit = winAmount - totalBetOnWinners;

        float longestIncoming = 0f;

        if (profit > 0 && chipPrefab != null && winChipSpawnOrigin != null)
        {
            Vector3 spawnPos = winChipSpawnOrigin.position;
            float winChipStagger = 0f;
            const float winChipStaggerStep = 0.08f;

            foreach (var key in winningSlots)
            {
                if (!buttonMap.TryGetValue(key, out BetButton bb)) continue;
                if (!slotTotals.ContainsKey(key)) continue;

                double share = totalBetOnWinners > 0
                    ? profit * (slotTotals[key] / totalBetOnWinners)
                    : profit / winningSlots.Count;

                List<double> chipValues = DecomposeIntoChipValues(share);

                foreach (double chipValue in chipValues)
                {
                    double capturedValue = chipValue;
                    float capturedStagger = winChipStagger;

                    // Give every profit chip its own scatter offset so multiple chips
                    // don't land exactly on top of each other and appear as one chip.
                    Vector3 capturedScatter = new Vector3(
                        Random.Range(-landingScatterRadius, landingScatterRadius),
                        Random.Range(-landingScatterRadius * 0.5f, landingScatterRadius * 0.5f),
                        0f);

                    GameObject profitChip = Instantiate(chipPrefab, bb.WinAnimationObject.transform.GetComponentInParent<RectTransform>());

                    // FIX: Our player's profit chips always on top
                    profitChip.transform.SetAsLastSibling();

                    profitChip.transform.position = spawnPos;
                    profitChip.transform.localScale = Vector3.zero;

                    // Sprite
                    Image chipImage = profitChip.GetComponent<Image>();
                    if (chipImage == null) chipImage = profitChip.GetComponentInChildren<Image>();
                    if (chipImage != null)
                    {
                        Sprite best = GetChipSpriteForAmount(capturedValue);
                        if (best != null) chipImage.sprite = best;
                    }

                    // Label
                    TMP_Text lbl = profitChip.GetComponentInChildren<TMP_Text>();
                    if (lbl != null) lbl.text = FormatAmount(capturedValue);

                    // Pop in + fly to bet button (with per-chip scatter), staggered
                    profitChip.transform.DOScale(Vector3.one, 0.15f)
                        .SetDelay(capturedStagger).SetEase(Ease.OutBack);
                    profitChip.transform.DOMove(bb.chipParent.position + capturedScatter, 0.45f)
                        .SetDelay(capturedStagger).SetEase(Ease.InOutQuad)
                        .OnComplete(() =>
                        {
                            profitChip.transform.DOScale(Vector3.one * 0.85f, 0.12f).SetEase(Ease.OutSine)
                                .OnComplete(() =>
                                    profitChip.transform.DOPunchScale(new Vector3(0.12f, -0.12f, 0f), 0.15f, 4, 0.4f));
                        });

                    // Track so Phase 2 can pick it up
                    if (!slotChips.ContainsKey(key)) slotChips[key] = new List<GameObject>();
                    slotChips[key].Add(profitChip);

                    float thisEnd = capturedStagger + 0.45f + 0.27f;
                    if (thisEnd > longestIncoming) longestIncoming = thisEnd;

                    winChipStagger += winChipStaggerStep;
                }
            }
        }

        // Wait for all profit chips to land + settle buffer
        yield return new WaitForSeconds(longestIncoming + 1f);

        // ── Phase 2: ALL chips on winning buttons rise + fly to profileIcon ───
        Vector3 iconPos = profileIcon.position;
        float delay = 0f;
        bool pulseScheduled = false;

        foreach (var key in winningSlots)
        {
            if (!slotChips.ContainsKey(key)) continue;
            foreach (var chip in slotChips[key])
            {
                if (chip == null) continue;
                GameObject captured = chip;
                float capturedDelay = delay;
                bool isFirst = !pulseScheduled;
                if (isFirst) pulseScheduled = true;

                captured.transform.DOKill();

                Vector3 risePos = captured.transform.position + new Vector3(0f, 10f, 0f);

                Sequence seq = DOTween.Sequence();
                seq.SetDelay(capturedDelay);
                seq.Append(captured.transform.DOMove(risePos, 0.25f).SetEase(Ease.OutQuad));
                seq.AppendCallback(() =>
                {
                    captured.transform.DOMove(iconPos, 0.8f).SetEase(Ease.InOutQuad)
                        .OnComplete(() =>
                        {
                            if (isFirst)
                            {
                                //profileIcon.DOPunchScale(Vector3.one * 0.25f, 0.3f, 5, 0.5f);
                                uiManager.ShowCashoutUI(netAmount);
                            }
                            Destroy(captured);
                        });
                    captured.transform.DOScale(Vector3.zero, 0.8f).SetEase(Ease.InQuad);
                });

                delay += 0.06f;
            }
        }

        yield return new WaitForSeconds(delay + 0.55f);

        slotChips.Clear();
        slotTotals.Clear();
        RefreshAllSlotLabels();
    }

    private IEnumerator PopOutChips(double netAmount)
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

        // Show net loss amount (netAmount will be negative here)
        //uiManager.ShowCashoutUI(netAmount);
    }


    internal void SpawnOtherPlayerBetChip(string betOption, RectTransform origin, float amount, string username)
    {
        audioManager.PlayChips();
        if (otherPlayerChipPrefab == null) return;
        if (!buttonMap.TryGetValue(betOption, out BetButton targetButton)) return;

        // Fall back to the default icon if the player is not on the leaderboard
        RectTransform spawnOrigin = (origin != null) ? origin : defaultOtherPlayerIcon;
        if (spawnOrigin == null)
        {
            Debug.LogWarning("BetManager: no origin for other-player chip — assign defaultOtherPlayerIcon");
            return;
        }

        // Spawn chip at the leaderboard row / default icon position, parented to the bet button
        // so it stays in place after landing (no canvas reparent needed)
        GameObject chip = Instantiate(otherPlayerChipPrefab, targetButton.chipParent);

        // FIX: Push other-player chips behind our own chips.
        // In Unity UI, sibling index 0 renders behind all higher-index siblings.
        // Our chips call SetAsLastSibling() so they always sit above these.
        chip.transform.SetAsFirstSibling();

        //foreach (var biggest in leaderBoardController.BiggestRows)
        {
            if (leaderBoardController.MaskUsername(username) == leaderBoardController.BiggestRows[0].playerNameText.text)
            {
                ImageAnimation chan = chip.transform.GetChild(0).GetComponent<ImageAnimation>();
                chan.gameObject.SetActive(true);
                chan.StartAnimation();
                chan.doLoopAnimation = true;
            }
        }
        //foreach (var richest in leaderBoardController.RichestRows)
        {
            if (leaderBoardController.MaskUsername(username) == leaderBoardController.RichestRows[0].playerNameText.text)
            {
                GameObject big = chip.transform.GetChild(0).gameObject;
                big.SetActive(false);
                ImageAnimation chan = chip.transform.GetChild(1).GetComponent<ImageAnimation>();
                chan.gameObject.SetActive(true);
                chan.StartAnimation();
                chan.doLoopAnimation = true;
            }
        }
        chip.transform.GetComponentInChildren<TMP_Text>().text = amount.ToString();

        // Start at the origin's world position
        chip.transform.position = spawnOrigin.position;
        chip.transform.localScale = Vector3.zero;

        Vector3 randomPosi = new Vector3(Random.Range(-landingScatterRadius, landingScatterRadius), Random.Range(-5f, 5f));

        // Pop in scale
        chip.transform.DOScale(Vector3.one, 0.15f).SetEase(Ease.OutBack);

        // Fly straight to the bet button parent position, then squish on landing
        chip.transform.DOMove(targetButton.chipParent.position + randomPosi, 0.5f)
            .SetDelay(0.1f)
            .SetEase(Ease.InOutQuad)
            .OnComplete(() =>
            {
                chip.transform.DOScale(Vector3.one * 0.85f, 0.12f).SetEase(Ease.OutSine)
                    .OnComplete(() =>
                        chip.transform.DOPunchScale(new Vector3(0.12f, -0.12f, 0f), 0.15f, 4, 0.4f))
                        .OnComplete(() =>
                            chip.transform.DOScale(Vector3.one, 0.2f));
            });

        // Track this chip under username → betOption
        if (!otherPlayerChips.ContainsKey(username))
            otherPlayerChips[username] = new Dictionary<string, List<GameObject>>();
        if (!otherPlayerChips[username].ContainsKey(betOption))
            otherPlayerChips[username][betOption] = new List<GameObject>();
        otherPlayerChips[username][betOption].Add(chip);
    }


    /// <summary>
    /// Called at cashout for every OTHER player in the payouts list.
    /// If win > 0: only chips on WINNING bet slots rise-then-fly to the leaderboard row;
    ///             chips on losing slots pop and vanish.
    /// If win == 0: all chips pop and vanish.
    /// </summary>
    internal void OnOtherPlayerCashout(string username, double win, RectTransform destination)
    {
        StartCoroutine(OtherPlayerCashoutCoroutine(username, win, destination));
    }

    private IEnumerator OtherPlayerCashoutCoroutine(string username, double win, RectTransform destination)
    {
        if (win > 0) yield return new WaitForSeconds(1.1f);
        if (!otherPlayerChips.TryGetValue(username, out var slotDict) || slotDict.Count == 0)
            yield break;

        if (win > 0)
        {
            // ── Build winning-options set ─────────────────────────────────────────
            string resultCard = gameManager.lastResultCard;
            string resultSuit = gameManager.lastResultSuit;
            var winningOptions = new HashSet<string>();
            if (!string.IsNullOrEmpty(resultCard) && !string.IsNullOrEmpty(resultSuit))
            {
                winningOptions.Add($"{resultCard}_{resultSuit}");
                winningOptions.Add(resultCard);
                string suitCap = char.ToUpper(resultSuit[0]) + resultSuit.Substring(1);
                winningOptions.Add($"specific_{suitCap}");
            }

            RectTransform target = (destination != null) ? destination : defaultOtherPlayerIcon;
            Vector3 targetPos = target != null ? target.position : Vector3.zero;
            Vector3 spawnPos = winChipSpawnOrigin != null ? winChipSpawnOrigin.position : targetPos;

            // ── Compute total weight for proportional win distribution ────────────
            double totalWeight = 0;
            var winningSlotsInDict = new List<string>();
            foreach (var kvp in slotDict)
            {
                if (!winningOptions.Contains(kvp.Key)) continue;
                winningSlotsInDict.Add(kvp.Key);
                totalWeight += kvp.Value.Count;
            }

            // ── Phase 1 ───────────────────────────────────────────────────────────
            // Losing chips   → pop immediately
            // Winning chips  → keep sitting; spawn denomination-split win chips
            //                  flying from winChipSpawnOrigin to each bet button

            var phase2Chips = new Dictionary<string, List<GameObject>>();
            float longestIncoming = 0f;

            foreach (var kvp in slotDict)
            {
                string betOption = kvp.Key;
                List<GameObject> existingChips = kvp.Value;
                bool isWinningSlot = winningOptions.Contains(betOption);

                if (!isWinningSlot)
                {
                    // Pop losing chips
                    foreach (var chip in existingChips)
                    {
                        if (chip == null) continue;
                        chip.transform.DOKill();
                        chip.transform.DOScale(Vector3.zero, 0.25f)
                            .SetEase(Ease.InBack)
                            .OnComplete(() => Destroy(chip));
                    }
                    continue;
                }

                if (!buttonMap.TryGetValue(betOption, out BetButton bb)) continue;

                // Seed Phase 2 with the existing sitting bet chips
                phase2Chips[betOption] = new List<GameObject>(existingChips);

                // Reparent winning chips to WinAnimationObject — same as our player cashout
                foreach (var chip in existingChips)
                    if (chip != null)
                        chip.transform.SetParent(bb.WinAnimationObject.transform.GetComponentInParent<RectTransform>(), false);

                // Proportional win share for this slot
                double slotWeight = totalWeight > 0
                    ? existingChips.Count / totalWeight
                    : 1.0 / System.Math.Max(1, winningSlotsInDict.Count);
                double slotWinAmount = win * slotWeight;

                // Break win amount into denominations — same greedy logic as SpawnChipsForAmount
                var denominations = new List<double>();
                if (chipDenominations != null && chipDenominations.Length > 0)
                {
                    double[] sorted = (double[])chipDenominations.Clone();
                    System.Array.Sort(sorted);
                    System.Array.Reverse(sorted);

                    double remaining = slotWinAmount;
                    while (remaining > 0.001)
                    {
                        double chosen = -1;
                        foreach (double d in sorted)
                            if (d <= remaining + 0.001) { chosen = d; break; }

                        if (chosen < 0)
                        {
                            if (denominations.Count > 0)
                                denominations[denominations.Count - 1] += remaining;
                            else
                                denominations.Add(remaining);
                            break;
                        }

                        if (remaining - chosen < 0.001)
                        {
                            denominations.Add(remaining);
                            remaining = 0;
                        }
                        else
                        {
                            denominations.Add(chosen);
                            remaining -= chosen;
                        }
                    }
                }
                else
                {
                    denominations.Add(slotWinAmount);
                }

                // Spawn one chip per denomination, staggered, flying to the bet button
                float incomingDelay = 0f;
                const float incomingStagger = 0.08f;

                foreach (double denomValue in denominations)
                {
                    double capturedDenom = denomValue;
                    float capturedIncomingDelay = incomingDelay;

                    GameObject winChip = Instantiate(otherPlayerChipPrefab, bb.chipParent);

                    // FIX: Other-player win chips also go behind our chips
                    winChip.transform.SetSiblingIndex(1);

                    TMP_Text lbl = winChip.GetComponentInChildren<TMP_Text>();
                    if (lbl != null) lbl.text = FormatAmount(capturedDenom);

                    winChip.transform.position = spawnPos;
                    winChip.transform.localScale = Vector3.zero;

                    winChip.transform.DOScale(Vector3.one, 0.15f)
                        .SetDelay(capturedIncomingDelay).SetEase(Ease.OutBack);
                    winChip.transform.DOMove(bb.chipParent.position, 0.45f)
                        .SetDelay(capturedIncomingDelay).SetEase(Ease.InOutQuad)
                        .OnComplete(() =>
                        {
                            winChip.transform.DOScale(Vector3.one * 0.85f, 0.12f).SetEase(Ease.OutSine)
                                .OnComplete(() =>
                                    winChip.transform.DOPunchScale(new Vector3(0.12f, -0.12f, 0f), 0.15f, 4, 0.4f));
                        });

                    phase2Chips[betOption].Add(winChip);

                    float thisChipEnd = capturedIncomingDelay + 0.45f + 0.27f;
                    if (thisChipEnd > longestIncoming) longestIncoming = thisChipEnd;

                    incomingDelay += incomingStagger;
                }
            }

            // Wait for all win chips to land and settle
            yield return new WaitForSeconds(longestIncoming + 1f);

            // ── Phase 2 ───────────────────────────────────────────────────────────
            // ALL chips (original bets + win chips) on every winning button
            // rise up then fly together to the destination

            float delay = 0f;
            bool pulseScheduled = false;

            foreach (var kvp in phase2Chips)
            {
                foreach (var chip in kvp.Value)
                {
                    if (chip == null) continue;
                    GameObject captured = chip;
                    float capturedDelay = delay;
                    bool isFirst = !pulseScheduled;
                    if (isFirst) pulseScheduled = true;

                    captured.transform.DOKill();

                    Vector3 risePos = captured.transform.position + new Vector3(0f, 10f, 0f);

                    Sequence seq = DOTween.Sequence();
                    seq.SetDelay(capturedDelay);
                    seq.Append(captured.transform.DOMove(risePos, 0.25f).SetEase(Ease.OutQuad));
                    seq.AppendCallback(() =>
                    {
                        captured.transform.DOMove(targetPos, 0.8f).SetEase(Ease.InOutQuad)
                            .OnComplete(() =>
                            {
                                if (isFirst && target != null)
                                    target.DOPunchScale(Vector3.one * 0.25f, 0.3f, 5, 0.5f);
                                Destroy(captured);
                            });
                        captured.transform.DOScale(Vector3.zero, 0.8f).SetEase(Ease.InQuad);
                    });

                    delay += 0.05f;
                }
            }
        }
        else
        {
            // Player lost entirely — pop all chips
            foreach (var chips in slotDict.Values)
                foreach (var chip in chips)
                {
                    if (chip == null) continue;
                    chip.transform.DOKill();
                    chip.transform.DOScale(Vector3.zero, 0.25f)
                        .SetEase(Ease.InBack)
                        .OnComplete(() => Destroy(chip));
                }
        }

        otherPlayerChips.Remove(username);
    }

    private void ClearAllOtherPlayerChips()
    {
        foreach (var slotDict in otherPlayerChips.Values)
            foreach (var list in slotDict.Values)
                foreach (var chip in list)
                    if (chip != null) Destroy(chip);
        otherPlayerChips.Clear();
    }

    /// <summary>
    /// Destroys chips for any tracked other-player who does NOT appear in the payouts list
    /// (i.e. they placed bets but didn't win — no payout entry was sent for them).
    /// </summary>
    internal void PopUnpaidOtherPlayerChips(List<Payout> payouts, string currentPlayerUsername)
    {
        var paidUsernames = new HashSet<string>();
        if (payouts != null)
            foreach (var p in payouts)
                paidUsernames.Add(p.username);

        // Collect keys to avoid mutating dict while iterating
        var allTracked = new List<string>(otherPlayerChips.Keys);
        foreach (var username in allTracked)
        {
            // Skip current player (handled separately) and already-processed winners
            if (username == currentPlayerUsername) continue;
            if (paidUsernames.Contains(username)) continue;

            // This player had no payout → pop their chips
            OnOtherPlayerCashout(username, 0, null);
        }
    }


    /// <summary>
    /// Decomposes <paramref name="amount"/> into a list of chip face-values using the same
    /// greedy largest-denomination-first algorithm as <see cref="SpawnChipsForAmount"/>.
    /// Remainder values smaller than the smallest denomination are absorbed into the last chip.
    /// </summary>
    private List<double> DecomposeIntoChipValues(double amount)
    {
        var result = new List<double>();

        if (chipDenominations == null || chipDenominations.Length == 0)
        {
            result.Add(amount);
            return result;
        }

        double[] sorted = (double[])chipDenominations.Clone();
        System.Array.Sort(sorted);
        System.Array.Reverse(sorted);

        double remaining = amount;

        while (remaining > 0.001)
        {
            double chosen = -1;
            foreach (double d in sorted)
            {
                if (d <= remaining + 0.001)
                {
                    chosen = d;
                    break;
                }
            }

            if (chosen < 0)
            {
                // Remaining is smaller than the smallest denomination — absorb into last chip
                if (result.Count > 0)
                    result[result.Count - 1] += remaining;
                else
                    result.Add(remaining);
                break;
            }

            if (remaining - chosen < 0.001)
            {
                result.Add(remaining);
                remaining = 0;
            }
            else
            {
                result.Add(chosen);
                remaining -= chosen;
            }
        }

        return result;
    }

    private void SpawnChipsForAmount(BetButton bb, double amount)
    {
        if (chipDenominations == null || chipDenominations.Length == 0)
        {
            // No denomination data — fall back to single chip
            SpawnChip(bb, amount, 0f);
            return;
        }

        // Sort denominations descending (work on a copy to avoid mutating the inspector array)
        double[] sorted = (double[])chipDenominations.Clone();
        System.Array.Sort(sorted);
        System.Array.Reverse(sorted);

        double remaining = amount;
        float delay = 0f;
        const float stagger = 0.08f;

        while (remaining > 0.001)
        {
            // Find the largest denomination that fits
            double chosen = -1;
            foreach (double d in sorted)
            {
                if (d <= remaining + 0.001)
                {
                    chosen = d;
                    break;
                }
            }

            if (chosen < 0)
            {
                // Nothing fits (remaining < smallest denom) — spawn one remainder chip
                SpawnChip(bb, remaining, delay);
                break;
            }

            double chipValue;
            if (remaining - chosen < 0.001)
            {
                // Exact match or floating-point dust — use remaining as the label
                chipValue = remaining;
                remaining = 0;
            }
            else
            {
                chipValue = chosen;
                remaining -= chosen;
            }

            SpawnChip(bb, chipValue, delay);
            delay += stagger;
        }
    }

    private void SpawnChip(BetButton bb, double amount)
    {
        SpawnChip(bb, amount, 0f);
    }

    private void SpawnChip(BetButton bb, double amount, float spawnDelay)
    {
        if (chipPrefab == null) return;

        // ── Instantiate ──────────────────────────────────────────────────────────
        GameObject chip = Instantiate(chipPrefab, bb.chipParent);

        // FIX: Always keep our player's chips rendered on top of other-player chips.
        // Unity UI renders higher sibling index on top; SetAsLastSibling ensures
        // our chip is always above any other-player chip already in the parent.
        chip.transform.SetAsLastSibling();

        // ── Pick correct sprite ──────────────────────────────────────────────────
        Image chipImage = chip.GetComponent<Image>();
        if (chipImage == null) chipImage = chip.GetComponentInChildren<Image>();
        if (chipImage != null)
        {
            Sprite best = GetChipSpriteForAmount(amount);
            if (best != null) chipImage.sprite = best;
        }

        TMP_Text lbl = chip.GetComponentInChildren<TMP_Text>();
        if (lbl != null) lbl.text = FormatAmount(amount);

        // ── Random X within scatter range, fixed Y above parent centre ──────────
        float randomX = Random.Range(-landingScatterRadius, landingScatterRadius);
        chip.transform.localPosition = new Vector3(randomX, spawnYOffset, 0f);
        chip.transform.localScale = Vector3.zero;

        // ── Landing spot: same X, Y = 0 (straight drop) ─────────────────────────
        Vector3 landingLocal = new Vector3(randomX, 0f, 0f);

        // ── Animation ────────────────────────────────────────────────────────────
        // 1. Pop scale in (delayed so staggered chips don't all appear at once)
        chip.transform.DOScale(Vector3.one, 0.15f).SetDelay(spawnDelay).SetEase(Ease.OutBack);

        // 2. Straight drop down to landing spot, then tiny squish on impact
        chip.transform.DOLocalMove(landingLocal, chipDropDuration)
            .SetDelay(spawnDelay)
            .SetEase(Ease.InQuad)               // accelerates downward like gravity
            .OnComplete(() =>
            {
                // small squish on landing
                chip.transform.DOPunchScale(new Vector3(0.15f, -0.15f, 0f), 0.18f, 4, 0.4f);
            });

        // ── Book-keeping ─────────────────────────────────────────────────────────
        string key = bb.betOption;
        if (!slotChips.ContainsKey(key)) slotChips[key] = new List<GameObject>();
        slotChips[key].Add(chip);

        slotTotals[key] = slotTotals.ContainsKey(key) ? slotTotals[key] + amount : amount;
        UpdateSlotLabel(bb, slotTotals[key]);
    }

    /// <summary>
    /// Returns the chip sprite whose denomination is the largest value
    /// that is less than or equal to <paramref name="amount"/>.
    /// Falls back to the smallest denomination sprite if nothing fits.
    /// </summary>
    private Sprite GetChipSpriteForAmount(double amount)
    {
        if (chipSprites == null || chipSprites.Length == 0) return null;
        if (chipDenominations == null || chipDenominations.Length != chipSprites.Length) return chipSprites[0];

        Sprite best = chipSprites[0];
        double bestVal = chipDenominations[0];

        for (int i = 0; i < chipDenominations.Length; i++)
        {
            if (chipDenominations[i] <= amount && chipDenominations[i] >= bestVal)
            {
                bestVal = chipDenominations[i];
                best = chipSprites[i];
            }
        }
        return best;
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
            UpdateSlotLabel(bb, slotTotals.ContainsKey(betOption) ? slotTotals[betOption] : 0);
    }

    /// <summary>
    /// Removes chips from the named slot whose label values sum to <paramref name="amount"/>,
    /// starting from the top of the stack (LIFO). Updates the slot total and label.
    /// </summary>
    private void RemoveChipsByAmount(string betOption, double amount)
    {
        if (string.IsNullOrEmpty(betOption) || amount <= 0) return;
        if (!slotChips.ContainsKey(betOption) || slotChips[betOption].Count == 0) return;

        var list = slotChips[betOption];
        double remaining = amount;

        while (remaining > 0.001 && list.Count > 0)
        {
            GameObject top = list[list.Count - 1];
            list.RemoveAt(list.Count - 1);

            // Read the chip's displayed value from its label, fall back to remaining amount.
            double chipValue = remaining;
            if (top != null)
            {
                TMP_Text lbl = top.GetComponentInChildren<TMP_Text>();
                if (lbl != null && double.TryParse(
                        lbl.text.Replace("K", "").Trim(),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double parsed))
                {
                    // Re-expand K suffix if present
                    chipValue = lbl.text.EndsWith("K") ? parsed * 1000 : parsed;
                }

                GameObject captured = top;
                captured.transform.DOScale(Vector3.zero, 0.15f)
                    .SetEase(Ease.InBack)
                    .OnComplete(() => Destroy(captured));
            }

            remaining -= chipValue;
        }

        // Recalculate the slot total from backend truth: subtract the full undo amount.
        if (slotTotals.ContainsKey(betOption))
        {
            slotTotals[betOption] = System.Math.Max(0, slotTotals[betOption] - amount);
            if (slotTotals[betOption] <= 0)
                slotTotals.Remove(betOption);
        }

        if (list.Count == 0)
            slotChips.Remove(betOption);

        if (buttonMap.TryGetValue(betOption, out BetButton bb))
            UpdateSlotLabel(bb, slotTotals.ContainsKey(betOption) ? slotTotals[betOption] : 0);
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
    }

    private void RefreshAllSlotLabels()
    {
        foreach (var kvp in buttonMap) UpdateSlotLabel(kvp.Value, slotTotals.ContainsKey(kvp.Key) ? slotTotals[kvp.Key] : 0);
    }

    private void RefreshTotalLabel()
    {
        if (totalBetLabel != null)
            totalBetLabel.text = currentTotalBet > 0 ? FormatAmount(currentTotalBet) : "0";
    }

    private string FormatAmount(double val)
    {
        if (val >= 10000) return $"{val / 1000:0.##}K";
        return val.ToString("0.##");
    }

    private void ShowPopup(string msg)
    {
        Debug.LogWarning("BetManager: " + msg);
    }

    // Repeatpanel and betbuttons panel slide in from the left coin image

    // Call this once before any animation runs — add to Start() or OnRoundStart()
    private void RecordPanelHomes()
    {
        if (homesRecorded) return;
        homesRecorded = true;
        repeatBetPanelHomeX = RepeatBetPanel.GetComponent<RectTransform>().anchoredPosition.x;
        betButtonPanelHomeX = BetButtonPanel.GetComponent<RectTransform>().anchoredPosition.x;
    }

    private void SlideInFromLeft(GameObject panel)
    {
        if (panel == null) return;
        if (panel == BetButtonPanel)
        {
            RepeatBetPanel.SetActive(false);
        }
        if (panel == RepeatBetPanel)
        {
            BetButtonPanel.SetActive(false);
        }
        RecordPanelHomes();

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.DOKill(true); // ← kill any in-progress tween, snap to its current target

        // Determine the correct home X for this panel
        float homeX = (panel == RepeatBetPanel) ? repeatBetPanelHomeX : betButtonPanelHomeX;

        panel.SetActive(true);
        rect.anchoredPosition = new Vector2(homeX - 200f, rect.anchoredPosition.y); // start offscreen
        rect.DOAnchorPosX(homeX, 0.7f).SetEase(Ease.OutCubic);
    }

    internal void SlideOutToLeft(GameObject panel)
    {
        if (panel == null) return;
        RecordPanelHomes();

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.DOKill(true); // ← kill any in-progress tween first

        float homeX = (panel == RepeatBetPanel) ? repeatBetPanelHomeX : betButtonPanelHomeX;

        rect.DOAnchorPosX(homeX - 200f, 0.5f).SetEase(Ease.InCubic)
            .OnComplete(() =>
            {
                panel.SetActive(false);
                // Reset to home so SlideIn always starts from a known position
                rect.anchoredPosition = new Vector2(homeX, rect.anchoredPosition.y);
            });
    }
}