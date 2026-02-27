using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HistoryController : MonoBehaviour
{


    [Header("Row Views (fixed list — one per visible row in the UI)")]
    [SerializeField] private List<HistoryRowView> HistoryRows;

    [Header("Pagination")]
    [SerializeField] private TMP_Text CurrentPageInfo_Text;
    [SerializeField] private TMP_Text TotalPageInfo_Text;
    [SerializeField] private Button PrevPage_Button;
    [SerializeField] private Button NextPage_Button;

    [Header("Sprites")]
    [SerializeField] internal Sprite ClubsSprite;
    [SerializeField] internal Sprite DiamondsSprite;
    [SerializeField] internal Sprite HeartsSprite;
    [SerializeField] internal Sprite SpadesSprite;

    [SerializeField] internal Sprite KSprite;
    [SerializeField] internal Sprite QSprite;
    [SerializeField] internal Sprite JSprite;

    [Header("References")]
    [SerializeField] private GameManager gameManager;

    // ── State ──────────────────────────────────────────────────────────────────
    private int currentPage = 1;
    private int totalPages = 1;
    private bool waitingForData = false;

    // ─────────────────────────────────────────────────────────────────────────
    //  Unity
    // ─────────────────────────────────────────────────────────────────────────
    private void Start()
    {
        PrevPage_Button?.onClick.AddListener(OnPrevPage);
        NextPage_Button?.onClick.AddListener(OnNextPage);

        // Hide all rows at startup
        foreach (var row in HistoryRows)
            row.gameObject.SetActive(false);
    }

    // Called by GameManager after SocketIOManager receives the history ack
    internal void OnDataReceived(List<HistoryRound> history, HistoryMeta meta)
    {
        waitingForData = false;

        if (history == null || meta == null) return;

        currentPage = meta.page;
        totalPages = meta.pages;

        PopulateRows(history);
        RefreshPageUI();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Pagination buttons
    // ─────────────────────────────────────────────────────────────────────────
    private void OnPrevPage()
    {
        if (currentPage > 1) RequestPage(currentPage - 1);
    }

    private void OnNextPage()
    {
        if (currentPage < totalPages) RequestPage(currentPage + 1);
    }

    private void RequestPage(int page)
    {
        if (waitingForData) return;
        waitingForData = true;
        gameManager.RequestHistory(page);
        SetNavButtonsInteractable(false); // disable while waiting
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Display
    // ─────────────────────────────────────────────────────────────────────────
    private void PopulateRows(List<HistoryRound> history)
    {
        // Hide all rows first
        foreach (var row in HistoryRows)
            row?.gameObject.SetActive(false);

        // Fill only as many rows as we have data for
        int count = Mathf.Min(HistoryRows.Count, history.Count);
        for (int i = 0; i < count; i++)
        {
            HistoryRows[i].SetData(history[i], i + 1, currentPage);
            HistoryRows[i].gameObject.SetActive(true);
        }
    }

    private void RefreshPageUI()
    {
        if (CurrentPageInfo_Text != null)
            CurrentPageInfo_Text.text = $"{currentPage}";

        if (TotalPageInfo_Text != null)
            TotalPageInfo_Text.text = $"{totalPages}";

        SetNavButtonsInteractable(true);
    }

    private void SetNavButtonsInteractable(bool enable)
    {
        if (PrevPage_Button != null)
            PrevPage_Button.interactable = enable && currentPage > 1;
        if (NextPage_Button != null)
            NextPage_Button.interactable = enable && currentPage < totalPages;
    }
}