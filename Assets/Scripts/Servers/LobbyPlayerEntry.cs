using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyPlayerEntry : MonoBehaviour {
    [SerializeField] private TextMeshProUGUI playerName;
    [SerializeField] private Button kickButton;
    private string playerId;

    public void SetData(string playerName, string playerId, bool enableKickButton) {
        this.playerName.text = playerName;
        this.playerId = playerId;
        kickButton.gameObject.SetActive(enableKickButton);
        kickButton.onClick.RemoveAllListeners();
        kickButton.onClick.AddListener(delegate { ServerBrowserManager.instance.KickPlayer(playerId); } );
    }
}