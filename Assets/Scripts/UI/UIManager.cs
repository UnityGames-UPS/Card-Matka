using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using System.Threading;
using System.Linq;

public class UiManager : MonoBehaviour
{
    [SerializeField]
    private SocketIOManager socketManager;
    [SerializeField] private BetManager betManager;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private AnimationManager animationManager;
    [SerializeField] private InGamePopupManager inGamePopupManager;
    [SerializeField] private JSFunctCalls jsFunctCalls;

    [Header("Screens UI")]
    [SerializeField] internal GameObject LoadingScreen_Object;
    [SerializeField] internal GameObject GameScreen_Object;

    [Header("Timer Objects")]
    [SerializeField] private GameObject HighTimer_Object;
    [SerializeField] private GameObject LowTimer_Object;
    [SerializeField] private GameObject LockedTimer_Object;
    [SerializeField] private GameObject NextRoundTimer_Object;

    [Header("Texts")]
    [SerializeField] private TMP_Text UserId_Text;
    [SerializeField] private TMP_Text RoomId_Text;
    [SerializeField] private TMP_Text Timer_Text;
    [SerializeField] private TMP_Text UserName_Text;
    [SerializeField] private TMP_Text Balance_Text;

    [Header("Win Objects")]
    [SerializeField] private TMP_Text winText;

    [Header("side panel")]
    [SerializeField] private Button MenuInGame_button;
    [SerializeField] private RectTransform menuMainButton;
    [SerializeField] private Button History_button;
    [SerializeField] private Button Info_button;
    [SerializeField] private Button Sound_button;
    [SerializeField] private Sprite SoundImage;
    [SerializeField] private Sprite SoundMuteImage;
    [SerializeField] private Button Music_button;
    [SerializeField] private Sprite MusicImage;
    [SerializeField] private Sprite MusicMuteImage;
    [SerializeField] private Button SoundMute_button;
    [SerializeField] private Button MusicMute_button;
    [SerializeField] private Button Home_button;
    [SerializeField] private Button YesHome_button;
    [SerializeField] private Button NoHome_button;

    [SerializeField] private GameObject MenuPanel_Object;
    [SerializeField] private GameObject MenuPanelContainer_Object;
    [SerializeField] private GameObject Homebutton_Object;

    [SerializeField] private Button HistoryClose_button;
    [SerializeField] internal HistoryController historyController;

    [SerializeField] private Button InfoClose_button;

    [SerializeField] private Button InfoLeft_button;

    [SerializeField] private Button InfoRight_button;

    [SerializeField] private List<GameObject> InfoPages_Objects;
    [SerializeField] private List<GameObject> InfoActive_Objects;
    [SerializeField] private TMP_Text pageInfoText;
    private int currentInfoPage = 0;

    private bool IsMenuPanelOpen = false;

    [Header("Popus UI")]
    [SerializeField]
    private GameObject MainPopup_Object;
    [SerializeField]
    private GameObject PaytablePopup_Object;
    [SerializeField] private GameObject GameQuitPopup;
    [SerializeField] private GameObject HistoryPopup_Object;
    [SerializeField] private GameObject InfoPopup_Object;

    [Header("Settings Popup")]
    [SerializeField]
    private GameObject SettingsPopup_Object;
    [SerializeField]
    private Button SettingsExit_Button;
    [SerializeField]
    private Button Sound_Button;
    [SerializeField]
    private Button Music_Button;

    [SerializeField]
    private GameObject MusicOn_Object;
    [SerializeField]
    private GameObject MusicOff_Object;
    [SerializeField]
    private GameObject SoundOn_Object;
    [SerializeField]
    private GameObject SoundOff_Object;

    [Header("Disconnection Popup")]
    [SerializeField]
    private Button CloseDisconnect_Button;
    [SerializeField]
    private GameObject DisconnectPopup_Object;

    [Header("AnotherDevice Popup")]
    [SerializeField]
    private Button CloseAD_Button;
    [SerializeField]
    private GameObject ADPopup_Object;

    [Header("Reconnection Popup")]
    [SerializeField]
    private TMP_Text reconnect_Text;
    [SerializeField]
    private GameObject ReconnectPopup_Object;

    [Header("LowBalance Popup")]
    [SerializeField]
    private Button LBExit_Button;
    [SerializeField]
    private GameObject LBPopup_Object;

    [Header("Quit Popup")]
    [SerializeField]
    private GameObject QuitPopup_Object;
    [SerializeField]
    private Button YesQuit_Button;
    [SerializeField]
    private Button NoQuit_Button;
    [SerializeField]
    private Button CrossQuit_Button;

    [SerializeField]
    internal GameObject touchDisable;
    [SerializeField]
    private Button Settings_Button;
    [SerializeField]
    private Button Paytable_Button;
    [SerializeField]
    private Button PaytableExit_Button;
    [SerializeField]
    private Button GameExit_Button;
    //[SerializeField]
    //private GameManager gameManager;

    [Space(100)]
    [Header("HomePage")]
    [SerializeField] private Button CloseStartupPanelBtn;
    [SerializeField] private Button ReadmoreStartupPanelBtn;
    [SerializeField] internal GameObject StartupPanel;
    [SerializeField] private RectTransform ToggleTextObj;



    [Space(100)]
    [Header("gamePage")]
    [SerializeField] internal Button coinSelector;      // Main button
    [SerializeField] private List<Button> Coins;       // Other coins
    [SerializeField] private GameObject BigChipObject;
    [SerializeField] private List<Sprite> CasualLevelCoins;
    [SerializeField] private List<Sprite> NoviceLevelCoins;
    [SerializeField] private List<Sprite> ExpertLevelCoins;
    [SerializeField] private List<Sprite> HighRollerLevelCoins;

    [Header("sidePanel")]
    [SerializeField] private Button MenueButtonGP;
    [SerializeField] private Button GameRulesGP;
    [SerializeField] private Button HistoryGP;
    [SerializeField] private Button SoundGP;
    [SerializeField] private Button MusicGP;
    [SerializeField] private Button ExpandShinkButton;
    [SerializeField] private Button HomeGP;
    [SerializeField] private GameObject sidepanelGP;
    [SerializeField] private Sprite ExpandImage;
    [SerializeField] private Sprite ShrinkImage;

    // [SerializeField] private float spacing = 70f;      // Space between coins
    // [SerializeField] private float duration = 0.3f;    // Animation duration

    [Header("loadingPage")]
    [SerializeField] private GameObject loadingPage;


    [Header("Animation Settings")]

    private List<Button> menuButtonsGP;
    private bool isMenueExpanded = false;



    private bool isexpanded = false;   //For Coins
    private bool isExpanded = false;     //For Screen

    private Vector3 startPos;

    public float spacing = 100f;
    public float duration = 0.5f;
    public float delayStep = 0.05f;

    private Vector3[] originalPositions;
    private RectTransform[] buttonRects;
    private CanvasGroup[] buttonGroups;

    [SerializeField]
    private AudioManager audioController;
    internal bool isExit;
    bool isMusic;
    bool isSound;




    private void Start()
    {
        //ShowCashoutUI(0.91564f);

        assignButtonListeners();

        // bhutton panel anim
        //  menuMainPos = menuMainButton.anchoredPosition;
        buttonRects = new RectTransform[] {
            History_button.GetComponent<RectTransform>(),
            Info_button.GetComponent<RectTransform>(),
            Sound_button.GetComponent<RectTransform>(),
            Music_button.GetComponent<RectTransform>(),
            //SoundMute_button.GetComponent<RectTransform>(),
        };

        buttonGroups = new CanvasGroup[buttonRects.Length];
        originalPositions = new Vector3[buttonRects.Length];

        for (int i = 0; i < buttonRects.Length; i++)
        {
            originalPositions[i] = buttonRects[i].anchoredPosition;

            // Add CanvasGroup if missing
            var cg = buttonRects[i].GetComponent<CanvasGroup>();
            if (cg == null)
                cg = buttonRects[i].gameObject.AddComponent<CanvasGroup>();

            cg.alpha = 0f; // start hidden
            buttonGroups[i] = cg;
        }



        menuButtonsGP = new List<Button> { GameRulesGP, HistoryGP, SoundGP, MusicGP, ExpandShinkButton, HomeGP };

        // Hide them initially
        foreach (var btn in menuButtonsGP)
        {
            btn.gameObject.SetActive(false);
            var cg = btn.GetComponent<CanvasGroup>();
            if (cg == null) cg = btn.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0;
        }

        MenueButtonGP.onClick.RemoveAllListeners();
        MenueButtonGP.onClick.AddListener(ToggleMenuGP);

        InitializeExpandShrink();
    }



    #region ButtonSetup

    private void assignButtonListeners()
    {
        if (Paytable_Button) Paytable_Button.onClick.RemoveAllListeners();
        if (Paytable_Button) Paytable_Button.onClick.AddListener(delegate { OpenPopup(PaytablePopup_Object); });

        if (PaytableExit_Button) PaytableExit_Button.onClick.RemoveAllListeners();
        if (PaytableExit_Button) PaytableExit_Button.onClick.AddListener(delegate { ClosePopup(PaytablePopup_Object); });

        if (Settings_Button) Settings_Button.onClick.RemoveAllListeners();
        if (Settings_Button) Settings_Button.onClick.AddListener(delegate { OpenPopup(SettingsPopup_Object); });

        if (SettingsExit_Button) SettingsExit_Button.onClick.RemoveAllListeners();
        if (SettingsExit_Button) SettingsExit_Button.onClick.AddListener(delegate { ClosePopup(SettingsPopup_Object); });

        if (MusicOn_Object) MusicOn_Object.SetActive(true);
        if (MusicOff_Object) MusicOff_Object.SetActive(false);

        if (SoundOn_Object) SoundOn_Object.SetActive(true);
        if (SoundOff_Object) SoundOff_Object.SetActive(false);

        if (GameExit_Button) GameExit_Button.onClick.RemoveAllListeners();
        if (GameExit_Button) GameExit_Button.onClick.AddListener(delegate
        {
            OpenPopup(QuitPopup_Object);
            Debug.Log("Quit event: pressed Big_X button");

        });

        if (NoQuit_Button) NoQuit_Button.onClick.RemoveAllListeners();
        if (NoQuit_Button) NoQuit_Button.onClick.AddListener(delegate
        {
            if (!isExit)
            {
                ClosePopup(QuitPopup_Object);
                Debug.Log("quit event: pressed NO Button ");
            }
        });

        if (CrossQuit_Button) CrossQuit_Button.onClick.RemoveAllListeners();
        if (CrossQuit_Button) CrossQuit_Button.onClick.AddListener(delegate
        {
            if (!isExit)
            {
                ClosePopup(QuitPopup_Object);
                Debug.Log("quit event: pressed Small_X Button ");

            }
        });

        if (LBExit_Button) LBExit_Button.onClick.RemoveAllListeners();
        if (LBExit_Button) LBExit_Button.onClick.AddListener(delegate { ClosePopup(LBPopup_Object); });

        if (YesQuit_Button) YesQuit_Button.onClick.RemoveAllListeners();
        if (YesQuit_Button) YesQuit_Button.onClick.AddListener(delegate
        {
            CallOnExitFunction();
            Debug.Log("quit event: pressed YES Button ");

        });

        if (CloseDisconnect_Button) CloseDisconnect_Button.onClick.RemoveAllListeners();
        if (CloseDisconnect_Button) CloseDisconnect_Button.onClick.AddListener((delegate { CallOnExitFunction(); socketManager.ReactNativeCallOnFailedToConnect(); }));

        if (CloseAD_Button) CloseAD_Button.onClick.RemoveAllListeners();
        if (CloseAD_Button) CloseAD_Button.onClick.AddListener(CallOnExitFunction);



        //if (audioController) audioController.ToggleMute(false);

        isMusic = true;
        isSound = true;

        if (Sound_Button) Sound_Button.onClick.RemoveAllListeners();
        if (Sound_Button) Sound_Button.onClick.AddListener(ToggleSound);

        if (Music_Button) Music_Button.onClick.RemoveAllListeners();
        if (Music_Button) Music_Button.onClick.AddListener(ToggleMusic);

        // if (MenuMain_button) MenuMain_button.onClick.RemoveAllListeners();
        // if (MenuMain_button) MenuMain_button.onClick.AddListener(delegate { ResetMenuPanel(false); ToggleMenuPanel(); });

        // if (MenuInGame_button) MenuInGame_button.onClick.RemoveAllListeners();
        // if (MenuInGame_button) MenuInGame_button.onClick.AddListener(delegate { ResetMenuPanel(true); ToggleMenuPanel(); });


        if (SoundMute_button) SoundMute_button.onClick.RemoveAllListeners();
        if (SoundMute_button) SoundMute_button.onClick.AddListener(delegate { ToggleSound(); });


        if (MusicMute_button) MusicMute_button.onClick.RemoveAllListeners();
        if (MusicMute_button) MusicMute_button.onClick.AddListener(delegate { ToggleMusic(); });

        //Gamepage

        if (GameRulesGP) GameRulesGP.onClick.RemoveAllListeners();
        if (GameRulesGP) GameRulesGP.onClick.AddListener(delegate { OpenPopup(InfoPopup_Object); MenuPanel_Object.SetActive(false); });

        if (HistoryGP) HistoryGP.onClick.RemoveAllListeners();
        if (HistoryGP) HistoryGP.onClick.AddListener(delegate { OpenPopup(HistoryPopup_Object); gameManager.RequestHistory(1); MenuPanel_Object.SetActive(false); });

        if (SoundGP) SoundGP.onClick.RemoveAllListeners();
        if (SoundGP) SoundGP.onClick.AddListener(delegate { ToggleSound(); });

        if (SoundMute_button) SoundMute_button.onClick.RemoveAllListeners();
        if (SoundMute_button) SoundMute_button.onClick.AddListener(delegate { ToggleSound(); });

        if (MusicGP) MusicGP.onClick.RemoveAllListeners();
        if (MusicGP) MusicGP.onClick.AddListener(delegate { ToggleMusic(); });

        if (MusicMute_button) MusicMute_button.onClick.RemoveAllListeners();
        if (MusicMute_button) MusicMute_button.onClick.AddListener(delegate { ToggleMusic(); });

        if (HomeGP) HomeGP.onClick.RemoveAllListeners();
        if (HomeGP) HomeGP.onClick.AddListener(delegate { OpenPopup(GameQuitPopup); });



        // end

        if (YesHome_button) YesHome_button.onClick.RemoveAllListeners();
        if (YesHome_button) YesHome_button.onClick.AddListener(delegate { ClosePopup(GameQuitPopup); GameScreen_Object.SetActive(false); ResetMenuPanel(false); });

        if (NoHome_button) NoHome_button.onClick.RemoveAllListeners();
        if (NoHome_button) NoHome_button.onClick.AddListener(delegate { ClosePopup(GameQuitPopup); });

        if (InfoLeft_button) InfoLeft_button.onClick.RemoveAllListeners();
        if (InfoLeft_button) InfoLeft_button.onClick.AddListener(delegate { GoToPreviousInfoPage(); });

        if (InfoRight_button) InfoRight_button.onClick.RemoveAllListeners();
        if (InfoRight_button) InfoRight_button.onClick.AddListener(delegate { GoToNextInfoPage(); });

        if (InfoClose_button) InfoClose_button.onClick.RemoveAllListeners();
        if (InfoClose_button) InfoClose_button.onClick.AddListener(delegate { ClosePopup(InfoPopup_Object); });

        if (HistoryClose_button) HistoryClose_button.onClick.RemoveAllListeners();
        if (HistoryClose_button) HistoryClose_button.onClick.AddListener(delegate { ClosePopup(HistoryPopup_Object); });


        if (coinSelector) coinSelector.onClick.RemoveAllListeners();
        if (coinSelector) coinSelector.onClick.AddListener(delegate { ToggleCoins(); });




        if (CloseStartupPanelBtn) CloseStartupPanelBtn.onClick.RemoveAllListeners();
        if (CloseStartupPanelBtn) CloseStartupPanelBtn.onClick.AddListener(delegate
        {

            ClosePopup(StartupPanel);



        });

        if (ReadmoreStartupPanelBtn) ReadmoreStartupPanelBtn.onClick.RemoveAllListeners();
        if (ReadmoreStartupPanelBtn) ReadmoreStartupPanelBtn.onClick.AddListener(delegate
        {

            // ClosePopup(StartupPanel);
            StartupPanel.SetActive(false);
            OpenPopup(InfoPopup_Object);

        });
    }
    private void UpdateFrequency(float value)
    {
        Mathf.Clamp(value, 0.2f, 2);
    }

    private void ResetMenuPanel(bool IsGameScreen)
    {
        // MenuPanel_Object.SetActive(false);
        // if (IsGameScreen)
        // {
        //     Homebutton_Object.SetActive(true);
        //     //  MenuPanelContainer_Object.transform.localPosition = new Vector2(56, 394);
        //     MenuPanelContainer_Object.GetComponent<RectTransform>().anchoredPosition = new Vector2(56, 394);
        //     MenuPanel_Object.transform.SetParent(GameScreen_Object.transform, true);
        //     int lastIndex = GameScreen_Object.transform.childCount - 1;
        //     MenuPanel_Object.transform.SetSiblingIndex(lastIndex - 1);
        //     // Spread();
        //     //ExpandMenu();
        // }
        // else
        // {
        //     Homebutton_Object.SetActive(false);
        //     // MenuPanelContainer_Object.transform.localPosition = new Vector2(56, 221);
        //     MenuPanelContainer_Object.GetComponent<RectTransform>().anchoredPosition = new Vector2(56, 221);
        //     //  Retract();
        // }
    }



    internal void LowBalPopup()
    {
        OpenPopup(LBPopup_Object);
    }

    internal void DisconnectionPopup()
    {
        if (!isExit)
        {
            OpenPopup(DisconnectPopup_Object);
        }
    }

    internal void ReconnectionPopup()
    {
        OpenPopup(ReconnectPopup_Object);
    }

    internal void CheckAndClosePopups()
    {
        if (ReconnectPopup_Object.activeInHierarchy)
        {
            ClosePopup(ReconnectPopup_Object);
        }
        if (DisconnectPopup_Object.activeInHierarchy)
        {
            ClosePopup(DisconnectPopup_Object);
        }
    }



    internal void ADfunction()
    {
        OpenPopup(ADPopup_Object);
    }


    private void CallOnExitFunction()
    {
        isExit = true;
        StartCoroutine(socketManager.CloseSocket());
        audioController.PlayUiButton();

    }



    internal void OpenPopup(GameObject Popup)
    {
        if (audioController) audioController.PlayUiButton();
        if (MainPopup_Object) MainPopup_Object.SetActive(true);

        if (Popup)
        {
            Popup.SetActive(true);
            var rect = Popup.transform;

            // Start from small
            rect.localScale = Vector3.zero;

            // Scale up with bounce
            rect.DOScale(Vector3.one, 0.4f)
                .SetEase(Ease.OutBack);
        }
    }

    internal void ClosePopup(GameObject Popup)
    {
        if (audioController) audioController.PlayUiButton();

        if (Popup)
        {
            var rect = Popup.transform;

            // Scale down smoothly
            rect.DOScale(Vector3.zero, 0.3f)
                .SetEase(Ease.InBack)
                .OnComplete(() =>
                {
                    Popup.SetActive(false);
                    if (MainPopup_Object) MainPopup_Object.SetActive(false);
                });
        }
        else
        {
            if (MainPopup_Object) MainPopup_Object.SetActive(false);
        }
    }

    private void ToggleMusic()
    {
        isMusic = !isMusic;
        if (isMusic)
        {
            // Music_button.gameObject.SetActive(true);
            // MusicMute_button.gameObject.SetActive(false);
            Music_button.gameObject.GetComponent<Image>().sprite = MusicImage;
            //audioController.ToggleMute(false, "bg");
            audioController.MuteBackground(false);
        }
        else
        {
            // Music_button.gameObject.SetActive(false);
            // MusicMute_button.gameObject.SetActive(true);
            Music_button.gameObject.GetComponent<Image>().sprite = MusicMuteImage;
            //audioController.ToggleMute(true, "bg");
            audioController.MuteBackground(true);
        }
    }


    private void ToggleSound()
    {
        isSound = !isSound;
        if (isSound)
        {
            // Sound_button.gameObject.SetActive(true);
            // SoundMute_button.gameObject.SetActive(false);
            Sound_button.gameObject.GetComponent<Image>().sprite = SoundImage;
            // if (audioController) audioController.ToggleMute(false, "button");
            // if (audioController) audioController.ToggleMute(false, "wl");
            // if (audioController) audioController.ToggleMute(false, "win");
            // if (audioController) audioController.ToggleMute(false, "bet");
            audioController.MuteGame(false);

        }
        else
        {
            // Sound_button.gameObject.SetActive(false);
            // SoundMute_button.gameObject.SetActive(true);
            Sound_button.gameObject.GetComponent<Image>().sprite = SoundMuteImage;
            // if (audioController) audioController.ToggleMute(true, "button");
            // if (audioController) audioController.ToggleMute(true, "wl");
            // if (audioController) audioController.ToggleMute(true, "win");
            // if (audioController) audioController.ToggleMute(true, "bet");
            audioController.MuteGame(true);
        }
    }

    private void UpdateInfoUI()
    {
        for (int i = 0; i < InfoPages_Objects.Count; i++)
            InfoPages_Objects[i].SetActive(i == currentInfoPage);

        pageInfoText.text = (currentInfoPage + 1).ToString();

        // for (int i = 0; i < InfoActive_Objects.Count; i++)
        //     InfoActive_Objects[i].SetActive(i == currentInfoPage);

    }

    private void GoToPreviousInfoPage()
    {
        if (audioController) audioController.PlayUiButton();
        currentInfoPage--;
        if (currentInfoPage < 0)
            currentInfoPage = InfoPages_Objects.Count - 1;

        UpdateInfoUI();
    }

    private void GoToNextInfoPage()
    {
        if (audioController) audioController.PlayUiButton();
        currentInfoPage++;
        if (currentInfoPage >= InfoPages_Objects.Count)
            currentInfoPage = 0;

        UpdateInfoUI();
    }

    #endregion

    #region Expand / Shrink

    private void InitializeExpandShrink()
    {
        SetExpandShrinkButtons(isExpanded: false);
    }

    private void OnExpand()
    {
        isExpanded = true;
        jsFunctCalls?.RequestExpandGame();
        SetExpandShrinkButtons(isExpanded: true);
    }

    private void OnShrink()
    {
        isExpanded = false;
        jsFunctCalls?.RequestShrinkGame();
        SetExpandShrinkButtons(isExpanded: false);
    }


    private void SetExpandShrinkButtons(bool isExpanded)
    {
        // if (ExpandHome_Button) ExpandHome_Button.gameObject.SetActive(!isExpanded);
        // if (ShrinkHome_Button) ShrinkHome_Button.gameObject.SetActive(isExpanded);
        // if (ExpandMenu_Button) ExpandMenu_Button.gameObject.SetActive(!isExpanded);
        // if (ShrinkMenu_Button) ShrinkMenu_Button.gameObject.SetActive(isExpanded);
        // if (ExpandSideMenu_Button)
        // {
        //     RectTransform rect = ExpandSideMenu_Button.GetComponent<RectTransform>();
        //     if (rect != null) rect.anchoredPosition = expandSideMenuOriginalPosition;
        //     ExpandSideMenu_Button.gameObject.SetActive(!isExpanded);
        //     ExpandSideMenu_Button.interactable = !isExpanded;
        // }
        // if (ShrinkSideMenu_Button)
        // {
        //     RectTransform rect = ShrinkSideMenu_Button.GetComponent<RectTransform>();
        //     if (rect != null) rect.anchoredPosition = shrinkSideMenuOriginalPosition;
        //     ShrinkSideMenu_Button.gameObject.SetActive(isExpanded);
        //     ShrinkSideMenu_Button.interactable = isExpanded;
        // }

        Image ExpandShrinkButtonImage = ExpandShinkButton.gameObject.GetComponent<Image>();
        if (!isExpanded)
        {
            ExpandShrinkButtonImage.sprite = ExpandImage;
            ExpandShinkButton.onClick.RemoveAllListeners();
            ExpandShinkButton.onClick.AddListener(() => OnExpand());
            ExpandShinkButton.GetComponentInChildren<TMP_Text>().text = "Expand";
        }
        if (isExpanded)
        {
            ExpandShrinkButtonImage.sprite = ShrinkImage;
            ExpandShinkButton.onClick.RemoveAllListeners();
            ExpandShinkButton.onClick.AddListener(() => OnShrink());
            ExpandShinkButton.GetComponentInChildren<TMP_Text>().text = "Shrink";
        }
    }

    private void RegisterFullscreenListener()
    {
        jsFunctCalls?.RegisterFullscreenListener(gameObject.name);
    }

    internal void OnFullscreenChanged(string isFullscreen)
    {
        bool newExpandedState = isFullscreen == "1";
        Debug.Log($"[UI] OnFullscreenChanged callback: isFullscreen={isFullscreen}, newState={newExpandedState}");

        // Only update if state actually changed
        if (isExpanded != newExpandedState)
        {
            isExpanded = newExpandedState;
            SetExpandShrinkButtons(isExpanded);
            Debug.Log($"[UI] Button states synced to fullscreen: {(isExpanded ? "EXPANDED" : "SHRINK")}");
        }
    }
    #endregion


    #region  homePage

    void StartScroll()
    {
        // Start at "fromX"
        ToggleTextObj.anchoredPosition = new Vector2(1000f, startPos.y);

        // Tween to "toX"
        ToggleTextObj.DOAnchorPosX(-1000f, 10f)
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                ToggleTextObj.anchoredPosition = new Vector2(1000f, startPos.y);
                StartScroll(); // repeat
            });
    }



    #endregion

    #region  gamePage


    private void ToggleCoins()
    {
        if (isexpanded)
            RetractCoins();
        else
            ExpandCoins();
    }

    private void ExpandCoins()
    {
        float radius = 200f;            // Distance from center
        float startAngle = 90f;        // Start of arc (left)
        float endAngle = -90f;            // End of arc (right)


        for (int i = 0; i < Coins.Count; i++)
        {
            var coin = Coins[i];
            coin.gameObject.SetActive(true);

            // Calculate angle for this coin on the arc (in radians)
            float t = (float)i / (Coins.Count - 1); // normalized 0 → 1
            float angleDeg = Mathf.Lerp(startAngle, endAngle, t);
            float angleRad = angleDeg * Mathf.Deg2Rad;

            // Calculate arc position relative to selector
            float targetX = coinSelector.transform.localPosition.x + radius * Mathf.Cos(angleRad);
            float targetY = coinSelector.transform.localPosition.y + radius * Mathf.Sin(angleRad);

            coin.transform.DORotate(new Vector3(0, 0, 0), duration).SetEase(Ease.InOutSine);
            // Move on arc
            coin.transform.DOLocalMove(new Vector3(targetX, targetY, 0), duration)
                .SetEase(Ease.OutBack);

            // Fade-in
            coin.GetComponent<CanvasGroup>().DOFade(1, duration);
        }
        BigChipObject.transform.DOLocalMoveY(coinSelector.transform.localPosition.y + 30f, 0.3f);

        isexpanded = true;
    }

    internal void RetractCoins()
    {
        BigChipObject.transform.DOLocalMoveY(coinSelector.transform.localPosition.y, 0.3f);
        for (int i = 0; i < Coins.Count; i++)
        {
            var coin = Coins[i];

            coin.transform.DORotate(new Vector3(0, 0, 90), duration).SetEase(Ease.InOutSine);
            coin.transform.DOLocalMove(
                coinSelector.transform.localPosition,
                duration
            )
            .SetEase(Ease.InBack)
            .OnComplete(() => coin.gameObject.SetActive(false));

            coin.GetComponent<CanvasGroup>().DOFade(0, duration);
        }

        isexpanded = false;
    }


    internal void OnCoinSelected(Button selectedCoin, int chipIndex)
    {
        // Swap visuals (text, image) between main selector and selected coin
        var tempImage = coinSelector.image.sprite;
        coinSelector.image.sprite = selectedCoin.image.sprite;
        coinSelector.GetComponentInChildren<TMP_Text>().text = selectedCoin.GetComponentInChildren<TMP_Text>().text;
        //selectedCoin.image.sprite = tempImage;

        // Fold back coins
        RetractCoins();
        betManager.selectedChipIndex = chipIndex;
        string currentLevel = betManager.currentLevel;
        List<Sprite> currentLevelCoinSprites = null;
        switch (currentLevel)
        {
            case "casual":
                currentLevelCoinSprites = CasualLevelCoins;
                break;
            case "novice":
                currentLevelCoinSprites = NoviceLevelCoins;
                break;
            case "expert":
                currentLevelCoinSprites = ExpertLevelCoins;
                break;
            case "high_roller":
                currentLevelCoinSprites = HighRollerLevelCoins;
                break;
        }
        betManager.chipPrefab.GetComponentInChildren<Image>().sprite = currentLevelCoinSprites[chipIndex];
        //betManager.chipPrefab = chipPrefab;
    }









    private void ToggleMenuGP()
    {
        if (isMenueExpanded)
            RetractMenuGP();
        else
            ExpandMenuGP();
    }

    private void ExpandMenuGP()
    {
        sidepanelGP.SetActive(true); // show panel immediately

        for (int i = 0; i < menuButtonsGP.Count; i++)
        {
            var btn = menuButtonsGP[i];
            btn.gameObject.SetActive(true);

            //btn.transform.localPosition = MenueButtonGP.transform.localPosition;

            float delay = i * delayStep;

            btn.transform.DOLocalMoveY(
                MenueButtonGP.transform.localPosition.y - spacing * 1.5f * (i + 1),
                duration
            ).SetEase(Ease.OutBack).SetDelay(delay);

            btn.GetComponent<CanvasGroup>().DOFade(1, duration).SetDelay(delay);
        }

        isMenueExpanded = true;
    }

    private void RetractMenuGP()
    {
        for (int i = 0; i < menuButtonsGP.Count; i++)
        {
            var btn = menuButtonsGP[i];
            float delay = i * delayStep;

            // If it's the last button → turn off sidepanel after animation
            bool isLast = (i == menuButtonsGP.Count - 1);

            btn.transform.DOLocalMoveY(
                MenueButtonGP.transform.localPosition.y,
                duration
            ).SetEase(Ease.InBack).SetDelay(delay)
             .OnComplete(() =>
             {
                 btn.gameObject.SetActive(false);
                 if (isLast)
                     sidepanelGP.SetActive(false); // hide panel after last finishes
             });

            btn.GetComponent<CanvasGroup>().DOFade(0, duration).SetDelay(delay);
        }

        isMenueExpanded = false;
    }
    #endregion

    internal void UpdateTimer(int time)
    {
        Timer_Text.text = time.ToString();
        if (gameManager.currentPhase == GameManager.GamePhase.Betting)
        {
            if (time <= 5)
            {
                LowTimer_Object.SetActive(true);
                HighTimer_Object.SetActive(false);
                if (time == 5)
                {
                    LowTimer_Object.GetComponent<RectTransform>().DOPunchScale(new Vector3(1.05f, 1.05f, 1.05f), 0.3f, vibrato: 0);
                }
                Timer_Text.gameObject.GetComponent<RectTransform>().transform.localScale = new Vector3(1f, 1f, 1f);
                Timer_Text.gameObject.GetComponent<RectTransform>().DOPunchScale(Vector3.one * 1.03f, 0.4f, vibrato: 0);
            }
            if (time < 1)
            {
                LockedTimer_Object.SetActive(true);
                LowTimer_Object.SetActive(false);
                Timer_Text.text = "";
            }
            else
            {
                HighTimer_Object.SetActive(true);
                NextRoundTimer_Object.SetActive(false);
                LockedTimer_Object.SetActive(false);
            }
        }
        // if (gameManager.currentPhase == GameManager.GamePhase.Waiting)
        // {
        //     NextRoundTimer_Object.SetActive(true);
        //     LockedTimer_Object.SetActive(false);
        // }
    }

    internal void SetPhase()
    {
        Timer_Text.text = "";
        HighTimer_Object.SetActive(false);
        LowTimer_Object.SetActive(false);
        LockedTimer_Object.SetActive(false);
        NextRoundTimer_Object.SetActive(false);
        if (gameManager.currentPhase == GameManager.GamePhase.Waiting)
        {
            NextRoundTimer_Object.SetActive(true);
        }
        if (gameManager.currentPhase == GameManager.GamePhase.Betting)
        {
            HighTimer_Object.SetActive(true);
            HighTimer_Object.GetComponent<RectTransform>().DOPunchScale(new Vector3(1.05f, 1.05f, 1.05f), 0.3f, vibrato: 0);
            Timer_Text.gameObject.GetComponent<RectTransform>().transform.localScale = new Vector3(1f, 1f, 1f);
            Timer_Text.gameObject.GetComponent<RectTransform>().DOPunchScale(Vector3.one * 1.03f, 0.4f, vibrato: 0);
        }
        if (gameManager.currentPhase == GameManager.GamePhase.Bonus)
        {
            LockedTimer_Object.SetActive(true);
            LockedTimer_Object.GetComponent<RectTransform>().DOPunchScale(new Vector3(1.05f, 1.05f, 1.05f), 0.3f, vibrato: 0);
        }
        if (gameManager.currentPhase == GameManager.GamePhase.CardReveal)
        {
            LockedTimer_Object.SetActive(true);
        }
        if (gameManager.currentPhase == GameManager.GamePhase.Cashout)
        {
            NextRoundTimer_Object.SetActive(true);
        }
    }


    internal void UpdateBalance(double balance)
    {
        Balance_Text.text = balance.ToString("F2");
    }

    internal void RoundStart()
    {
        loadingPage.SetActive(false);
    }

    internal void BetLocked(string bonusPosition, int bonusMultiplier)
    {
        HighTimer_Object.SetActive(false);
        LowTimer_Object.SetActive(false);
        LockedTimer_Object.SetActive(true);

        betManager.isBettingOpen = false;
        animationManager.ShowBonusCards(bonusPosition, bonusMultiplier);
    }


    internal void ShowCashoutUI(double winAmount)
    {
        RectTransform winRect = winText.GetComponent<RectTransform>();

        CanvasGroup cg = winText.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = winText.gameObject.AddComponent<CanvasGroup>();

        // reset state
        winText.text = "+" + winAmount.ToString();

        winRect.anchoredPosition = new Vector2(winRect.anchoredPosition.x, 0f);
        winRect.localScale = Vector3.zero;
        cg.alpha = 0f;

        Sequence seq = DOTween.Sequence();

        // Fade + Scale + Move together
        seq.Join(cg.DOFade(1f, 0.35f));
        seq.Join(winRect.DOScale(1f, 0.35f).SetEase(Ease.OutBack));
        seq.Join(winRect.DOAnchorPosY(250f, 0.8f).SetEase(Ease.OutCubic));

        // optional hold
        seq.AppendInterval(0.5f);

        // fade out
        seq.Append(cg.DOFade(0f, 0.3f));
        winText.text = "";
    }

    internal void SetInitialRoomJoinData(Root roomData, GameData initialData)
    {
        RoomId_Text.text = roomData.Payload.roomId;

        string level = roomData.Payload.level;
        gameManager.OnLeaderboardUpdate(roomData.Payload.leaderboards);
        // Coins.Clear();
        // foreach (var coin in Coins)
        // {
        //     coin.gameObject.SetActive(false);
        // }

        //Coin Values
        for (int i = 0; i < Coins.Count; i++)
        {
            if (level == "casual")
            {
                // Coins = CasualLevelCoins;
                // foreach (var coin in Coins)
                // {
                //     coin.gameObject.SetActive(true);
                // }
                List<double> coinValues = initialData.bets.casual;
                Coins[i].GetComponentInChildren<Image>().sprite = CasualLevelCoins[i];
                if (coinValues[i] > 10000)
                {
                    double chipvalue = coinValues[i] / 10000;
                    Coins[i].GetComponentInChildren<TMP_Text>().text = chipvalue.ToString();
                }
                else
                {
                    Coins[i].GetComponentInChildren<TMP_Text>().text = coinValues[i].ToString();
                }
                coinSelector.GetComponentInChildren<TMP_Text>().text = coinValues[0].ToString();
                coinSelector.GetComponentInChildren<Image>().sprite = CasualLevelCoins[0];

                betManager.chipSprites = null;
                betManager.chipSprites = CasualLevelCoins.ToArray();
                betManager.chipDenominations = coinValues.ToArray();
            }
            else if (level == "novice")
            {
                // Coins = NoviceLevelCoins;
                // foreach (var coin in Coins)
                // {
                //     coin.gameObject.SetActive(true);
                // }
                List<int> coinValues = initialData.bets.novice;
                Coins[i].GetComponentInChildren<Image>().sprite = NoviceLevelCoins[i];
                if (coinValues[i] > 10000)
                {
                    float chipvalue = coinValues[i] / 10000;
                    Coins[i].GetComponentInChildren<TMP_Text>().text = chipvalue.ToString();
                }
                else
                {
                    Coins[i].GetComponentInChildren<TMP_Text>().text = coinValues[i].ToString();
                }
                coinSelector.GetComponentInChildren<TMP_Text>().text = coinValues[0].ToString();
                coinSelector.GetComponentInChildren<Image>().sprite = NoviceLevelCoins[0];

                betManager.chipSprites = null;
                betManager.chipSprites = NoviceLevelCoins.ToArray();
                betManager.chipDenominations = coinValues.Select(x => (double)x).ToArray();
            }
            else if (level == "expert")
            {
                // Coins = ExpertLevelCoins;
                // foreach (var coin in Coins)
                // {
                //     coin.gameObject.SetActive(true);
                // }
                List<int> coinValues = initialData.bets.expert;
                Coins[i].GetComponentInChildren<Image>().sprite = ExpertLevelCoins[i];
                if (coinValues[i] > 10000)
                {
                    float chipvalue = coinValues[i] / 10000;
                    Coins[i].GetComponentInChildren<TMP_Text>().text = chipvalue.ToString();
                }
                else
                {
                    Coins[i].GetComponentInChildren<TMP_Text>().text = coinValues[i].ToString();
                }
                coinSelector.GetComponentInChildren<TMP_Text>().text = coinValues[0].ToString();
                coinSelector.GetComponentInChildren<Image>().sprite = ExpertLevelCoins[0];

                betManager.chipSprites = null;
                betManager.chipSprites = ExpertLevelCoins.ToArray();
                betManager.chipDenominations = coinValues.Select(x => (double)x).ToArray();
            }
            else if (level == "high_roller")
            {
                // Coins = HighRollerLevelCoins;
                // foreach (var coin in Coins)
                // {
                //     coin.gameObject.SetActive(true);
                // }
                List<int> coinValues = initialData.bets.high_roller;
                Coins[i].GetComponentInChildren<Image>().sprite = HighRollerLevelCoins[i];
                if (coinValues[i] >= 10000)
                {
                    float chipvalue = coinValues[i] / 1000f;
                    Coins[i].GetComponentInChildren<TMP_Text>().text = chipvalue.ToString() + "k";
                }
                else
                {
                    Coins[i].GetComponentInChildren<TMP_Text>().text = coinValues[i].ToString();
                }
                coinSelector.GetComponentInChildren<TMP_Text>().text = coinValues[0].ToString();
                coinSelector.GetComponentInChildren<Image>().sprite = HighRollerLevelCoins[0];

                betManager.chipSprites = null;
                betManager.chipSprites = HighRollerLevelCoins.ToArray();
                betManager.chipDenominations = coinValues.Select(x => (double)x).ToArray();
            }
        }
    }

    internal void SetInitialGameData(GameData gameData)
    {
        // Set any game data that needs to be initialized at the start of each game
        UserId_Text.text = socketManager.playerdata.username;
        UserName_Text.text = socketManager.playerdata.username;
        Balance_Text.text = socketManager.playerdata.balance.ToString();
        inGamePopupManager.SetInitialGameData(gameData);
    }

}