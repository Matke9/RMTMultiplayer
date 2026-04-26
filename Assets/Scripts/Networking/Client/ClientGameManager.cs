using System.Threading.Tasks;
using Unity.Services.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ClientGameManager
{

    private const string MenuSceneName = "Menu";

    public async Task<bool> InitAsync()
    {
        await UnityServices.InitializeAsync();


        if (await AuthenticationWrapper.DoAuth() == AuthState.Authenticated) {return true;}

        return false;
    } 

    public void GoToMenu()
    {
        SceneManager.LoadScene(MenuSceneName);
    }
}
