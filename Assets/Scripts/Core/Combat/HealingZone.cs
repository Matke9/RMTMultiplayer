using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;

public class HealingZone : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Image healPowerBar;

    [Header("Settings")]
    [SerializeField] private int maxHealPower = 3;
    [SerializeField] private float healCooldown = 30f;
    [SerializeField] private float healTickRate = 1f;
    [SerializeField] private int coinsPerTickCost = 10;
    [SerializeField] private int healthPerTick = 1;

    private float remainingCooldown;
    private float tickTimer;

    private List<TankPlayer> playersInZone = new List<TankPlayer>();

    private NetworkVariable<int> HealPower = new NetworkVariable<int>();

    public override void OnNetworkSpawn()
    {
        if (IsClient)
        {
            HealPower.OnValueChanged += HandleHealPowerChanged;
            HandleHealPowerChanged(0, HealPower.Value);
        }

        if (IsServer)
        {
            HealPower.Value = maxHealPower;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsClient)
        {
            HealPower.OnValueChanged -= HandleHealPowerChanged;
        }
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if(!IsServer){return;}
        if(!collision.attachedRigidbody.TryGetComponent<TankPlayer>(out TankPlayer player)) {return;}
        playersInZone.Add(player);
    }

    void OnTriggerExit2D(Collider2D collision)
    {
        if(!IsServer){return;}
        if(!collision.attachedRigidbody.TryGetComponent<TankPlayer>(out TankPlayer player)) {return;}
        playersInZone.Remove(player);
    }

    private void Update()
    {
        if(!IsServer) {return;}

        if(remainingCooldown > 0f)
        {
            remainingCooldown -= Time.deltaTime;
            if(remainingCooldown <= 0)
            {
                HealPower.Value = maxHealPower;
            }
            else
            {
                return;
            }
        }

        tickTimer += Time.deltaTime;
        if(tickTimer >= 1/healTickRate)
        {
            foreach(TankPlayer player in playersInZone)
            {
                if(HealPower.Value == 0) {break;}

                if(player.Health.CurrentHealth.Value == player.Health.MaxHealth) {continue;}

                if(player.Wallet.totalCoins.Value < coinsPerTickCost) {continue;}

                player.Wallet.SpendCoins(coinsPerTickCost);
                player.Health.RestoreHealth(healthPerTick);

                HealPower.Value -= 1;

                if(HealPower.Value == 0)
                {
                    remainingCooldown = healCooldown;
                }
            }

            tickTimer = tickTimer % (1/healTickRate);
        }
    }

    private void HandleHealPowerChanged(int oldHealPower, int newHealPower)
    {
        healPowerBar.fillAmount = (float)newHealPower / maxHealPower;
    }
}
