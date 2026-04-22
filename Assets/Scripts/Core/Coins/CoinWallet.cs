using System;
using Unity.Netcode;
using UnityEngine;

public class CoinWallet : NetworkBehaviour
{
    public NetworkVariable<int> totalCoins = new();

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
}
