using System;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class ServerBrowserEntry : MonoBehaviour {
    [SerializeField] private TextMeshProUGUI serverName;
    [SerializeField] private TextMeshProUGUI playerCount;
    [SerializeField] private TextMeshProUGUI mode;
    [SerializeField] private TextMeshProUGUI ping;
    private Button button;
    private Lobby serverData;

    private void Awake() {
        button = GetComponent<Button>();
        button.onClick.AddListener(delegate { ServerBrowserManager.instance.JoinServerById(serverData.Id); });
    }

    public void SetData(Lobby serverDataValue, bool isLobbyStatus = false) {
        serverName.text = serverDataValue.Name;
        playerCount.text = serverDataValue.Players.Count.ToString();
        mode.text = "Jenga";
        ping.text = isLobbyStatus ? serverDataValue.LobbyCode : "?ms";
        serverData = serverDataValue;
        if (isLobbyStatus) {
            button.onClick.RemoveAllListeners();
        }
    }
}