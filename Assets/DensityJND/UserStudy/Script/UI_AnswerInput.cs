using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_AnswerInput : MonoBehaviour
{
    #region =============== Inspector Configuration ===============

    [Header("Study Manager")]
    [SerializeField] private StudyManager studyManager;
    [SerializeField] private StudyUIController uiController;


    [Header("Information Text")]
    [SerializeField] private TMP_Text phaseText;           // 只显示 Training，Formal，Redo
    [SerializeField] private TMP_Text blockText;           // 多 Block 实验时显示当前 Block
    [SerializeField] private TMP_Text trialText;           // 这个 Block 的第几个 trial
    [SerializeField] private TMP_Text countdownText;       // 这个 Trial 的倒计时
    [SerializeField] private TMP_Text messageText;         // 提示文本


    [Header("Answer Buttons")]
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;
    [SerializeField] private Button submitButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button trainingAgainButton; 
    [SerializeField] private Button startFormalButton;

    #endregion ====================================================


    #region =============== UI Input & Update ===============

    private int currentAnswer = 0; // 0=None, 1=Left, 2=Right
    private ColorBlock leftDefaultColors;
    private ColorBlock rightDefaultColors;

    #endregion ====================================================


    #region =============== Answer UI Synchronization ===============

    // StudyManager 的 AnswerUIReflash 调用的功能
    public void RefreshTrialInformation()
    {
        currentAnswer = 0;

        if (messageText != null) messageText.text = "";
        
        // 注：给用户看都是从 1 开始数

        if (phaseText != null) phaseText.text = studyManager.currentPhase.ToString();
        if (blockText != null)
        {
            blockText.gameObject.SetActive(studyManager.BlockCount > 1);
            blockText.text = "Block " + (studyManager.currentBlock + 1);
        }
        if (trialText != null)
        {
            int trial = studyManager.currentPhase == StudyManager.StudyPhase.Training
                ? studyManager.currentTrainingTrial
                : studyManager.currentFormalTrial;
            trialText.text = "Trial " + (trial + 1);
        }

        ResetAnswerColors();
        SetAnswerButtonsInteractable(true);
        if (submitButton != null) submitButton.interactable = true;

        if (studyManager.currentPhase == StudyManager.StudyPhase.Training)
        {
            SetButtonState(nextButton, true, false);
            SetButtonState(trainingAgainButton, true, false);
            SetButtonState(startFormalButton, true, true);
        }
        else
        {
            SetButtonState(nextButton, false, false);
            SetButtonState(trainingAgainButton, false, false);
            SetButtonState(startFormalButton, false, false);
        }

        uiController?.ShowAnswering();
    }


    public void ShowCorrect(int correctId)
    {
        ApplyResultColor(leftButton, 1, correctId);
        ApplyResultColor(rightButton, 2, correctId);
    }
    
    
    
    #endregion ====================================================


    #region =============== Uinity Lifecycle ======================

    private void Awake()
    {
        if (studyManager == null)
        {
            // TODO：根据最后的UI group 的设置需求修改获取的方式
            studyManager = this.transform.parent.GetComponentInChildren<StudyManager>();
        }

        if (uiController == null)
        {
            uiController = transform.root.GetComponentInChildren<StudyUIController>(true);
        }

        if (leftButton != null) leftDefaultColors = leftButton.colors;
        if (rightButton != null) rightDefaultColors = rightButton.colors;
    }
    
    
    private void Update()
    {
        if (studyManager == null)
        {
            if (countdownText != null) countdownText.text = "";
            return;
        }

        if (studyManager.trialStartTime < 0f)
        {
            if (countdownText != null) countdownText.text = "";
            return;
        }

        float remainingTime = studyManager.stimulusVisibleSeconds - (Time.time - studyManager.trialStartTime);

        if (remainingTime > 0f)
        {
            if (countdownText != null) countdownText.text = Mathf.CeilToInt(remainingTime).ToString();
        }
        else
        {
            if (countdownText != null) countdownText.text = "";
        }
    }
    
    

    #endregion ====================================================


    #region =============== Answer Input ===============

    public void OnLeftClicked()
    {
        currentAnswer = 1;
        UpdateSelectionColors();
    }


    public void OnRightClicked()
    {
        currentAnswer = 2;
        UpdateSelectionColors();
    }


    public void OnSubmitClicked()
    {
        if (currentAnswer == 0)
        {
            if (messageText != null) messageText.text = "Please select an answer.";
            return;
        }

        if (submitButton != null) submitButton.interactable = false;
        SetAnswerButtonsInteractable(false);

        studyManager.OnTrialSubmit(currentAnswer);

        if (studyManager.currentPhase == StudyManager.StudyPhase.Training)
        {
            bool isLastTrial = studyManager.IsLastTrainingTrial;
            SetButtonState(nextButton, true, !isLastTrial);
            SetButtonState(trainingAgainButton, true, isLastTrial);
            SetButtonState(startFormalButton, true, true);
        }
    }

    public void OnNextClicked()
    {
        SetButtonState(nextButton, nextButton != null && nextButton.gameObject.activeSelf, false);
        studyManager.NextTrial();
    }

    public void OnTrainingAgainClicked()
    {
        SetButtonState(trainingAgainButton, true, false);
        SetButtonState(startFormalButton, true, true);
        studyManager.RestartTraining();
    }

    public void OnStartFormalClicked()
    {
        SetButtonState(trainingAgainButton, true, false);
        SetButtonState(startFormalButton, true, false);
        studyManager.FinishTraining();
    }

    private void SetAnswerButtonsInteractable(bool interactable)
    {
        if (leftButton != null) leftButton.interactable = interactable;
        if (rightButton != null) rightButton.interactable = interactable;
    }

    private static void SetButtonState(Button button, bool visible, bool interactable)
    {
        if (button == null) return;
        button.gameObject.SetActive(visible);
        button.interactable = interactable;
    }

    private void ResetAnswerColors()
    {
        if (leftButton != null) leftButton.colors = leftDefaultColors;
        if (rightButton != null) rightButton.colors = rightDefaultColors;
    }

    private void UpdateSelectionColors()
    {
        ResetAnswerColors();
        Button selected = currentAnswer == 1 ? leftButton : rightButton;
        if (selected == null) return;

        ColorBlock colors = selected.colors;
        colors.normalColor = new Color(0.071f, 0.412f, 0.788f, 1f);
        colors.selectedColor = colors.normalColor;
        selected.colors = colors;
        if (messageText != null) messageText.text = "";
    }

    private void ApplyResultColor(Button button, int answerId, int correctId)
    {
        if (button == null) return;
        ColorBlock colors = button.colors;
        if (answerId == correctId)
        {
            colors.normalColor = new Color(0.220f, 0.647f, 0.408f, 1f);
            colors.disabledColor = colors.normalColor;
        }
        else if (answerId == currentAnswer)
        {
            colors.normalColor = new Color(0.878f, 0.337f, 0.380f, 1f);
            colors.disabledColor = colors.normalColor;
        }
        button.colors = colors;
    }

    #endregion ====================================================



}
