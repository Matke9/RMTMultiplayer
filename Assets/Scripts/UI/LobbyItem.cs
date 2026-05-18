using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class LobbyItem : MonoBehaviour
{
    [SerializeField] private TMP_Text lobbyNameText;
    [SerializeField] private TMP_Text lobbyPlayersText;
    
    private LobbiesList lobbiesList;
    private Lobby lobby;

    public void Initialize(LobbiesList lobbiesList, Lobby lobby)
    {
        this.lobbiesList = lobbiesList;
        this.lobby = lobby;
        
        lobbyNameText.text = lobby.Name;
        lobbyPlayersText.text = lobby.Players.Count + " / " + lobby.MaxPlayers; //ovo sam malo drugacije nebitno isti klinac samo nacin prikazivanja teksta 
    }

    public void Join()
    {
        lobbiesList.JoinAsync(lobby);
    }
    
}
