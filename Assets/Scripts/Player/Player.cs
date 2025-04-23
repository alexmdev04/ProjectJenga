using System;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.Serialization;

public class Player : NetworkBehaviour {
    public bool 
        frictionType;
    public float
        // groundedAccelerate = 15f,
        // airAccelerate = 15f,
        // maxVelocityGrounded = 6.5f,
        // maxVelocityAir = 6.5f,
        acceleration = 15f;

    [Range(0.01f, 1f)] public float friction1 = 0.7f;
    [Range(1f, 5f)] public float friction2 = 1f;
    
    [Space] public bool moveFixedUpdate;
    public bool 
        lookActive = true,
        moveActive = true;
    public Rigidbody rb { get; private set; }

    [SerializeField, Range(0f, 0.99f)] private float 
        friction = 0.85f;
    [SerializeField, Range(0f, 0.1f)] private float 
        forceToApplyFriction = 0.1f,
        flatvelMin = 0.1f;
    [SerializeField] private float
        walkSpeed = 4f,
        sprintSpeed = 6.5f,
        cameraHeight = 0.825f,
        movementAcceleration = 0.1f,
        movementDecceleration = 0.05f,
        jumpForce = 5f,
        playerHeight = 180f, // in cm
        playerCrouchHeight = 100f, // in cm
        groundedRayDistance = 1f,
        movementRampTime;
    [SerializeField] private GameObject body;
    [SerializeField] private Camera cam;
    
    [Header("Interaction")]
    [SerializeField] private float interactDistance = 100.0f;
    [SerializeField] private LayerMask interactLayerMask = int.MaxValue;
    
    // Net Vars
    public NetworkVariable<NetworkBehaviourReference> heldObject = 
        new (new NetworkBehaviourReference(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<Vector2> playerEulerAngles = 
        new (Vector2.zero, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Owner);
    public NetworkVariable<float> heldObjectDistance = 
        new (6.0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    
    private bool grounded => MathF.Round(rb.linearVelocity.y, 3) == 0;

    private void Awake() {
        rb = GetComponent<Rigidbody>();
    }

    private void Start() {
        if (!IsOwner) { return; }
        ClientInputHandler.instance.OnJump += Jump;
        ClientInputHandler.instance.OnInteract += Interact;
        cam = Camera.main;
        cam!.transform.SetParent(body.transform);
        cam.transform.localPosition = new Vector3(0.0f, cameraHeight, 0.0f);
    }

    private void OnDisable() {
        if (!IsOwner) { return; }
        ClientInputHandler.instance.OnJump -= Jump;
        ClientInputHandler.instance.OnInteract -= Interact;
    }

    private void Update() {
        if (!IsOwner) { return; }
        if (moveActive && !moveFixedUpdate) { Move(); }
        heldObjectDistance.Value = Math.Clamp(heldObjectDistance.Value + ClientInputHandler.instance.scrollDelta.y, 1.0f, 15.0f);
        Crouch();
    }
    
    private void FixedUpdate() {
        if (!IsOwner) { return; }
        if (moveActive && moveFixedUpdate) { Move(); }
    }
    
    private void LateUpdate() {
        if (!IsOwner) { return; }
        if (lookActive) { Look(); }
    }

    private void Look() {
        Vector2 mouseDeltaMult = ClientInputHandler.instance.mouseDelta * ClientInputHandler.instance.sensitivity;

        playerEulerAngles.Value = new Vector2(
            Math.Clamp(
                playerEulerAngles.Value.x - mouseDeltaMult.y,
                -90f,
                90f
            ),
            playerEulerAngles.Value.y + mouseDeltaMult.x
        );

        body.transform.localEulerAngles = new Vector3(0f, playerEulerAngles.Value.y, 0f);
        cam.transform.localEulerAngles = new Vector3(playerEulerAngles.Value.x, 0f, 0f);
    }
    
    private void Move() {
        float maxVelocity = ClientInputHandler.instance.sprinting ? sprintSpeed : walkSpeed;
        Vector3 movementDirectionGlobal = body.transform.TransformDirection(ClientInputHandler.instance.movementDirection);
        
        if (grounded) {
            // apply friction
            float speed = rb.linearVelocity.magnitude;

            if (speed <= flatvelMin) {
                rb.linearVelocity = Vector3.zero;
            }

            if (frictionType) {
                if (speed > 0) // Scale the velocity based on friction.
                {
                    rb.linearVelocity *= (speed - (speed * friction2 * Time.fixedDeltaTime)) / speed;
                }

            }
            else {
                rb.linearVelocity *= friction1;
            }
        }
        else {
            //maxVelocity = maxVelocityAir;
            //acceleration = airAccelerate;
        }

        float projVel = Vector3.Dot(rb.linearVelocity, movementDirectionGlobal); // Vector projection of Current velocity onto accelDir.
        float accelVel = acceleration * Time.fixedDeltaTime; // Accelerated velocity in direction of movment

        // If necessary, truncate the accelerated velocity so the vector projection does not exceed max_velocity
        if (projVel + accelVel > maxVelocity) {
            accelVel = maxVelocity - projVel;
        }

        rb.linearVelocity += movementDirectionGlobal * accelVel;
    }

    private void Jump() {
        if (!grounded) { return; }
        rb.AddForce(jumpForce * Vector3.up, ForceMode.VelocityChange);
    }
    
    private void Crouch() {
        body.transform.localScale = 
            new Vector3(
                body.transform.localScale.x,
                (ClientInputHandler.instance.crouched ? playerCrouchHeight : playerHeight) / 200,
                body.transform.localScale.z
            );
    }
    
    private void Interact() {
        if (heldObject.Value.TryGet(out _)) {
            ClientToServerManager.instance.PlayerDropObjectRpc(OwnerClientId);
            return;
        }
        
        // this would probably be moved to server side otherwise cheaters could pickup anything from anywhere
        if (!Physics.Raycast(
            new Ray(cam.transform.position, cam.transform.forward),
            out var hit,
            interactDistance, 
            interactLayerMask)) {
            return;
        }

        if (!hit.transform.TryGetComponent(out NetworkPhysicsObject netPhyObj)) {
            Debug.LogError("[Player] Interacted object is not a NetworkPhysicsObject");
            return;
        }
        
        ClientToServerManager.instance.PlayerPickupObjectRpc(
            OwnerClientId,
            new NetworkBehaviourReference(netPhyObj)
        );
    }
}