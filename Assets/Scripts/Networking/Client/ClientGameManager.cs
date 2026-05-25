using System;
using System.Net.Http;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ClientGameManager : IDisposable
{

    private JoinAllocation allocation;
    private NetworkClient networkClient;
    
    private const string MenuSceneName = "Menu";

    public async Task<bool> InitAsync()
    {
        await UnityServices.InitializeAsync();

        networkClient = new NetworkClient(NetworkManager.Singleton);

        if (await AuthenticationWrapper.DoAuth() == AuthState.Authenticated) {return true;}

        return false;
    } 

    public void GoToMenu()
    {
        SceneManager.LoadScene(MenuSceneName);
    }

    public async Task StartClientAsync(string joinCode)
    {
        try
        {
            allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            GameHUD.SetLobbyCode(joinCode);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return;
        }
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        
#if UNITY_WEBGL && !UNITY_EDITOR
        RelayServerData relayServerData = AllocationUtils.ToRelayServerData(allocation, "wss");
        transport.UseWebSockets = true;
#else
        RelayServerData relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");
#endif
        transport.SetRelayServerData(relayServerData);

        //ovo sam ja stavio da napravi random ime ako ne uneses pravo
        string userName = PlayerPrefs.GetString(NameSelector.PlayerNameKey, "");
        if (string.IsNullOrEmpty(userName))
        {
            userName = "Player" + UnityEngine.Random.Range(0, 10000);
            PlayerPrefs.SetString(NameSelector.PlayerNameKey, userName);
        }

        UserData userData = new UserData()
        {
            userName = PlayerPrefs.GetString(NameSelector.PlayerNameKey, "??"),
            userAuthId = AuthenticationService.Instance.PlayerId
            
        };
        string payload = JsonUtility.ToJson(userData);
        byte[] payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload);
        
        NetworkManager.Singleton.NetworkConfig.ConnectionData = payloadBytes;
        
        
        NetworkManager.Singleton.StartClient();
    }

    public void Dispose()
    {
        networkClient?.Dispose();
        
    }

    public void Disconnect()
    {
        networkClient.Disconnect();
    }
}
