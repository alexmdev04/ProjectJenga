using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Per client manager for general Game and UI management
public class ClientGameManager : MonoBehaviour {
    public static ClientGameManager instance { get; private set; }
    [SerializeField] public GameObject ctsRoot;
    [SerializeField] public GameObject testCube;
    public bool paused { get; private set; }
    public bool inGame { get; private set; }
    public string playerName { get; private set; } = "Player";
    
    public enum NetworkLoadMode {
        asHost,
        asClient,
        asServer
    }
    
    private void Awake() {
        if (instance != null) {
            Debug.LogError("[ClientGameManager] There is already an instance of this singleton");
            return;
        }
        instance = this;
        Application.targetFrameRate = 165;
        QualitySettings.vSyncCount = 1;
    }

    private void Start() {
        
#if UNITY_EDITOR
        if (NetworkManager.Singleton) {
            Debug.LogWarning("The NetworkManager was present during startup," + 
                             " this likely means the 'Persistent Network' scene was left open in the editor." +
                             " Destroying NetworkManager...");
            Destroy(NetworkManager.Singleton.gameObject);
        }
#endif
        
        ClientInputHandler.instance.OnPause += SetPause;
    }

    private void OnDisable() {
        ClientInputHandler.instance.OnPause -= SetPause;
        PlayerPrefs.Save();
    }

    public async void Disconnect() {
        await ServerBrowserManager.instance.LeaveServer(ServerBrowserManager.instance.isHost);
        NetworkManager.Singleton.Shutdown();
    }
    
    private void OnPreShutdown() {
        SceneManager.UnloadSceneAsync("Persistent Network");
        var cam = Camera.main!;
        cam.transform.SetParent(null);
        cam.transform.position = new Vector3(0.0f, 0.825f, 0.0f);
        cam.transform.eulerAngles = new Vector3(-35.0f, 0.0f, 0.0f);
        SetPause(false);
        ClientUI.instance.SetMenuState(ClientUI.MenuState.mainMenu);
        inGame = false;
    }
    
    public void QuitToDesktop() {
        if (NetworkManager.Singleton) {
            if (NetworkManager.Singleton.IsConnectedClient) {
                Disconnect();
            }
        }
        Application.Quit();
    }

    public void SetCursorVisible(bool state) {
        Cursor.lockState = state ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = state;
    }

    public AsyncOperation StartNetwork(NetworkLoadMode mode, string joinCode = null, ClientUI.MenuState? targetMenuState = null) {
        var returnOperation = SceneManager.LoadSceneAsync("Persistent Network", LoadSceneMode.Additive)!;
        returnOperation.completed += operation => {
            StartNetworkComplete(mode, joinCode, targetMenuState);
        };
        return returnOperation;
    }

    // this actually happens when the network scene is loaded, not when the network manager has finished loading
    public async void StartNetworkComplete(NetworkLoadMode mode, string joinCode, ClientUI.MenuState? targetMenuState) {
        bool success = false;
        string logName = "";
        
        switch (mode) {
            case NetworkLoadMode.asHost: {
                await CreateRelay();
                success = NetworkManager.Singleton.StartHost();
                logName = "as Host";
                break;
            }
            case NetworkLoadMode.asClient: {
                await JoinRelay(joinCode);
                success = NetworkManager.Singleton.StartClient();
                logName = "as Client";
                break;
            }
            case NetworkLoadMode.asServer: {
                await CreateRelay();
                success = NetworkManager.Singleton.StartServer();
                logName = "as Server";
                break;
            }
            default: {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }
        
        if (success) {
            // this could be moved into an event like NetworkManager.OnInstantiated
            NetworkManager.Singleton.OnPreShutdown += OnPreShutdown;
            SceneManager.MoveGameObjectToScene(NetworkManager.Singleton.gameObject, SceneManager.GetSceneByName("Persistent Network"));
            
            Debug.Log("[ClientGameManager] Successfully started " + logName);
            
            if (mode != NetworkLoadMode.asClient) {
                // this might cause jank, its a client singleton networked object used for client to server rpc
                Instantiate(ctsRoot).GetComponent<NetworkObject>().Spawn();
            }
            
            inGame = true;
            
            if (targetMenuState.HasValue) {
                ClientUI.instance.SetMenuState(targetMenuState.Value);
            }
        }
        else {
            Debug.LogError("[ClientGameManager] Failed to start " + logName);
        }
    }
    
    public void SetPause(bool state) {
        if (!inGame) { return; }
        paused = state;
        if (state) {
            ClientUI.instance.SetMenuState(ClientUI.MenuState.paused);
            ClientInputHandler.instance.input.Player.Disable();
            ClientInputHandler.instance.input.Player.Pause.Enable();
        }
        else {
            ClientUI.instance.SetMenuState(ClientUI.MenuState.inGame);
            ClientInputHandler.instance.input.Player.Enable();
        }
    }

    public void SetPlayerName(string input) => playerName = input;

    public async Task CreateRelay() {
        try { // hard-coded check for europe servers first because the initial test was in London
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(3, "europe-west4");
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log("Allocated Relay JoinCode: " + joinCode);
            
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(allocation.ToRelayServerData("dtls"));
            ServerBrowserManager.instance.SetRelayJoinCode(joinCode);
            
        } catch (RelayServiceException e) {
            Debug.LogException(e);
        }
    }

    public async Task JoinRelay(string joinCode) {
        try {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(joinAllocation.ToRelayServerData("dtls"));
            
        } catch (RelayServiceException e) {
            Debug.LogException(e);
        }
    }

    public void StartGame() {
        if (ServerBrowserManager.instance.isHost) {
            StartNetwork(NetworkLoadMode.asHost, targetMenuState: ClientUI.MenuState.inGame);
        }
        else {
            StartNetwork(NetworkLoadMode.asClient, ServerBrowserManager.instance.relayJoinCode, ClientUI.MenuState.inGame);
        }
    }
}

// old StartNetworkComplete
// bool success = mode switch {
//     NetworkLoadMode.asHost => NetworkManager.Singleton.StartHost(),
//     NetworkLoadMode.asClient => NetworkManager.Singleton.StartClient(),
//     NetworkLoadMode.asServer => NetworkManager.Singleton.StartServer(),
//     _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
// };

// this is pointless but i was feeling extra, its camel case to sentence case
//string logName = Regex.Replace(mode.ToString(), "([a-z])([A-Z])", "$1 $2");
