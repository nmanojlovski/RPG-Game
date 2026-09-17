using UnityEngine;
using UnityEngine.InputSystem;

public class SkillUI : MonoBehaviour
{

    public CanvasGroup canvasGroup;


    public void OpenSkillUI(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            if (canvasGroup.alpha == 1 && Time.timeScale == 0)
            {
                canvasGroup.alpha = 0;
                Time.timeScale = 1;
            }
            else if(canvasGroup.alpha == 0 && Time.timeScale == 1)
            {
                canvasGroup.alpha = 1;
                Time.timeScale = 0;
            }

        }
    }
}
