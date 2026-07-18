using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
#endif
#if UNITY_EDITOR
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
#endif

/// <summary>
/// Owns presentation state only. Trial sequencing and data remain in StudyManager.
/// </summary>
public sealed class StudyUIController : MonoBehaviour
{
    private const string TemporaryAnswerPreviewName = "AnswerUIView_TEMP_PREVIEW_DELETE_BEFORE_BUILD";
    private const float PresentationDistance = 2f;
    private const float InitialHeadPoseFollowSeconds = 0.4f;
    private const int HeadPoseWaitFrameLimit = 300;

    public enum ViewState
    {
        Start,
        Loading,
        Answering,
        BlockEnd,
        Finished,
        Warning
    }

    [Header("Experiment")]
    [SerializeField] private StudyManager studyManager;
    [SerializeField] private UI_StartInput startInput;

    [Header("Views")]
    [SerializeField] private GameObject startUI;
    [SerializeField] private GameObject answerUI;
    [SerializeField] private GameObject endUI;
    [SerializeField] private GameObject finishUI;
    [SerializeField] private GameObject warningUI;

    [Header("Presentation Placement")]
    [SerializeField, Range(0f, 60f)] private float answerElevationDegrees = 20f;

    [Header("Start Page Tabs")]
    [SerializeField] private GameObject experimenterStartPage;
    [SerializeField] private GameObject developerStartPage;
    [SerializeField] private Button experimenterTabButton;
    [SerializeField] private Button developerTabButton;

    [Header("Feedback")]
    [SerializeField] private TMP_Text warningText;
    [SerializeField] private TMP_Text endTitleText;
    [SerializeField] private TMP_Text endSummaryText;
    [SerializeField] private Button nextBlockButton;
    [SerializeField] private Button retryButton;

    public ViewState CurrentState { get; private set; } = ViewState.Start;

    private ViewState stateBeforeWarning = ViewState.Start;
    private StudyManager.StudyPhase observedPhase = (StudyManager.StudyPhase)(-1);
    private bool applicationQuitting;
    private Coroutine presentationRecenterRoutine;
    private bool recenterStimulusWhenHeadPoseIsReady;
    private UnityEngine.XR.InputDevice leftHandDevice;
    private UnityEngine.XR.InputDevice rightHandDevice;
    private bool previousAnyTrigger;
    private bool confirmationTriggerArmed;
    private readonly List<XRInputSubsystem> subscribedInputSubsystems = new List<XRInputSubsystem>();
    private readonly List<XRInputSubsystem> discoveredInputSubsystems = new List<XRInputSubsystem>();

    private void OnEnable()
    {
        SubscribeToXRTrackingOriginUpdates();
    }

    private void Awake()
    {
#if ENABLE_INPUT_SYSTEM
        DisableXRThumbsticks();
#endif

#if UNITY_EDITOR
        ConfigureEditorMouseInput();
#endif

        if (studyManager == null)
        {
            studyManager = GetComponentInParent<StudyManager>();
        }

#if UNITY_EDITOR
        UseTemporaryAnswerPreviewInEditor();
#else
        RemoveTemporaryAnswerPreviewFromPlayer();
#endif

        Application.logMessageReceived += HandleLogMessage;
        ShowStart();
    }

#if ENABLE_INPUT_SYSTEM
    private static void DisableXRThumbsticks()
    {
        foreach (InputActionManager manager in FindObjectsOfType<InputActionManager>(true))
        {
            foreach (InputActionAsset actionAsset in manager.actionAssets)
            {
                if (actionAsset == null)
                {
                    continue;
                }

                bool wasEnabled = actionAsset.enabled;
                if (wasEnabled)
                {
                    actionAsset.Disable();
                }

                foreach (InputActionMap actionMap in actionAsset.actionMaps)
                {
                    foreach (InputAction action in actionMap.actions)
                    {
                        for (int bindingIndex = 0; bindingIndex < action.bindings.Count; bindingIndex++)
                        {
                            string path = action.bindings[bindingIndex].path;
                            if (!string.IsNullOrEmpty(path) &&
                                path.IndexOf("Primary2DAxis", System.StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                action.ApplyBindingOverride(bindingIndex, string.Empty);
                            }
                        }
                    }
                }

                if (wasEnabled)
                {
                    actionAsset.Enable();
                }
            }
        }
    }
#endif

#if UNITY_EDITOR
    private void UseTemporaryAnswerPreviewInEditor()
    {
        GameObject preview = FindTemporaryAnswerPreview();
        if (preview == null)
        {
            return;
        }

        UI_AnswerInput previewInput = preview.GetComponentInChildren<UI_AnswerInput>(true);
        if (previewInput == null)
        {
            Debug.LogWarning("[DensityJND] The temporary Answer UI preview has no UI_AnswerInput component.");
            return;
        }

        GameObject authoredAnswerUI = answerUI;
        answerUI = preview;
        if (authoredAnswerUI != null && authoredAnswerUI != preview)
        {
            authoredAnswerUI.SetActive(false);
        }

        if (studyManager != null)
        {
            studyManager.answerUI = preview;
            studyManager.answerInput = previewInput;
        }

        Debug.Log("[DensityJND] Editor Play Mode is using the temporary Answer UI preview.");
    }

#else
    private static void RemoveTemporaryAnswerPreviewFromPlayer()
    {
        GameObject preview = FindTemporaryAnswerPreview();
        if (preview != null)
        {
            Destroy(preview);
        }
    }
#endif

    private static GameObject FindTemporaryAnswerPreview()
    {
        foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (candidate.scene.IsValid() && candidate.name == TemporaryAnswerPreviewName)
            {
                return candidate;
            }
        }

        return null;
    }

#if UNITY_EDITOR
    private static void ConfigureEditorMouseInput()
    {
        // A Canvas cannot reliably keep both raycasters enabled: Unity's raycaster
        // registry keys them by GameObject. Swap to the mouse raycaster in Editor
        // Play Mode; Android/Quest builds keep the tracked-device raycaster.
        foreach (TrackedDeviceGraphicRaycaster tracked in
                 FindObjectsOfType<TrackedDeviceGraphicRaycaster>(true))
        {
            tracked.enabled = false;
        }

        foreach (GraphicRaycaster mouse in FindObjectsOfType<GraphicRaycaster>(true))
        {
            mouse.ignoreReversedGraphics = false;
            mouse.enabled = true;
        }

        EventSystem eventSystem = EventSystem.current ?? FindObjectOfType<EventSystem>(true);
        if (eventSystem == null)
        {
            return;
        }

        InputSystemUIInputModule mouseModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (mouseModule == null)
        {
            mouseModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }
        mouseModule.enabled = true;

        foreach (BaseInputModule module in eventSystem.GetComponents<BaseInputModule>())
        {
            if (!(module is InputSystemUIInputModule))
            {
                module.enabled = false;
            }
        }
    }
#endif

    private void OnDestroy()
    {
        Application.logMessageReceived -= HandleLogMessage;
        UnsubscribeFromXRTrackingOriginUpdates();
    }

    private void OnDisable()
    {
        UnsubscribeFromXRTrackingOriginUpdates();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus || !isActiveAndEnabled)
        {
            return;
        }

        // The Quest system menu temporarily removes focus while the user holds the Meta
        // button to reset view. Re-acquire the subsystem and place app content using the
        // new head pose after focus returns.
        SubscribeToXRTrackingOriginUpdates();
        RequestPresentationRecenter(true);
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus || !isActiveAndEnabled)
        {
            return;
        }

        // Quest can pause an app while the headset is removed. A new participant may put
        // it on at a different position and facing direction, so treat resume as a handoff.
        SubscribeToXRTrackingOriginUpdates();
        RequestPresentationRecenter(true);
    }

    private void OnApplicationQuit()
    {
        applicationQuitting = true;
    }

    private void Update()
    {
        PollStandaloneConfirmationTrigger();

        if (studyManager == null || observedPhase == studyManager.currentPhase)
        {
            return;
        }

        observedPhase = studyManager.currentPhase;

        switch (observedPhase)
        {
            case StudyManager.StudyPhase.Idle:
                if (CurrentState != ViewState.Loading && CurrentState != ViewState.Warning)
                {
                    ShowStart();
                }
                break;
            case StudyManager.StudyPhase.Training:
            case StudyManager.StudyPhase.Formal:
            case StudyManager.StudyPhase.Redo:
                ShowAnswering();
                break;
            case StudyManager.StudyPhase.Finished:
                ShowFinished();
                break;
        }
    }

    private void PollStandaloneConfirmationTrigger()
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

        bool canConfirmBlock = CurrentState == ViewState.BlockEnd && nextBlockButton != null &&
                               nextBlockButton.isActiveAndEnabled && nextBlockButton.interactable;
        bool canConfirmWarning = CurrentState == ViewState.Warning;
        if (!canConfirmBlock && !canConfirmWarning)
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
        if (!triggerPressed)
        {
            return;
        }

        confirmationTriggerArmed = false;
        if (canConfirmBlock)
        {
            OnNextBlockClicked();
        }
        else if (retryButton != null && retryButton.isActiveAndEnabled && retryButton.interactable)
        {
            RetryLastAction();
        }
        else
        {
            DismissWarning();
        }
    }

    private static bool ReadButton(UnityEngine.XR.InputDevice device, InputFeatureUsage<bool> usage)
    {
        return device.isValid && device.TryGetFeatureValue(usage, out bool pressed) && pressed;
    }

    public void ShowStart()
    {
        SetOnly(startUI);
        ShowExperimenterStartPage();
        CurrentState = ViewState.Start;
        startInput?.SetInteractable(true);
        startInput?.ShowMessage("");
        RequestPresentationRecenter();
    }

    /// <summary>
    /// Developer-only shortcut exposed by the visually unchanged Study Complete title.
    /// </summary>
    public void ReturnToStartFromStudyComplete()
    {
        ShowStart();
    }

    public void ShowExperimenterStartPage()
    {
        SetStartPage(experimenterStartPage);
    }

    public void ShowDeveloperStartPage()
    {
        SetStartPage(developerStartPage);
    }

    public void BeginLoading()
    {
        CurrentState = ViewState.Loading;
        startInput?.SetInteractable(false);
        startInput?.ShowMessage("Loading experiment data...");
    }

    public void SetStartBusy(bool busy)
    {
        startInput?.SetInteractable(!busy);
        if (!busy && CurrentState == ViewState.Loading)
        {
            CurrentState = ViewState.Start;
        }
    }

    public void ShowAnswering()
    {
        SetOnly(answerUI);
        CurrentState = ViewState.Answering;
        RequestPresentationRecenter();
    }

    public void ShowBlockEnd(string summary)
    {
        SetOnly(endUI);
        if (endTitleText != null)
        {
            endTitleText.text = "Block Complete";
        }
        if (endSummaryText != null)
        {
            endSummaryText.text = summary;
        }
        if (nextBlockButton != null)
        {
            nextBlockButton.gameObject.SetActive(true);
            nextBlockButton.interactable = studyManager != null && studyManager.HasNextBlock;
        }
        CurrentState = ViewState.BlockEnd;
        RequestPresentationRecenter();
    }

    public void OnNextBlockClicked()
    {
        if (nextBlockButton != null)
        {
            nextBlockButton.interactable = false;
        }

        studyManager?.StartNextBlock();
    }

    public void ShowFinished()
    {
        bool requiresReview = studyManager != null && studyManager.HasPendingRecovery;
        SetActive(startUI, false);
        SetActive(answerUI, false);
        SetActive(warningUI, false);
        SetActive(endUI, true);
        SetActive(finishUI, false);
        if (endTitleText != null)
        {
            endTitleText.text = "Study Complete";
        }
        if (nextBlockButton != null)
        {
            nextBlockButton.gameObject.SetActive(false);
        }
        if (endSummaryText != null)
        {
            endSummaryText.text = requiresReview
                ? "The study is complete. Some data requires researcher review."
                : "The study is complete. Results were saved successfully.";
        }
        CurrentState = ViewState.Finished;
        RequestPresentationRecenter();
    }

    public void ShowWarning(string message, bool canRetry = true)
    {
        if (warningUI == null)
        {
            Debug.LogWarning(message);
            SetStartBusy(false);
            return;
        }

        if (CurrentState != ViewState.Warning)
        {
            stateBeforeWarning = CurrentState;
        }

        if (warningText != null)
        {
            warningText.text = string.IsNullOrWhiteSpace(message)
                ? "Unable to continue. Please check the configuration and try again."
                : message;
        }

        if (retryButton != null)
        {
            retryButton.gameObject.SetActive(canRetry);
        }

        warningUI.SetActive(true);
        CurrentState = ViewState.Warning;
        SetStartBusy(false);
        RequestPresentationRecenter();
    }

    /// <summary>
    /// Places the full-size study canvas in the participant's current horizontal direction.
    /// During trials the answer view sits above the stimulus, so it only enters the centre of
    /// view when the participant deliberately looks up.
    /// </summary>
    public void RecenterPresentationUI()
    {
        Canvas presentationCanvas = FindPresentationCanvas();
        Camera presentationCamera = FindPresentationCamera(presentationCanvas);
        if (presentationCanvas == null || presentationCamera == null)
        {
            return;
        }

        Transform head = presentationCamera.transform;
        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (!IsFinite(head.position) || !IsFinite(forward) || forward.sqrMagnitude < 0.0001f)
        {
            return;
        }

        forward.Normalize();
        float elevation = CurrentState == ViewState.Answering ? answerElevationDegrees : 0f;
        float elevationRadians = elevation * Mathf.Deg2Rad;
        Vector3 presentationDirection =
            forward * Mathf.Cos(elevationRadians) + Vector3.up * Mathf.Sin(elevationRadians);
        Quaternion presentationRotation = Quaternion.LookRotation(presentationDirection, Vector3.up);
        presentationCanvas.transform.SetPositionAndRotation(
            head.position + presentationDirection * PresentationDistance,
            presentationRotation);
    }

    /// <summary>
    /// Recenters Quest/OpenXR tracking when supported, then moves the study UI and any
    /// currently visible stimulus to the participant's new horizontal forward direction.
    /// This method can also be connected to an in-app Button OnClick event.
    /// </summary>
    public void RecenterTrackingAndPresentation()
    {
        SubscribeToXRTrackingOriginUpdates();
        foreach (XRInputSubsystem subsystem in subscribedInputSubsystems)
        {
            if (subsystem != null && subsystem.running)
            {
                subsystem.TryRecenter();
            }
        }

        RequestPresentationRecenter(true);
    }

    private void RequestPresentationRecenter(bool includeVisibleStimulus = false)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        recenterStimulusWhenHeadPoseIsReady |= includeVisibleStimulus;
        if (presentationRecenterRoutine != null)
        {
            StopCoroutine(presentationRecenterRoutine);
        }
        presentationRecenterRoutine = StartCoroutine(RecenterAfterHeadTrackingStarts());
    }

    private IEnumerator RecenterAfterHeadTrackingStarts()
    {
        float followUntil = -1f;

        for (int frame = 0; frame < HeadPoseWaitFrameLimit; frame++)
        {
            yield return new WaitForEndOfFrame();

            Canvas presentationCanvas = FindPresentationCanvas();
            Camera presentationCamera = FindPresentationCamera(presentationCanvas);
            if (presentationCanvas == null || presentationCamera == null ||
                !presentationCamera.isActiveAndEnabled || !IsFinite(presentationCamera.transform.position))
            {
                continue;
            }

            if (followUntil < 0f)
            {
                // Quest applies the tracked head pose over the first few rendered frames. Briefly
                // follow it so the canvas is not left at the scene's authored world coordinates.
                followUntil = Time.realtimeSinceStartup + InitialHeadPoseFollowSeconds;
            }

            RecenterPresentationUI();
            if (Time.realtimeSinceStartup >= followUntil)
            {
                break;
            }
        }

        if (recenterStimulusWhenHeadPoseIsReady)
        {
            studyManager?.RecenterVisibleStimulus();
            recenterStimulusWhenHeadPoseIsReady = false;
        }
        presentationRecenterRoutine = null;
    }

    private void SubscribeToXRTrackingOriginUpdates()
    {
        discoveredInputSubsystems.Clear();
        SubsystemManager.GetInstances(discoveredInputSubsystems);

        foreach (XRInputSubsystem subsystem in discoveredInputSubsystems)
        {
            if (subsystem == null || subscribedInputSubsystems.Contains(subsystem))
            {
                continue;
            }

            subsystem.trackingOriginUpdated += HandleTrackingOriginUpdated;
            subscribedInputSubsystems.Add(subsystem);
        }
    }

    private void UnsubscribeFromXRTrackingOriginUpdates()
    {
        foreach (XRInputSubsystem subsystem in subscribedInputSubsystems)
        {
            if (subsystem != null)
            {
                subsystem.trackingOriginUpdated -= HandleTrackingOriginUpdated;
            }
        }
        subscribedInputSubsystems.Clear();
    }

    private void HandleTrackingOriginUpdated(XRInputSubsystem subsystem)
    {
        // OpenXR raises this when the runtime changes the reference-space origin, including
        // the Quest system Reset View action. Wait for the new pose before moving content.
        RequestPresentationRecenter(true);
    }

    private Canvas FindPresentationCanvas()
    {
        return startUI != null ? startUI.GetComponentInParent<Canvas>(true) : null;
    }

    private static Camera FindPresentationCamera(Canvas presentationCanvas)
    {
        if (presentationCanvas != null && presentationCanvas.worldCamera != null)
        {
            return presentationCanvas.worldCamera;
        }
        return Camera.main;
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
               !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }

    public void DismissWarning()
    {
        bool formalRecoveryRequested = studyManager != null &&
            (studyManager.currentPhase == StudyManager.StudyPhase.Formal ||
             studyManager.currentPhase == StudyManager.StudyPhase.Redo ||
             (stateBeforeWarning == ViewState.Loading && startInput != null && startInput.LastRequestWasFormal));
        if (formalRecoveryRequested)
        {
            ShowDeveloperRecovery();
            return;
        }

        if (stateBeforeWarning == ViewState.Loading)
        {
            ShowStart();
            return;
        }

        SetActive(warningUI, false);
        CurrentState = stateBeforeWarning;
    }

    public void ShowDeveloperRecovery()
    {
        studyManager?.PauseForFormalRecovery();
        SetOnly(startUI);
        ShowDeveloperStartPage();
        CurrentState = ViewState.Start;
        startInput?.SetInteractable(true);
        if (studyManager != null)
        {
            startInput?.PrepareDeveloperRecovery(
                studyManager.ParticipantID,
                studyManager.currentBlock,
                studyManager.currentFormalTrial);
        }
        RequestPresentationRecenter();
    }

    public void RetryLastAction()
    {
        bool retryFormalStart = stateBeforeWarning == ViewState.Loading &&
                                startInput != null && startInput.LastRequestWasFormal;
        DismissWarning();

        if (studyManager == null)
        {
            ShowWarning("Study Manager is not available.", false);
            return;
        }

        if (retryFormalStart || studyManager.currentPhase == StudyManager.StudyPhase.Idle)
        {
            startInput?.RetryLastAction();
        }
        else if (studyManager.currentPhase != StudyManager.StudyPhase.Finished)
        {
            studyManager.ReloadTrial();
        }
    }

    private void SetOnly(GameObject target)
    {
        SetActive(startUI, target == startUI);
        SetActive(answerUI, target == answerUI);
        SetActive(endUI, target == endUI);
        SetActive(finishUI, false);
        SetActive(warningUI, false);
    }

    private void SetStartPage(GameObject target)
    {
        SetActive(experimenterStartPage, target == experimenterStartPage);
        SetActive(developerStartPage, target == developerStartPage);
        SetTabVisual(experimenterTabButton, target == experimenterStartPage);
        SetTabVisual(developerTabButton, target == developerStartPage);
    }

    private static void SetTabVisual(Button button, bool selected)
    {
        if (button == null)
        {
            return;
        }

        Color normal = selected
            ? new Color(0.071f, 0.412f, 0.788f, 1f)
            : new Color(0.204f, 0.204f, 0.216f, 1f);
        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.selectedColor = Color.Lerp(normal, Color.white, 0.16f);
        colors.highlightedColor = colors.selectedColor;
        colors.pressedColor = Color.Lerp(normal, Color.black, 0.2f);
        button.colors = colors;

        if (button.targetGraphic != null)
        {
            button.targetGraphic.CrossFadeColor(normal, 0f, true, true);
        }
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private void HandleLogMessage(string condition, string stackTrace, LogType type)
    {
        if (applicationQuitting || !Application.isPlaying || CurrentState == ViewState.Warning)
        {
            return;
        }

        if (type == LogType.Exception || type == LogType.Error)
        {
            string firstLine = string.IsNullOrWhiteSpace(condition)
                ? "No error details were provided."
                : condition.Split('\n')[0].Trim();
            if (firstLine.Length > 180)
            {
                firstLine = firstLine.Substring(0, 177) + "...";
            }

            ShowWarning(
                "An unexpected error occurred:\n" + firstLine +
                "\n\nCheck the Android log for the full stack trace.",
                false);
        }
    }
}
