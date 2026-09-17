using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.SceneManagement;

public class ExpManager : MonoBehaviour
{

    
    private int xP = 0;
    private int xPToLevel = 10;
    private float xPMultiplier = 1.2f;

    [SerializeField] private int firstLevelSceneIndex = 5;

    private int flag = 0;

    public Slider slider;
    public TMP_Text levelText;

    public static event Action<int> OnLevelUp;

    void Start()
    {
        UpdateUI();
    }


    private void OnEnable()
    {
        EnemyHealth.OnMonsterDefeated += GainXP;
    }

    private void OnDisable()
    {
        EnemyHealth.OnMonsterDefeated -= GainXP;
    }

    public void GainXP(int amount)
    {
        xP += amount;
        if(xP >= xPToLevel)
        {
            LevelUp();
        }
        UpdateUI();
    }

    private void LevelUp()
    {
        GameManager.Instance.UpdateLevel(1);

        xP -= xPToLevel;
        xPToLevel = Mathf.RoundToInt(xPMultiplier + xPToLevel);

        OnLevelUp?.Invoke(1);

        if (GameManager.Instance.level < 18)
        {
            if (GameManager.Instance.level % 3 == 0)
            {
                LoadNextScene();
            }
        }

        if(GameManager.Instance.level == 20)
        {
            SceneManager.LoadScene("EndScene");
        }
    }

    private void UpdateUI()
    {
        slider.maxValue = xPToLevel;
        slider.value = xP;
        levelText.text = "Level: " + GameManager.Instance.level;
    }

    public void LoadNextScene()
    {
        flag = 0;

        int mapIndex = ((GameManager.Instance.level - 1) / 3) + +firstLevelSceneIndex;

        if (mapIndex >= SceneManager.sceneCountInBuildSettings)
        {
            mapIndex = SceneManager.sceneCountInBuildSettings - 1;
        }

        SceneManager.LoadScene(mapIndex);
    }
}

