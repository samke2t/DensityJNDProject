using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_StartInput : MonoBehaviour
{
    #region =============== Inspector Configuration ===============

    [Header("Study Manager")]
    [SerializeField] private StudyManager studyManager;


    [Header("Participant Input")]
    [SerializeField] private TMP_InputField participantIDInput;


    [Header("Specific Trial Input")]
    [SerializeField] private TMP_InputField blockIDInput;
    [SerializeField] private TMP_InputField trialIDInput;
    
    // 用于选择阶段，可改, GPT 建议给这两个Toggle 设置同一个 ToggleGroup，实现二选一
    [SerializeField] private Toggle trainingToggle;
    [SerializeField] private Toggle formalToggle;
    


    [Header("Message Text")]
    [SerializeField] private TMP_Text messageText;

    #endregion ====================================================


    #region =============== Unity Lifecycle =======================

    private void Awake()
    {
        if (studyManager == null)
        {
            // TODO：根据最终的对象层级调整获取方式
            studyManager = transform.parent.GetComponentInChildren<StudyManager>();
        }

        messageText.text = "";
        
    }

    #endregion ====================================================


    #region =============== Start Input ===========================

    // Start 按钮调用：正常开始整个实验
    public void OnStartStudyClicked()
    {
        if (!int.TryParse(participantIDInput.text, out int participantID))
        {
            messageText.text = "Please enter a valid participant ID.";
            return;
        }

        messageText.text = "";

        studyManager.StartStudy(participantID);
    }


    // Start Trial 按钮调用：从指定 Trial 开始
    public void OnStartTrialClicked()
    {
        if (!int.TryParse(participantIDInput.text, out int participantID_Input))
        {
            messageText.text = "Please enter a valid participant ID.";
            return;
        }

        if (!int.TryParse(blockIDInput.text, out int blockID_Input))
        {
            messageText.text = "Please enter a valid block ID.";
            return;
        }

        if (!int.TryParse(trialIDInput.text, out int trialID_Input))
        {
            messageText.text = "Please enter a valid trial ID.";
            return;
        }

        StudyManager.StudyPhase phase;

        if (trainingToggle.isOn)
        {
            phase = StudyManager.StudyPhase.Training;

        }
        else
        {
            phase = StudyManager.StudyPhase.Formal;
        }

        messageText.text = "";
        
       

        // TODO：输入的时候可以按照 1 作为起始点输入，但数据存储按照 0 开始
        studyManager.StartTrial(participantID_Input, blockID_Input-1, trialID_Input-1, phase);
    }

    #endregion ====================================================
}