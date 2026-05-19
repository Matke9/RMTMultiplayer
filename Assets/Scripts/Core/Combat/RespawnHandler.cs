using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class RespawnHandler : NetworkBehaviour
{
    [SerializeField] private TankPlayer playerPrefab;
    [SerializeField] private float keptCoinPerecntage;
    private Dictionary<TankPlayer, Action<Health>> onDieHandlers = new();

    public override void OnNetworkSpawn()
    {
        if(!IsServer) {return;}

        TankPlayer[] players = FindObjectsByType<TankPlayer>(FindObjectsSortMode.None);
        foreach(TankPlayer player in players)
        {
            HandlePlayerSpawned(player);
        }

        TankPlayer.OnPlayerSpawned += HandlePlayerSpawned;
        TankPlayer.OnPlayerDespawned += HandlePlayerDespawned;
    }

    public override void OnNetworkDespawn()
    {
        if(!IsServer) {return;}

        TankPlayer.OnPlayerSpawned -= HandlePlayerSpawned;
        TankPlayer.OnPlayerDespawned -= HandlePlayerDespawned;
    }

    private void HandlePlayerSpawned(TankPlayer player)
    {
        Action<Health> handler = (health) => HandlePlayerDie(player);
        onDieHandlers[player] = handler;
        player.Health.OnDie += handler;
    }

    private void HandlePlayerDespawned(TankPlayer player)
    {
        if (onDieHandlers.TryGetValue(player, out Action<Health> handler))
        {
            player.Health.OnDie -= handler;
            onDieHandlers.Remove(player);
        }
    }

    private void HandlePlayerDie(TankPlayer player)
    {
        int keptCoins =(int) (player.Wallet.totalCoins.Value*(keptCoinPerecntage/100));

        Destroy(player.gameObject);

        StartCoroutine(RespawnPlayer(player.OwnerClientId, keptCoins));

    }

    private IEnumerator RespawnPlayer(ulong ownerClientId, int keptCoins)
    {
        yield return null;

        TankPlayer playerInstance = Instantiate(playerPrefab, SpawnPoint.GetRandomSpawnPos(), Quaternion.identity);
        
        playerInstance.NetworkObject.SpawnAsPlayerObject(ownerClientId);

        playerInstance.Wallet.totalCoins.Value += keptCoins;
    }
}
