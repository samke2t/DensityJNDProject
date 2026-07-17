using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM && UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

public class UI_StartInput : MonoBehaviour
{
    private const int MinimumParticipantId = 1;
    private const int MaximumParticipantId = 20;

    private enum StartAction
    {
        Study,
        DeveloperResume,
        DeveloperRepair
    }

    private StartAction lastStartAction = StartAction.Study;
    private int pendingParticipantID;
    private int pendingBlockIndex;
    private int pendingTrialIndex;
    private bool pendingFormalStart;
    private bool hasPendingStart;
    private UnityEngine.XR.InputDevice leftHandDevice;
    private UnityEngine.XR.InputDevice rightHandDevice;
    private bool previousAnyTrigger;
    private bool confirmationTriggerArmed;

    #region =============== Inspector Configuration ===============

    [Header("Study Manager")]
    [SerializeField] private StudyManager studyManager;
    [SerializeField] private StudyUIController uiController;

    [Header("Participant Input")]
    [SerializeField] private TMP_InputField participantIDInput;

    [Tooltip("Maximum number of digits accepted by the VR numeric keypad.")]
    [SerializeField] private int maxNumericCharacters = 6;

    [SerializeField] private Toggle trainingToggle;
    [SerializeField] private Toggle formalToggle;

    [Header("Message Text")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button startStudyButton;

    [Header("Developer Page Copy")]
    [SerializeField] private TMP_InputField developerParticipantIDInput;
    [SerializeField] private TMP_InputField developerBlockIDInput;
    [SerializeField] private TMP_InputField developerTrialIDInput;
    [SerializeField] private TMP_Text developerMessageText;
    [SerializeField] private Button developerResumeStudyButton;
    [SerializeField] private Button developerRepairTrialButton;

    [Header("Ready Confirmation")]
    [SerializeField] private GameObject readyConfirmationView;
    [SerializeField] private Button readyConfirmationButton;

    private TMP_InputField activeNumericInput;

    public bool LastRequestWasFormal { get; private set; }

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

        // Start on Training by default while still allowing the researcher to
        // explicitly switch to Formal before beginning the study.
        formalToggle?.SetIsOnWithoutNotify(false);
        trainingToggle?.SetIsOnWithoutNotify(true);

        ConfigureNumericInput(participantIDInput, SelectParticipantInput);
        ConfigureNumericInput(developerParticipantIDInput, SelectDeveloperParticipantInput);
        ConfigureNumericInput(developerBlockIDInput, SelectDeveloperBlockInput);
        ConfigureNumericInput(developerTrialIDInput, SelectDeveloperTrialInput);
        activeNumericInput = participantIDInput;

        if (readyConfirmationView != null)
        {
            readyConfirmationView.SetActive(false);
        }

        ShowMessage("");
    }

    private void OnDestroy()
    {
        RemoveInputListener(participantIDInput, SelectParticipantInput);
        RemoveInputListener(developerParticipantIDInput, SelectDeveloperParticipantInput);
        RemoveInputListener(developerBlockIDInput, SelectDeveloperBlockInput);
        RemoveInputListener(developerTrialIDInput, SelectDeveloperTrialInput);
    }

    private void OnEnable()
    {
        confirmationTriggerArmed = false;
        previousAnyTrigger = false;
    }

    private void Update()
    {
        PollReadyConfirmationTrigger();
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
            participantID < MinimumParticipantId ||
            participantID > MaximumParticipantId)
        {
            ShowMessage($"Please enter a participant ID between {MinimumParticipantId} and {MaximumParticipantId}.");
            return;
        }

        ShowMessage("");
        lastStartAction = StartAction.Study;
        LastRequestWasFormal = formalToggle != null && formalToggle.isOn;
        QueueStartConfirmation(participantID, 0, 0, LastRequestWasFormal);
    }

    public void OnDeveloperResumeStudyClicked()
    {
        if (studyManager == null)
        {
            ShowDeveloperStatus("Study Manager is not available.");
            return;
        }

        if (developerParticipantIDInput == null ||
            !int.TryParse(developerParticipantIDInput.text, out int participantID) ||
            participantID < MinimumParticipantId || participantID > MaximumParticipantId)
        {
            ShowDeveloperStatus($"Please enter a participant ID between {MinimumParticipantId} and {MaximumParticipantId}.");
            return;
        }

        ShowDeveloperStatus("");
        lastStartAction = StartAction.DeveloperResume;
        LastRequestWasFormal = true;
        QueueStartConfirmation(participantID, 0, 0, true);
    }

    public void OnDeveloperRepairTrialClicked()
    {
        if (studyManager == null)
        {
            ShowDeveloperStatus("Study Manager is not available.");
            return;
        }

        if (developerParticipantIDInput == null ||
            !int.TryParse(developerParticipantIDInput.text, out int participantID) ||
            participantID < MinimumParticipantId || participantID > MaximumParticipantId)
        {
            ShowDeveloperStatus($"Please enter a participant ID between {MinimumParticipantId} and {MaximumParticipantId}.");
            return;
        }

        if (developerBlockIDInput == null ||
            !int.TryParse(developerBlockIDInput.text, out int blockID) || blockID < 1)
        {
            ShowDeveloperStatus("Block number must start at 1.");
            return;
        }

        if (developerTrialIDInput == null ||
            !int.TryParse(developerTrialIDInput.text, out int trialID) || trialID < 1)
        {
            ShowDeveloperStatus("Trial number must start at 1.");
            return;
        }

        ShowDeveloperStatus("");
        lastStartAction = StartAction.DeveloperRepair;
        LastRequestWasFormal = true;
        QueueStartConfirmation(participantID, blockID - 1, trialID - 1, true);
    }

    public void OnReadyToBeginClicked()
    {
        if (!hasPendingStart || studyManager == null)
        {
            return;
        }

        hasPendingStart = false;
        if (readyConfirmationView != null)
        {
            readyConfirmationView.SetActive(false);
        }

        uiController?.BeginLoading();

        if (lastStartAction == StartAction.DeveloperResume)
        {
            Debug.Log($"[DensityJND] Resume requested for participant {pendingParticipantID}.");
            studyManager.ResumeStudy(pendingParticipantID);
        }
        else if (lastStartAction == StartAction.DeveloperRepair)
        {
            Debug.Log(
                $"[DensityJND] Formal start from specific trial requested: participant {pendingParticipantID}, " +
                $"block {pendingBlockIndex + 1}, trial {pendingTrialIndex + 1}.");
            studyManager.RepairSpecificTrial(pendingParticipantID, pendingBlockIndex, pendingTrialIndex);
        }
        else if (pendingFormalStart)
        {
            Debug.Log($"[DensityJND] Formal study requested for participant {pendingParticipantID}.");
            studyManager.StartTrial(pendingParticipantID, 0, 0, StudyManager.StudyPhase.Formal);
        }
        else
        {
            Debug.Log($"[DensityJND] Training study requested for participant {pendingParticipantID}.");
            studyManager.StartStudy(pendingParticipantID);
        }
    }

    private void QueueStartConfirmation(int participantID, int blockIndex, int trialIndex, bool formalStart)
    {
        pendingParticipantID = participantID;
        pendingBlockIndex = blockIndex;
        pendingTrialIndex = trialIndex;
        pendingFormalStart = formalStart;
        hasPendingStart = true;
        // If the preceding UI button was selected with a controller ray, that same held
        // trigger must not immediately accept the confirmation page as well.
        confirmationTriggerArmed = false;

        if (readyConfirmationView != null)
        {
            readyConfirmationView.SetActive(true);
        }
        else
        {
            // Preserve a usable start flow if an older scene has not been rebuilt yet.
            OnReadyToBeginClicked();
        }
    }

    public void RetryLastAction()
    {
        if (lastStartAction == StartAction.DeveloperResume)
        {
            OnDeveloperResumeStudyClicked();
        }
        else if (lastStartAction == StartAction.DeveloperRepair)
        {
            OnDeveloperRepairTrialClicked();
        }
        else
        {
            OnStartStudyClicked();
        }
    }

    private void PollReadyConfirmationTrigger()
    {
        if (!leftHandDevice.isValid)
        {
            leftHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        }
        if (!rightHandDevice.isValid)
        {
            rightHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        }

        bool anyTrigger = ReadButton(leftHandDevice, UnityEngine.XR.CommonUsages.triggerButton) ||
                          ReadButton(rightHandDevice, UnityEngine.XR.CommonUsages.triggerButton);
#if ENABLE_INPUT_SYSTEM && UNITY_EDITOR
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            anyTrigger |= keyboard.spaceKey.isPressed || keyboard.enterKey.isPressed;
        }
#endif

        bool canConfirm = hasPendingStart && readyConfirmationView != null &&
                          readyConfirmationView.activeInHierarchy &&
                          (readyConfirmationButton == null || readyConfirmationButton.interactable);
        if (!canConfirm)
        {
            confirmationTriggerArmed = false;
            previousAnyTrigger = anyTrigger;
            return;
        }

        if (!confirmationTriggerArmed)
        {
            confirmationTriggerArmed = !anyTrigger;
        }

        bool triggerPressed = confirmationTriggerArmed && anyTrigger && !previousAnyTrigger;
        previousAnyTrigger = anyTrigger;
        if (triggerPressed)
        {
            confirmationTriggerArmed = false;
            OnReadyToBeginClicked();
        }
    }

    private static bool ReadButton(UnityEngine.XR.InputDevice device, InputFeatureUsage<bool> usage)
    {
        return device.isValid && device.TryGetFeatureValue(usage, out bool pressed) && pressed;
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
        if (trainingToggle != null) trainingToggle.interactable = interactable;
        if (formalToggle != null) formalToggle.interactable = interactable;
        if (startStudyButton != null) startStudyButton.interactable = interactable;
        if (developerParticipantIDInput != null) developerParticipantIDInput.interactable = interactable;
        if (developerBlockIDInput != null) developerBlockIDInput.interactable = interactable;
        if (developerTrialIDInput != null) developerTrialIDInput.interactable = interactable;
        if (developerResumeStudyButton != null) developerResumeStudyButton.interactable = interactable;
        if (developerRepairTrialButton != null) developerRepairTrialButton.interactable = interactable;
        if (readyConfirmationButton != null) readyConfirmationButton.interactable = interactable;
    }

    public void ShowMessage(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }
        if (developerMessageText != null)
        {
            developerMessageText.text = message;
        }
    }

    public void ShowDeveloperStatus(string message)
    {
        if (developerMessageText != null)
        {
            developerMessageText.text = message;
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
    private void SelectDeveloperParticipantInput(string unused) => activeNumericInput = developerParticipantIDInput;
    private void SelectDeveloperBlockInput(string unused) => activeNumericInput = developerBlockIDInput;
    private void SelectDeveloperTrialInput(string unused) => activeNumericInput = developerTrialIDInput;

    public void PrepareDeveloperRecovery(int participantID, int blockIndex, int trialIndex)
    {
        if (developerParticipantIDInput != null && string.IsNullOrWhiteSpace(developerParticipantIDInput.text) &&
            participantID >= MinimumParticipantId)
        {
            developerParticipantIDInput.SetTextWithoutNotify(participantID.ToString());
        }
        if (developerBlockIDInput != null && string.IsNullOrWhiteSpace(developerBlockIDInput.text) && blockIndex >= 0)
        {
            developerBlockIDInput.SetTextWithoutNotify((blockIndex + 1).ToString());
        }
        if (developerTrialIDInput != null && string.IsNullOrWhiteSpace(developerTrialIDInput.text) && trialIndex >= 0)
        {
            developerTrialIDInput.SetTextWithoutNotify((trialIndex + 1).ToString());
        }

        activeNumericInput = developerParticipantIDInput;
        ShowMessage("");
    }

    #endregion ====================================================
}
