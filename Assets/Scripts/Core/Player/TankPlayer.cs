using System;
using Unity.Cinemachine;
using Unity.Collections;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class TankPlayer : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private CinemachineCamera followCamera;
    [SerializeField] private SpriteRenderer minimapIconRenderer;
    [field:SerializeField] public Health Health {get; private set;}
    [field:SerializeField] public CoinWallet Wallet {get; private set;}

    [Header("Settings")]
    public NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>();
    [SerializeField] private Color ownerColor;
    
    public static event Action<TankPlayer> OnPlayerSpawned;
    public static event Action<TankPlayer> OnPlayerDespawned;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            UserData userData = HostSingleton.Instance.GameManager.NetworkServer.GetUserDataByClientId(OwnerClientId);

            PlayerName.Value = userData.userName;

            OnPlayerSpawned?.Invoke(this);
        }


        if (IsOwner)
        {
            followCamera.Priority = 100;
            followCamera.Prioritize();
            minimapIconRenderer.color = ownerColor;
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
