using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class UI_AnswerInput : MonoBehaviour
{
    private const string QuestionCopy =
        "Is the left cluster denser or less dense than the right?\n左边这个团的密度比右边更大还是更小？";
    private const string GreaterCopy = "Greater\n更大";
    private const string SmallerCopy = "Smaller\n更小";
    private const string BilingualFontResource = "Fonts/NotoSansSC-AnswerTMP";
    private const float TrainingAnswerButtonY = 35f;
    private const float FormalAnswerButtonY = 0f;
    private const float TrainingSubmitY = -130f;
    private const float FormalSubmitY = -210f;
    private static readonly Color FormalIndicatorIdle = new Color(0.204f, 0.204f, 0.216f, 0.92f);
    private static readonly Color FormalIndicatorSelected = new Color(0.071f, 0.412f, 0.788f, 1f);
    private static readonly Color ResultCorrect = new Color(0.220f, 0.647f, 0.408f, 1f);
    private static readonly Color ResultIncorrect = new Color(0.878f, 0.337f, 0.380f, 1f);
    private static TMP_FontAsset bilingualFontAsset;

    #region =============== Inspector Configuration ===============

    [Header("Study Manager")]
    [SerializeField] private StudyManager studyManager;
    [SerializeField] private StudyUIController uiController;


    [Header("Information Text")]
    [SerializeField] private TMP_Text phaseText;           // 只显示 Training，Formal，Redo
    [SerializeField] private TMP_Text blockText;           // 多 Block 实验时显示当前 Block
    [SerializeField] private TMP_Text trialText;           // 这个 Block 的第几个 trial
    [SerializeField] private TMP_Text countdownText;       // Current trial countdown
    [SerializeField] private TMP_Text messageText;         // 提示文本
    [SerializeField] private TMP_Text questionText;        // 中英文密度判断问题


    [Header("Answer Buttons")]
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;
    [SerializeField] private Button submitButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button trainingAgainButton;
    [SerializeField] private Button startFormalButton;

    [Header("Formal HUD")]
    [SerializeField] private GameObject trainingPanel;
    [SerializeField] private GameObject formalHud;
    [SerializeField] private TMP_Text formalBlockText;
    [SerializeField] private TMP_Text formalTrialText;
    [SerializeField] private TMP_Text formalCountdownText;
    [SerializeField] private TMP_Text formalMessageText;
    [SerializeField] private Image formalLeftIndicator;
    [SerializeField] private Image formalRightIndicator;

    [Header("Training HUD")]
    [SerializeField] private GameObject trainingHud;
    [SerializeField] private TMP_Text trainingBlockText;
    [SerializeField] private TMP_Text trainingTrialText;
    [SerializeField] private TMP_Text trainingCountdownText;
    [SerializeField] private TMP_Text trainingFeedbackText;
    [SerializeField] private TMP_Text trainingMessageText;
    [SerializeField] private Image trainingLeftIndicator;
    [SerializeField] private Image trainingRightIndicator;

    [Header("Training Complete")]
    [SerializeField] private GameObject trainingReadyHud;
    [SerializeField] private Image trainingReadyNoIndicator;
    [SerializeField] private Image trainingReadyYesIndicator;
    [SerializeField] private TMP_Text trainingReadyMessageText;

    #endregion ====================================================


    #region =============== UI Input & Update ===============

    private int currentAnswer = 0; // 0=None, 1=Greater, 2=Smaller
    private ColorBlock leftDefaultColors;
    private ColorBlock rightDefaultColors;
    private TMP_Text leftLabelText;
    private TMP_Text rightLabelText;
    private UnityEngine.XR.InputDevice leftHandDevice;
    private UnityEngine.XR.InputDevice rightHandDevice;
    private bool previousLeftPrimary;
    private bool previousLeftSecondary;
    private bool previousRightPrimary;
    private bool previousRightSecondary;
    private bool previousAnyTrigger;
    private bool submitInputArmed;
    private int trainingCorrectAnswer;
    private int trainingReadyChoice;

    private enum TrainingUiState
    {
        Answering,
        Feedback,
        ReadyForFormal,
        Transitioning
    }

    private TrainingUiState trainingUiState = TrainingUiState.Answering;

    #endregion ====================================================


    #region =============== Answer UI Synchronization ===============

    // StudyManager 的 AnswerUIReflash 调用的功能
    public void RefreshTrialInformation()
    {
        currentAnswer = 0;
        ApplyAnswerCopy();

        bool showFormalHud =
            studyManager.currentPhase == StudyManager.StudyPhase.Formal ||
            studyManager.currentPhase == StudyManager.StudyPhase.Redo;
        bool showTrainingHud = studyManager.currentPhase == StudyManager.StudyPhase.Training;
        if (trainingPanel != null) trainingPanel.SetActive(!showFormalHud && !showTrainingHud);
        if (formalHud != null) formalHud.SetActive(showFormalHud);
        if (trainingHud != null) trainingHud.SetActive(showTrainingHud);
        if (trainingReadyHud != null) trainingReadyHud.SetActive(false);
        trainingUiState = TrainingUiState.Answering;
        trainingCorrectAnswer = 0;
        trainingReadyChoice = 0;
        submitInputArmed = false;

        if (messageText != null) messageText.text = "";
        if (formalMessageText != null) formalMessageText.text = "";
        if (trainingMessageText != null) trainingMessageText.text = "";
        if (trainingFeedbackText != null) trainingFeedbackText.text = "";
        if (trainingReadyMessageText != null) trainingReadyMessageText.text = "";

        // 注：给用户看都是从 1 开始数

        if (phaseText != null) phaseText.text = studyManager.CurrentPhaseLabel;
        if (blockText != null)
        {
            blockText.gameObject.SetActive(
                studyManager.currentPhase != StudyManager.StudyPhase.Training &&
                studyManager.BlockCount > 1);
            blockText.text = "Block " + (studyManager.currentBlock + 1);
        }
        if (trialText != null)
        {
            int trial = studyManager.currentPhase == StudyManager.StudyPhase.Training
                ? studyManager.currentTrainingTrial
                : studyManager.currentFormalTrial;
            trialText.text = "Trial " + (trial + 1);
        }

        if (formalBlockText != null)
        {
            formalBlockText.text = $"BLOCK {studyManager.currentBlock + 1} / {studyManager.BlockCount}";
        }
        if (formalTrialText != null)
        {
            formalTrialText.text = $"TRIAL {studyManager.currentFormalTrial + 1} / {studyManager.FormalTrialCount}";
        }
        if (trainingBlockText != null)
        {
            trainingBlockText.gameObject.SetActive(false);
        }
        if (trainingTrialText != null)
        {
            trainingTrialText.text = $"TRIAL {studyManager.currentTrainingTrial + 1} / {studyManager.TrainingTrialCount}";
        }

        ResetAnswerColors();
        UpdateFormalSelectionIndicators();
        UpdateTrainingSelectionIndicators();
        UpdateTrainingReadyIndicators();
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
        trainingCorrectAnswer = correctId;
        trainingUiState = TrainingUiState.Feedback;
        UpdateTrainingResultIndicators();
        if (trainingFeedbackText != null)
        {
            bool correct = currentAnswer == correctId;
            trainingFeedbackText.text = correct ? "CORRECT" : "INCORRECT";
            trainingFeedbackText.color = correct ? ResultCorrect : ResultIncorrect;
        }
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

        // Training is self-paced, so its countdown is intentionally absent.
        if (trainingCountdownText != null)
        {
            trainingCountdownText.text = "";
            trainingCountdownText.gameObject.SetActive(false);
        }
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
            if (formalCountdownText != null) formalCountdownText.text = "";
            if (trainingCountdownText != null) trainingCountdownText.text = "";
            return;
        }

        if (studyManager.currentPhase == StudyManager.StudyPhase.Formal ||
            studyManager.currentPhase == StudyManager.StudyPhase.Training ||
            studyManager.currentPhase == StudyManager.StudyPhase.Redo)
        {
            PollControllerInput();
        }

        if (studyManager.currentPhase == StudyManager.StudyPhase.Training)
        {
            if (countdownText != null) countdownText.text = "";
            if (formalCountdownText != null) formalCountdownText.text = "";
            if (trainingCountdownText != null) trainingCountdownText.text = "";
            return;
        }

        if (studyManager.trialStartTime < 0f)
        {
            if (countdownText != null) countdownText.text = "";
            if (formalCountdownText != null) formalCountdownText.text = "";
            if (trainingCountdownText != null) trainingCountdownText.text = "";
            return;
        }

        float remainingTime = studyManager.stimulusVisibleSeconds - (Time.time - studyManager.trialStartTime);

        if (remainingTime > 0f)
        {
            int seconds = Mathf.CeilToInt(remainingTime);
            if (countdownText != null) countdownText.text = seconds.ToString();
            if (formalCountdownText != null) formalCountdownText.text = $"COUNTDOWN: {seconds}";
            if (trainingCountdownText != null) trainingCountdownText.text = $"COUNTDOWN: {seconds}";
        }
        else
        {
            if (countdownText != null) countdownText.text = "";
            if (formalCountdownText != null) formalCountdownText.text = "";
            if (trainingCountdownText != null) trainingCountdownText.text = "";
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
        if (studyManager != null && studyManager.currentPhase == StudyManager.StudyPhase.Training &&
            trainingUiState != TrainingUiState.Answering)
        {
            return;
        }

        if (currentAnswer == 0)
        {
            if (studyManager != null &&
                (studyManager.currentPhase == StudyManager.StudyPhase.Formal ||
                 studyManager.currentPhase == StudyManager.StudyPhase.Redo))
            {
                if (formalMessageText != null) formalMessageText.text = "Please select a point cloud first.";
            }
            else if (studyManager != null && studyManager.currentPhase == StudyManager.StudyPhase.Training)
            {
                if (trainingMessageText != null) trainingMessageText.text = "Please select a point cloud first.";
            }
            else if (messageText != null)
            {
                messageText.text = "Please select an answer.";
            }
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
        position.y = studyManager.currentPhase == StudyManager.StudyPhase.Training
            ? TrainingSubmitY
            : FormalSubmitY;
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
        UpdateFormalSelectionIndicators();
        UpdateTrainingSelectionIndicators();
        Button selected = currentAnswer == 1 ? leftButton : rightButton;
        if (selected == null) return;

        ColorBlock colors = selected.colors;
        colors.normalColor = new Color(0.071f, 0.412f, 0.788f, 1f);
        colors.selectedColor = colors.normalColor;
        selected.colors = colors;
        if (messageText != null) messageText.text = "";
        if (formalMessageText != null) formalMessageText.text = "";
        if (trainingMessageText != null) trainingMessageText.text = "";
    }

    private void UpdateFormalSelectionIndicators()
    {
        if (formalLeftIndicator != null)
        {
            formalLeftIndicator.color = currentAnswer == 1 ? FormalIndicatorSelected : FormalIndicatorIdle;
        }
        if (formalRightIndicator != null)
        {
            formalRightIndicator.color = currentAnswer == 2 ? FormalIndicatorSelected : FormalIndicatorIdle;
        }
    }

    private void UpdateTrainingSelectionIndicators()
    {
        if (trainingUiState != TrainingUiState.Answering)
        {
            return;
        }

        if (trainingLeftIndicator != null)
        {
            trainingLeftIndicator.color = currentAnswer == 1 ? FormalIndicatorSelected : FormalIndicatorIdle;
        }
        if (trainingRightIndicator != null)
        {
            trainingRightIndicator.color = currentAnswer == 2 ? FormalIndicatorSelected : FormalIndicatorIdle;
        }
    }

    private void UpdateTrainingResultIndicators()
    {
        if (trainingLeftIndicator != null)
        {
            trainingLeftIndicator.color = TrainingResultColor(1);
        }
        if (trainingRightIndicator != null)
        {
            trainingRightIndicator.color = TrainingResultColor(2);
        }
    }

    private Color TrainingResultColor(int answerId)
    {
        if (answerId == trainingCorrectAnswer) return ResultCorrect;
        if (answerId == currentAnswer) return ResultIncorrect;
        return FormalIndicatorIdle;
    }

    private void UpdateTrainingReadyIndicators()
    {
        if (trainingReadyNoIndicator != null)
        {
            trainingReadyNoIndicator.color = trainingReadyChoice == 1
                ? FormalIndicatorSelected
                : FormalIndicatorIdle;
        }
        if (trainingReadyYesIndicator != null)
        {
            trainingReadyYesIndicator.color = trainingReadyChoice == 2
                ? FormalIndicatorSelected
                : FormalIndicatorIdle;
        }
    }

    private void ShowTrainingReadyPage()
    {
        trainingUiState = TrainingUiState.ReadyForFormal;
        trainingReadyChoice = 0;
        if (trainingHud != null) trainingHud.SetActive(false);
        if (trainingReadyHud != null) trainingReadyHud.SetActive(true);
        if (trainingReadyMessageText != null) trainingReadyMessageText.text = "";
        UpdateTrainingReadyIndicators();
        studyManager.HideCurrentStimulus();
        submitInputArmed = false;
    }

    private void ConfirmTrainingReadyChoice()
    {
        if (trainingReadyChoice == 0)
        {
            if (trainingReadyMessageText != null)
            {
                trainingReadyMessageText.text = "Please select an option first.";
            }
            return;
        }

        if (trainingReadyChoice == 1)
        {
            trainingUiState = TrainingUiState.Transitioning;
            submitInputArmed = false;
            studyManager.RestartTraining();
        }
        else
        {
            trainingUiState = TrainingUiState.Transitioning;
            submitInputArmed = false;
            if (trainingReadyMessageText != null)
            {
                trainingReadyMessageText.text = "Loading formal study...";
            }
            studyManager.FinishTraining();
        }
    }

    private void PollControllerInput()
    {
        if (!leftHandDevice.isValid)
        {
            leftHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        }
        if (!rightHandDevice.isValid)
        {
            rightHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        }

        bool leftPrimary = ReadButton(leftHandDevice, UnityEngine.XR.CommonUsages.primaryButton);
        bool leftSecondary = ReadButton(leftHandDevice, UnityEngine.XR.CommonUsages.secondaryButton);
        bool rightPrimary = ReadButton(rightHandDevice, UnityEngine.XR.CommonUsages.primaryButton);
        bool rightSecondary = ReadButton(rightHandDevice, UnityEngine.XR.CommonUsages.secondaryButton);
        bool anyTrigger = ReadButton(leftHandDevice, UnityEngine.XR.CommonUsages.triggerButton) ||
                          ReadButton(rightHandDevice, UnityEngine.XR.CommonUsages.triggerButton);

        bool chooseLeft = (leftPrimary && !previousLeftPrimary) ||
                          (leftSecondary && !previousLeftSecondary);
        bool chooseRight = (rightPrimary && !previousRightPrimary) ||
                           (rightSecondary && !previousRightSecondary);

#if ENABLE_INPUT_SYSTEM && UNITY_EDITOR
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            chooseLeft |= keyboard.xKey.wasPressedThisFrame || keyboard.yKey.wasPressedThisFrame;
            chooseRight |= keyboard.aKey.wasPressedThisFrame || keyboard.bKey.wasPressedThisFrame;
            anyTrigger |= keyboard.spaceKey.isPressed || keyboard.enterKey.isPressed ||
                          keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame;
        }
#endif

        if (!submitInputArmed)
        {
            submitInputArmed = !anyTrigger;
        }
        bool triggerPressed = submitInputArmed && anyTrigger && !previousAnyTrigger;

        if (studyManager.currentPhase == StudyManager.StudyPhase.Formal ||
            studyManager.currentPhase == StudyManager.StudyPhase.Redo)
        {
            if (chooseLeft) OnLeftClicked();
            if (chooseRight) OnRightClicked();
            if (triggerPressed) OnSubmitClicked();
        }
        else if (trainingUiState == TrainingUiState.ReadyForFormal)
        {
            if (chooseLeft) trainingReadyChoice = 1;
            if (chooseRight) trainingReadyChoice = 2;
            if (chooseLeft || chooseRight)
            {
                if (trainingReadyMessageText != null) trainingReadyMessageText.text = "";
                UpdateTrainingReadyIndicators();
            }
            if (triggerPressed) ConfirmTrainingReadyChoice();
        }
        else if (trainingUiState == TrainingUiState.Answering)
        {
            if (chooseLeft) OnLeftClicked();
            if (chooseRight) OnRightClicked();
            if (triggerPressed) OnSubmitClicked();
        }
        else if (trainingUiState == TrainingUiState.Feedback && triggerPressed)
        {
            if (studyManager.IsLastTrainingTrial)
            {
                ShowTrainingReadyPage();
            }
            else
            {
                studyManager.NextTrial();
            }
        }

        previousLeftPrimary = leftPrimary;
        previousLeftSecondary = leftSecondary;
        previousRightPrimary = rightPrimary;
        previousRightSecondary = rightSecondary;
        previousAnyTrigger = anyTrigger;
    }

    private static bool ReadButton(UnityEngine.XR.InputDevice device, InputFeatureUsage<bool> usage)
    {
        return device.isValid && device.TryGetFeatureValue(usage, out bool pressed) && pressed;
    }

#if UNITY_EDITOR
    public void ShowTrainingReadyPageForPreview()
    {
        ShowTrainingReadyPage();
    }
#endif

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
