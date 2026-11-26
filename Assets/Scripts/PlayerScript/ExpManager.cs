using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.SceneManagement;

public class ExpManager : MonoBehaviour
{


    private float xPMultiplier = 1.2f;

    [SerializeField] private string proceduralLevelScene = "ProceduralLevel";

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
        GameManager gm = GameManager.Instance;
        gm.xp += amount;
        if(gm.xp >= gm.xpToLevel)
        {
            LevelUp();
        }
        UpdateUI();
    }

    private void LevelUp()
    {
        GameManager gm = GameManager.Instance;
        gm.UpdateLevel(1);

        gm.xp -= gm.xpToLevel;
        gm.xpToLevel = Mathf.RoundToInt(xPMultiplier + gm.xpToLevel);

        OnLevelUp?.Invoke(1);

        if (GameManager.Instance.level >= 20)
        {
            SceneManager.LoadScene("EndScene");
            return;
        }

        ProceduralMapGenerator generator = ProceduralMapGenerator.Instance;
        if (generator != null && generator.MapForLevel(GameManager.Instance.level) != generator.MapNumber)
        {
            LoadNextScene();
        }
    }

    private void UpdateUI()
    {
        slider.maxValue = GameManager.Instance.xpToLevel;
        slider.value = GameManager.Instance.xp;
        levelText.text = "Level: " + GameManager.Instance.level;
    }

    public void LoadNextScene()
    {
        flag = 0;

        // Reloading the procedural scene generates a fresh, bigger map for the new level.
        SceneManager.LoadScene(proceduralLevelScene);
    }
}

