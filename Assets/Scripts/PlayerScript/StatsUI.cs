using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class StatsUI : MonoBehaviour
{
    public GameObject[] statsSlots;
    public CanvasGroup canvasGroup;

    private void Start()
    {
        UpdateStats();
    }

    public void UpdateDamage()
    {
        statsSlots[0].GetComponentInChildren<TMP_Text>().text = "Damage: " + StatsManager.Instance.damage;
    }

    public void UpdateSpeed()
    {
        statsSlots[1].GetComponentInChildren<TMP_Text>().text = "Speed: " + StatsManager.Instance.speed;
    }

    public void UpdateStats()
    {
        UpdateDamage();
        UpdateSpeed();
    }

    public void OpenMenu(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            if(canvasGroup.alpha == 1 && Time.timeScale == 0)
            {
                canvasGroup.alpha = 0;
                Time.timeScale = 1;
            }
            else if(canvasGroup.alpha == 0 && Time.timeScale == 1)
            {
                canvasGroup.alpha = 1;
                Time.timeScale = 0;
                UpdateStats();
            }

        }
    }
}
