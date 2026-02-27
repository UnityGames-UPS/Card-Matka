using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// Attach to each fixed history row GameObject in your History Panel.
/// Wire the text fields in the Inspector, then add all rows to HistoryController.HistoryRows list.
/// </summary>
public class HistoryRowView : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────────
    [SerializeField] private TMP_Text SerialNo;    // e.g. "K ♠"
    [SerializeField] private TMP_Text RoundIDText;
    [SerializeField] private TMP_Text DateText;      // e.g. "26 Feb, 09:59"
    [SerializeField] private TMP_Text BetText;       // e.g. "17"
    [SerializeField] private TMP_Text WinText;       // e.g. "+17.8" or "-"
    [SerializeField] private TMP_Text PLText;        // e.g. "+0.8" or "-0.2"
    [SerializeField] private Image SymbolImage;     // e.g. "♠" icon
    [SerializeField] private Image TextImage;

    [Header("References")]
    [SerializeField] private HistoryController historyController;

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────────
    internal void SetData(HistoryRound round, int rowIndex, int currentPage)
    {
        if (round == null) return;

        if (SerialNo != null)
            SerialNo.text = (rowIndex + (currentPage - 1) * 8).ToString();

        if (RoundIDText != null)
            RoundIDText.text = round.round_id;

        // Date  e.g. "26 Feb, 09:59"
        if (DateText != null)
        {
            if (DateTime.TryParse(round.created_at, out DateTime dt))
                DateText.text = dt.ToString("dd MMM, HH:mm");
            else
                DateText.text = round.created_at ?? "";
        }

        // Amounts
        if (BetText != null)
            BetText.text = Format(round.bet_amount);

        //bool won = round.win_amount > 0;
        if (WinText != null)
            WinText.text = round.win_amount.ToString();

        if (PLText != null)
            //double pl = round.win_amount - round.bet_amount;
            PLText.text = (round.win_amount - round.bet_amount).ToString();

        // Result  e.g. "K ♠"
        if (SymbolImage != null)
            SymbolImage.sprite = ToSymbol(round.result_suit);

        if (TextImage != null)
            TextImage.sprite = ToText(round.result_card);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────
    private Sprite ToSymbol(string suit)
    {
        return suit switch
        {
            "spades" => historyController.SpadesSprite,
            "clubs" => historyController.ClubsSprite,
            "diamonds" => historyController.DiamondsSprite,
            "hearts" => historyController.HeartsSprite,
            _ => null
        };
    }

    private Sprite ToText(string suit)
    {
        return suit switch
        {
            "K" => historyController.KSprite,
            "Q" => historyController.QSprite,
            "J" => historyController.JSprite,
            _ => null
        };
    }

    private string Format(double val)
    {
        if (val >= 1000) return $"{val / 1000:0.#}K";
        return val.ToString("0.##");
    }
}