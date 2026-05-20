using Unity.Netcode;
using UnityEngine;

public class GameHUD : MonoBehaviour
{
    public void LeaveGame()
    {
        if (NetworkManager.Singleton.IsHost)
        {
            HostSingleton.Instance.GameManager.Shutdown();
        }
    }
}
