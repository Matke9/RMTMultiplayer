using Unity.Netcode;
using UnityEngine;

public class CoinSpawner : NetworkBehaviour
{
    [SerializeField] private RespawningCoin coinPrefab;
    [SerializeField] private int maxCoins = 50;
    [SerializeField] private int coinValue = 10;
    [SerializeField] private Vector2 xSpawnRange;
    [SerializeField] private Vector2 ySpawnRange;
    [SerializeField] private LayerMask layerMask;

    private Collider2D[] coinBuffer = new Collider2D[1];
    private float coinRadius;
    
    public override void OnNetworkSpawn()
    {
        if (!IsServer) {return;}
        
        coinRadius = coinPrefab.GetComponent<CircleCollider2D>().radius;

        for (int i = 0; i < maxCoins; i++)
        {
            SpawnCoin();
        }
    }

    private void SpawnCoin()
    {
        RespawningCoin coinInstance = Instantiate(coinPrefab, getSpawnPoint(), Quaternion.identity);
        coinInstance.setValue(coinValue);
        coinInstance.GetComponent<NetworkObject>().Spawn();

        coinInstance.onCollected += HandleCoinCollected;
    }

    private void HandleCoinCollected(RespawningCoin coin)
    {
        coin.transform.position = getSpawnPoint();
        coin.Reset();
    }

    private Vector2 getSpawnPoint()
    {
        float x = 0;
        float y = 0;
        while (true)
        {
            x = Random.Range(xSpawnRange.x, xSpawnRange.y);
            y = Random.Range(ySpawnRange.x, ySpawnRange.y);
            Vector2 spawnPoint = new Vector2(x, y); 
            ContactFilter2D filter = new ContactFilter2D();
            filter.layerMask = layerMask;
            //ovo je njihovo bilo deprecated pa sam promenio na noviju funkciju bez non alloc
            int numColliders = Physics2D.OverlapCircle(spawnPoint, coinRadius, filter, coinBuffer);
            if (numColliders == 0) { return spawnPoint; }
        }
    }
}
