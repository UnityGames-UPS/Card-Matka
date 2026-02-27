using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

[RequireComponent(typeof(Button))]
public class BetButton : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────────
    [Header("Bet Info")]
    [Tooltip("'main_bets' | 'side_bets' | 'op_bets'")]
    [SerializeField] internal string betType;

    [Tooltip("e.g. 'K', 'specific_Hearts', 'Q_clubs'")]
    [SerializeField] internal string betOption;

    [Header("UI References")]
    [SerializeField] internal RectTransform chipParent;
    [SerializeField] internal GameObject RedBg;
    [SerializeField] internal TMP_Text totalBetLabel;
    [SerializeField] private Button slotButton;

    // ── Private ────────────────────────────────────────────────────────────────
    private BetManager betManager;

    internal void Init(BetManager manager)
    {
        betManager = manager;

        if (slotButton == null)
            slotButton = GetComponentInChildren<Button>();

        slotButton.onClick.RemoveAllListeners();
        slotButton.onClick.AddListener(OnButtonClicked);
    }

    private void OnButtonClicked()
    {
        transform.DOKill(false);
        transform.DOPunchScale(Vector3.one * 0.12f, 0.25f, 5, 0.5f);
        betManager.PlaceBet(this);
    }
}