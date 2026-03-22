using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.EventSystems;
using Radishmouse;

public class WinHistoryController : MonoBehaviour
{
    [Header("Prefabs & Sprites")]
    [SerializeField] private GameObject PlainDotImagePrefab;
    [SerializeField] private GameObject RedDotImagePrefab;
    [SerializeField] private GameObject CardPrefab;
    [SerializeField] private GameObject WhiteCardPrefab;

    [SerializeField] internal Sprite BlackCardBg;
    [SerializeField] internal Sprite RedCardBg;
    [SerializeField] internal Sprite GreenCardBg;
    [SerializeField] internal Sprite WhiteCardBg;

    [SerializeField] internal Sprite KWhiteSymbolImage;
    [SerializeField] internal Sprite QWhiteSymbolImage;
    [SerializeField] internal Sprite JWhiteSymbolImage;

    [SerializeField] internal Sprite SpadeWhiteSymbolImage;
    [SerializeField] internal Sprite HeartWhiteSymbolImage;
    [SerializeField] internal Sprite ClubWhiteSymbolImage;
    [SerializeField] internal Sprite DiamondWhiteSymbolImage;

    [SerializeField] internal Sprite KRedSymbolImage;
    [SerializeField] internal Sprite QRedSymbolImage;
    [SerializeField] internal Sprite JRedSymbolImage;

    [SerializeField] internal Sprite HeartRedSymbolImage;
    [SerializeField] internal Sprite DiamondRedSymbolImage;

    [SerializeField] internal Sprite KBlackSymbolImage;
    [SerializeField] internal Sprite QBlackSymbolImage;
    [SerializeField] internal Sprite JBlackSymbolImage;

    [SerializeField] internal Sprite SpadeBlackSymbolImage;
    [SerializeField] internal Sprite ClubBlackSymbolImage;

    [Header("Line Renderer Settings")]
    [SerializeField] private Color lineColor = Color.black;
    [SerializeField] private float lineThickness = 3f;

    [Header("Line Overlay Panels (stretch-fill over each graph area)")]
    [SerializeField] private RectTransform SymbolGraphOverlay;  // overlay covering spade/heart/club/diamond rows
    [SerializeField] private RectTransform TextGraphOverlay;    // overlay covering K/Q/J rows

    [Header("Outer Panel")]
    [SerializeField] private GameObject CardPanel;
    [SerializeField] private List<GameObject> InfoObjects;
    [SerializeField] private TMP_Text SpadeOuterPrecentageText;
    [SerializeField] private TMP_Text HeartOuterPrecentageText;
    [SerializeField] private TMP_Text ClubOuterPrecentageText;
    [SerializeField] private TMP_Text DiamondOuterPrecentageText;
    [SerializeField] private TMP_Text KOuterPrecentageText;
    [SerializeField] private TMP_Text QOuterPrecentageText;
    [SerializeField] private TMP_Text JOuterPrecentageText;


    [Header("Big Stat Panel Object")]
    [SerializeField] private GameObject statPanel;
    [SerializeField] private GameObject BigStatPanel;
    [SerializeField] private List<Button> StatPanelButtons;
    [SerializeField] private Toggle AutoOpenToggle;

    [Header("Roadmap Button")]
    [SerializeField] private Button RoadMapButton;
    [SerializeField] private float scaleFactor = 1.1f;
    [SerializeField] private float duration = 0.2f;

    [Header("Top Panel")]
    [SerializeField] private GameObject InnerCardPanel;

    [Header("SymbolGraph Row Objects")]
    [SerializeField] private GameObject SpadeObject;
    [SerializeField] private GameObject HeartObject;
    [SerializeField] private GameObject ClubObject;
    [SerializeField] private GameObject DiamondObject;

    [SerializeField] private TMP_Text SpadePrecentageText;
    [SerializeField] private TMP_Text HeartPrecentageText;
    [SerializeField] private TMP_Text ClubPrecentageText;
    [SerializeField] private TMP_Text DiamondPrecentageText;

    [Header("TextGraph Row Objects")]
    [SerializeField] private GameObject KObject;
    [SerializeField] private GameObject QObject;
    [SerializeField] private GameObject JObject;

    [SerializeField] private TMP_Text KPrecentageText;
    [SerializeField] private TMP_Text QPrecentageText;
    [SerializeField] private TMP_Text JPrecentageText;

    [Header("Card Grid Object")]
    [SerializeField] private GameObject CardGridObject;


    private int totalResults = 0;
    private bool isStatPanelOpen = false;
    private Vector3 cardPanelStartPos;
    private Vector2 smallStartPanelDimension;
    private bool isAutoOpenStatPanelDisable = false;

    private Dictionary<string, int> suitCounts = new Dictionary<string, int>
        { { "spade", 0 }, { "heart", 0 }, { "club", 0 }, { "diamond", 0 } };
    private Dictionary<string, int> textCounts = new Dictionary<string, int>
        { { "K", 0 }, { "Q", 0 }, { "J", 0 } };

    private List<RectTransform> symbolGraphRedDots = new List<RectTransform>();
    private List<RectTransform> textGraphRedDots = new List<RectTransform>();

    private UILineRenderer symbolGraphLine = null;
    private UILineRenderer textGraphLine = null;

    // Debounce handles — we cancel & restart instead of stacking coroutines
    private Coroutine symbolRefreshCoroutine = null;
    private Coroutine textRefreshCoroutine = null;

    // Set to Time.time whenever a DOTween panel animation starts that moves the overlay.
    // The refresh loop keeps redrawing until this time has passed.
    private float lineRefreshUntil = 0f;

    private List<GameObject> gridCards = new List<GameObject>();

    private Canvas rootCanvas;


    [Serializable]
    private class StatEntry
    {
        public string resultCard;
        public string resultSuit;
    }


    private void Awake()
    {
        rootCanvas = GetComponentInParent<Canvas>();
    }

    private void Start()
    {
        foreach (var btn in StatPanelButtons)
            btn.onClick.AddListener(StatPanelClicked);
        cardPanelStartPos = CardPanel.transform.localPosition;

        AutoOpenToggle.isOn = isAutoOpenStatPanelDisable;
        AutoOpenToggle.onValueChanged.AddListener((value) =>
        {
            isAutoOpenStatPanelDisable = value;
        });

        smallStartPanelDimension = statPanel.GetComponent<RectTransform>().rect.size;

        AddClickScaler(RoadMapButton);

    }


    private bool isLoadingStats = false;

    internal void LoadStats(List<string> stats)
    {
        if (stats == null || stats.Count == 0) return;
        ClearHistory();

        isLoadingStats = true; // suppress per-dot coroutines

        foreach (string statJson in stats)
        {
            StatEntry entry = JsonUtility.FromJson<StatEntry>(statJson);
            if (entry == null) { Debug.LogWarning($"WinHistoryController: bad json: {statJson}"); continue; }

            string cardKey = entry.resultCard?.ToUpper();
            string suitKey = NormalizeSuit(entry.resultSuit);
            if (string.IsNullOrEmpty(cardKey) || string.IsNullOrEmpty(suitKey)) continue;

            totalResults++;
            if (suitCounts.ContainsKey(suitKey)) suitCounts[suitKey]++;
            if (textCounts.ContainsKey(cardKey)) textCounts[cardKey]++;

            AddRoadmapCard(cardKey, suitKey);
            AddGraphDots(cardKey, suitKey);
            UpdateGrid(cardKey, suitKey);
        }

        UpdatePercentages();
        isLoadingStats = false;

        // One bulk refresh after all dots are in the hierarchy and layout has settled
        StartCoroutine(RefreshLineAfterLayout(symbolGraphRedDots, SymbolGraphOverlay, "symbol"));
        StartCoroutine(RefreshLineAfterLayout(textGraphRedDots, TextGraphOverlay, "text"));
    }

    internal void ClearHistory()
    {
        totalResults = 0;
        foreach (string k in new[] { "spade", "heart", "club", "diamond" }) suitCounts[k] = 0;
        foreach (string k in new[] { "K", "Q", "J" }) textCounts[k] = 0;

        symbolGraphRedDots.Clear();
        textGraphRedDots.Clear();
        gridCards.Clear();

        // Stop any pending refresh coroutines
        if (symbolRefreshCoroutine != null) { StopCoroutine(symbolRefreshCoroutine); symbolRefreshCoroutine = null; }
        if (textRefreshCoroutine != null) { StopCoroutine(textRefreshCoroutine); textRefreshCoroutine = null; }

        // Destroy line renderers
        if (symbolGraphLine != null) { Destroy(symbolGraphLine.gameObject); symbolGraphLine = null; }
        if (textGraphLine != null) { Destroy(textGraphLine.gameObject); textGraphLine = null; }

        if (InnerCardPanel != null)
            foreach (Transform c in InnerCardPanel.transform) Destroy(c.gameObject);

        foreach (var row in new[] { SpadeObject, HeartObject, ClubObject, DiamondObject, KObject, QObject, JObject })
            if (row != null)
                foreach (Transform c in row.transform) Destroy(c.gameObject);

        if (CardGridObject != null)
            foreach (Transform c in CardGridObject.transform) Destroy(c.gameObject);

        foreach (var lbl in new[] { SpadePrecentageText, HeartPrecentageText, ClubPrecentageText,
                                    DiamondPrecentageText, KPrecentageText, QPrecentageText, JPrecentageText ,
                                    SpadeOuterPrecentageText,HeartOuterPrecentageText,ClubOuterPrecentageText,
                                    DiamondOuterPrecentageText,KOuterPrecentageText,QOuterPrecentageText,JOuterPrecentageText})
            if (lbl != null) lbl.text = "0.0%";
    }


    internal void OnResult(string resultCard, string resultSuit)
    {
        totalResults++;
        string suitKey = NormalizeSuit(resultSuit);
        string cardKey = resultCard.ToUpper();

        if (suitCounts.ContainsKey(suitKey)) suitCounts[suitKey]++;
        if (textCounts.ContainsKey(cardKey)) textCounts[cardKey]++;

        AddRoadmapCard(cardKey, suitKey);
        AddGraphDots(cardKey, suitKey);
        UpdatePercentages();
        UpdateGrid(cardKey, suitKey);
        if (!isAutoOpenStatPanelDisable && !BigStatPanel.activeSelf)
        {
            StatPanelClicked();
        }
    }


    private void AddRoadmapCard(string cardKey, string suitKey)
    {
        if (CardPrefab == null || InnerCardPanel == null) return;
        GameObject go = Instantiate(WhiteCardPrefab, InnerCardPanel.transform);
        Card card = go.GetComponent<Card>();
        if (suitKey == "spade" || suitKey == "club")
        {
            if (card != null)
                card.SetData(WhiteCardBg, GetSuitSprite(suitKey, SpriteVariant.Black), GetTextSprite(cardKey, SpriteVariant.Black));
        }
        if (suitKey == "heart" || suitKey == "diamond")
        {
            if (card != null)
                card.SetData(WhiteCardBg, GetSuitSprite(suitKey, SpriteVariant.Red), GetTextSprite(cardKey, SpriteVariant.Red));
        }
    }


    private void AddGraphDots(string cardKey, string suitKey)
    {
        SpawnDot(SpadeObject, suitKey == "spade", symbolGraphRedDots, SymbolGraphOverlay);
        SpawnDot(HeartObject, suitKey == "heart", symbolGraphRedDots, SymbolGraphOverlay);
        SpawnDot(ClubObject, suitKey == "club", symbolGraphRedDots, SymbolGraphOverlay);
        SpawnDot(DiamondObject, suitKey == "diamond", symbolGraphRedDots, SymbolGraphOverlay);

        SpawnDot(KObject, cardKey == "K", textGraphRedDots, TextGraphOverlay);
        SpawnDot(QObject, cardKey == "Q", textGraphRedDots, TextGraphOverlay);
        SpawnDot(JObject, cardKey == "J", textGraphRedDots, TextGraphOverlay);
    }


    private void SpawnDot(GameObject rowObject, bool isWinner, List<RectTransform> sharedDotList, RectTransform overlay)
    {
        if (rowObject == null) return;
        GameObject prefab = isWinner ? RedDotImagePrefab : PlainDotImagePrefab;
        if (prefab == null) return;

        GameObject dot = Instantiate(prefab, rowObject.transform);

        if (isWinner)
        {
            sharedDotList.Add(dot.GetComponent<RectTransform>());

            if (!isLoadingStats)
            {
                bool isSymbol = (sharedDotList == symbolGraphRedDots);
                ScheduleLineRefresh(isSymbol, sharedDotList, overlay, isSymbol ? "symbol" : "text");
            }
        }
    }


    // ── Line refresh helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Cancels any pending refresh for this graph and starts a fresh one.
    /// Called from SpawnDot so only the LAST dot per result triggers the rebuild.
    /// </summary>
    private void ScheduleLineRefresh(bool isSymbol, List<RectTransform> dots,
                                     RectTransform overlay, string graphKey)
    {
        if (isSymbol)
        {
            if (symbolRefreshCoroutine != null) StopCoroutine(symbolRefreshCoroutine);
            symbolRefreshCoroutine = StartCoroutine(RefreshLineAfterLayout(dots, overlay, graphKey));
        }
        else
        {
            if (textRefreshCoroutine != null) StopCoroutine(textRefreshCoroutine);
            textRefreshCoroutine = StartCoroutine(RefreshLineAfterLayout(dots, overlay, graphKey));
        }
    }

    /// <summary>
    /// Waits for layout to settle (2 frames), draws the line once, then keeps
    /// redrawing every frame until lineRefreshUntil has passed — this covers the
    /// case where the stats panel is animating (DOSizeDelta) and dot world-positions
    /// are still changing mid-tween.
    /// </summary>
    private IEnumerator RefreshLineAfterLayout(List<RectTransform> dots,
                                               RectTransform overlay, string graphKey)
    {
        // Wait for HorizontalLayoutGroup to finish repositioning
        yield return null;
        yield return null;

        if (dots.Count < 2) yield break;

        UILineRenderer lineRef = GetOrCreateLineRenderer(overlay, graphKey);
        if (lineRef == null) yield break;

        // Draw once immediately after layout settles
        DrawLine(lineRef, dots, overlay);

        // Keep redrawing every frame while the panel is still animating
        while (Time.time < lineRefreshUntil)
        {
            yield return null;
            DrawLine(lineRef, dots, overlay);
        }

        // One final draw after animation fully completes
        DrawLine(lineRef, dots, overlay);
    }

    /// <summary>
    /// Call this whenever a DOTween animation starts that moves/resizes the overlay panel.
    /// Pass the animation duration so the refresh loop knows how long to keep redrawing.
    /// </summary>
    internal void NotifyPanelAnimating(float duration)
    {
        lineRefreshUntil = Time.time + duration + 0.05f; // small buffer

        // Re-trigger both graph refreshes so they enter the "keep drawing" loop
        if (symbolGraphRedDots.Count >= 2)
            ScheduleLineRefresh(true, symbolGraphRedDots, SymbolGraphOverlay, "symbol");
        if (textGraphRedDots.Count >= 2)
            ScheduleLineRefresh(false, textGraphRedDots, TextGraphOverlay, "text");
    }

    private UILineRenderer GetOrCreateLineRenderer(RectTransform overlay, string graphKey)
    {
        bool isSymbol = (graphKey == "symbol");
        UILineRenderer lineRef = isSymbol ? symbolGraphLine : textGraphLine;

        if (lineRef != null) return lineRef;

        if (overlay == null)
        {
            Debug.LogWarning($"WinHistoryController: {graphKey} overlay not assigned!");
            return null;
        }

        GameObject lineGO = new GameObject($"GraphLine_{graphKey}", typeof(RectTransform));
        lineGO.transform.SetParent(overlay, false);

        UILineRenderer lr = lineGO.AddComponent<UILineRenderer>();
        lr.color = lineColor;
        lr.thickness = lineThickness;
        lr.center = false;

        RectTransform rt = lineGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);

        if (isSymbol) symbolGraphLine = lr;
        else textGraphLine = lr;

        return lr;
    }

    private void DrawLine(UILineRenderer lineRef, List<RectTransform> dots, RectTransform overlay)
    {
        var points = new Vector2[dots.Count];
        for (int i = 0; i < dots.Count; i++)
        {
            if (dots[i] == null) continue;
            Vector3 worldCentre = dots[i].TransformPoint(Vector3.zero);
            points[i] = overlay.InverseTransformPoint(worldCentre);
        }
        lineRef.points = points;
        lineRef.SetVerticesDirty();
    }


    private void UpdatePercentages()
    {
        if (totalResults == 0) return;
        SetPercentText(SpadePrecentageText, suitCounts["spade"]);
        SetPercentText(HeartPrecentageText, suitCounts["heart"]);
        SetPercentText(ClubPrecentageText, suitCounts["club"]);
        SetPercentText(DiamondPrecentageText, suitCounts["diamond"]);
        SetPercentText(KPrecentageText, textCounts["K"]);
        SetPercentText(QPrecentageText, textCounts["Q"]);
        SetPercentText(JPrecentageText, textCounts["J"]);

        SetPercentText(SpadeOuterPrecentageText, suitCounts["spade"]);
        SetPercentText(HeartOuterPrecentageText, suitCounts["heart"]);
        SetPercentText(ClubOuterPrecentageText, suitCounts["club"]);
        SetPercentText(DiamondOuterPrecentageText, suitCounts["diamond"]);
        SetPercentText(KOuterPrecentageText, textCounts["K"]);
        SetPercentText(QOuterPrecentageText, textCounts["Q"]);
        SetPercentText(JOuterPrecentageText, textCounts["J"]);
    }

    private void SetPercentText(TMP_Text label, int count)
    {
        if (label == null) return;
        label.text = $"{(float)count / totalResults * 100f:0.0}%";
    }


    private void UpdateGrid(string cardKey, string suitKey)
    {
        if (CardPrefab == null || CardGridObject == null) return;

        if (gridCards.Count > 0)
        {
            GameObject prev = gridCards[gridCards.Count - 1];
            Card prevCard = prev.GetComponent<Card>();
            if (prevCard != null)
            {
                string pk = prev.name.Split('|')[0];
                string ps = prev.name.Split('|')[1];
                prevCard.SetData(GetGridBg(pk, ps, false),
                                 GetSuitSprite(ps, SpriteVariant.White),
                                 GetTextSprite(pk, SpriteVariant.White));
            }
        }

        GameObject cardGO = Instantiate(CardPrefab, CardGridObject.transform);
        cardGO.name = $"{cardKey}|{suitKey}";
        Card card = cardGO.GetComponent<Card>();
        if (card != null)
            card.SetData(GreenCardBg, GetSuitSprite(suitKey, SpriteVariant.White), GetTextSprite(cardKey, SpriteVariant.White));

        gridCards.Add(cardGO);
    }

    private Sprite GetGridBg(string cardKey, string suitKey, bool isLatest)
    {
        if (isLatest) return GreenCardBg;
        return (suitKey == "heart" || suitKey == "diamond") ? RedCardBg : BlackCardBg;
    }


    private enum SpriteVariant { White, Red, Black }

    private static string NormalizeSuit(string suit)
    {
        if (string.IsNullOrEmpty(suit)) return suit;
        suit = suit.ToLower();
        if (suit == "spades" || suit == "hearts" || suit == "clubs" || suit == "diamonds")
            suit = suit.TrimEnd('s');
        return suit;
    }

    private Sprite GetSuitSprite(string suit, SpriteVariant variant)
    {
        switch (suit)
        {
            case "spade": return variant == SpriteVariant.White ? SpadeWhiteSymbolImage : SpadeBlackSymbolImage;
            case "heart": return variant == SpriteVariant.White ? HeartWhiteSymbolImage : HeartRedSymbolImage;
            case "club": return variant == SpriteVariant.White ? ClubWhiteSymbolImage : ClubBlackSymbolImage;
            case "diamond": return variant == SpriteVariant.White ? DiamondWhiteSymbolImage : DiamondRedSymbolImage;
            default: Debug.LogWarning($"Unknown suit '{suit}'"); return null;
        }
    }

    private Sprite GetTextSprite(string card, SpriteVariant variant)
    {
        switch (card)
        {
            case "K": return variant == SpriteVariant.White ? KWhiteSymbolImage : variant == SpriteVariant.Red ? KRedSymbolImage : KBlackSymbolImage;
            case "Q": return variant == SpriteVariant.White ? QWhiteSymbolImage : variant == SpriteVariant.Red ? QRedSymbolImage : QBlackSymbolImage;
            case "J": return variant == SpriteVariant.White ? JWhiteSymbolImage : variant == SpriteVariant.Red ? JRedSymbolImage : JBlackSymbolImage;
            default: Debug.LogWarning($"Unknown card '{card}'"); return null;
        }
    }


    private Coroutine _infoFadeCoroutine;

    internal void StartInfoFadeAnimation()
    {
        if (_infoFadeCoroutine != null)
        {
            StopCoroutine(_infoFadeCoroutine);
            _infoFadeCoroutine = null;
        }

        // Reset all objects to clean state before starting
        foreach (var obj in InfoObjects)
        {
            if (obj == null) continue;
            CanvasGroup cg = obj.GetComponent<CanvasGroup>() ?? obj.AddComponent<CanvasGroup>();
            cg.DOKill();
            cg.alpha = 0;
            obj.SetActive(false);
        }

        _infoFadeCoroutine = StartCoroutine(InfoFadeAnimation());
    }

    internal IEnumerator InfoFadeAnimation()
    {
        foreach (var io in InfoObjects)
        {
            io.SetActive(true);
        }
        while (true)
        {
            for (int i = 0; i < InfoObjects.Count; i++)
            {
                GameObject obj = InfoObjects[i];
                if (obj == null) continue;

                CanvasGroup cg = obj.GetComponent<CanvasGroup>() ?? obj.AddComponent<CanvasGroup>();

                // Kill any running tween on this cg before starting
                cg.DOKill();
                cg.alpha = 0;
                obj.SetActive(true);

                yield return cg.DOFade(1f, 0.4f)
                    .SetEase(Ease.InOutSine)
                    .WaitForCompletion();

                yield return new WaitForSeconds(1f);

                yield return cg.DOFade(0f, 0.4f)
                    .SetEase(Ease.InOutSine)
                    .WaitForCompletion();

                obj.SetActive(false); // hide cleanly after fade out
            }
        }
    }

    // Call this when something externally toggles objects, to resync
    internal void ResyncInfoFadeAnimation()
    {

        StartInfoFadeAnimation(); // restarts cleanly from index 0
    }

    // internal void StatPanelClicked()
    // {
    //     if (!isStatPanelOpen)
    //     {
    //         // BigStatPanel.SetActive(true);
    //         // BigStatPanel.transform.localScale = Vector3.zero;
    //         // BigStatPanel.transform.DOScale(1f, 0.35f).SetEase(Ease.OutBack);
    //         // CardPanel.transform.DOLocalMove(cardPanelStartPos + new Vector3(-200f, 0, 0), 0.4f);
    //         // isStatPanelOpen = true;
    //         Vector2 newSize = new Vector2(1081.6f, 744.3838f);
    //         statPanel.transform.DOScale(newSize, 1.2f);
    //     }
    //     else
    //     {
    //         // CardPanel.transform.DOLocalMove(cardPanelStartPos, 0.3f);
    //         // BigStatPanel.transform.DOScale(0f, 0.4f).OnComplete(() => BigStatPanel.SetActive(false));
    //         // isStatPanelOpen = false;
    //     }
    // }

    internal void StatPanelClicked()
    {
        foreach (var btn in StatPanelButtons)
            btn.interactable = false;

        RectTransform rect = statPanel.GetComponent<RectTransform>();

        if (!isStatPanelOpen)
        {
            isStatPanelOpen = true;

            if (_infoFadeCoroutine != null)
            {
                StopCoroutine(_infoFadeCoroutine);
                _infoFadeCoroutine = null;
            }
            foreach (var io in InfoObjects)
            {
                io.SetActive(false);
            }

            Vector2 newSize = new Vector2(1081.6f, 744.3838f);
            CardPanel.transform.DOLocalMove(cardPanelStartPos + new Vector3(-172f, 0, 0), 0.55f);

            rect.DOSizeDelta(newSize, 0.35f)
                .SetEase(Ease.OutCubic)
                .OnComplete(() =>
                {
                    BigStatPanel.SetActive(true);
                    foreach (var btn in StatPanelButtons)
                        btn.interactable = true;
                });

            // Tell the line refresh loop the panel is animating for 0.6s
            NotifyPanelAnimating(0.35f);
        }
        else
        {
            isStatPanelOpen = false;

            BigStatPanel.SetActive(false);
            CardPanel.transform.DOLocalMove(cardPanelStartPos, 0.2f);
            rect.DOSizeDelta(smallStartPanelDimension, 0.4f)
                .SetEase(Ease.OutCubic).OnComplete(() =>
                {
                    foreach (var io in InfoObjects)
                    {
                        io.SetActive(true);
                        ResyncInfoFadeAnimation();
                        foreach (var btn in StatPanelButtons)
                            btn.interactable = true;
                    }
                }
                );

            // Tell the line refresh loop the panel is animating for 0.6s
            NotifyPanelAnimating(0.4f);
        }
    }

    private void AddClickScaler(Button button)
    {
        Vector3 originalScale = button.transform.localScale;

        EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();

        trigger.triggers.Clear(); // prevent duplicate listeners

        // POINTER DOWN → scale down
        var entryDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        entryDown.callback.AddListener((data) =>
        {
            button.transform.DOKill();

            button.transform
                .DOScale(originalScale * 0.8f, 0.1f)
                .SetEase(Ease.OutQuad);
        });
        trigger.triggers.Add(entryDown);

        // POINTER UP → scale up (pop)
        var entryUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        entryUp.callback.AddListener((data) =>
        {
            button.transform.DOKill();

            button.transform
                .DOScale(originalScale, 0.15f)
                .SetEase(Ease.OutBack);
                // .OnComplete(() =>
                // {
                //     button.transform.DOScale(originalScale, 0.1f);
                // });
        });
        trigger.triggers.Add(entryUp);

        var entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        entryExit.callback.AddListener((data) =>
        {
            button.transform.DOKill();
            button.transform.DOScale(originalScale, 0.1f);
        });
        trigger.triggers.Add(entryExit);

    }

}