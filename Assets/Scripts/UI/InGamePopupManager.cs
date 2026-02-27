using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using TMPro;
using System;

public class InGamePopupManager : MonoBehaviour
{

    [Header("Managers")]
    [SerializeField] private SocketIOManager socketIOManager;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private UiManager uiManager;

    [Header("Level Selection Popup")]
    [SerializeField] private Button LevelSelectionButton;
    [SerializeField] private TMP_Text MinBetText;
    [SerializeField] private TMP_Text MaxBetText;
    [SerializeField] private GameObject LevelSelectionPopup;
    [SerializeField] private Button LevelSelectionCloseButton;
    //[SerializeField] private List<GameObject> BetLimitObjects;
    [SerializeField] private Button CasualButton;
    [SerializeField] private Button NoviceButton;
    [SerializeField] private Button ExpertButton;
    [SerializeField] private Button HighRollerButton;
    [SerializeField] private BetLimit K;
    [SerializeField] private BetLimit Q;
    [SerializeField] private BetLimit J;
    [SerializeField] private BetLimit Specific_Clubs;
    [SerializeField] private BetLimit Specific_Diamonds;
    [SerializeField] private BetLimit Specific_Hearts;
    [SerializeField] private BetLimit Specific_Spades;
    [SerializeField] private BetLimit K_spades;
    [SerializeField] private BetLimit K_hearts;
    [SerializeField] private BetLimit K_clubs;
    [SerializeField] private BetLimit K_diamonds;
    [SerializeField] private BetLimit Q_spades;
    [SerializeField] private BetLimit Q_hearts;
    [SerializeField] private BetLimit Q_clubs;
    [SerializeField] private BetLimit Q_diamonds;
    [SerializeField] private BetLimit J_spades;
    [SerializeField] private BetLimit J_hearts;
    [SerializeField] private BetLimit J_clubs;
    [SerializeField] private BetLimit J_diamonds;
    [SerializeField] private Button ConfirmButton;

    [Header("Max Bet Popup")]
    [SerializeField] private GameObject MaxBetPopup;

    private string currentLevel = "casual";

    private void Start()
    {
        LevelSelectionButton.onClick.AddListener(OpenLevelPopup);
        LevelSelectionCloseButton.onClick.AddListener(CloseLevelPopup);
        CasualButton.onClick.AddListener(() => LevelButtonClicked("casual"));
        NoviceButton.onClick.AddListener(() => LevelButtonClicked("novice"));
        ExpertButton.onClick.AddListener(() => LevelButtonClicked("expert"));
        HighRollerButton.onClick.AddListener(() => LevelButtonClicked("highroller"));
        ConfirmButton.onClick.AddListener(ConfirmLevelSelection);
    }
    
    private void OpenLevelPopup()
    {
        if (LevelSelectionPopup == null) return;
        //pendingLevel = betManager.currentLevel; // default highlight = current level
        LevelSelectionPopup.SetActive(true);
        LevelSelectionPopup.transform.DOKill(false);
        LevelSelectionPopup.transform.localScale = Vector3.zero;
        LevelSelectionPopup.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);

        // Refresh bet limits for the currently active level
        //RefreshBetLimitUI(pendingLevel);
    }

    private void CloseLevelPopup()
    {
        if (LevelSelectionPopup == null) return;
        LevelSelectionPopup.transform.DOScale(Vector3.zero, 0.2f)
            .SetEase(Ease.InBack)
            .OnComplete(() => LevelSelectionPopup.SetActive(false));
    }
    
    private void LevelButtonClicked(string level)
    {
        currentLevel = level;
        RefreshBetLimits();
        List<Button> levelButtons = new List<Button> { CasualButton, NoviceButton, ExpertButton, HighRollerButton };
        foreach (Button btn in levelButtons)
        {
            btn.transform.GetChild(0).gameObject.SetActive(false); // Deactivate all highlights
        }

        switch (level)
        {
            case "casual":
                CasualButton.transform.GetChild(0).gameObject.SetActive(true); // Highlight casual button
                break;
            case "novice":
                NoviceButton.transform.GetChild(0).gameObject.SetActive(true); // Highlight novice button
                break;
            case "expert":
                ExpertButton.transform.GetChild(0).gameObject.SetActive(true); // Highlight expert button
                break;
            case "highroller":
                HighRollerButton.transform.GetChild(0).gameObject.SetActive(true); // Highlight high roller button
                break;
        }

    }

    private void RefreshBetLimits()
    {
        
    }

    private void ConfirmLevelSelection()
    {
        StartCoroutine(SwitchLevel());
    }

    private IEnumerator SwitchLevel()
    {
        
        // Send selected level to server
        gameManager.SelectLevel(currentLevel);
        CloseLevelPopup();
        return null;
    }

    // A method for popup to slide from left
    internal void SlideInFromLeft(GameObject popup)
    {
        if (popup == null) return;
        popup.SetActive(true);
        RectTransform rect = popup.GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(-Screen.width, rect.anchoredPosition.y);
        rect.DOAnchorPosX(0, 0.5f).SetEase(Ease.OutCubic);
    }

    internal void SetInitialGameData(GameData gameData)
    {
        // Set any game data that needs to be initialized at the start of each game
        List<Button> levelButtons = new List<Button> { CasualButton, NoviceButton, ExpertButton, HighRollerButton };

        levelButtons[0].transform.GetChild(1).GetComponent<TMP_Text>().text = gameData.levelBetLimit.casual.min_bet_limit.ToString();
        levelButtons[0].transform.GetChild(2).GetComponent<TMP_Text>().text = gameData.levelBetLimit.casual.max_bet_limit.ToString();

        levelButtons[1].transform.GetChild(1).GetComponent<TMP_Text>().text = gameData.levelBetLimit.novice.min_bet_limit.ToString();
        levelButtons[1].transform.GetChild(2).GetComponent<TMP_Text>().text = gameData.levelBetLimit.novice.max_bet_limit.ToString();

        levelButtons[2].transform.GetChild(1).GetComponent<TMP_Text>().text = gameData.levelBetLimit.expert.min_bet_limit.ToString();
        levelButtons[2].transform.GetChild(2).GetComponent<TMP_Text>().text = gameData.levelBetLimit.expert.max_bet_limit.ToString();

        levelButtons[3].transform.GetChild(1).GetComponent<TMP_Text>().text = gameData.levelBetLimit.high_roller.min_bet_limit.ToString();
        levelButtons[3].transform.GetChild(2).GetComponent<TMP_Text>().text = gameData.levelBetLimit.high_roller.max_bet_limit.ToString();
    }

}

[Serializable]
public class BetLimit
{
    [SerializeField] private TMP_Text MinBetText;
    [SerializeField] private TMP_Text MaxBetText;
}