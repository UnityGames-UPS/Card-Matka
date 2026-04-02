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
    [SerializeField] private Button PrevPageDouble_Button;
    [SerializeField] private Button NextPageDouble_Button;


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

    private int currentPage = 1;
    private int totalPages = 1;
    private bool waitingForData = false;


    private void Start()
    {
        PrevPage_Button?.onClick.AddListener(OnPrevPage);
        NextPage_Button?.onClick.AddListener(OnNextPage);

        PrevPageDouble_Button?.onClick.AddListener(OnPrevDoublePage);
        NextPageDouble_Button?.onClick.AddListener(OnNextDoublePage);

        // Hide all rows at startup
        foreach (var row in HistoryRows)
            row.gameObject.SetActive(false);
    }

    internal void OnDataReceived(List<HistoryRound> history, HistoryMeta meta)
    {
        waitingForData = false;

        if (history == null || meta == null) return;

        currentPage = meta.page;
        totalPages = meta.pages;

        PopulateRows(history);
        RefreshPageUI();
    }


    private void OnPrevPage()
    {
        if (currentPage > 1)
        {
            RequestPage(currentPage - 1);
            NextPage_Button.interactable = true;
            NextPageDouble_Button.interactable = true;
        }
        else
        {
            PrevPage_Button.interactable = false;
            PrevPageDouble_Button.interactable = false;
        }
    }

    private void OnNextPage()
    {
        if (currentPage < totalPages)
        {
            RequestPage(currentPage + 1);
            PrevPage_Button.interactable = true;
            PrevPageDouble_Button.interactable = true;
        }
        else
        {
            NextPage_Button.interactable = false;
            NextPageDouble_Button.interactable = false;
        }
    }

    private void OnPrevDoublePage()
    {
        if (currentPage > 1)
        {
            RequestPage(1);
            NextPageDouble_Button.interactable = true;
            NextPage_Button.interactable = true;
        }
        else
        {
            PrevPage_Button.interactable = false;
            PrevPageDouble_Button.interactable = false;
        }
    }

    private void OnNextDoublePage()
    {
        if (currentPage < totalPages)
        {
            RequestPage(totalPages);
            PrevPage_Button.interactable = true;
            PrevPageDouble_Button.interactable = true;
        }
        else
        {
            NextPage_Button.interactable = false;
            NextPageDouble_Button.interactable = false;
        }
    }

    private void RequestPage(int page)
    {
        if (waitingForData) return;
        waitingForData = true;
        gameManager.RequestHistory(page);
        SetNavButtonsInteractable(false); // disable while waiting
    }


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
            if (currentPage > 1)
            {
                PrevPage_Button.interactable = enable;
                PrevPageDouble_Button.interactable = enable;
            }
        if (NextPage_Button != null)
            if (currentPage < totalPages)
            {
                NextPage_Button.interactable = enable;
                NextPageDouble_Button.interactable = enable;
            }
        if (totalPages == 1)
        {
            PrevPage_Button.interactable = false;
            PrevPageDouble_Button.interactable = false;
            NextPage_Button.interactable = false;
            NextPageDouble_Button.interactable = false;
        }
    }
}