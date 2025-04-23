using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class ServerBrowserManager : MonoBehaviour {
    public static ServerBrowserManager instance { get; private set; }
    public Lobby currentLobby { get; private set;}
    public string relayJoinCode { get; private set;}
    private float heartbeatTimer;
    private float lobbyPollTimer;

    private void Awake() {
        if (instance != null) {
            Debug.LogError("[ServerBrowserManager] There is already an instance of this singleton");
            return;
        }
        instance = this;
    }

    public async Task Initialize() {
        if (UnityServices.Instance.State != ServicesInitializationState.Uninitialized) {
            return;
        }
        
        var initOptions = new InitializationOptions();
        initOptions.SetProfile(ClientGameManager.instance.playerName);
        
        await UnityServices.InitializeAsync(initOptions);

        AuthenticationService.Instance.SignedIn += () => {
            Debug.Log("Signed in anonymously: " + AuthenticationService.Instance.PlayerId);
        };
        
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    private void Update() {
        if (currentLobby != null) {
            HeartbeatPing();
            UpdateLobby();
        }
    }

    private async void HeartbeatPing() {
        if (!isHost) { return; }
        heartbeatTimer -= Time.deltaTime;
        if (heartbeatTimer < 0.0f) {
            heartbeatTimer = 15.0f;
            try {
                await LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
            }
            catch (LobbyServiceException e) {
                Debug.LogException(e);
            }
        }
    }

    private async void UpdateLobby() {
        lobbyPollTimer -= Time.deltaTime;
        if (lobbyPollTimer < 0.0f) {
            lobbyPollTimer = 1.1f;
            try {
                currentLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
                ClientUI.instance.UpdateLobby(currentLobby);

                if (!isHost && !ClientGameManager.instance.inGame) {
                    var relayJoinCode = currentLobby.Data["RelayJoinCode"].Value;
                    if (relayJoinCode != "") {
                        JoinGame(relayJoinCode);
                    }
                }
                
                //breaks the game if the host leaves
                if (!IsPlayerInLobby()) {
                    Debug.Log("Kicked from Lobby!");
                    if (NetworkManager.Singleton) {
                        NetworkManager.Singleton.Shutdown();
                    }
                    else {
                        currentLobby = null;
                    }
                    ClientUI.instance.GoToServerBrowser();
                }
            }
            catch (LobbyServiceException e) {
                currentLobby = null;
                Debug.LogException(e);
            }
        }
    }

    public async Task CreateServer() {
        try {
            var lobbyOptions = new CreateLobbyOptions(){
                Player = GetSelfPlayer(),
                Data = new() {
                    { "RelayJoinCode" , new DataObject(DataObject.VisibilityOptions.Public, "")},
                    { "GameStarted" , new DataObject(DataObject.VisibilityOptions.Public, "false")}
                }
            };
            currentLobby = await LobbyService.Instance.CreateLobbyAsync(ClientGameManager.instance.playerName, 10, lobbyOptions);
            Debug.Log("Created Lobby.");
            ClientUI.instance.GoToLobby(currentLobby);
        }
        catch (LobbyServiceException e) {
            Debug.LogException(e);
        }
    }

    public async Task JoinServerByCode(string lobbyCode) {
        try {
            var joinOptions = new JoinLobbyByCodeOptions(){
                Player = GetSelfPlayer()
            };
            currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, joinOptions);
            Debug.Log("Joined Lobby " + lobbyCode + ".");
            ClientUI.instance.GoToLobby(currentLobby);
        }
        catch (LobbyServiceException e) {
            Debug.LogException(e);
            throw;
        }
    }
    
    public async Task JoinServerById(string id) {
        try {
            var joinOptions = new JoinLobbyByIdOptions(){
                Player = GetSelfPlayer()
            };
            currentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(id, joinOptions);
            Debug.Log("Joined Lobby " + id + ".");
            ClientUI.instance.GoToLobby(currentLobby);
        }
        catch (LobbyServiceException e) {
            Debug.LogException(e);
            throw;
        }
    }

    public async Task LeaveServer(bool closeServer = false) {
        try {
            if (closeServer) {
                await LobbyService.Instance.DeleteLobbyAsync(currentLobby.Id);
            }
            else {
                await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id, AuthenticationService.Instance.PlayerId);
            }
            currentLobby = null;
            Debug.Log("Left Lobby.");
        }
        catch (LobbyServiceException e) {
            Debug.LogException(e);
            throw;
        }
    }

    public async void KickPlayer(string playerId) {
        try {
            if (playerId == AuthenticationService.Instance.PlayerId) {
                Debug.Log("Cannot kick self");
                return;
            }

            if (!isHost) {
                return;
            }
            
            await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id, playerId);
            Debug.Log("Kicked Player " + playerId + ".");
        }
        catch (LobbyServiceException e) {
            Debug.LogException(e);
            throw;
        }
    }
    
    public async void PopulateServerList() {
        try {
            var response = await LobbyService.Instance.QueryLobbiesAsync();
            ClientUI.instance.PopulateServerList(response.Results);
        }
        catch (LobbyServiceException e) {
            Debug.LogException(e);
        }
    }

    public static Unity.Services.Lobbies.Models.Player GetSelfPlayer() {
        return new () {
            Data = new () { {
                    "playerName",
                    new PlayerDataObject(
                        PlayerDataObject.VisibilityOptions.Public,
                        ClientGameManager.instance.playerName
                    )
                }
            }
        };
    }

    public bool isHost => currentLobby != null && currentLobby.HostId == AuthenticationService.Instance.PlayerId;

    public async void StartGame() {
        try {
            Debug.Log("Starting Game...");

            currentLobby = await LobbyService.Instance.UpdateLobbyAsync(currentLobby.Id, new UpdateLobbyOptions() {
                Data = new() {
                    { "StartedGame", new DataObject(DataObject.VisibilityOptions.Public, "true") }
                }
            });

            ClientGameManager.instance.StartGame();
        }
        catch (LobbyServiceException e) {
            Debug.LogException(e);
            throw;
        }
    }

    private void JoinGame(string relayJoinCodeVal) {
        Debug.Log("Joining Game...");
        if (string.IsNullOrEmpty(relayJoinCodeVal)) {
            Debug.LogError("Invalid Relay Code...");
            return;
        }

        relayJoinCode = relayJoinCodeVal;
        ClientGameManager.instance.StartGame();
    }
    
    public async void SetRelayJoinCode(string relayJoinCode) {
        try {
            Debug.Log("Relay join code set to " + relayJoinCode);

            currentLobby = await LobbyService.Instance.UpdateLobbyAsync(currentLobby.Id, new UpdateLobbyOptions {
                Data = new Dictionary<string, DataObject> {
                    { "RelayJoinCode", new DataObject(DataObject.VisibilityOptions.Public, relayJoinCode) }
                }
            });
        } catch (LobbyServiceException e) {
            Debug.Log(e);
        }
    }
    
    private bool IsPlayerInLobby() {
        if (currentLobby != null && currentLobby.Players != null) {
            foreach (var player in currentLobby.Players) {
                if (player.Id == AuthenticationService.Instance.PlayerId) {
                    return true;
                }
            }
        }
        return false;
    }
}