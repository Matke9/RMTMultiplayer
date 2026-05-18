using System;
using Unity.Cinemachine;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor.U2D.Aseprite;
using UnityEngine;

public class TankPlayer : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private CinemachineCamera followCamera;
    [field:SerializeField] public Health Health {get; private set;}

    [Header("Settings")]
    public NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>();
    
    public static event Action<TankPlayer> OnPlayerSpawned;
    public static event Action<TankPlayer> OnPlayerDespawned;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            //Deo iz respawninga
            OnPlayerSpawned?.Invoke(this);
        }


        if (IsOwner)
        {
            followCamera.Priority = 100;
            followCamera.Prioritize();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            OnPlayerDespawned?.Invoke(this);
        }
    }
}
