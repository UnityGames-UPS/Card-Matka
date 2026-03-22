using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LeaderBoardController : MonoBehaviour
{


    [Header("Row Views (fixed list — one per visible row in the UI)")]
    [SerializeField] internal List<LeaderBoardObejct> RichestRows;
    [SerializeField] internal List<LeaderBoardObejct> BiggestRows;

    [Header("References")]
    [SerializeField] private GameManager gameManager;

    private bool waitingForData = false;


    private void Start()
    {
        // Hide all rows at startup
        foreach (var row in RichestRows)
            row.gameObject.SetActive(false);
        foreach (var row in BiggestRows)
            row.gameObject.SetActive(false);
    }

    internal void OnLeaderBoardDataReceived(Leaderboards leaderboards)
    {
        waitingForData = false;

        if (leaderboards == null || leaderboards.richest == null || leaderboards.winners == null) return;

        PopulateRows(leaderboards);

    }

    /// <summary>
    /// Returns the RectTransform of the leaderboard row that belongs to the given username.
    /// Searches both Richest and Winners (Biggest) lists.
    /// Returns null if the username is not currently visible in the leaderboard.
    /// </summary>
    internal RectTransform GetOriginForUsername(string username)
    {
        if (string.IsNullOrEmpty(username)) return null;

        foreach (var row in RichestRows)
            if (row != null && row.gameObject.activeSelf && row.playerNameText.text == username)
                return row.GetComponent<RectTransform>();

        foreach (var row in BiggestRows)
            if (row != null && row.gameObject.activeSelf && row.playerNameText.text == username)
                return row.GetComponent<RectTransform>();

        return null;
    }

    private void PopulateRows(Leaderboards leaderboards)
    {
        foreach (var row in RichestRows)
            row?.gameObject.SetActive(false);
        foreach (var row in BiggestRows)
            row?.gameObject.SetActive(false);


        if (leaderboards.richest != null)
        {
            List<Richest> richests = leaderboards.richest;
            int count = Mathf.Min(RichestRows.Count, richests.Count);
            for (int i = 0; i < count; i++)
            {
                RichestRows[i].SetRichestData(richests[i], i);
                RichestRows[i].gameObject.SetActive(true);
            }
        }
        if (leaderboards.winners != null)
        {
            List<Winner> winners = leaderboards.winners;

            int count = Mathf.Min(BiggestRows.Count, winners.Count);
            for (int i = 0; i < count; i++)
            {
                BiggestRows[i].SetWinnerData(winners[i], i);
                BiggestRows[i].gameObject.SetActive(true);
            }
        }
    }
}