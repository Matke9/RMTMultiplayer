using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkClient : IDisposable
{
    private NetworkManager networkManager;
    
    private const string MenuSceneName = "Menu";
    
    public NetworkClient(NetworkManager networkManager)
    {
        this.networkManager = networkManager;
        
        networkManager.OnClientDisconnectCallback += OnClientDisconnect;
    }
    
    private void OnClientDisconnect(ulong clientId)
    {
        if(clientId != 0 && clientId != networkManager.LocalClientId) {return;}

        Disconnect();
    }

    public void Dispose()
    {
        if (networkManager != null)
        {
            networkManager.OnClientDisconnectCallback -= OnClientDisconnect;
        }
    }

    public void Disconnect()
    {
        if (SceneManager.GetActiveScene().name != MenuSceneName)
        {
            SceneManager.LoadScene(MenuSceneName);
        }

        if (networkManager.IsConnectedClient)
        {
            networkManager.Shutdown();
        }
    }
}
