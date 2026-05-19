using System;
using System.Threading.Tasks;
using UnityEngine;

public class HostSingleton : MonoBehaviour
{
    static HostSingleton instance;

    public HostGameManager GameManager {get; private set;}

    public static HostSingleton Instance
    {
        get
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindFirstObjectByType<HostSingleton>();
            if (instance == null)
            {
                Debug.Log("no host singleton instance found");
                return null;
            }

            return instance;
        }
    }

    void Start()
    {
        DontDestroyOnLoad(gameObject);
    }

    public void CreateHost()
    {
        GameManager = new HostGameManager();
        
    }

    private void OnDestroy()
    {
        GameManager?.Dispose();
    }
}
