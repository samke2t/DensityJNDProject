using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_AnswerInput : MonoBehaviour
{
    #region =============== Inspector Configuration ===============

    [Header("Study Manager")]
    [SerializeField] private StudyManager studyManager;


    [Header("Information Text")]
    [SerializeField] private TMP_Text phaseText;           // TODO：只显示 Training，Formal，Redo
    [SerializeField] private TMP_Text blockText;           // 第几个 Block（TODO：如果只有一个 Block 可以不显示）
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

    #endregion ====================================================


    #region =============== UI Synchronization ===============

    // StudyManager 的 AnswerUIReflash 调用的功能
    public void RefreshTrialInformation()
    {
        currentAnswer = 0;

        messageText.text = "";
        
        // TODO: 每个Trial开始时调用。
        // 1. (如果有）清空上一个Trial的选择，初始化 UI 状态。
        // 2. 更新Participant ID。
        // 4. 更新Block编号。 studyManager.blockId + 1 (
        // 5. 更新Trial编号。 根据现在的 phase 来决定显示的是 trainingTrialId + 1 还是 formalTrialId + 1
        // 6. 清空提示文字。
        // 7. 各个Button的状态管理
        
        // 注：给用户看都是从 1 开始数

        submitButton.interactable = true;
        
        if (studyManager.currentPhase == StudyManager.StudyPhase.Training)
        {
            // TODO: 只有在 Training 的时候才显示的 Button
            // 1. nextButton 
            // 2. TrainingAgainButton
            // 3. StartFormalButton
            nextButton.interactable = false;
        }
    }


    public void ShowCorrect(int correctId)
    {
        // TODO: 根据传入的正确答案的 id，对比参与者的输入，参与者对了是绿色，参与者错了则输入的那个标红，正确答案标绿
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
    }
    
    
    private void Update()
    {
        if (studyManager.trialStartTime < 0f)
        {
            countdownText.text = "";
            return;
        }

        float remainingTime = studyManager.stimulusVisibleSeconds - (Time.time - studyManager.trialStartTime);

        if (remainingTime > 0f)
        {
            countdownText.text = Mathf.CeilToInt(remainingTime).ToString();
        }
        else
        {
            countdownText.text = "";
        }
    }
    
    

    #endregion ====================================================


    #region =============== Answer Input ===============

    public void OnLeftClicked()
    {
        currentAnswer = 1;

        // TODO：更新左右按钮的选中状态。
    }


    public void OnRightClicked()
    {
        currentAnswer = 2;

        // TODO：更新左右按钮的选中状态。
    }


    public void OnSubmitClicked()
    {
        if (currentAnswer == 0)
        {
            // TODO：绑定一个 Text object， 提示 "Please select an answer."，可改
            messageText.text = "Please select an answer.";
            return;
        }

        if (studyManager.currentPhase == StudyManager.StudyPhase.Training)
        {
            submitButton.interactable = false;
            nextButton.interactable = true;
        }

        studyManager.OnTrialSubmit(currentAnswer);
    }

    // TODO: 这个 Button 只在 Training 阶段出现
    public void OnNextClicked()
    {
        studyManager.NextTrial();
    }

    public void OnTrainingAgainClicked()
    {
        studyManager.RestartTraining();
    }

    public void OnStartFormalClicked()
    {
        studyManager.FinishTraining();
    }

    #endregion ====================================================



}