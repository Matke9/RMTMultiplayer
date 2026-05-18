using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class RespawnHandler : NetworkBehaviour
{
    [SerializeField] private NetworkObject playerPrefab;
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
        Destroy(player.gameObject);

        StartCoroutine(RespawnPlayer(player.OwnerClientId));

    }

    private IEnumerator RespawnPlayer(ulong ownerClientId)
    {
        yield return null;

        NetworkObject playerInstance = Instantiate(playerPrefab, SpawnPoint.GetRandomSpawnPos(), Quaternion.identity);

        playerInstance.SpawnAsPlayerObject(ownerClientId);
    }
}
