using System.Threading.Tasks;
using UnityEngine;

public class HostSingleton : MonoBehaviour
{
    static HostSingleton instance;

    private HostGameManager gameManager;

    public static HostSingleton Instance
    {
        get
        {
            if (instance != null)
            {
                return instance;
            }

            //instance = FindObjectOfType<HostSingleton>();
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
        gameManager = new HostGameManager();
        
    }
    
    
}
