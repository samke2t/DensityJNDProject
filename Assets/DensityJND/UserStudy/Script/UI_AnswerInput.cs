using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_AnswerInput : MonoBehaviour
{
    private const string QuestionCopy =
        "Is the left cluster denser or less dense than the right?\n左边这个团的密度比右边更大还是更小？";
    private const string GreaterCopy = "Greater\n更大";
    private const string SmallerCopy = "Smaller\n更小";
    private const string BilingualFontResource = "Fonts/NotoSansSC-AnswerTMP";
    private const float TrainingAnswerButtonY = -45f;
    private const float FormalAnswerButtonY = 55f;
    private const float TrainingSubmitY = -130f;
    private const float FormalSubmitY = -210f;
    private static TMP_FontAsset bilingualFontAsset;

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
    [SerializeField] private TMP_Text questionText;        // 中英文密度判断问题


    [Header("Answer Buttons")]
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;
    [SerializeField] private Button submitButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button trainingAgainButton; 
    [SerializeField] private Button startFormalButton;

    #endregion ====================================================


    #region =============== UI Input & Update ===============

    private int currentAnswer = 0; // 0=None, 1=Greater, 2=Smaller
    private ColorBlock leftDefaultColors;
    private ColorBlock rightDefaultColors;
    private TMP_Text leftLabelText;
    private TMP_Text rightLabelText;

    #endregion ====================================================


    #region =============== Answer UI Synchronization ===============

    // StudyManager 的 AnswerUIReflash 调用的功能
    public void RefreshTrialInformation()
    {
        currentAnswer = 0;
        ApplyAnswerCopy();

        if (messageText != null) messageText.text = "";
        
        // 注：给用户看都是从 1 开始数

        if (phaseText != null) phaseText.text = studyManager.CurrentPhaseLabel;
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
        UpdateAnswerButtonPositions();
        UpdateSubmitButtonPosition();

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

        CacheAnswerCopyReferences();
        ApplyAnswerCopy();
    }

    private void OnEnable()
    {
        if (questionText == null || leftLabelText == null || rightLabelText == null)
        {
            CacheAnswerCopyReferences();
        }

        ApplyAnswerCopy();
    }

    private void CacheAnswerCopyReferences()
    {
        Transform searchRoot = transform.parent != null ? transform.parent : transform;
        foreach (TMP_Text candidate in searchRoot.GetComponentsInChildren<TMP_Text>(true))
        {
            if (questionText == null && candidate.name == "Question")
            {
                questionText = candidate;
            }
        }

        leftLabelText = leftButton != null ? leftButton.GetComponentInChildren<TMP_Text>(true) : null;
        rightLabelText = rightButton != null ? rightButton.GetComponentInChildren<TMP_Text>(true) : null;
    }

    private void ApplyAnswerCopy()
    {
        TMP_FontAsset font = GetBilingualFontAsset();
        ApplyFont(questionText, font);
        ApplyFont(leftLabelText, font);
        ApplyFont(rightLabelText, font);

        if (questionText != null)
        {
            questionText.text = QuestionCopy;
            questionText.fontSize = 32f;
            questionText.rectTransform.sizeDelta = new Vector2(960f, 110f);
            questionText.ForceMeshUpdate();
        }
        if (leftLabelText != null)
        {
            leftLabelText.text = GreaterCopy;
            leftLabelText.fontSize = 30f;
            leftLabelText.ForceMeshUpdate();
        }
        if (rightLabelText != null)
        {
            rightLabelText.text = SmallerCopy;
            rightLabelText.fontSize = 30f;
            rightLabelText.ForceMeshUpdate();
        }
    }

    private static void ApplyFont(TMP_Text target, TMP_FontAsset font)
    {
        if (target == null || font == null)
        {
            return;
        }

        target.font = font;
        target.fontSharedMaterial = font.material;
    }

    private static TMP_FontAsset GetBilingualFontAsset()
    {
        if (bilingualFontAsset != null)
        {
            return bilingualFontAsset;
        }

        bilingualFontAsset = Resources.Load<TMP_FontAsset>(BilingualFontResource);
        if (bilingualFontAsset == null)
        {
            Debug.LogWarning("[DensityJND] The persistent bilingual Answer UI TMP font could not be loaded.");
            return null;
        }
        return bilingualFontAsset;
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

    private void LateUpdate()
    {
        TMP_FontAsset font = GetBilingualFontAsset();
        if (font == null)
        {
            return;
        }

        if ((questionText != null && questionText.font != font) ||
            (leftLabelText != null && leftLabelText.font != font) ||
            (rightLabelText != null && rightLabelText.font != font))
        {
            ApplyAnswerCopy();
        }
    }

    private void SetAnswerButtonsInteractable(bool interactable)
    {
        if (leftButton != null) leftButton.interactable = interactable;
        if (rightButton != null) rightButton.interactable = interactable;
    }

    private void UpdateSubmitButtonPosition()
    {
        if (submitButton == null || studyManager == null) return;

        RectTransform rect = submitButton.GetComponent<RectTransform>();
        if (rect == null) return;

        Vector2 position = rect.anchoredPosition;
        position.y = studyManager.currentPhase == StudyManager.StudyPhase.Formal
            ? FormalSubmitY
            : TrainingSubmitY;
        rect.anchoredPosition = position;
    }

    private void UpdateAnswerButtonPositions()
    {
        if (studyManager == null) return;

        float y = studyManager.currentPhase == StudyManager.StudyPhase.Training
            ? TrainingAnswerButtonY
            : FormalAnswerButtonY;
        SetButtonY(leftButton, y);
        SetButtonY(rightButton, y);
    }

    private static void SetButtonY(Button button, float y)
    {
        if (button == null) return;

        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect == null) return;

        Vector2 position = rect.anchoredPosition;
        position.y = y;
        rect.anchoredPosition = position;
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
