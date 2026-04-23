using System.Threading.Tasks;
using UnityEngine;

public class ClientSingleton : MonoBehaviour
{
    static ClientSingleton instance;

    private ClientGameManager gameManager;

    public static ClientSingleton Instance
    {
        get
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindObjectOfType<ClientSingleton>();
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

    public async Task CreateClient()
    {
        gameManager = new ClientGameManager();
        
        await gameManager.InitAsync();
    }
    
    
}
