using System;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(NetworkRigidbody))]
//[RequireComponent(typeof(Rigidbody))]

public class NetworkPhysicsObject : NetworkBehaviour {
    public NetworkVariable<bool> isHeld = new (false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    [HideInInspector] public NetworkTransform networkTransform;
    [HideInInspector] public NetworkRigidbody networkRigidbody;
    //[HideInInspector] public Rigidbody rb;

    private void Awake() {
        networkTransform = GetComponent<NetworkTransform>();
        networkRigidbody = GetComponent<NetworkRigidbody>();
        //rb = GetComponent<Rigidbody>();
    }

    private void Start() {
        if (!IsOwner) { return; }
        
    }

    private void Update() {
        if (!IsOwner) { return; }
        
    }

    public void ToggleHeld() {
        SetIsHeld(!isHeld.Value);
    }

    public void SetIsHeld(bool state) {
        isHeld.Value = state;
        networkRigidbody.Rigidbody.constraints = state ? RigidbodyConstraints.FreezeAll : RigidbodyConstraints.None;
        networkRigidbody.Rigidbody.isKinematic = state;
        networkRigidbody.Rigidbody.detectCollisions = !state;
        
        // rb.constraints = state ? RigidbodyConstraints.FreezeAll : RigidbodyConstraints.None;
        // rb.isKinematic = state;
        // rb.detectCollisions = !state;
    }
}