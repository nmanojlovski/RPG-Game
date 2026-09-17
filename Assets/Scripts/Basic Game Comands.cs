using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class BasicGameComands : MonoBehaviour
{
    public TMP_Text healthText;


    void Update()
    {
        healthText.text = "HP: " + StatsManager.Instance.currentHealth + "/" + StatsManager.Instance.maxHealth;
    }
    
    public void ExitGame(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            Application.Quit();
        }
    }


}
