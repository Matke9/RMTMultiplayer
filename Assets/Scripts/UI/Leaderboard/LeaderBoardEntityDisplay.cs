using System;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class LeaderBoardEntityDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text displayText;
    [SerializeField] private Color myColor;

    private FixedString32Bytes playerName;
    public ulong ClientId{get; private set;}
    public int Coins{get; private set;}

    public void Initialse(ulong clientId, FixedString32Bytes name, int coins)
    {
        ClientId = clientId;
        this.playerName = name;

        if(clientId == NetworkManager.Singleton.LocalClientId)
        {
            displayText.color = myColor;
        }

        UpdateCoins(coins);
    }

    public void UpdateCoins(int coins)
    {
        Coins = coins;
        UpdateText();
    }

    public void UpdateText()
    {
        displayText.text = $"{transform.GetSiblingIndex() + 1}. {playerName} ({Coins})";
    }
}
