using System;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Relay;
using UnityEngine;

public class GameHUD : NetworkBehaviour
{
    private static FixedString32Bytes lobbyCode;
    
    [SerializeField] private TMP_Text lobbyCodeText;
    
    
    public static void SetLobbyCode(FixedString32Bytes code)
    {
        lobbyCode = code;
    }
    
    public void UpdateLobbyCodeUI()
    {
        lobbyCodeText.text = "Lobby code: " + lobbyCode.Value;
    }
    
    private void Awake()
    {
        UpdateLobbyCodeUI();
    }

    public void LeaveGame()
    {
        if (NetworkManager.Singleton.IsHost)
        {
            HostSingleton.Instance.GameManager.Shutdown();
        } 
        ClientSingleton.Instance.GameManager.Disconnect();
    }
}
