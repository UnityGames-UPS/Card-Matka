using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System;

using UnityEngine.Networking;

using Newtonsoft.Json;
using Best.SocketIO;
using Best.SocketIO.Events;

using System.Runtime.Serialization;
using Best.HTTP.Shared;
using NUnit.Framework.Interfaces;
using DG.Tweening;

public class SocketIOManager : MonoBehaviour
{
    [SerializeField]
    internal GameManager gameManager;

    [SerializeField]
    private UiManager uiManager;

    [SerializeField]
    internal BetManager betManager;
    [SerializeField] private InGamePopupManager inGamePopupManager;

    internal Root roomData;
    internal Root gameLoopData;
    internal Root BetChipData;
    internal Bonus BonusData;
    internal Root OtherPlayerBetData;
    internal Root CashoutData;
    internal Root doubleBetData;
    internal Root LeaderBoardData;
    internal Root ReturnHome;
    internal Root TotalPlayerCountData;
    internal Root TimeRemaining;
    internal Root CashoutTime;
    internal Root CardData;
    internal GameData initialData = null;
    // internal Payload resultData = null;
    internal Player playerdata = null;

    //WebSocket currentSocket = null;
    internal bool isResultdone = false;
    // protected string nameSpace="game"; //BackendChanges
    protected string nameSpace = "playground-multiplayer"; //BackendChanges
    private Socket gameSocket; //BackendChanges


    private SocketManager manager;


    protected string SocketURI = null;
    protected string TestSocketURI = "https://devrealtime.dingdinghouse.com/";
    // protected string TestSocketURI = "http://localhost:5000/";
    private string savedToken;

    [SerializeField] internal JSFunctCalls JSManager;
    [SerializeField] private string testToken;
    protected string gameID = "ml-ab";
    //protected string gameID = "";

    internal bool isLoaded = false;

    internal bool SetInit = false;

    private const int maxReconnectionAttempts = 6;
    private readonly TimeSpan reconnectionDelay = TimeSpan.FromSeconds(10);
    private bool isConnected = false; //Back2 Start
    private bool hasEverConnected = false;
    private const int MaxReconnectAttempts = 5;
    private const float ReconnectDelaySeconds = 2f;

    private float lastPongTime = 0f;
    private float pingInterval = 2f;
    private float pongTimeout = 3f;
    private bool waitingForPong = false;
    private int missedPongs = 0;
    private const int MaxMissedPongs = 20;
    internal bool loadingPageLoading = false;
    internal bool NormalStart = false;
    internal bool DontDisplayDisconected = false;
    private bool isFirstRoom = true;
    private Coroutine PingRoutine; //Back2 end
    [SerializeField] private GameObject RaycastBlocker;

    private void Awake()
    {
        // Keep Unity's game loop running even when the browser tab is hidden.
        // This was already here — keeping it.
        Application.runInBackground = true;

        // Switch ALL DOTween animations project-wide to use unscaledDeltaTime.
        // This is the core fix: tweens now advance on real wall-clock time and
        // are completely immune to whatever the browser does to Time.timeScale
        // when a tab loses focus. Without this, tweens freeze when hidden and
        // then fire all at once when you return to the tab.
        DOTween.defaultTimeScaleIndependent = true;

        // Safety: ensure timeScale starts at 1.
        Time.timeScale = 1f;

        isLoaded = false;
        SetInit = false;
    }

    private void Start()
    {
        //OpenWebsocket();
        OpenSocket();
    }
    void CloseGame()
    {
        Debug.Log("Unity: Closing Game");
        StartCoroutine(CloseSocket());
    }


    void ReceiveAuthToken(string jsonData)
    {
        Debug.Log("Received data: " + jsonData);

        // Parse the JSON data
        var data = JsonUtility.FromJson<AuthTokenData>(jsonData);
        SocketURI = data.socketURL;
        myAuth = data.cookie;
        //  nameSpace = data.nameSpace;
        // Proceed with connecting to the server using myAuth and socketURL
    }


    string myAuth = null;

    private void OpenSocket()
    {
        //Create and setup SocketOptions
        SocketOptions options = new SocketOptions();
        options.AutoConnect = false;
        options.Reconnection = false;
        options.Timeout = TimeSpan.FromSeconds(3);
        options.ConnectWith = Best.SocketIO.Transports.TransportTypes.WebSocket; //BackendChanges


        //   Application.ExternalCall("window.parent.postMessage", "authToken", "*");

#if UNITY_WEBGL && !UNITY_EDITOR
        JSManager.SendCustomMessage("authToken");
        StartCoroutine(WaitForAuthToken(options));
#else
        Func<SocketManager, Socket, object> authFunction = (manager, socket) =>
        {
            return new
            {
                token = testToken,
                // gameId = gameID
            };
        };
        options.Auth = authFunction;
        savedToken = testToken;
        // Proceed with connecting to the server
        SetupSocketManager(options);
#endif
    }

    private IEnumerator WaitForAuthToken(SocketOptions options)
    {
        // Wait until myAuth is not null
        while (myAuth == null)
        {
            Debug.Log("My Auth is null");
            yield return null;
        }
        while (SocketURI == null)
        {
            Debug.Log("My Socket is null");
            yield return null;
        }
        Debug.Log("My Auth is not null");
        // Once myAuth is set, configure the authFunction
        Func<SocketManager, Socket, object> authFunction = (manager, socket) =>
        {
            return new
            {
                token = myAuth,
                // gameId = gameID
            };
        };
        options.Auth = authFunction;
        savedToken = myAuth;
        Debug.Log("Auth function configured with token: " + myAuth);

        // Proceed with connecting to the server
        SetupSocketManager(options);
        yield return null;
    }

    private void SetupSocketManager(SocketOptions options)
    {
        // Create and setup SocketManager
#if UNITY_EDITOR
        this.manager = new SocketManager(new Uri(TestSocketURI), options);
#else
        this.manager = new SocketManager(new Uri(SocketURI), options);
#endif
        if (string.IsNullOrEmpty(nameSpace))
        {  //BackendChanges Start
            gameSocket = this.manager.Socket;
        }
        else
        {
            print("nameSpace: " + nameSpace);
            gameSocket = this.manager.GetSocket("/" + nameSpace);
        }
        // Set subscriptions
        gameSocket.On<ConnectResponse>(SocketIOEventTypes.Connect, OnConnected);
        gameSocket.On(SocketIOEventTypes.Disconnect, OnDisconnected);
        gameSocket.On<Error>(SocketIOEventTypes.Error, OnError);
        //gameSocket.On<string>("message", OnListenEvent);
        gameSocket.On<string>("game:init", ManageInitData);
        gameSocket.On<string>("game:round_start", OnGameLoopStarted);
        gameSocket.On<string>("game:betting_timer", OnListenTimeEvent);
        gameSocket.On<string>("game:cashout_timer", OnListenCashOutTimeEvent);
        gameSocket.On<string>("game:bet_placed", ManageOtherPlayerbets);
        gameSocket.On<string>("game:bet_cancel", ManageBetCancel);
        gameSocket.On<string>("game:bonus", ManageBonus);
        gameSocket.On<string>("game:card_result", OnListenCard);
        gameSocket.On<string>("game:cashout", OnCashout);
        gameSocket.On<string>("game:round_end", OnGameLoopEnd);
        gameSocket.On<string>("game:lobby_count", OnLobbyCount);
        gameSocket.On<string>("game:leaderboard_update", UpdateLeaderBoard);
        //gameSocket.On<string>("game:card_dealt", OnListenCardEvent);
        gameSocket.On<bool>("socketState", OnSocketState);
        gameSocket.On<string>("internalError", OnSocketError);
        gameSocket.On<string>("alert", OnSocketAlert);
        gameSocket.On<string>("AnotherDevice", OnSocketOtherDevice);
        gameSocket.On<string>("pong", OnPongReceived);
        manager.Open();
    }

    // Connected event handler implementation
    void OnConnected(ConnectResponse resp) //Back2 Start
    {
        Debug.Log("✅ Connected to server.");

        if (hasEverConnected)
        {
            uiManager.CheckAndClosePopups();
        }

        isConnected = true;
        hasEverConnected = true;
        waitingForPong = false;
        missedPongs = 0;
        lastPongTime = Time.time;
        SendPing();
    } //Back2 end

    private void OnPongReceived(string data) //Back2 Start
    {
        // Debug.Log("✅ Received pong from server.");
        waitingForPong = false;
        missedPongs = 0;
        lastPongTime = Time.time;
        //  Debug.Log($"⏱️ Updated last pong time: {lastPongTime}");
        //  Debug.Log($"📦 Pong betPayload: {data}");
    } //Back2 end

    private void OnDisconnected() //Back2 Start
    {
        Debug.LogWarning("⚠️ Disconnected from server.");
        isConnected = false;
        if (!uiManager.isExit)
        {
            uiManager.DisconnectionPopup();
        }
        //RaycastBlocker.SetActive(true);
        ResetPingRoutine();
    } //Back2 end
    private void OnError(Error err)
    {
        Debug.LogError("Socket Error Message: " + err);
#if UNITY_WEBGL && !UNITY_EDITOR
    JSManager.SendCustomMessage("error");
#endif
    }
    private void OnListenTimeEvent(string data)
    {
        TimeRemaining = JsonConvert.DeserializeObject<Root>(data);
        Debug.Log(data);
        gameManager.OnTimerTick(TimeRemaining.timeRemaining);
    }
    private void OnListenCashOutTimeEvent(string data)
    {
        CashoutTime = JsonConvert.DeserializeObject<Root>(data);
        Debug.Log(data);
        gameManager.OnTimerTick(CashoutTime.timeRemaining);
    }
    private void OnListenCard(string data)
    {
        Debug.Log("Received Card:\n" + data);
        CardData = JsonConvert.DeserializeObject<Root>(data);
        gameManager.OnCardResult(CardData.resultCard, CardData.resultSuit, CardData.combination);
    }

    private void OnSocketState(bool state)
    {
        if (state)
        {
            Debug.Log("my state is " + state);
        }
        else
        {

        }
    }
    private void OnSocketError(string data)
    {
        Debug.Log("Received error with data: " + data);
    }
    private void OnSocketAlert(string data)
    {
        //        Debug.Log("Received alert with data: " + data);
    }
    private bool isFocused = true;
    private Coroutine focusCheckCoroutine;
    private bool disconnectionShown = false;   // <- NEW

    void OnApplicationFocus(bool focus)
    {
        Debug.Log("Focus: " + focus);
        isFocused = focus;

        // Prevent browser from freezing the game loop via timeScale
        Time.timeScale = 1f;

        // Mute audio when hidden, restore when visible
        uiManager?.OnAppFocusChanged(focus);

        if (!focus)
        {
            if (focusCheckCoroutine == null && !disconnectionShown)
                focusCheckCoroutine = StartCoroutine(IsNotInFocus());
        }
        else
        {
            if (disconnectionShown) return;

            if (focusCheckCoroutine != null)
            {
                StopCoroutine(focusCheckCoroutine);
                focusCheckCoroutine = null;
            }
        }
    }

    IEnumerator IsNotInFocus()
    {
        yield return new WaitForSeconds(120f); // 2 seconds, change as required

        // If still not focused AND popup not shown
        if (!isFocused && !disconnectionShown)
        {
            // disconnectionShown = true;  // Prevent future runs
            uiManager.DisconnectionPopup();
            Debug.Log("Disconnected: No Focus for 120 seconds");
        }

        focusCheckCoroutine = null;
    }

    private void OnSocketOtherDevice(string data)
    {
        Debug.Log("Received Device Error with data: " + data);
        uiManager.ADfunction();
    }

    private void SendPing() //Back2 Start
    {
        ResetPingRoutine();
        PingRoutine = StartCoroutine(PingCheck());
    }

    void ResetPingRoutine()
    {
        if (PingRoutine != null)
        {
            StopCoroutine(PingRoutine);
        }
        PingRoutine = null;
    }

    private IEnumerator PingCheck()
    {
        while (true)
        {
            //  Debug.Log($"🟡 PingCheck | waitingForPong: {waitingForPong}, missedPongs: {missedPongs}, timeSinceLastPong: {Time.time - lastPongTime}");

            if (missedPongs == 0)
            {
                uiManager.CheckAndClosePopups();
            }

            // If waiting for pong, and timeout passed
            if (waitingForPong)
            {
                if (missedPongs == 2)
                {
                    uiManager.ReconnectionPopup();
                }
                missedPongs++;
                //  Debug.LogWarning($"⚠️ Pong missed #{missedPongs}/{MaxMissedPongs}");

                if (missedPongs >= MaxMissedPongs)
                {
                    //  Debug.LogError("❌ Unable to connect to server — 5 consecutive pongs missed.");
                    isConnected = false;
                    uiManager.DisconnectionPopup();
                    yield break;
                }
            }

            // Send next ping
            waitingForPong = true;
            lastPongTime = Time.time;
            //  Debug.Log("📤 Sending ping...");
            SendDataWithNamespace("ping");
            yield return new WaitForSeconds(pingInterval);
        }
    } //Back2 end

    private void SendDataWithNamespace(string eventName, string json = null)
    {
        // Send the message
        if (gameSocket != null && gameSocket.IsOpen) //BackendChanges
        {
            if (json != null)
            {
                gameSocket.Emit(eventName, json);
                Debug.Log("JSON data sent: " + json);
            }
            else
            {
                gameSocket.Emit(eventName);
            }
        }
        else
        {
            Debug.LogWarning("Socket is not connected.");
        }
    }

    internal void ReactNativeCallOnFailedToConnect() //BackendChanges
    {
#if UNITY_WEBGL && !UNITY_EDITOR
    JSManager.SendCustomMessage("onExit");
#endif
    }

    internal IEnumerator CloseSocket() //Back2 Start
    {
        RaycastBlocker.SetActive(true);
        ResetPingRoutine();

        Debug.Log("Closing Socket");

        manager?.Close();
        manager = null;

        Debug.Log("Waiting for socket to close");

        yield return new WaitForSeconds(0.5f);

        Debug.Log("Socket Closed");

#if UNITY_WEBGL && !UNITY_EDITOR
    JSManager.SendCustomMessage("OnExit"); //Telling the react platform user wants to quit and go back to homepage
#endif
    }
    public void Reconnect()
    {
        Debug.Log("Reconnecting using saved token...");

        SocketOptions options = new SocketOptions();
        options.AutoConnect = false;
        options.Reconnection = false;
        options.Timeout = TimeSpan.FromSeconds(3);
        options.ConnectWith = Best.SocketIO.Transports.TransportTypes.WebSocket;

        // Use saved token here
        options.Auth = (manager, socket) =>
        {
            return new { token = savedToken };
        };
        SetupSocketManager(options);
        // Close old manager if any
        manager?.Close();

#if UNITY_EDITOR
        manager = new SocketManager(new Uri(TestSocketURI), options);
#else
            manager = new SocketManager(new Uri(SocketURI), options);
#endif

        // Get correct namespace
        if (string.IsNullOrEmpty(nameSpace))
            gameSocket = manager.Socket;
        else
            gameSocket = manager.GetSocket("/" + nameSpace);

        manager.Open();
        DontDisplayDisconected = false;
    }

    void ManageInitData(string jsonObject)
    {
        // Root myData = null;
        // try
        // {
        //     myData = JsonConvert.DeserializeObject<Root>(jsonObject);
        // }
        //         catch (Exception ex)
        //         {
        //             Debug.LogError("Failed to deserialize JSON. Exception: " + ex.Message + "\nJSON: " + jsonObject);
        //             return;
        //         }

        //         if (myData == null)
        //         {
        //             Debug.LogError("ParseResponse: myData is null. JSON = " + jsonObject);
        //             return;
        //         }
        Debug.Log("ParseResponse: " + jsonObject);

        //         string id = myData.id;
        //         initialData = myData.gameData;
        initialData = JsonConvert.DeserializeObject<Root>(jsonObject).gameData;
        playerdata = JsonConvert.DeserializeObject<Root>(jsonObject).player;

        //         setInitialData();
        //         // if (initialData.bets != null)
        //         // else
        //         //     Debug.LogWarning("initData: bets list is null.");

#if UNITY_WEBGL && !UNITY_EDITOR
                    JSManager.SendCustomMessage("OnEnter");
#endif
        uiManager.LoadingScreen_Object.SetActive(true);
        gameManager.OnInitData(initialData, playerdata);
        SendRoomSelection("casual");
        setInitialData();
    }

    private void ParseResponse(string jsonObject)
    {
        Debug.Log("ParseResponse JSON: " + jsonObject);

        Root myData = null;
        try
        {
            myData = JsonConvert.DeserializeObject<Root>(jsonObject);
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to deserialize JSON. Exception: " + ex.Message + "\nJSON: " + jsonObject);
            return;
        }

        if (myData == null)
        {
            Debug.LogError("ParseResponse: myData is null. JSON = " + jsonObject);
            return;
        }

        string id = myData.id;

        switch (id)
        {
            case "initData":
                {
                    //  gameManager.uiManager.touchDisable.SetActive(false);

                    if (myData.gameData == null)
                    {
                        Debug.LogError("initData missing gameData. JSON = " + jsonObject);
                        return;
                    }

                    if (myData.player == null)
                    {
                        Debug.LogWarning("initData missing player data. JSON = " + jsonObject);
                    }

                    initialData = myData.gameData;
                    playerdata = myData.player;

                    setInitialData();
                    // if (initialData.bets != null)
                    // else
                    //     Debug.LogWarning("initData: bets list is null.");

#if UNITY_WEBGL && !UNITY_EDITOR
                    JSManager.SendCustomMessage("OnEnter");
#endif

                    break;
                }

            case "ResultData":
                {
                    playerdata = myData.player;


                    isResultdone = true;
                    break;
                }

            case "ExitUser":
                {
                    if (gameSocket != null)
                    {
                        Debug.Log("Dispose my Socket");
                        this.manager.Close();
                    }

                    Application.ExternalCall("window.parent.postMessage", "onExit", "*");
#if UNITY_WEBGL && !UNITY_EDITOR
                    Application.ExternalEval(@"
                      if(window.ReactNativeWebView){
                        window.ReactNativeWebView.postMessage('onExit');
                      }
                    ");
#endif
                    break;
                }

            default:
                Debug.LogWarning("Unknown id in JSON: " + id);
                break;
        }
    }



    private void setInitialData()
    {
        isLoaded = true;
        //gameManager.SetInitialData();
        RaycastBlocker.SetActive(false);
        Application.ExternalCall("window.parent.postMessage", "OnEnter", "*");
#if UNITY_WEBGL && !UNITY_EDITOR //BackendChanges
            Application.ExternalEval(@"
            if(window.ReactNativeWebView){
            window.ReactNativeWebView.postMessage('OnEnter');
            }
            ");
#endif
    }

    internal void SendRoomSelection(string Room)
    {
        Debug.Log("*** send Data ***" + Room);
        SendRoom level = new SendRoom();
        level.payload = new Payload();
        level.type = "JOIN_LEVEL";
        level.payload.level = Room;

        string json = JsonUtility.ToJson(level);
        Debug.Log("*** send Data ***" + json);
        // SendDataWithNamespace("request", json);
        gameSocket.ExpectAcknowledgement<string>(OnRoomEnter).Emit("request", json);
    }
    internal void BetPlaced(int amountIndex, string betType, string betOption)
    {
        BetMessage message = new BetMessage();
        message.type = "PLACE_BET";
        message.payload = new BetPayload();
        message.payload.amountIndex = amountIndex;
        message.payload.betType = betType;
        message.payload.betOption = betOption;
        message.payload.level = betManager.currentLevel;

        string json = JsonUtility.ToJson(message);
        Debug.Log("Bet JSON: " + json);

        gameSocket.ExpectAcknowledgement<string>(OnBetAcknowledged).Emit("request", json);
    }
    internal void SendUndo()
    {
        SendRoom message = new SendRoom();
        message.payload = new Payload();
        message.type = "UNDO_BET";
        message.payload.level = betManager.currentLevel;

        string json = JsonUtility.ToJson(message);
        Debug.Log("Sending Undo with JSON: " + json);
        gameSocket.ExpectAcknowledgement<string>(OnUndo).Emit("request", json);
    }
    internal void SendRepeat()
    {
        SendRoom message = new SendRoom();
        message.payload = new Payload();
        message.type = "REPEAT_BET";
        message.payload.level = betManager.currentLevel;

        string json = JsonUtility.ToJson(message);
        Debug.Log("Sending Repeat with JSON: " + json);
        gameSocket.ExpectAcknowledgement<string>(OnRepeat).Emit("request", json);
    }
    internal void SendCancle()
    {
        SendRoom message = new SendRoom();
        message.payload = new Payload();
        message.type = "CANCEL_BET";
        message.payload.level = betManager.currentLevel;

        string json = JsonUtility.ToJson(message);
        Debug.Log("Sending Cancel with JSON: " + json);
        gameSocket.ExpectAcknowledgement<string>(OnCancle).Emit("request", json);
    }
    internal void SendDouble()
    {
        SendRoom message = new SendRoom();
        message.payload = new Payload();
        message.type = "DOUBLE_BET";
        message.payload.level = betManager.currentLevel;

        string json = JsonUtility.ToJson(message);
        Debug.Log("Sending Double with JSON: " + json);
        gameSocket.ExpectAcknowledgement<string>(OnDouble).Emit("request", json);
    }
    internal void SendHome()
    {
        SendRoom message = new SendRoom();
        message.payload = new Payload();
        message.type = "HOME";
        // message.betPayload.level = Room;
        loadingPageLoading = false;
        NormalStart = false;
        DontDisplayDisconected = true;
        string json = JsonUtility.ToJson(message);
        Debug.Log("Return home: " + json);
        // SendDataWithNamespace("request", json);
        gameSocket.ExpectAcknowledgement<string>(OnHome).Emit("request", json);
    }

    internal void SendHistory(int page)
    {
        SendRoom message = new SendRoom();
        message.type = "BET_HISTORY";
        message.payload = new Payload();
        message.payload.page = page;
        string json = JsonUtility.ToJson(message);
        Debug.Log("History sent page " + page + ": " + json);
        gameSocket.ExpectAcknowledgement<string>(OnHistory).Emit("request", json);
    }

    void OnHome(string json)
    {
        ///gameManager.ClearAllBets();
        betManager.OnHome();
        Debug.Log("Home Receved: " + json);
        ReturnHome = JsonUtility.FromJson<Root>(json);
        //gameManager.SetPlayerCountOnReturn(ReturnHome.betPayload.lobby, ReturnHome.betPayload.balance);
        //playerdata.balance = ReturnHome.Payload.balance;
        inGamePopupManager.isHome = true;
        //StartCoroutine(gameManager.ShowLoadingPage("Loading...."));
        //gameManager.GamePage.SetActive(false);
        //gameManager.HomePage.SetActive(true);
        //Invoke(nameof(Reconnect), 0.2f);`

    }
    void OnHistory(string json)
    {
        Debug.Log("**History Received**: " + json);
        try
        {
            HistoryRoot data = JsonConvert.DeserializeObject<HistoryRoot>(json);
            if (data != null && data.success && data.payload != null)
                gameManager.OnHistoryReceived(data.payload.history, data.payload.meta);
            else
                Debug.LogWarning("History request failed: " + json);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("History parse error: " + ex.Message);
        }
    }

    void OnDouble(string json)
    {
        Debug.Log("Double Ack: " + json);
        doubleBetData = JsonConvert.DeserializeObject<Root>(json);
        if (doubleBetData.success)
        {
            if (playerdata != null) playerdata.balance = doubleBetData.Payload.balance;
            betManager.OnDoubleDone(doubleBetData.Payload.bets, doubleBetData.Payload.balance, doubleBetData.Payload.totalBet);
        }
        else
        {
            gameManager.ShowPopupMessage(doubleBetData.Payload.message);
            Debug.LogWarning("Double failed: " + doubleBetData.betPayload?.message);
        }
    }
    void OnRepeat(string json)
    {
        Debug.Log("Repeat Ack: " + json);
        doubleBetData = JsonConvert.DeserializeObject<Root>(json);
        if (doubleBetData.success)
        {
            if (playerdata != null) playerdata.balance = doubleBetData.Payload.balance;
            // FIX: was incorrectly reading balance/totalBet from betPayload (null) instead of Payload
            betManager.OnRepeatDone(doubleBetData.Payload.bets, doubleBetData.Payload.balance, doubleBetData.Payload.totalBet);
        }
        else
        {
            gameManager.ShowPopupMessage(doubleBetData.Payload.message);
            Debug.LogWarning("Repeat failed: " + doubleBetData.betPayload?.message);
        }
    }
    void OnCancle(string json)
    {
        Debug.Log("Cancel Ack: " + json);
        doubleBetData = JsonConvert.DeserializeObject<Root>(json);
        if (doubleBetData.success)
        {
            if (playerdata != null) playerdata.balance = doubleBetData.Payload.balance;
            betManager.OnCancelDone(doubleBetData.Payload.balance);
        }
        else
        {
            gameManager.ShowPopupMessage(doubleBetData.Payload.message);
            Debug.LogWarning("Cancel failed: " + doubleBetData.betPayload?.message);
        }
    }
    void OnUndo(string json)
    {
        Debug.Log("Undo Ack: " + json);
        doubleBetData = JsonConvert.DeserializeObject<Root>(json);
        if (doubleBetData.success)
        {
            //if (playerdata != null) 
            playerdata.balance = doubleBetData.Payload.balance;
            double undoAmount = doubleBetData.Payload.bet?.amount ?? 0;
            betManager.OnUndoDone(doubleBetData.Payload.bet?.betOption,
                                   undoAmount,
                                   doubleBetData.Payload.balance, doubleBetData.Payload.totalBet);
        }
        else
        {
            gameManager.ShowPopupMessage(doubleBetData.Payload.message);
            Debug.LogWarning("Undo failed: " + doubleBetData.betPayload?.message);
        }
    }
    void OnRoomEnter(string json)
    {
        uiManager.LoadingScreen_Object.SetActive(false);
        Debug.Log("ON ROOM ENTER : " + json);
        roomData = JsonConvert.DeserializeObject<Root>(json);
        uiManager.SetInitialRoomJoinData(roomData, initialData);
        inGamePopupManager.isHome = false;
        if (isFirstRoom)
        {
            isFirstRoom = false;
            //uiManager.OpenPopup(uiManager.StartupPanel);
        }
        gameManager.OnRoomEnter(roomData.Payload.stats, roomData.Payload.leaderboards);
        gameManager.OnLobbyCount(roomData.Payload.playerCount);
        //if (roomData.success == false)
        //{
        //StartCoroutine(gameManager.ShowLoadingPage("Loading...."));
        //gameManager.HomePage.SetActive(true);
        // gameManager.LoadingPage.SetActive(false);
        //gameManager.GamePage.SetActive(false);
        //   return;

        // }
        //gameManager.SetCoinData();
        //gameManager.SetOptionData();
        //gameManager.SetOtherplayerData(roomData.betPayload.leaderboards);
    }

    void ManageOtherPlayerbets(string data)
    {
        Debug.Log("Bet Placed OtherPlayer\n" + data);
        OtherPlayerBetData = JsonConvert.DeserializeObject<Root>(data);

        string betOption = OtherPlayerBetData?.betOption;
        string username = OtherPlayerBetData?.username;
        float amount = OtherPlayerBetData.amount;

        if (!string.IsNullOrEmpty(betOption) && playerdata.username != username)
            gameManager.OnOtherPlayerBet(betOption, username, amount);
    }

    void ManageBetCancel(string data)
    {
        Debug.Log("Bet Cancel\n" + data);
        //BonusData = JsonUtility.FromJson<Root>(data);
        //gameManager.ManageBrodcastBetsOtherPlayers(BonusData);

    }
    void ManageBonus(string data)
    {
        Debug.Log("Bonus\n" + data);
        BonusData = JsonConvert.DeserializeObject<Bonus>(data);
        betManager.OnBettingClose();
        gameManager.OnBonus(BonusData);
    }

    void OnGameLoopStarted(string json)
    {
        if (!loadingPageLoading)
        {
            loadingPageLoading = true;
        }
        NormalStart = true;
        Debug.Log("Round Started\n" + json);
        gameLoopData = JsonConvert.DeserializeObject<Root>(json);
        betManager.OnRoundStart();
        gameManager.OnRoundStart(gameLoopData.roundId);
        gameManager.OnLobbyCount(gameLoopData.playerCount);
    }
    void OnGameLoopEnd(string data)
    {
        Debug.Log("Loop end\n" + data);
        betManager.OnRoundEnd();
        gameManager.OnRoundEnd(gameLoopData.roundId);
    }
    void OnCashout(string data)
    {
        Debug.Log("CashOut\n" + data);
        CashoutData = JsonConvert.DeserializeObject<Root>(data);

        // find this player's payout
        double win = 0;
        double balance = playerdata != null ? playerdata.balance : 0;
        if (CashoutData.payouts != null && playerdata != null)
        {
            foreach (var p in CashoutData.payouts)
            {
                if (p.username == playerdata.username)
                {
                    win = p.win;
                    balance = p.balance;
                    break;
                }
            }
        }

        if (playerdata != null) playerdata.balance = balance;
        //betManager.OnCashout(win, balance);
        gameManager.OnCashout(win, balance, CashoutData.payouts, CashoutData.leaderboards);
    }
    void OnLobbyCount(string data)
    {
        Debug.Log("player Count\n" + data);
        TotalPlayerCountData = JsonUtility.FromJson<Root>(data);
        if (TotalPlayerCountData.Payload != null)
            gameManager.OnLobbyCount(TotalPlayerCountData.Payload.count);
        else
            gameManager.OnLobbyCount(TotalPlayerCountData.playerCount);
    }

    void UpdateLeaderBoard(string data)
    {
        Debug.Log("LeaderBoard Update\n" + data);
        LeaderBoardData = JsonUtility.FromJson<Root>(data);
        gameManager.OnLeaderboardUpdate(LeaderBoardData.leaderboards);
    }

    private void OnBetAcknowledged(string data)
    {
        Debug.Log("Bet Acknowledgement: " + data);
        BetChipData = JsonConvert.DeserializeObject<Root>(data);
        if (BetChipData.success)
        {
            if (playerdata != null) playerdata.balance = BetChipData.Payload.balance;
            betManager.OnBetPlaced(BetChipData.Payload.bet.betOption, BetChipData.Payload.bet.amount,
                                    BetChipData.Payload.balance, BetChipData.Payload.totalBet);
        }
        else
        {
            gameManager.ShowPopupMessage(BetChipData.Payload.message);
            Debug.LogWarning("PlaceBet failed: " + BetChipData.betPayload?.message);
        }
    }
}

// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);

[Serializable]
public class AuthTokenData
{
    public string cookie;
    public string socketURL;
    public string nameSpace;
}

[Serializable]
public class Bets
{
    public List<double> casual { get; set; }
    public List<int> novice { get; set; }
    public List<int> expert { get; set; }
    public List<int> high_roller { get; set; }
}

[Serializable]
public class BonusMultipliers
{
    public int small { get; set; }
    public int big { get; set; }
    public int odd { get; set; }
    public int even { get; set; }
}

[Serializable]
public class GameData
{
    public List<string> betOptions { get; set; }
    public int roundInterval { get; set; }
    public int cardInterval { get; set; }
    public int diceLimit { get; set; }
    public int statsLimit { get; set; }
    public Bets bets { get; set; }
    public LevelBetLimit levelBetLimit { get; set; }
    public List<string> levels { get; set; }
    public Wagers wagers { get; set; }
    public Lobby lobby { get; set; }
    public Leaderboards leaderboards { get; set; }
    public List<string> stats { get; set; }
    public BonusMultipliers bonusMultipliers { get; set; }

}

[Serializable]
public class J
{
    public List<double> payout { get; set; }
    public MaxBetLimit max_bet_limit { get; set; }
}

[Serializable]
public class JClubs
{
    public List<int> payout { get; set; }
    public MaxBetLimit max_bet_limit { get; set; }
}

[Serializable]
public class JDiamonds
{
    public List<int> payout { get; set; }
    public MaxBetLimit max_bet_limit { get; set; }
}

[Serializable]
public class JHearts
{
    public List<int> payout { get; set; }
    public MaxBetLimit max_bet_limit { get; set; }
}

[Serializable]
public class JSpades
{
    public List<int> payout { get; set; }
    public MaxBetLimit max_bet_limit { get; set; }
}

[Serializable]
public class K
{
    public List<double> payout { get; set; }
    public MaxBetLimit max_bet_limit { get; set; }
}

[Serializable]
public class KClubs
{
    public List<int> payout { get; set; }
    public MaxBetLimit max_bet_limit { get; set; }
}

[Serializable]
public class KDiamonds
{
    public List<int> payout { get; set; }
    public MaxBetLimit max_bet_limit { get; set; }
}

[Serializable]
public class KHearts
{
    public List<int> payout { get; set; }
    public MaxBetLimit max_bet_limit { get; set; }
}

[Serializable]
public class KSpades
{
    public List<int> payout { get; set; }
    public MaxBetLimit max_bet_limit { get; set; }
}

[Serializable]
public class Leaderboards
{
    public List<Richest> richest { get; set; }
    public List<Winner> winners { get; set; }
}

[Serializable]
public class Lobby
{
    public int casual { get; set; }
    public int novice { get; set; }
    public int expert { get; set; }
    public int high_roller { get; set; }
}

[Serializable]
public class MainBets
{
    public K K { get; set; }
    public Q Q { get; set; }
    public J J { get; set; }
}

[Serializable]
public class MaxBetLimit
{
    public int casual { get; set; }
    public int novice { get; set; }
    public int expert { get; set; }
    public int high_roller { get; set; }
}

[Serializable]
public class OpBets
{
    public KSpades K_spades { get; set; }
    public QSpades Q_spades { get; set; }
    public JSpades J_spades { get; set; }
    public KHearts K_hearts { get; set; }
    public QHearts Q_hearts { get; set; }
    public JHearts J_hearts { get; set; }
    public KDiamonds K_diamonds { get; set; }
    public QDiamonds Q_diamonds { get; set; }
    public JDiamonds J_diamonds { get; set; }
    public KClubs K_clubs { get; set; }
    public QClubs Q_clubs { get; set; }
    public JClubs J_clubs { get; set; }
}

[Serializable]
public class Player
{
    public double balance { get; set; }
    public string username { get; set; }
}

[Serializable]
public class Q
{
    public List<double> payout { get; set; }
    public MaxBetLimit max_bet_limit { get; set; }
}

[Serializable]
public class QClubs
{
    public List<int> payout { get; set; }
    public MaxBetLimit max_bet_limit { get; set; }
}

[Serializable]
public class QDiamonds
{
    public List<int> payout { get; set; }
    public MaxBetLimit max_bet_limit { get; set; }
}

[Serializable]
public class QHearts
{
    public List<int> payout { get; set; }
    public MaxBetLimit max_bet_limit { get; set; }
}

[Serializable]
public class QSpades
{
    public List<int> payout { get; set; }
    public MaxBetLimit max_bet_limit { get; set; }
}

[Serializable]
public class Root
{
    public string id { get; set; }
    public GameData gameData { get; set; }
    public Player player { get; set; }

    // On Level Join
    public bool success { get; set; }
    public Payload Payload { get; set; }
    //public Payload payload { get; set; }

    //On Round Start
    //public string roundId { get; set; }
    //public long startedAt { get; set; }
    //public long bettingEndTime { get; set; }
    //public long serverTime { get; set; }
    public int playerCount { get; set; }

    // ack bet response fields
    //public bool success { get; set; }
    public BetResponsePayload betPayload { get; set; }


    //timer

    public string roundId { get; set; }
    public long serverTime { get; set; }
    public long bettingEndTime { get; set; }
    public int timeRemaining { get; set; }

    //Cashout Timer
    public long cashoutEndTime { get; set; }

    //Bonus 
    public Bonus bonus { get; set; }

    //Card Result
    //public string roundId { get; set; }
    public string resultCard { get; set; }
    public string resultSuit { get; set; }
    public string combination { get; set; }

    //CashOut
    public Leaderboards leaderboards { get; set; }
    public List<Payout> payouts { get; set; }

    //Room Left
    public string roomId { get; set; }

    //Lobby Count
    public Lobby lobby { get; set; }

    //other player bet
    public string username { get; set; }
    public string betId { get; set; }
    public string betType { get; set; }
    public string betOption { get; set; }
    public float amount { get; set; }

}


public class Bonus
{
    public string roundId { get; set; }
    public Dictionary<string, int> bonus { get; set; }
}



[Serializable]
public class BetResponsePayload
{
    public string message { get; set; }
    public double balance { get; set; }
    public double totalBet { get; set; }
    public double totalRefund { get; set; }
    public double totalDelta { get; set; }
    public BetEntry bet { get; set; }           // PLACE_BET and UNDO response
    public List<BetEntry> bets { get; set; }    // DOUBLE / REPEAT response
}

[Serializable]
public class BetEntry
{
    public string betId { get; set; }
    public string betType { get; set; }
    public string betOption { get; set; }
    public double amount { get; set; }
    public double oldAmount { get; set; }
    public double newAmount { get; set; }
    public double delta { get; set; }
}

// ── Request message classes ───────────────────────────────────────────────────

[Serializable]
public class SendRoom
{
    public string type;
    public Payload payload;
}

[Serializable]
public class Payload
{
    public string level;
    public int page;
    public Bet bet { get; set; }
    public double totalBet { get; set; }
    public double balance { get; set; }


    //On Level Join
    public string roomId { get; set; }
    public string oldRoomId { get; set; }
    public int playerCount { get; set; }
    //public string level { get; set; }
    public List<string> stats { get; set; }
    public Leaderboards leaderboards { get; set; }

    public RoundState roundState { get; set; }

    public List<Bet> bets { get; set; }
    public double totalDelta { get; set; }
    public int cancelledCount { get; set; }
    public double totalRefund { get; set; }

    //On Home
    public string message { get; set; }
    public Lobby lobby { get; set; }

    //Lobby Count
    public int count { get; set; }

}

[Serializable]
public class RoundState
{
    public string roundId { get; set; }
    public long startedAt { get; set; }
    public long bettingEndTime { get; set; }
    public long serverTime { get; set; }
    public int timeRemaining { get; set; }
    public string phase { get; set; }
}

[Serializable]
public class BetMessage
{
    public string type;
    public BetPayload payload;
}

[Serializable]
public class BetPayload
{
    public string betType;
    public string betOption;
    public int amountIndex;
    public string level;
}

[Serializable]
public class SideBets
{
    public SpecificClubs specific_Clubs { get; set; }
    public SpecificSpades specific_Spades { get; set; }
    public SpecificDiamonds specific_Diamonds { get; set; }
    public SpecificHearts specific_Hearts { get; set; }
}

[Serializable]
public class SpecificClubs
{
    public List<double> payout { get; set; }
    public MaxBetLimit max_bet_limit { get; set; }
}

[Serializable]
public class SpecificDiamonds
{
    public List<double> payout { get; set; }
    public MaxBetLimit max_bet_limit { get; set; }
}

[Serializable]
public class SpecificHearts
{
    public List<double> payout { get; set; }
    public MaxBetLimit max_bet_limit { get; set; }
}

[Serializable]
public class SpecificSpades
{
    public List<double> payout { get; set; }
    public MaxBetLimit max_bet_limit { get; set; }
}

[Serializable]
public class Wagers
{
    public MainBets main_bets { get; set; }
    public SideBets side_bets { get; set; }
    public OpBets op_bets { get; set; }
}

// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
[Serializable]
public class Bet
{
    public string betOption { get; set; }
    public string username { get; set; }
    public string betId { get; set; }
    public string userId { get; set; }
    public string level { get; set; }
    public int betIndex { get; set; }
    public string sessionId { get; set; }
    public double amount { get; set; }
    public string betType { get; set; }

    public int newAmount { get; set; }
    public double delta { get; set; }
    public double oldAmount { get; set; }

}

public class Richest
{
    public string username { get; set; }
    public double balance { get; set; }
    public int rank { get; set; }
}

public class Payout
{
    public double win { get; set; }
    public double balance { get; set; }
    public string username { get; set; }
    public string userId { get; set; }
}

public class Winner
{
    public string username { get; set; }
    public double totalWins { get; set; }
    public int rank { get; set; }
}

// ── Bet History Data Models ───────────────────────────────────────────────────

[System.Serializable]
public class HistoryBetEntry
{
    public string bet_id { get; set; }
    public string bet_type { get; set; }
    public string bet_option { get; set; }
    public double bet_amount { get; set; }
    public double win_amount { get; set; }
}

[System.Serializable]
public class HistoryRound
{
    public string round_id { get; set; }
    public double bet_amount { get; set; }
    public double win_amount { get; set; }
    public string level { get; set; }
    public string result_card { get; set; }
    public string result_suit { get; set; }
    public string created_at { get; set; }
    public List<HistoryBetEntry> bets { get; set; }
}

[Serializable]
public class HistoryMeta
{
    public int total { get; set; }
    public int page { get; set; }
    public int limit { get; set; }
    public int pages { get; set; }
}

[Serializable]
public class HistoryPayload
{
    public List<HistoryRound> history { get; set; }
    public HistoryMeta meta { get; set; }
}

[Serializable]
public class HistoryRoot
{
    public bool success { get; set; }
    public HistoryPayload payload { get; set; }
}

[Serializable]
public class Casual
{
    public int min_bet_limit { get; set; }
    public int max_bet_limit { get; set; }
}

[Serializable]
public class Expert
{
    public int min_bet_limit { get; set; }
    public int max_bet_limit { get; set; }
}

[Serializable]
public class HighRoller
{
    public int min_bet_limit { get; set; }
    public int max_bet_limit { get; set; }
}

[Serializable]
public class LevelBetLimit
{
    public Casual casual { get; set; }
    public Novice novice { get; set; }
    public Expert expert { get; set; }
    public HighRoller high_roller { get; set; }
}

[Serializable]
public class Novice
{
    public int min_bet_limit { get; set; }
    public int max_bet_limit { get; set; }
}