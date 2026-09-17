using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

public class SkillSlot : MonoBehaviour
{
    public List<SkillSlot> prerequisiteSkillSlots;
    public SkillSO skillSO;
    public string skillID;


    public int currentLevel;
    public bool isUnlocked;
    public Image skillIcon;
    public Button skillButton;
    public TMP_Text skillLevelText;

    public static event Action<SkillSlot> OnAbilityPointSpent;
    public static event Action<SkillSlot> OnSkillMaxed;


    /// <summary>
    /// /////////////
    /// </summary>
    void Start()
    {
        string skillName = skillSO.skillID;

        //if (GameManager.Instance.skillLevels.ContainsKey(skillName))
        //{
        //    currentLevel = GameManager.Instance.skillLevels[skillName];
        //    isUnlocked = true;
        //}
        if (GameManager.Instance.skillLevels.ContainsKey(skillID))
        {
            currentLevel = GameManager.Instance.skillLevels[skillID];
            isUnlocked = true;
            
        }

        CheckIfUnlock();
        UpdateUI();
    }

    void Update()
    {
        CheckIfUnlock();
        UpdateUI();
    }



    private void OnValidate()
    {
        if (skillSO != null && skillLevelText != null)
        {
            UpdateUI();
        }


    }

    private void UpdateUI()
    {
        skillIcon.sprite = skillSO.skillIcon;

        if (isUnlocked)
        {
            skillButton.interactable = true;
            skillLevelText.text = currentLevel.ToString() + "/" + skillSO.maxLevel.ToString();
            skillIcon.color = Color.white;
        }
        else
        {
            skillButton.interactable = false;
            skillLevelText.text = "Locked";
            skillIcon.color = Color.grey;
        }
    }

    public bool CanUnlockSkill()
    {
        foreach (SkillSlot slot in prerequisiteSkillSlots)
        {
            if (!slot.isUnlocked || slot.currentLevel < slot.skillSO.maxLevel)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>

    /// </summary>
    public void CheckIfUnlock()
    {
        foreach (SkillSlot slot in prerequisiteSkillSlots)
        {
            if (prerequisiteSkillSlots == null || prerequisiteSkillSlots.Count == 0)
            {
                Unlock();
            }


            if (slot.currentLevel == slot.skillSO.maxLevel && slot.isUnlocked)
            {

                Unlock();
            }
        }
    }


    public void Unlock()
    {
        isUnlocked = true;
        UpdateUI();
    }


    //public void TryUpgradeSkill()
    //{
    //    if (isUnlocked && currentLevel < skillSO.maxLevel)
    //    {
    //        currentLevel++;
    //        OnAbilityPointSpent?.Invoke(this);
    //
    //        if (currentLevel >= skillSO.maxLevel)
    //        {
    //            OnSkillMaxed?.Invoke(this);
    //        }
    //        UpdateUI();
    //    }
    //}
    public void TryUpgradeSkill()
    {
        if (isUnlocked && currentLevel < skillSO.maxLevel)
        {
            currentLevel++;

            // ?? SAVE LEVEL
            GameManager.Instance.skillLevels[skillID] = currentLevel;

            OnAbilityPointSpent?.Invoke(this);

            if (currentLevel >= skillSO.maxLevel)
            {
                OnSkillMaxed?.Invoke(this);
            }

            UpdateUI();
        }
    }
}