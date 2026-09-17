using UnityEngine;
using TMPro;

public class SkillTreeManager : MonoBehaviour
{
    public SkillSlot[] skillSlots;
    public TMP_Text pointsText;
    //public int availablePoints;

    private void OnEnable()
    {
        SkillSlot.OnAbilityPointSpent += HandleAbilityPointsSpent;
        SkillSlot.OnSkillMaxed += HandleSkillMaxed;
        ExpManager.OnLevelUp += UpdateAbilityPoints;
    }

    private void OnDisable()
    {
        SkillSlot.OnAbilityPointSpent -= HandleAbilityPointsSpent;
        SkillSlot.OnSkillMaxed -= HandleSkillMaxed;
        ExpManager.OnLevelUp -= UpdateAbilityPoints;
    }


    //private void Start()
    //{
    //    foreach (SkillSlot slot in skillSlots)
    //    {
    //        slot.skillButton.onClick.AddListener(() => CheckAvailablePoints(slot));
    //    }
    //    UpdateAbilityPoints(0);
    //}
    private void Start()
    {
        foreach (SkillSlot slot in skillSlots)
        {
            SkillSlot currentSlot = slot; // ?? FIX
            currentSlot.skillButton.onClick.AddListener(() => CheckAvailablePoints(currentSlot));
        }

        UpdateAbilityPoints(0);
    }

    //private void CheckAvailablePoints(SkillSlot slot)
    //{
    //    if(availablePoints > 0)
    //    {
    //        slot.TryUpgradeSkill();
    //    }
    //
    //}
    private void CheckAvailablePoints(SkillSlot slot)
    {
        if (GameManager.Instance.availablePoints > 0)
        {
            slot.TryUpgradeSkill();
        }
    }


    //private void HandleAbilityPointsSpent(SkillSlot skillSlot)
    //{
    //    if (availablePoints > 0)
    //    {
    //        UpdateAbilityPoints(-1);
    //    }
    //}
    private void HandleAbilityPointsSpent(SkillSlot skillSlot)
    {
        if (GameManager.Instance.availablePoints > 0)
        {
            UpdateAbilityPoints(-1);
        }
    }

    private void HandleSkillMaxed(SkillSlot skillSlot)
    {
        foreach(SkillSlot slot in skillSlots)
        {
            if(!slot.isUnlocked && slot.CanUnlockSkill())
            {
            slot.Unlock();
            }
        }
    }

    //public void UpdateAbilityPoints(int amount)
    //{
    //    availablePoints += amount;
    //    pointsText.text = "Points : " + availablePoints;
    //}
    public void UpdateAbilityPoints(int amount)
    {
        GameManager.Instance.UpdateAvailablePoints(amount);
        pointsText.text = "Points : " + GameManager.Instance.availablePoints;
    }
}
