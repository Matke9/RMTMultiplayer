using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class HostGameManager : IDisposable
{
    private Allocation allocation;
    private string joinCode;
    private string lobbyId;
    
    public GameNetworkServer NetworkServer {get; private set;}
    
    private const int MaxConnections = 20;
    private const string GameSceneName = "Game";
    
    public async Task StartHostAsync()
    {
        try
        {
            allocation = await RelayService.Instance.CreateAllocationAsync(MaxConnections);
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
            return;
        }
        try
        {
            joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log($"Join Code: {joinCode}");
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
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

        try
        {
            //ovo isto sve radi samo sam CreateLobbyOptions konstruktor stavio unutar argumenata funkcije CreateLobbyAsync, nista spec
            //takodje sve ono sto je njemu Relay i Lobbies, nama je RelayService i LobbyService, promenjena je sintaksa malo u novijim verzijama
            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(
                PlayerPrefs.GetString(NameSelector.PlayerNameKey, "??") + "'s Lobby", 
                MaxConnections, new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>
                {
                    {"JoinCode", new DataObject(DataObject.VisibilityOptions.Member, joinCode)}
                }
            });
            lobbyId = lobby.Id;

            HostSingleton.Instance.StartCoroutine(HeartbeatLobby(15));
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
            return;
        }

        NetworkServer = new GameNetworkServer(NetworkManager.Singleton);
        
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
        NetworkManager.Singleton.StartHost();

        NetworkServer.OnClientLeft += HandleClientLeft;
        
        NetworkManager.Singleton.SceneManager.LoadScene(GameSceneName, LoadSceneMode.Single);
    }

    private IEnumerator HeartbeatLobby(float waitTimeSeconds)
    {
        WaitForSecondsRealtime delay = new WaitForSecondsRealtime(waitTimeSeconds);
        while (true)
        {
            LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
            yield return delay;
        }
    }

    public void Dispose()
    {
        Shutdown();
    }

    public async void Shutdown()
    {
        Debug.Log(HostSingleton.Instance + " Shutdown");
        
        HostSingleton.Instance.StopCoroutine(nameof (HeartbeatLobby));
        if (!string.IsNullOrEmpty(lobbyId))
        {
            try
            {
                await LobbyService.Instance.DeleteLobbyAsync(lobbyId);
            }
            catch (LobbyServiceException e)
            {
                Debug.Log(e);
            }

            lobbyId = string.Empty;
        }

        NetworkServer.OnClientLeft -= HandleClientLeft;

        NetworkServer?.Dispose();
    }

    private async void HandleClientLeft(string authId)
    {
        try
        {
            await LobbyService.Instance.RemovePlayerAsync(lobbyId, authId);
        }
        catch(LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }
}
