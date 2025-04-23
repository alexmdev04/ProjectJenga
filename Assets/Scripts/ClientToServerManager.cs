using System;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

public class ClientToServerManager : NetworkBehaviour
{
    public static ClientToServerManager instance { get; private set; }

    private void Awake() {
        if (instance != null) {
            Debug.LogError("[ClientToServerManager] There is already an instance of this singleton");
            return;
        }
        instance = this;
        Debug.Log("[ClientToServerManager] Initialised");
    }

    private void Update() {
        if (Keyboard.current.f6Key.wasPressedThisFrame) {
            SpawnTestCubeRpc();
        }
        if (!IsServer) { return; }
        UpdateHeldObjects();
    }

    [Rpc(SendTo.Server)]
    public void SpawnTestCubeRpc() {
        Instantiate(ClientGameManager.instance.testCube, new Vector3(0.0f, 4.0f, 0.0f), Quaternion.identity)
            .GetComponent<NetworkObject>().Spawn();
    }

    // These could be combined into a single Interact method with a enum InteractType param
    [Rpc(SendTo.Server)]
    public void PlayerPickupObjectRpc(ulong playerClientId, NetworkBehaviourReference netPhyObjRef) {
        var playerClient = NetworkManager.ConnectedClients[playerClientId];
        var player = playerClient.PlayerObject.GetComponent<Player>();
        
        if (!netPhyObjRef.TryGet(out NetworkPhysicsObject netPhyObj)) {
            Debug.LogError("[ClientToServerManager.PlayerPickupObjectRpc] Object is not a NetworkPhysicsObject");
            return;
        }
        
        netPhyObj.SetIsHeld(true);
        player.heldObject.Value = netPhyObjRef;
    }

    [Rpc(SendTo.Server)]
    public void PlayerDropObjectRpc(ulong playerClientId) {
        var playerClient = NetworkManager.ConnectedClients[playerClientId];
        var player = playerClient.PlayerObject.GetComponent<Player>();

        if (!player.heldObject.Value.TryGet(out NetworkPhysicsObject netPhyObj)) {
            return;
        }
        
        netPhyObj.SetIsHeld(false);
        player.heldObject.Value = new NetworkBehaviourReference();
    }
    
    // This could be optimised by storing a dictionary of the client IDs to player components
    // Then have the player cameras be network transforms to streamline the position setting
    public void UpdateHeldObjects() {
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList) {
            var player = client.PlayerObject.GetComponent<Player>();
            
            if (!player.heldObject.Value.TryGet(out NetworkPhysicsObject netPhyObj)) {
                continue;
            }

            var playerCamPosition = player.transform.position + new Vector3(0.0f, 0.825f, 0.0f);
            var playerCamRotation = player.playerEulerAngles.Value;
            var camForward = Quaternion.Euler(new Vector3(playerCamRotation.x, playerCamRotation.y, 0.0f)) * Vector3.forward;
            
            netPhyObj.transform.position = Vector3.Lerp(
                netPhyObj.transform.position,
                playerCamPosition + camForward * Math.Clamp(player.heldObjectDistance.Value, 1.0f, 15.0f), // distance
                10.0f * Time.deltaTime // lerp speed
            );
        }
    }
}
