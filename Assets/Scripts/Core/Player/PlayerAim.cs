using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAim : NetworkBehaviour
{
    InputAction aimAction;
    
    [SerializeField] Transform turretTransform;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) {return;}
        aimAction = InputSystem.actions.FindAction("Aim");
    }

    private void LateUpdate()
    {
        if (!IsOwner) {return;}
        
        Vector2 aimInput = Camera.main.ScreenToWorldPoint(aimAction.ReadValue<Vector2>());
        turretTransform.up = aimInput-(Vector2)turretTransform.position;
    }
}
