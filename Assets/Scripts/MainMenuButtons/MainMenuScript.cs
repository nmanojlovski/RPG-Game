using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuScript : MonoBehaviour
{
    public void PlayGame()
    {
        SceneManager.LoadScene("ProceduralLevel");
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void GoToLobby()
    {
        SceneManager.LoadScene("LoadingScene");
    }

}