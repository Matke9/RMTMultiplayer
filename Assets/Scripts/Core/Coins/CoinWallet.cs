using System;
using Unity.Netcode;
using UnityEngine;

public class CoinWallet : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Health health;
    [SerializeField] private BountyCoin coinPrefab;
    
    [Header("Settings")]
    [SerializeField] private float coinSpread = 3f;
    [SerializeField] private float bountyPercentage = 50f;
    [SerializeField] private int bountyCoinCount = 10;
    [SerializeField] private int minBountyCoinValue = 5;
    [SerializeField] private LayerMask layerMask;

    private Collider2D[] coinBuffer = new Collider2D[1];
    private float coinRadius;
    public NetworkVariable<int> totalCoins = new();

    public override void OnNetworkSpawn()
    {
        if(!IsServer){return;}

        coinRadius = coinPrefab.GetComponent<CircleCollider2D>().radius;

        health.OnDie += HandleDie;
    }

    public override void OnNetworkDespawn()
    {
        if(!IsServer) {return;}

        health.OnDie -= HandleDie;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(!other.TryGetComponent<Coin>(out Coin coin)) {return;}
        
        int value = coin.Collect();
        
        if (!IsServer) { return; }
        
        totalCoins.Value += value;
    }
    
    public void SpendCoins(int amount)
    {
        if (!IsServer) { return; }
        
        if (totalCoins.Value >= amount)
        {
            totalCoins.Value -= amount;
        }
    }

    private void HandleDie(Health health)
    {
        int bountyValue =(int) (totalCoins.Value*(bountyPercentage /100f));
        int bountyCoinValue = bountyValue / bountyCoinCount;

        if(bountyCoinValue < minBountyCoinValue) {return;}

        for(int i = 0; i < bountyCoinCount; i++)
        {
            BountyCoin coinInstance = Instantiate(coinPrefab, getSpawnPoint(), Quaternion.identity);
            coinInstance.setValue(bountyCoinCount);
            coinInstance.NetworkObject.Spawn();
        }
    }

    private Vector2 getSpawnPoint()
    {
        while (true)
        {
            Vector2 spawnPoint = (Vector2) transform.position + UnityEngine.Random.insideUnitCircle * coinSpread; 
            ContactFilter2D filter = new ContactFilter2D();
            filter.layerMask = layerMask;
            //ovo je njihovo bilo deprecated pa sam promenio na noviju funkciju bez non alloc
            int numColliders = Physics2D.OverlapCircle(spawnPoint, coinRadius, filter, coinBuffer);
            if (numColliders == 0) { return spawnPoint; }
        }
    }
}
