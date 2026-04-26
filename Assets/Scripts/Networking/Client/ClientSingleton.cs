using System.Threading.Tasks;
using UnityEngine;

public class ClientSingleton : MonoBehaviour
{
    static ClientSingleton instance;

    public ClientGameManager GameManager {get; private set;}

    public static ClientSingleton Instance
    {
        get
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindFirstObjectByType<ClientSingleton>();
            if (instance == null)
            {
                Debug.Log("no client singleton instance found");
                return null;
            }

            return instance;
        }
    }

    void Start()
    {
        DontDestroyOnLoad(gameObject);
    }

    public async Task<bool> CreateClient()
    {
        GameManager = new ClientGameManager();
        
        return await GameManager.InitAsync();
    }
    
    
}
