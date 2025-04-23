using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class ClientUI : MonoBehaviour {
    public static ClientUI instance { get; private set; }
    public static MenuState currentMenuState { get; private set; }
    [SerializeField] private GameObject mainMenuRoot;
    [SerializeField] private GameObject multiplayerMenuRoot;
    [SerializeField] private GameObject hudRoot;
    [SerializeField] private GameObject pauseMenuRoot;
    [SerializeField] private GameObject serverBrowserRoot;
    [SerializeField] private GameObject lobbyRoot;
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_InputField codeInput;
    [SerializeField] private TMP_InputField sensInput;
    [SerializeField] private GameObject serverBrowserEntry;
    [SerializeField] private GameObject serverBrowserBox;
    [SerializeField] private GameObject lobbyEntry;
    [SerializeField] private GameObject lobbyBox;
    [SerializeField] private ServerBrowserEntry lobbyServerData;
    [SerializeField] private Button lobbyStartButton;
    private string previousSensInput;
    private string lobbyCode;

    private void Awake() {
        if (instance != null) {
            Debug.LogError("[ClientUI] There is already an instance of this singleton");
            return;
        }
        instance = this;
        SetMenuState(MenuState.mainMenu);
        nameInput.onValueChanged.AddListener(SetCurrentPlayerName);
        codeInput.onValueChanged.AddListener(SetCurrentLobbyCode);
        sensInput.onValueChanged.AddListener(SetSensitivity);
        sensInput.onEndEdit.AddListener(SetSensitivity);
    }

    private void Start() {
        ClientInputHandler.instance.OnPause += paused => { if (!paused) { SetSensitivity(sensInput.text); } };
    }

    // Name input field
    public void SetCurrentPlayerName(string input) {
        ClientGameManager.instance.SetPlayerName(input);
        //ApplyPlayerName();
    }
    
    // Name button (useless but looks more cohesive)
    // public void ApplyPlayerName() {
    //     Debug.Log($"[ClientUI] Set player name to { playerName } ");
    //     // set the clients name
    // } // todo
    
    // Host game button
    // public void StartAsHost() {
    //     ClientGameManager.instance.StartNetwork(ClientGameManager.NetworkLoadMode.asHost, targetMenuState:MenuState.inGame);
    // }
    
    // Join input field
    public void SetCurrentLobbyCode(string input) {
        lobbyCode = input;
        Debug.Log($"[ClientUI] Set lobby code to { lobbyCode } ");
    } // todo

    // Join button
    // public void StartAsClient() {
    //     ClientGameManager.instance.StartNetwork(ClientGameManager.NetworkLoadMode.asClient, targetMenuState: MenuState.inGame);
    // }

    // Main menu play button
    public void StartMultiplayer() {
        SetMenuState(MenuState.multiplayerMenu);
    }

    public void Quit() {
        ClientGameManager.instance.QuitToDesktop();
    }
    
    public void SetCursorVisible(bool state) {
        Cursor.lockState = state ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = state;
    }

    public enum MenuState {
        mainMenu,
        multiplayerMenu,
        inGame,
        paused,
        serverBrowser,
        lobby
    }
    
    public void SetMenuState(MenuState state) {
        switch (state) {
            case MenuState.mainMenu: {
                mainMenuRoot.SetActive(true);
                multiplayerMenuRoot.SetActive(false);
                hudRoot.SetActive(false);
                pauseMenuRoot.SetActive(false);
                serverBrowserRoot.SetActive(false);
                lobbyRoot.SetActive(false);
                SetCursorVisible(true);
                break;
            }
            case MenuState.multiplayerMenu: {
                mainMenuRoot.SetActive(false);
                multiplayerMenuRoot.SetActive(true);
                hudRoot.SetActive(false);
                pauseMenuRoot.SetActive(false);
                serverBrowserRoot.SetActive(false);
                lobbyRoot.SetActive(false);
                SetCursorVisible(true);
                break;
            }
            case MenuState.inGame: {
                mainMenuRoot.SetActive(false);
                multiplayerMenuRoot.SetActive(false);
                hudRoot.SetActive(true);
                pauseMenuRoot.SetActive(false);
                serverBrowserRoot.SetActive(false);
                lobbyRoot.SetActive(false);
                SetCursorVisible(false);
                break;
            }
            case MenuState.paused: {
                mainMenuRoot.SetActive(false);
                multiplayerMenuRoot.SetActive(false);
                hudRoot.SetActive(false);
                pauseMenuRoot.SetActive(true);
                serverBrowserRoot.SetActive(false);
                lobbyRoot.SetActive(false);
                sensInput.text = ClientInputHandler.instance.sensitivity.ToString();
                SetCursorVisible(true);
                break;
            }
            case MenuState.serverBrowser: {
                mainMenuRoot.SetActive(false);
                multiplayerMenuRoot.SetActive(false);
                hudRoot.SetActive(false);
                pauseMenuRoot.SetActive(false);
                serverBrowserRoot.SetActive(true);
                lobbyRoot.SetActive(false);
                SetCursorVisible(true);
                break;
            }
            case MenuState.lobby: {
                mainMenuRoot.SetActive(false);
                multiplayerMenuRoot.SetActive(false);
                hudRoot.SetActive(false);
                pauseMenuRoot.SetActive(false);
                serverBrowserRoot.SetActive(false);
                lobbyRoot.SetActive(true);
                SetCursorVisible(true);
                break;
            }
            default: {
                throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        currentMenuState = state;
    }

    public void Disconnect() {
        ClientGameManager.instance.Disconnect();
    }
    
    public void SetSensitivity(string value) {
        if (value == string.Empty) {
            previousSensInput = string.Empty;
            return;
        }
        
        if (float.TryParse(value, out float sens)) {
            ClientInputHandler.instance.sensitivity = Math.Clamp(sens, 0.0f, float.MaxValue);
            previousSensInput = value;
            return;
        }
        
        sensInput.text = previousSensInput;
    }

    public void GoToMultiplayerMenu() {
        SetMenuState(MenuState.multiplayerMenu);
    }

    public void GoToServerBrowser() {
        GoToServerBrowserAsync();
    }

    private async void GoToServerBrowserAsync() {
        await ServerBrowserManager.instance.Initialize();
        SetMenuState(MenuState.serverBrowser);
        RefreshServers();
    }
    
    public void RefreshServers() {
        ServerBrowserManager.instance.PopulateServerList();
    }

    public void PopulateServerList(List<Lobby> servers) {
        foreach (Transform child in serverBrowserBox.transform) {
            Destroy(child.gameObject);
        }
        
        foreach (var server in servers) {
            var entry = Instantiate(serverBrowserEntry, serverBrowserBox.transform).GetComponent<ServerBrowserEntry>();
            entry.SetData(server);
        }
    }

    public void PopulateLobbyPlayerList(List<Unity.Services.Lobbies.Models.Player> players) {
        foreach (Transform child in lobbyBox.transform) {
            Destroy(child.gameObject);
        }
        
        bool isHost = ServerBrowserManager.instance.isHost;
        foreach (var player in players) {
            var entry = Instantiate(lobbyEntry, lobbyBox.transform).GetComponent<LobbyPlayerEntry>();
            entry.SetData(
                player.Data["playerName"].Value,
                player.Id,
                isHost && (player.Id != AuthenticationService.Instance.PlayerId)
            );
        }
    }
    
    public void CreateServer() {
        CreateServerAsync();
    }

    private async void CreateServerAsync() {
        await ServerBrowserManager.instance.CreateServer();
    }

    public void JoinServer() {
        JoinServerAsync();
    }

    private async void JoinServerAsync() {
        await ServerBrowserManager.instance.Initialize();
        await ServerBrowserManager.instance.JoinServerByCode(lobbyCode);
    }

    public void LeaveServer() {
        LeaveServerAsync();
    }

    private async void LeaveServerAsync() {
        await ServerBrowserManager.instance.LeaveServer();
        SetMenuState(MenuState.multiplayerMenu);
    }

    public void UpdateLobby(Lobby lobby) {
        lobbyServerData.SetData(lobby, true);
        PopulateLobbyPlayerList(lobby.Players);
    }

    public void GoToLobby(Lobby lobby) {
        SetMenuState(MenuState.lobby);
        lobbyStartButton.gameObject.SetActive(ServerBrowserManager.instance.isHost);
        UpdateLobby(lobby);
        if (bool.TryParse(lobby.Data["GameStarted"].Value, out bool gameStarted)) {
            if (gameStarted) {
                StartGame();
            }
        }
    }

    public void StartGame() {
        ServerBrowserManager.instance.StartGame();
    }
}