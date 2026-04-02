using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LeaderBoardController : MonoBehaviour
{
    [Header("Row Views (fixed list — one per visible row in the UI)")]
    [SerializeField] internal List<LeaderBoardObejct> RichestRows;
    [SerializeField] internal List<LeaderBoardObejct> BiggestRows;

    [Header("Avatar Sprites Pool")]
    [Tooltip("Fill these with all your available profile icon sprites. " +
             "Each unique username joining will be assigned the next sprite in order, " +
             "and that assignment persists for the entire session.")]
    [SerializeField] private List<Sprite> avatarSprites;

    [Header("References")]
    [SerializeField] private GameManager gameManager;

    // ── Persistent username → sprite mapping ─────────────────────────────────
    private Dictionary<string, Sprite> usernameAvatarMap = new Dictionary<string, Sprite>();
    private int nextAvatarIndex = 0;

    private bool waitingForData = false;


    private void Start()
    {
        foreach (var row in RichestRows)
            row.gameObject.SetActive(false);
        foreach (var row in BiggestRows)
            row.gameObject.SetActive(false);
    }

    internal void OnLeaderBoardDataReceived(Leaderboards leaderboards)
    {
        waitingForData = false;

        // FIX 1: Guard changed — allow empty lists so stale rows get cleared.
        // Previously returning early when richest/winners were null meant rows
        // from departed players were never hidden.
        if (leaderboards == null) return;

        PopulateRows(leaderboards);
    }

    /// <summary>
    /// Returns the RectTransform of the leaderboard row for the given username.
    /// Uses the RAW (unmasked) username stored in the row's dataset, not the
    /// displayed text, so masking doesn't break chip-spawn lookups.
    /// </summary>
    internal RectTransform GetOriginForUsername(string username)
    {
        if (string.IsNullOrEmpty(username)) return null;

        foreach (var row in RichestRows)
            if (row != null && row.gameObject.activeSelf && row.RawUsername == username)
                return row.GetComponent<RectTransform>();

        foreach (var row in BiggestRows)
            if (row != null && row.gameObject.activeSelf && row.RawUsername == username)
                return row.GetComponent<RectTransform>();

        return null;
    }

    /// <summary>
    /// Returns (or creates) a stable Sprite assignment for the given username.
    /// </summary>
    internal Sprite GetOrAssignAvatar(string username)
    {
        if (string.IsNullOrEmpty(username)) return null;

        if (usernameAvatarMap.TryGetValue(username, out Sprite existing))
            return existing;

        if (avatarSprites == null || avatarSprites.Count == 0)
        {
            Debug.LogWarning("LeaderBoardController: avatarSprites pool is empty!");
            usernameAvatarMap[username] = null;
            return null;
        }

        Sprite assigned = avatarSprites[nextAvatarIndex % avatarSprites.Count];
        nextAvatarIndex++;
        usernameAvatarMap[username] = assigned;

        Debug.Log($"LeaderBoardController: Assigned avatar index {nextAvatarIndex - 1} to '{username}'");
        return assigned;
    }

    /// <summary>
    /// FIX 2: Masks a username so only the first 2 and last 2 characters are
    /// visible; everything in between is replaced with '*'.
    ///
    /// Examples:
    ///   "yash-v"   → "ya**-v"    (6 chars: show 2 + 2 stars + last 2)
    ///   "ab"       → "ab"        (≤4 chars: show as-is, nothing to mask)
    ///   "abc"      → "ab*"       (3 chars: show first 2, mask last 1 — last 2 overlaps)
    ///   "abcd"     → "ab**"      (4 chars: boundary case)
    ///   "abcde"    → "ab*de"     (5 chars)
    ///   "abcdefgh" → "ab****gh"  (8 chars)
    /// </summary>
    internal string MaskUsername(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        int len = name.Length;

        // Names of 4 characters or fewer: nothing meaningful to hide
        if (len <= 4) return name;

        // First 2 chars + stars for the middle + last 2 chars
        int middleLen = len - 4; // characters between the first 2 and last 2
        string stars = new string('*', middleLen);
        return name.Substring(0, 2) + stars + name.Substring(len - 2, 2);
    }

    private void PopulateRows(Leaderboards leaderboards)
    {
        // ── Hide ALL rows first ───────────────────────────────────────────────
        // This is what actually fixes the stale-player bug: every row is
        // deactivated unconditionally before we re-populate with fresh data,
        // so any row whose player is no longer in the list simply stays hidden.
        foreach (var row in RichestRows)
            row?.gameObject.SetActive(false);
        foreach (var row in BiggestRows)
            row?.gameObject.SetActive(false);

        // ── Richest ──────────────────────────────────────────────────────────
        if (leaderboards.richest != null)
        {
            List<Richest> richests = leaderboards.richest;
            int count = Mathf.Min(RichestRows.Count, richests.Count);
            for (int i = 0; i < count; i++)
            {
                Sprite avatar = GetOrAssignAvatar(richests[i].username);

                // Store raw username on the row object for GetOriginForUsername()
                RichestRows[i].RawUsername = richests[i].username;

                // Mask the display name before passing to SetRichestData
                Richest masked = new Richest
                {
                    username = MaskUsername(richests[i].username),
                    balance  = richests[i].balance,
                    rank     = richests[i].rank
                };

                RichestRows[i].SetRichestData(masked, i, avatar);
                RichestRows[i].gameObject.SetActive(true);
            }
        }

        // ── Winners (Biggest) ─────────────────────────────────────────────────
        if (leaderboards.winners != null)
        {
            List<Winner> winners = leaderboards.winners;
            int count = Mathf.Min(BiggestRows.Count, winners.Count);
            for (int i = 0; i < count; i++)
            {
                Sprite avatar = GetOrAssignAvatar(winners[i].username);

                BiggestRows[i].RawUsername = winners[i].username;

                Winner masked = new Winner
                {
                    username   = MaskUsername(winners[i].username),
                    totalWins  = winners[i].totalWins,
                    rank       = winners[i].rank
                };

                BiggestRows[i].SetWinnerData(masked, i, avatar);
                BiggestRows[i].gameObject.SetActive(true);
            }
        }
    }
}