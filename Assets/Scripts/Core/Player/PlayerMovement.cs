using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{
    InputAction moveAction;
    Vector2 previousMoveInput;
    [Header("References")]
    [SerializeField] Transform bodyTransform;
    [SerializeField] Rigidbody2D rb;
    [Header("Settings")]
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] float turnRate = 30f;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) { return; }
        moveAction = InputSystem.actions.FindAction("Move");
    }
    
    public override void OnNetworkDespawn()
    {
        if (!IsOwner) { return; }
    }

    void Update()
    {
        if (!IsOwner) { return; }
        
        HandleMoveInput();
        float zRotation = previousMoveInput.x * -turnRate * Time.deltaTime;
        bodyTransform.Rotate(0,0,zRotation);
    }

    private void FixedUpdate()
    {
        if (!IsOwner) { return; }
        
        rb.linearVelocity = (Vector2)bodyTransform.up * previousMoveInput.y * moveSpeed;
    }

    void HandleMoveInput()
    {
        previousMoveInput = moveAction.ReadValue<Vector2>();
    }
}
