using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;


public class LeaderBoardObejct : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────────
    [SerializeField] private TMP_Text BalanceText;    
    [SerializeField] internal TMP_Text playerNameText;

    [Header("References")]
    [SerializeField] private LeaderBoardController leaderBoardControllerController;

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────────
    internal void SetRichestData(Richest richests, int rowIndex)
    {


        if (playerNameText != null)
            playerNameText.text = richests.username;

        if (BalanceText != null)
            BalanceText.text = richests.balance.ToString();
    }

    internal void SetWinnerData(Winner winners, int rowIndex)
    {


        if (playerNameText != null)
            playerNameText.text = winners.username;

        if (BalanceText != null)
            BalanceText.text = winners.totalWins.ToString();
    }

}