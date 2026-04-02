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

    [Tooltip("Assign the Image component that should display the player's profile icon.")]
    [SerializeField] private Image avatarImage;

    [Header("References")]
    [SerializeField] private LeaderBoardController leaderBoardControllerController;

    internal string RawUsername;

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────────

    internal void SetRichestData(Richest richest, int rowIndex, Sprite avatar)
    {
        if (playerNameText != null)
            playerNameText.text = richest.username;

        if (BalanceText != null)
            BalanceText.text = FormatAmount(richest.balance);

        SetAvatar(avatar);
    }

    private string FormatAmount(double val)
    {
        if (val >= 1000) return $"{val / 1000:0.#}K";
        return val.ToString("0.##");
    }

    internal void SetWinnerData(Winner winner, int rowIndex, Sprite avatar)
    {
        if (playerNameText != null)
            playerNameText.text = winner.username;

        if (BalanceText != null)
            BalanceText.text = FormatAmount(winner.totalWins);

        SetAvatar(avatar);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void SetAvatar(Sprite avatar)
    {
        if (avatarImage == null) return;

        if (avatar != null)
        {
            avatarImage.sprite = avatar;
            avatarImage.enabled = true;
        }
        else
        {
            // No sprite available — hide the image so the slot stays clean
            avatarImage.enabled = false;
        }
    }
}