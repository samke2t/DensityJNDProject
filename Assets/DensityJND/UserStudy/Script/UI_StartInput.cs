using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_StartInput : MonoBehaviour
{
    #region =============== Inspector Configuration ===============

    [Header("Study Manager")]
    [SerializeField] private StudyManager studyManager;
    [SerializeField] private StudyUIController uiController;


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
    [SerializeField] private Button startStudyButton;
    [SerializeField] private Button startTrialButton;

    #endregion ====================================================


    #region =============== Unity Lifecycle =======================

    private void Awake()
    {
        if (studyManager == null)
        {
            // TODO：根据最终的对象层级调整获取方式
            studyManager = transform.parent.GetComponentInChildren<StudyManager>();
        }

        if (uiController == null)
        {
            uiController = transform.root.GetComponentInChildren<StudyUIController>(true);
        }

        ShowMessage("");
        
    }

    #endregion ====================================================


    #region =============== Start Input ===========================

    // Start 按钮调用：正常开始整个实验
    public void OnStartStudyClicked()
    {
        if (studyManager == null)
        {
            ShowMessage("Study Manager is not available.");
            return;
        }

        if (participantIDInput == null || !int.TryParse(participantIDInput.text, out int participantID) || participantID < 0)
        {
            ShowMessage("Please enter a valid participant ID (0 or greater).");
            return;
        }

        ShowMessage("");
        uiController?.BeginLoading();

        studyManager.StartStudy(participantID);
    }


    // Start Trial 按钮调用：从指定 Trial 开始
    public void OnStartTrialClicked()
    {
        if (studyManager == null)
        {
            ShowMessage("Study Manager is not available.");
            return;
        }

        if (participantIDInput == null || !int.TryParse(participantIDInput.text, out int participantID_Input) || participantID_Input < 0)
        {
            ShowMessage("Please enter a valid participant ID (0 or greater).");
            return;
        }

        if (blockIDInput == null || !int.TryParse(blockIDInput.text, out int blockID_Input) || blockID_Input < 1)
        {
            ShowMessage("Block number must start at 1.");
            return;
        }

        if (trialIDInput == null || !int.TryParse(trialIDInput.text, out int trialID_Input) || trialID_Input < 1)
        {
            ShowMessage("Trial number must start at 1.");
            return;
        }

        StudyManager.StudyPhase phase;

        if (trainingToggle != null && trainingToggle.isOn)
        {
            phase = StudyManager.StudyPhase.Training;
        }
        else if (formalToggle != null && formalToggle.isOn)
        {
            phase = StudyManager.StudyPhase.Formal;
        }
        else
        {
            ShowMessage("Please select Training or Formal.");
            return;
        }

        ShowMessage("");
        uiController?.BeginLoading();
        
       

        studyManager.StartTrial(participantID_Input, blockID_Input-1, trialID_Input-1, phase);
    }

    public void SetInteractable(bool interactable)
    {
        if (participantIDInput != null) participantIDInput.interactable = interactable;
        if (blockIDInput != null) blockIDInput.interactable = interactable;
        if (trialIDInput != null) trialIDInput.interactable = interactable;
        if (trainingToggle != null) trainingToggle.interactable = interactable;
        if (formalToggle != null) formalToggle.interactable = interactable;
        if (startStudyButton != null) startStudyButton.interactable = interactable;
        if (startTrialButton != null) startTrialButton.interactable = interactable;
    }

    public void ShowMessage(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }
    }

    #endregion ====================================================
}
