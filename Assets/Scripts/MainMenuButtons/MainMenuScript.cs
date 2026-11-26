using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuScript : MonoBehaviour
{
    public void PlayGame()
    {
        ResetRun();
        SceneManager.LoadScene("ProceduralLevel");
    }

    // The managers survive scene loads, so a new run would keep the last run's level, XP, skills and stats.
    // Removing them lets the level scene create fresh ones, exactly like the first start of the game.
    private void ResetRun()
    {
        Time.timeScale = 1f;

        if (GameManager.Instance != null)
        {
            Destroy(GameManager.Instance.gameObject);
            GameManager.Instance = null;
        }

        if (StatsManager.Instance != null)
        {
            Destroy(StatsManager.Instance.gameObject);
            StatsManager.Instance = null;
        }

        if (DifficultyManager.Instance != null)
        {
            Destroy(DifficultyManager.Instance.gameObject);
            DifficultyManager.Instance = null;
        }
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