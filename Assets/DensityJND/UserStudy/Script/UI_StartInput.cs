using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UI_StartInput : MonoBehaviour
{
    private enum StartAction
    {
        Study,
        SpecificTrial
    }

    private StartAction lastStartAction = StartAction.Study;

    #region =============== Inspector Configuration ===============

    [Header("Study Manager")]
    [SerializeField] private StudyManager studyManager;
    [SerializeField] private StudyUIController uiController;

    [Header("Participant Input")]
    [SerializeField] private TMP_InputField participantIDInput;

    [Tooltip("Maximum number of digits accepted by the VR numeric keypad.")]
    [SerializeField] private int maxNumericCharacters = 6;

    [Header("Specific Trial Input")]
    [Tooltip("Show the optional Block and Trial index fields for researcher recovery workflows.")]
    [SerializeField] private bool showAdvancedStart = false;
    [SerializeField] private TMP_InputField blockIDInput;
    [SerializeField] private TMP_InputField trialIDInput;

    [SerializeField] private Toggle trainingToggle;
    [SerializeField] private Toggle formalToggle;

    [Header("Message Text")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button startStudyButton;

    private TMP_InputField activeNumericInput;

    #endregion ====================================================

    #region =============== Unity Lifecycle =======================

    private void Awake()
    {
        if (studyManager == null)
        {
            studyManager = transform.parent.GetComponentInChildren<StudyManager>();
        }

        if (uiController == null)
        {
            uiController = transform.root.GetComponentInChildren<StudyUIController>(true);
        }

        // Training configuration files are not present yet, so make Formal the
        // safe default every time the start page is opened in Play mode.
        trainingToggle?.SetIsOnWithoutNotify(false);
        formalToggle?.SetIsOnWithoutNotify(true);
        SetAdvancedStartVisible(showAdvancedStart);

        ConfigureNumericInput(participantIDInput, SelectParticipantInput);
        ConfigureNumericInput(blockIDInput, SelectBlockInput);
        ConfigureNumericInput(trialIDInput, SelectTrialInput);
        activeNumericInput = participantIDInput;

        ShowMessage("");
    }

    private void OnDestroy()
    {
        RemoveInputListener(participantIDInput, SelectParticipantInput);
        RemoveInputListener(blockIDInput, SelectBlockInput);
        RemoveInputListener(trialIDInput, SelectTrialInput);
    }

    #endregion ====================================================

    #region =============== Start Input ===========================

    public void OnStartStudyClicked()
    {
        if (studyManager == null)
        {
            ShowMessage("Study Manager is not available.");
            return;
        }

        if (participantIDInput == null ||
            !int.TryParse(participantIDInput.text, out int participantID) ||
            participantID < 0)
        {
            ShowMessage("Please enter a valid participant ID (0 or greater).");
            return;
        }

        ShowMessage("");
        lastStartAction = StartAction.Study;
        Debug.Log($"[DensityJND] Start Study requested for participant {participantID}.");
        uiController?.BeginLoading();

        // The main Start button honors the selected phase. Formal starts from
        // Block 1 / Trial 1 and never touches the absent Training files.
        if (formalToggle != null && formalToggle.isOn)
        {
            studyManager.StartTrial(participantID, 0, 0, StudyManager.StudyPhase.Formal);
        }
        else
        {
            studyManager.StartStudy(participantID);
        }
    }

    public void OnStartTrialClicked()
    {
        if (studyManager == null)
        {
            ShowMessage("Study Manager is not available.");
            return;
        }

        if (participantIDInput == null ||
            !int.TryParse(participantIDInput.text, out int participantID) ||
            participantID < 0)
        {
            ShowMessage("Please enter a valid participant ID (0 or greater).");
            return;
        }

        int blockID = 1;
        if (blockIDInput != null && blockIDInput.gameObject.activeInHierarchy &&
            (!int.TryParse(blockIDInput.text, out blockID) || blockID < 1))
        {
            ShowMessage("Block number must start at 1.");
            return;
        }

        int trialID = 1;
        if (trialIDInput != null && trialIDInput.gameObject.activeInHierarchy &&
            (!int.TryParse(trialIDInput.text, out trialID) || trialID < 1))
        {
            ShowMessage("Trial number must start at 1.");
            return;
        }

        StudyManager.StudyPhase phase;

        // Formal takes precedence if a scene/prefab accidentally leaves both toggles on.
        if (formalToggle != null && formalToggle.isOn)
        {
            phase = StudyManager.StudyPhase.Formal;
        }
        else if (trainingToggle != null && trainingToggle.isOn)
        {
            phase = StudyManager.StudyPhase.Training;
        }
        else
        {
            ShowMessage("Please select Training or Formal.");
            return;
        }

        ShowMessage("");
        lastStartAction = StartAction.SpecificTrial;
        Debug.Log(
            $"[DensityJND] Start Trial requested: participant {participantID}, " +
            $"block {blockID}, trial {trialID}, phase {phase}.");
        uiController?.BeginLoading();

        studyManager.StartTrial(participantID, blockID - 1, trialID - 1, phase);
    }

    public void RetryLastAction()
    {
        if (lastStartAction == StartAction.SpecificTrial)
        {
            OnStartTrialClicked();
        }
        else
        {
            OnStartStudyClicked();
        }
    }

    /// <summary>
    /// Called by the world-space keypad. This avoids TouchScreenKeyboard entirely,
    /// which is unreliable for TMP input fields in Quest standalone builds.
    /// </summary>
    public void AppendDigit(string digit)
    {
        if (activeNumericInput == null || !activeNumericInput.interactable ||
            string.IsNullOrEmpty(digit) || digit.Length != 1 || !char.IsDigit(digit[0]))
        {
            return;
        }

        int characterLimit = activeNumericInput.characterLimit > 0
            ? activeNumericInput.characterLimit
            : maxNumericCharacters;
        if (activeNumericInput.text.Length >= characterLimit)
        {
            ShowMessage($"Use at most {characterLimit} digits.");
            return;
        }

        activeNumericInput.text += digit;
        activeNumericInput.caretPosition = activeNumericInput.text.Length;
        ShowMessage("");
    }

    public void BackspaceNumericInput()
    {
        if (activeNumericInput == null || !activeNumericInput.interactable ||
            string.IsNullOrEmpty(activeNumericInput.text))
        {
            return;
        }

        activeNumericInput.text = activeNumericInput.text.Substring(0, activeNumericInput.text.Length - 1);
        activeNumericInput.caretPosition = activeNumericInput.text.Length;
        ShowMessage("");
    }

    public void ClearNumericInput()
    {
        if (activeNumericInput == null || !activeNumericInput.interactable)
        {
            return;
        }

        activeNumericInput.text = "";
        ShowMessage("");
    }

    public void SetInteractable(bool interactable)
    {
        if (participantIDInput != null) participantIDInput.interactable = interactable;
        if (blockIDInput != null) blockIDInput.interactable = interactable;
        if (trialIDInput != null) trialIDInput.interactable = interactable;
        if (trainingToggle != null) trainingToggle.interactable = interactable;
        if (formalToggle != null) formalToggle.interactable = interactable;
        if (startStudyButton != null) startStudyButton.interactable = interactable;
    }

    public void ShowMessage(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }
    }

    private void ConfigureNumericInput(TMP_InputField input, UnityAction<string> onSelected)
    {
        if (input == null)
        {
            return;
        }

        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = Mathf.Max(1, maxNumericCharacters);

        // Quest reports itself as Android. Suppressing TMP's soft keyboard prevents
        // TouchScreenKeyboard.Open from reaching the unsupported phone IME path.
        input.shouldHideSoftKeyboard = true;
        input.onSelect.RemoveListener(onSelected);
        input.onSelect.AddListener(onSelected);
    }

    private static void RemoveInputListener(TMP_InputField input, UnityAction<string> onSelected)
    {
        input?.onSelect.RemoveListener(onSelected);
    }

    private void SelectParticipantInput(string unused) => activeNumericInput = participantIDInput;
    private void SelectBlockInput(string unused) => activeNumericInput = blockIDInput;
    private void SelectTrialInput(string unused) => activeNumericInput = trialIDInput;

    private void SetAdvancedStartVisible(bool visible)
    {
        if (participantIDInput != null)
        {
            Transform advancedTitle = participantIDInput.transform.parent?.Find("AdvancedTitle");
            if (advancedTitle != null) advancedTitle.gameObject.SetActive(visible);
        }

        if (blockIDInput != null) blockIDInput.gameObject.SetActive(visible);
        if (trialIDInput != null) trialIDInput.gameObject.SetActive(visible);
    }

    #endregion ====================================================
}
