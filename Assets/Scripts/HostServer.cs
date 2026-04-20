using Unity.Netcode;
using UnityEngine;

public class HostServer : MonoBehaviour
{
    public void Host()
    {
        NetworkManager.Singleton.StartHost();
    }
}
