using UnityEngine;
using UnityEngine.InputSystem;

public class SkillUI : MonoBehaviour
{

    public CanvasGroup canvasGroup;


    void Start()
    {
        SetVisible(canvasGroup.alpha == 1);
    }

    public void OpenSkillUI(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            if (canvasGroup.alpha == 1 && Time.timeScale == 0)
            {
                SetVisible(false);
                Time.timeScale = 1;
            }
            else if(canvasGroup.alpha == 0 && Time.timeScale == 1)
            {
                SetVisible(true);
                Time.timeScale = 0;
            }

        }
    }

    // A hidden panel must not keep blocking clicks (this canvas sorts above the X / return button canvas)
    private void SetVisible(bool visible)
    {
        canvasGroup.alpha = visible ? 1 : 0;
        canvasGroup.blocksRaycasts = visible;
        canvasGroup.interactable = visible;
    }
}
