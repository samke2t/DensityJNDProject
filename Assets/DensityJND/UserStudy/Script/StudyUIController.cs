using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns presentation state only. Trial sequencing and data remain in StudyManager.
/// </summary>
public sealed class StudyUIController : MonoBehaviour
{
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

    [Header("Feedback")]
    [SerializeField] private TMP_Text warningText;
    [SerializeField] private TMP_Text endSummaryText;
    [SerializeField] private Button retryButton;

    public ViewState CurrentState { get; private set; } = ViewState.Start;

    private ViewState stateBeforeWarning = ViewState.Start;
    private StudyManager.StudyPhase observedPhase = (StudyManager.StudyPhase)(-1);
    private bool applicationQuitting;

    private void Awake()
    {
        if (studyManager == null)
        {
            studyManager = GetComponentInParent<StudyManager>();
        }

        Application.logMessageReceived += HandleLogMessage;
        ShowStart();
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= HandleLogMessage;
    }

    private void OnApplicationQuit()
    {
        applicationQuitting = true;
    }

    private void Update()
    {
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

    public void ShowStart()
    {
        SetOnly(startUI);
        CurrentState = ViewState.Start;
        startInput?.SetInteractable(true);
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
    }

    public void ShowBlockEnd(string summary)
    {
        SetOnly(endUI);
        if (endSummaryText != null)
        {
            endSummaryText.text = summary;
        }
        CurrentState = ViewState.BlockEnd;
    }

    public void ShowFinished()
    {
        bool hasRedo = finishUI != null && finishUI.activeSelf;
        SetActive(startUI, false);
        SetActive(answerUI, false);
        SetActive(warningUI, false);
        SetActive(endUI, true);
        SetActive(finishUI, hasRedo);
        if (endSummaryText != null)
        {
            endSummaryText.text = hasRedo
                ? "The study is complete. Some trials require review."
                : "The study is complete. Results were saved successfully.";
        }
        CurrentState = ViewState.Finished;
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
    }

    public void DismissWarning()
    {
        SetActive(warningUI, false);
        CurrentState = stateBeforeWarning;
    }

    public void RetryLastAction()
    {
        DismissWarning();

        if (studyManager == null)
        {
            ShowWarning("Study Manager is not available.", false);
            return;
        }

        if (studyManager.currentPhase == StudyManager.StudyPhase.Idle)
        {
            startInput?.OnStartStudyClicked();
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
            ShowWarning("An unexpected error occurred. Please check the Console before continuing.", false);
        }
    }
}
