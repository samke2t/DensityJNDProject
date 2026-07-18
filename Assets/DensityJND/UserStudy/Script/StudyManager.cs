using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

public class StudyManager : MonoBehaviour
{
    private const int TrainingSequenceIndex = 0;

    #region =============== Data Definition ===============

    public enum StudyPhase
    {
        Idle,
        Training,
        Formal,
        Finished,
        Redo
    }

    private enum TrialRunMode
    {
        Normal,
        Resume
    }
    
    public struct ConfigInfo
    {
        public string stimuliFolder;
        public string participantCsv; // 用户根据序号对应特定的实验顺序
        public string orderListCsv; // 实验的所有候选顺序（顺序跟scene的对应）
        public string sceneCsv; // 所有的 Dataset + 设置 + 答案
    }
    
    public struct TrialInfo
    {
        public int SceneID;
        public int ConditionID;
        public int RepetitionID;
        public int SourceLabel;
        public int SamplingSeed;

        public string StimuliName;
        public Vector3[] Stimulus1;
        public Vector3[] Stimulus2;

        public int CorrectAnswer;
        
        public float Distance;
        public float DensityRatio;
    }
    
    public struct RedoTrial
    {
        public int blockIndex;
        public int trialIndex;
    }
    
    #endregion =============================================


    #region =============== Inspector Configuration ===============

    [Header("VR")]
    public Transform playerPosition; // TODO: 根据 Meta 的 sdk 去调整，控制用户的位置

    
    // TODO：绑定UI的管理组
    [Header("UI Manager")]
    public GameObject startUI;
    public GameObject answerUI;
    public GameObject warningUI;
    public GameObject endUI;
    public GameObject redoUI;
    
    [SerializeField] public StimuliRender stimuliRender;
    [SerializeField] public UI_StartInput startInput;
    [SerializeField] public UI_AnswerInput answerInput;

    private StudyUIController uiController;
    
    

    #endregion ====================================================
    
    
    #region ============= UI Input & Synchronization ==============
    
    public StudyPhase currentPhase = StudyPhase.Idle; // 当下的程序阶段
    public int ParticipantID;                   // 参与者 Id
    public int currentBlock;                   // 当下实验的 block
    public int currentFormalTrial = -1;         // 当下正式实验的 Trial 的序号
    public int currentTrainingTrial = -1;       // 当下训练的 Trial 的序号
    public int reTrialId = -1;                 // 当下补做的 Trial 的序号
    public int currentAnswer;                  // 参与者答案
    public float stimulusVisibleSeconds = 5f; // TODO：Stimuli 可见的时长，后期可以调整
    public float trialStartTime;               // Used with stimulusVisibleSeconds to calculate time remaining

    public bool IsLastTrainingTrial =>
        currentPhase == StudyPhase.Training && currentTrainingTrial >= trainingCountPB - 1;
    public int BlockCount => blockCount;
    public int FormalTrialCount => trialCountPB;
    public int TrainingTrialCount => trainingCountPB;
    public bool HasNextBlock => currentPhase == StudyPhase.Redo
        ? HasNextRecoveryBlock()
        : currentBlock + 1 < blockCount;
    public bool HasPendingRecovery => redoList != null && redoList.Length > 0;
    public string CurrentPhaseLabel => currentRunMode == TrialRunMode.Resume
        ? "Resume"
        : currentPhase.ToString();
    
    #endregion ====================================================
    
   
    #region =============== Internal Runtime Data ===============
    
    
    
    
    // 程序启动时的基础配置
    private int blockCount;                    // 实验有几个 block
    private int trialCountPB;                  // 每个 block 有几个正式实验的 Trial
    private int trainingCountPB;               // 每个 block 有几个训练的 Trial
    private float clusterCenterAngleDeg = 20f; // 两个团中心之间的水平视角夹角
    private float defaultStimulusDistance = 3f; // CSV 距离暂为 0 时，使用可见的预览距离
    
    
    
    // 文件夹，路径等配置信息
    private ConfigInfo trainingConfig;         // 训练文件的配置
    private ConfigInfo formalConfig;           // 正式实验文件的配置
    private string resultFolder;               // 结果导出文件夹
    public string[] blockFolderName;           // 每个 block 按实际距离生成的结果文件夹名

    
    // 该参与者实验程序中使用的所有 Trial Stimuli 信息的列表
    public TrialInfo[][] trainingTrials;       // 单一训练序列；训练不按正式实验的 block 拆分
    public TrialInfo[][] formalTrials;         // 按该用户的实验顺序记录的所有 trial 的具体信息
    public RedoTrial[] redoList;               // 数据有问题，重做的列表
    
    
    // 正在运行的 Trial 的相关信息
    private Vector3 stimuliOrigin;                     // 刺激渲染的中点
    private TrialInfo currentTrial;                    // 当下的trial 的信息
    private Coroutine stimulusTimerCoroutine;          // Active stimulus countdown routine
    private TrialRunMode currentRunMode = TrialRunMode.Normal;
    
    
    // 状态记录
    private bool debugMode = false;            // 是否输出 Log 文档来记录运行信息
    private bool loadSuccess = true;           // 是否成功读取实验配置文件
    
    #endregion ====================================================
    





    




    #region ====================================== Uinity Lifecycle ====================================================

    public void Awake()
    {
        // TODO: 根据最终的 Unity 层级安排再调整
        if(stimuliRender == null)
        {
            stimuliRender = transform.parent.GetComponentInChildren<StimuliRender>();
        }
        if(answerInput == null)
        {
            answerInput = transform.parent.GetComponentInChildren<UI_AnswerInput>();
        }
        if(startInput == null)
        {
            startInput = transform.parent.GetComponentInChildren<UI_StartInput>();
        }
        uiController = transform.parent.GetComponentInChildren<StudyUIController>(true);
        if (playerPosition == null && Camera.main != null)
        {
            playerPosition = Camera.main.transform;
        }
        
        // TODO: 激活StartUI，等待用户输入（有需要修改可以改）
        currentPhase = StudyPhase.Idle;
        
        startUI.SetActive(true); //TODO: startUI 中要有 Awake 的函数
        answerUI.SetActive(false);
        warningUI.SetActive(false);
        endUI.SetActive(false);
        redoUI.SetActive(false);
        
        
        
        trialCountPB = 60; // Final Formal_OrderList: 60 trials per order
        trainingCountPB = 12; // Final Training_OrderList: 12 trials per order

        blockCount = 4; // 新的 Formal_Participant 定义了 4 个 block
        blockFolderName = new String[blockCount];

        trialStartTime = -1f;
    }

    
    #endregion =========================================================================================================
    
    
    
    
    #region ======================================== Study Entry =======================================================
    
    
    // TODO：对接 stratUI 上，直接开始的按钮，只需要输入participantID
    public void StartStudy(int participantId)
    {
        StartCoroutine(StartStudyRoutine(participantId));
    }
    
    public IEnumerator StartStudyRoutine(int participantId)
    {
        yield return ParticipantInit(participantId, true, false);
        if (!loadSuccess)
        {
            uiController?.SetStartBusy(false);
            yield break;
        }
        
        // ===============================================================
        // 加载完成后，进入第一个 training trial
        // ===============================================================
        EnterTrial(0, 0, StudyPhase.Training);
    }
    
    
    
    // TODO：对接 startUI 上，从某个具体trial 开始的按钮，需要输入participantId，（这个用户完成的）第几个block，这个block的第几个 trial，什么模式（训练或者是正式）
    public void StartTrial(int participantId, int blockId, int trialId, StudyPhase phase)
    {
        if (blockId < 0 || blockId >= blockCount)
        {
            ShowWarning($"Block must be between 1 and {blockCount}.");
            uiController?.SetStartBusy(false);
            return;
        }

        if (phase == StudyPhase.Training && (trialId < 0 || trialId >= trainingCountPB))
        {
            ShowWarning($"Training trial must be between 1 and {trainingCountPB}.");
            uiController?.SetStartBusy(false);
            return;
        }

        if (phase == StudyPhase.Formal && (trialId < 0 || trialId >= trialCountPB))
        {
            ShowWarning($"Formal trial must be between 1 and {trialCountPB}.");
            uiController?.SetStartBusy(false);
            return;
        }

        if (phase != StudyPhase.Training && phase != StudyPhase.Formal)
        {
            ShowWarning("Please select Training or Formal.");
            uiController?.SetStartBusy(false);
            return;
        }
        
        StartCoroutine(StartTrialRoutine(participantId, blockId, trialId, phase));
    }
    
    public IEnumerator StartTrialRoutine(int participantId, int blockId, int trialId, StudyPhase phase)
    {
        // A direct Formal start must not reuse Training data left over from an
        // earlier run in the same Play Mode session.
        if (phase == StudyPhase.Formal)
        {
            trainingTrials = null;
        }

        yield return ParticipantInit(
            participantId,
            phase == StudyPhase.Training,
            phase == StudyPhase.Formal);
        if (!loadSuccess)
        {
            uiController?.SetStartBusy(false);
            yield break;
        }

        EnterTrial(blockId, trialId, phase);
    }

    public void ResumeStudy(int participantId)
    {
        StartCoroutine(ResumeStudyRoutine(participantId));
    }

    public void CheckStudyProgress(int participantId)
    {
        StartCoroutine(CheckStudyProgressRoutine(participantId));
    }

    private IEnumerator CheckStudyProgressRoutine(int participantId)
    {
        yield return ParticipantInit(participantId, false, true);
        if (!loadSuccess)
        {
            uiController?.SetStartBusy(false);
            yield break;
        }

        bool hasResults = TryBuildRecoveryList(
            out List<RedoTrial> pendingTrials,
            out int validCount,
            out string error);

        int totalTrials = blockCount * trialCountPB;
        if (!hasResults && error == "No saved study was found for this participant.")
        {
            startInput?.ShowDeveloperStatus($"Completed: 0 / {totalTrials}\nRemaining: {totalTrials}");
        }
        else
        {
            startInput?.ShowDeveloperStatus(hasResults
                ? BuildRecoveryStatus(validCount, pendingTrials)
                : error);
        }

        uiController?.SetStartBusy(false);
    }

    private IEnumerator ResumeStudyRoutine(int participantId)
    {
        yield return ParticipantInit(participantId, false, true);
        if (!loadSuccess)
        {
            uiController?.SetStartBusy(false);
            yield break;
        }

        if (!TryBuildRecoveryList(out List<RedoTrial> pendingTrials, out int validCount, out string error))
        {
            uiController?.SetStartBusy(false);
            startInput?.ShowDeveloperStatus(error);
            yield break;
        }

        redoList = pendingTrials.ToArray();
        startInput?.ShowDeveloperStatus(BuildRecoveryStatus(validCount, redoList));
        if (redoList.Length == 0)
        {
            uiController?.SetStartBusy(false);
            yield break;
        }

        currentRunMode = TrialRunMode.Resume;
        currentPhase = StudyPhase.Redo;
        reTrialId = 0;
        EnterRecoveryTrial(redoList[reTrialId]);
    }

    public void RepairSpecificTrial(int participantId, int blockId, int trialId)
    {
        if (blockId < 0 || blockId >= blockCount)
        {
            startInput?.ShowDeveloperStatus($"Block must be between 1 and {blockCount}.");
            uiController?.SetStartBusy(false);
            return;
        }

        if (trialId < 0 || trialId >= trialCountPB)
        {
            startInput?.ShowDeveloperStatus($"Trial must be between 1 and {trialCountPB}.");
            uiController?.SetStartBusy(false);
            return;
        }

        StartCoroutine(RepairSpecificTrialRoutine(participantId, blockId, trialId));
    }

    private IEnumerator RepairSpecificTrialRoutine(int participantId, int blockId, int trialId)
    {
        yield return ParticipantInit(participantId, false, true);
        if (!loadSuccess)
        {
            uiController?.SetStartBusy(false);
            yield break;
        }

        // The developer's block/trial fields are a direct entry point into the
        // normal Formal sequence. From here the remaining trials in this block,
        // the Block Complete page, and Next Block all use the production flow.
        EnterTrial(blockId, trialId, StudyPhase.Formal);
    }

    private void EnterTrial(int blockId, int trialId, StudyPhase phase)
    {
        currentRunMode = TrialRunMode.Normal;
        currentBlock = phase == StudyPhase.Training ? 0 : blockId;
        currentPhase = phase;

        if (phase == StudyPhase.Training)
        {
            currentTrainingTrial = trialId;
            currentTrial = trainingTrials[TrainingSequenceIndex][currentTrainingTrial];
        }
        else
        {
            currentFormalTrial = trialId;
            currentTrial = formalTrials[currentBlock][currentFormalTrial];
        }

        if (uiController != null)
        {
            uiController.ShowAnswering();
        }
        else
        {
            startUI.SetActive(false);
            answerUI.SetActive(true);
        }

        SetupTrial(currentTrial);
    }
    
    
    // TODO：redoUI 的 Button 绑定的函数
    public void StartRedo()
    {
        if (redoList == null || redoList.Length == 0)
        {
            return;
        }

        currentRunMode = TrialRunMode.Resume;
        currentPhase = StudyPhase.Redo;

        endUI.SetActive(false);
        answerUI.SetActive(true);

        reTrialId = 0;

        EnterRecoveryTrial(redoList[reTrialId]);
    }

    private void EnterRecoveryTrial(RedoTrial trial)
    {
        currentBlock = trial.blockIndex;
        currentFormalTrial = trial.trialIndex;
        currentTrial = formalTrials[currentBlock][currentFormalTrial];
        uiController?.ShowAnswering();
        SetupTrial(currentTrial);
    }
    
    
    #endregion =========================================================================================================


    

    #region ==================================== Experiment Phase Control ==============================================
    
    
    // TODO：对接 training 模式下， training again 的 Button 调用的函数
    public void RestartTraining()
    {
        currentTrainingTrial = 0;
        currentTrial = trainingTrials[TrainingSequenceIndex][currentTrainingTrial];
        
        StopStimulusTimer();
        SetupTrial(currentTrial);
    }
    
    
    // TODO：开始正式测试的Button 绑定的函数
    public void FinishTraining()
    {
        stimuliRender.ReleaseForReinit();
        //clearEvent.Invoke();

        if (formalTrials == null)
        {
            uiController?.BeginLoading();
            StartCoroutine(StartFormalAfterLoadRoutine());
            return;
        }

        StartFormal();
    }

    private IEnumerator StartFormalAfterLoadRoutine()
    {
        yield return ParticipantInit(ParticipantID, false, true);
        if (!loadSuccess)
        {
            uiController?.SetStartBusy(false);
            yield break;
        }

        EnterTrial(0, 0, StudyPhase.Formal);
    }
    
    // 开始训练部分
    public void StartTraining()
    {
        currentRunMode = TrialRunMode.Normal;
        currentPhase = StudyPhase.Training;

        currentTrainingTrial = 0;
        currentTrial = trainingTrials[TrainingSequenceIndex][currentTrainingTrial];

        StopStimulusTimer();
        SetupTrial(currentTrial);
    }
    
    
    // 开始正式的实验
    public void StartFormal()
    {
        currentRunMode = TrialRunMode.Normal;
        currentPhase = StudyPhase.Formal;
        currentFormalTrial = 0;
        currentTrial = formalTrials[currentBlock][currentFormalTrial];
        
        StopStimulusTimer();
        SetupTrial(currentTrial);
    }
    

    #endregion =========================================================================================================


    

    #region ========================================= Trial Control ====================================================

    
    // 加载每一个 trial 的内容
    public void SetupTrial(TrialInfo currTrial)
    {
        StopStimulusTimer();
        RenderTrialAtCurrentHeadPose(currTrial);

        answerInput.RefreshTrialInformation(); // 同步这个Trial的信息

        // Training is self-paced: keep both point clouds visible until the
        // participant advances or leaves training. Formal and redo trials keep
        // the configured limited presentation time.
        if (currentPhase == StudyPhase.Training)
        {
            trialStartTime = -1f;
        }
        else
        {
            stimulusTimerCoroutine = StartCoroutine(HideStimulusAfterDelay());
        }
    }

    /// <summary>
    /// Moves an already-visible stimulus to the current participant's horizontal forward
    /// direction without restarting the trial timer or changing the answer state.
    /// </summary>
    public void RecenterVisibleStimulus()
    {
        if (stimuliRender == null || !stimuliRender.IsRendering ||
            (currentPhase != StudyPhase.Training &&
             currentPhase != StudyPhase.Formal &&
             currentPhase != StudyPhase.Redo))
        {
            return;
        }

        RenderTrialAtCurrentHeadPose(currentTrial);
    }

    private void RenderTrialAtCurrentHeadPose(TrialInfo currTrial)
    {
        // ===============================================================
        // 1. 计算用户当前水平 forward 和 right
        // ===============================================================

        Vector3 forward = Vector3.ProjectOnPlane(playerPosition.forward, Vector3.up).normalized;

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        Quaternion stimulusRotation = Quaternion.LookRotation(forward, Vector3.up);

        // ===============================================================
        // 2. 根据距离计算整个数据集的中心
        // ===============================================================

        float configuredDistance = Mathf.Abs(currTrial.Distance);
        float distance = configuredDistance > 0.01f
            ? configuredDistance
            : defaultStimulusDistance;

        Vector3 datasetCenter = playerPosition.position + forward * distance;

        Vector3[] allWorldPoints;
        if (currTrial.SourceLabel <= 0)
        {
            // 新正式刺激已在 CSV 中定义 label 1/2 的相对位置。
            // 先合并再整体变换，避免分别居中后破坏原始左右间距。
            Vector3[] rawPoints = MergeStimuli(currTrial.Stimulus1, currTrial.Stimulus2);
            allWorldPoints = TransformStimulusToWorld(rawPoints, datasetCenter, stimulusRotation);
        }
        else
        {
            // 训练刺激从同一个源点云生成两个密度版本，需要单独放到左右。
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            float halfAngleRad = clusterCenterAngleDeg * Mathf.Deg2Rad * 0.5f;
            float halfGap = Mathf.Tan(halfAngleRad) * distance;
            Vector3 stimulus1Center = datasetCenter - right * halfGap;
            Vector3 stimulus2Center = datasetCenter + right * halfGap;

            Vector3[] stimulus1World = TransformStimulusToWorld(
                currTrial.Stimulus1,
                stimulus1Center,
                stimulusRotation
            );
            Vector3[] stimulus2World = TransformStimulusToWorld(
                currTrial.Stimulus2,
                stimulus2Center,
                stimulusRotation
            );
            allWorldPoints = MergeStimuli(stimulus1World, stimulus2World);
        }

        // ===============================================================
        // 3. 只渲染一次
        // ===============================================================

        stimuliRender.Render(allWorldPoints); // 数据渲染
    }

    
    // TODO：对接 answerUI 中 Submit Button 点击后调用的函数
    public void OnTrialSubmit(int participantAnswer)
    {
        StopStimulusTimer();
        //TODO: 禁用 answerUI 中的交互

        if (currentPhase == StudyPhase.Training)
        {
            answerInput.ShowCorrect(currentTrial.CorrectAnswer);
        }
        
        // 输出答案
        if(currentPhase == StudyPhase.Formal || currentPhase == StudyPhase.Redo)
        {
            AnswerOutput(resultFolder, currentBlock, ParticipantID, currentFormalTrial, participantAnswer, currentTrial);
            // 重新配置
            NextTrial();
        }
        
        
        
        
    }
    
    
     // TODO：Training 模式下 Next 的 Button 绑定的函数
    public void NextTrial()
    {
        StopStimulusTimer();
        
        stimuliRender.ReleaseForReinit();

        if (currentPhase == StudyPhase.Training)
        {
            // 如果当前已经是最后一个 training trial，就不要再往后取
            if (currentTrainingTrial >= trainingCountPB - 1)
            {
                return;
            }
            else
            {
                currentTrainingTrial++;
                currentTrial = trainingTrials[TrainingSequenceIndex][currentTrainingTrial];
                
                SetupTrial(currentTrial);
            }
        }

        if (currentPhase == StudyPhase.Formal)
        {
            // 如果当前 formal block 已经结束
            if (currentFormalTrial >= trialCountPB - 1)
            {
                // 如果当前已经是最后一个 block，实验结束
                if (currentBlock >= blockCount - 1)
                {
                    currentPhase = StudyPhase.Finished;
                    
                    answerUI.SetActive(false);
                    stimuliRender.ReleaseForReinit();

                    ResultsValidator();
                    
                    endUI.SetActive(true); // TODO：同步数据扫描的信息, 结束且呈现一个 Summary
                    
                }
                else
                {
                    // Pause on the block summary. The researcher/participant explicitly
                    // starts the next block from the new Next Block button.
                    ShowCurrentBlockEnd();
                }
                
            }
            else
            {
                currentFormalTrial++;
                currentTrial = formalTrials[currentBlock][currentFormalTrial];
                SetupTrial(currentTrial);
            }
            
        }

        if (currentPhase == StudyPhase.Redo)
        {

            if (reTrialId + 1 >= redoList.Length)
            {
                FinishResumeStudy();
            }
            else if (redoList[reTrialId + 1].blockIndex != currentBlock)
            {
                // Resume only contains missing/invalid trials. Stop before entering
                // the next pending block so recovery uses the same block handoff as
                // a normal Formal run.
                ShowCurrentBlockEnd();
            }
            else
            {
                reTrialId++;
                EnterRecoveryTrial(redoList[reTrialId]);
            }
        }
    }

    public void StartNextBlock()
    {
        if (!HasNextBlock)
        {
            return;
        }

        if (currentPhase == StudyPhase.Redo)
        {
            reTrialId++;
            EnterRecoveryTrial(redoList[reTrialId]);
            return;
        }

        if (currentPhase != StudyPhase.Formal)
        {
            return;
        }

        int nextBlock = currentBlock + 1;
        EnterTrial(nextBlock, 0, StudyPhase.Formal);
    }

    private bool HasNextRecoveryBlock()
    {
        return redoList != null &&
               reTrialId >= 0 &&
               reTrialId + 1 < redoList.Length &&
               redoList[reTrialId + 1].blockIndex != currentBlock;
    }

    private void ShowCurrentBlockEnd()
    {
        if (uiController != null)
        {
            uiController.ShowBlockEnd(
                $"Block {currentBlock + 1} of {blockCount} is complete.");
        }
        else
        {
            answerUI.SetActive(false);
            endUI.SetActive(true);
        }
    }

    private void FinishResumeStudy()
    {
        stimuliRender.ReleaseForReinit();

        TryBuildRecoveryList(out List<RedoTrial> pendingTrials, out int validCount, out string error);
        redoList = pendingTrials != null ? pendingTrials.ToArray() : Array.Empty<RedoTrial>();

        uiController?.ShowDeveloperRecovery();
        startInput?.ShowDeveloperStatus(string.IsNullOrEmpty(error)
            ? "Recovery complete.\n" + BuildRecoveryStatus(validCount, redoList)
            : error);
    }

    // Todo：在实验过程中直接重新开当下的 trial 调用的函数（UI 或者手柄上特定的按键交互）
    public void ReloadTrial()
    {
        StopStimulusTimer();
        stimuliRender.ReleaseForReinit();
        
        SetupTrial(currentTrial);
    }

    public void HideCurrentStimulus()
    {
        StopStimulusTimer();
        stimuliRender?.ReleaseForReinit();
    }

    public void PauseForFormalRecovery()
    {
        if (currentPhase != StudyPhase.Formal && currentPhase != StudyPhase.Redo)
        {
            return;
        }

        StopStimulusTimer();
        stimuliRender?.ReleaseForReinit();
    }
    

    #endregion =========================================================================================================


    
    
    #region ======================================= Results Management =================================================
    
    
    // 实验结果记录
    private void AnswerOutput(string folderPath, int block, int participantID, int trialId, int pAnswer, TrialInfo trialInfo)
    {
        // ===============================================================
        // 1. 创建距离文件夹并定位参与者结果文件
        // ===============================================================

        string filePath = ResolveResultFilePath(folderPath, block, participantID, true);

        // ===============================================================
        // 2. 如果文件不存在，先写表头
        // ===============================================================

        if (!File.Exists(filePath))
        {
            string header =
                "TrialID," +
                "ParticipantAnswer," +
                "CorrectAnswer," +
                "Accuracy," +
                "ConditionID," +
                "RepetitionID," +
                "SceneID," +
                "Distance," +
                "DensityRatio\n";

            File.WriteAllText(filePath, header, Encoding.UTF8);
        }

        // ===============================================================
        // 3. 计算正确性
        // ===============================================================

        int accuracy = 0;

        if (pAnswer == trialInfo.CorrectAnswer)
        {
            accuracy = 1;
        }

        // ===============================================================
        // 4. 写入当前 trial 的一行结果
        // ===============================================================

        string line =
            trialId.ToString(CultureInfo.InvariantCulture) + "," +
            pAnswer.ToString(CultureInfo.InvariantCulture) + "," +
            trialInfo.CorrectAnswer.ToString(CultureInfo.InvariantCulture) + "," +
            accuracy.ToString(CultureInfo.InvariantCulture) + "," +
            trialInfo.ConditionID.ToString(CultureInfo.InvariantCulture) + "," +
            trialInfo.RepetitionID.ToString(CultureInfo.InvariantCulture) + "," +
            trialInfo.SceneID.ToString(CultureInfo.InvariantCulture) + "," +
            trialInfo.Distance.ToString(CultureInfo.InvariantCulture) + "," +
            trialInfo.DensityRatio.ToString(CultureInfo.InvariantCulture);

        UpdateTrialResult(filePath, trialId, line);
    }
    
    
    private void UpdateTrialResult(string filePath, int trialId, string newLine)
    {
        List<string> lines = new List<string>(File.ReadAllLines(filePath));
        lines.RemoveAll(string.IsNullOrWhiteSpace);
        newLine = newLine.TrimEnd('\r', '\n');
        
        bool replaced = false;

        // 从第1行开始，跳过header
        for (int i = 1; i < lines.Count; i++)
        {
            string[] cells = lines[i].Split(',');
            
            if (cells.Length > 0 && int.TryParse(cells[0], out int existingTrialID))
            {
                if (existingTrialID == trialId)
                {
                    // 找到同一个trial，直接覆盖
                    lines[i] = newLine;
                    replaced = true;
                    break;
                }
            }
        }


        // 如果没有找到，说明第一次写入
        if (!replaced)
        {
            lines.Add(newLine);
        }

        File.WriteAllLines(filePath, lines, Encoding.UTF8);
    }
    
    
    // 完成实验后检查数据有效性，是否有需要重做的需求
    private void ResultsValidator()
    {
        redoUI.SetActive(false);

        if (!TryBuildRecoveryList(out List<RedoTrial> pendingTrials, out _, out string error))
        {
            redoList = Array.Empty<RedoTrial>();
            ShowWarning(error);
            return;
        }

        redoList = pendingTrials.ToArray();
    }

    private bool TryBuildRecoveryList(out List<RedoTrial> pendingTrials, out int validCount, out string error)
    {
        pendingTrials = new List<RedoTrial>();
        validCount = 0;
        error = "";
        bool foundAnyResultFile = false;
        bool foundLegacyResultFile = false;

        for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            string filePath = ResolveResultFilePath(resultFolder, blockIndex, ParticipantID, false);
            bool[] validTrials = new bool[trialCountPB];

            if (File.Exists(filePath))
            {
                foundAnyResultFile = true;
                string[] lines = File.ReadAllLines(filePath);
                for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
                {
                    if (string.IsNullOrWhiteSpace(lines[lineIndex]))
                    {
                        continue;
                    }

                    string[] cells = lines[lineIndex].Split(',');
                    if (cells.Length == 0 ||
                        !int.TryParse(cells[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int trialId) ||
                        trialId < 0 || trialId >= trialCountPB)
                    {
                        continue;
                    }

                    validTrials[trialId] = IsValidResultRow(
                        cells,
                        formalTrials[blockIndex][trialId]);
                }
            }
            else if (LegacyResultFileExists(resultFolder, blockIndex, ParticipantID))
            {
                foundLegacyResultFile = true;
            }

            for (int trialIndex = 0; trialIndex < trialCountPB; trialIndex++)
            {
                if (validTrials[trialIndex])
                {
                    validCount++;
                }
                else
                {
                    pendingTrials.Add(new RedoTrial { blockIndex = blockIndex, trialIndex = trialIndex });
                }
            }
        }

        if (!foundAnyResultFile)
        {
            error = foundLegacyResultFile
                ? "Only legacy 20-trial results were found for this participant.\n" +
                  "Start a new Formal study for the current 240-trial protocol."
                : "No saved study was found for this participant.";
            pendingTrials.Clear();
            validCount = 0;
            return false;
        }

        return true;
    }

    private static bool IsValidResultRow(string[] cells, TrialInfo expectedTrial)
    {
        if (cells.Length < 9)
        {
            return false;
        }

        for (int index = 1; index < cells.Length; index++)
        {
            if (cells[index].IndexOf("NaN", StringComparison.OrdinalIgnoreCase) >= 0 ||
                cells[index].IndexOf("Infinity", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }
        }

        if (!int.TryParse(cells[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int participantAnswer) ||
            (participantAnswer != 1 && participantAnswer != 2) ||
            !int.TryParse(cells[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int correctAnswer) ||
            !int.TryParse(cells[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int accuracy) ||
            !int.TryParse(cells[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int conditionId) ||
            !int.TryParse(cells[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out int repetitionId) ||
            !int.TryParse(cells[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out int sceneId) ||
            !float.TryParse(cells[7], NumberStyles.Float, CultureInfo.InvariantCulture, out float distance) ||
            !float.TryParse(cells[8], NumberStyles.Float, CultureInfo.InvariantCulture, out float densityRatio))
        {
            return false;
        }

        int expectedAccuracy = participantAnswer == correctAnswer ? 1 : 0;
        return correctAnswer == expectedTrial.CorrectAnswer &&
               accuracy == expectedAccuracy &&
               conditionId == expectedTrial.ConditionID &&
               repetitionId == expectedTrial.RepetitionID &&
               sceneId == expectedTrial.SceneID &&
               Mathf.Approximately(distance, expectedTrial.Distance) &&
               Mathf.Approximately(densityRatio, expectedTrial.DensityRatio);
    }

    private string BuildRecoveryStatus(int validCount, IReadOnlyList<RedoTrial> pendingTrials)
    {
        int totalTrials = blockCount * trialCountPB;
        int remainingCount = pendingTrials?.Count ?? Mathf.Max(0, totalTrials - validCount);
        return $"Completed: {validCount} / {totalTrials}\nRemaining: {remainingCount}";
    }

    private static string FormatTrialNumbers(IReadOnlyList<RedoTrial> pendingTrials)
    {
        StringBuilder result = new StringBuilder();
        int index = 0;
        while (index < pendingTrials.Count)
        {
            int rangeStart = pendingTrials[index].trialIndex + 1;
            int rangeEnd = rangeStart;
            int next = index + 1;
            while (next < pendingTrials.Count &&
                   pendingTrials[next].trialIndex == pendingTrials[next - 1].trialIndex + 1)
            {
                rangeEnd = pendingTrials[next].trialIndex + 1;
                next++;
            }

            if (result.Length > 0) result.Append(", ");
            if (rangeEnd - rangeStart >= 2)
            {
                result.Append(rangeStart).Append('-').Append(rangeEnd);
            }
            else
            {
                result.Append(rangeStart);
                if (rangeEnd > rangeStart) result.Append(", ").Append(rangeEnd);
            }

            index = next;
        }

        return result.ToString();
    }
    
    
    #endregion =========================================================================================================
    
    
    
    
    #region ==================================== Study Preparation Pipeline ============================================
    
    
    // 初始化所有相关的路径
    private void BuildPaths(int participantId)
    {
        string trainingConfigPath = Application.streamingAssetsPath + "/Config" + "/Training";
        trainingConfig = new ConfigInfo();
        trainingConfig.stimuliFolder = trainingConfigPath + "/Stimuli";
        trainingConfig.participantCsv = trainingConfigPath+ "/Training_Participant.csv";
        trainingConfig.orderListCsv = trainingConfigPath+ "/Training_OrderList.csv";
        trainingConfig.sceneCsv = trainingConfigPath+ "/Training_Scene.csv";
        
        
        string formalConfigPath = Application.streamingAssetsPath + "/Config" + "/Formal";
        formalConfig = new ConfigInfo();
        formalConfig.stimuliFolder = formalConfigPath + "/Stimuli";
        formalConfig.participantCsv = formalConfigPath + "/Formal_Participant.csv";
        formalConfig.orderListCsv = formalConfigPath + "/Formal_OrderList.csv";
        formalConfig.sceneCsv = formalConfigPath + "/Formal_Scene.csv";

        resultFolder = Application.persistentDataPath + "/Results";
        if (!Directory.Exists(resultFolder)) { Directory.CreateDirectory(resultFolder); }
       
        
        
        if (debugMode)
        {
            // TODO: Log 文档
        }
    }

    
    // 根据用户 ID 初始化
    public IEnumerator ParticipantInit(int participantId)
    {
        yield return ParticipantInit(participantId, true, true);
    }

    private IEnumerator ParticipantInit(int participantId, bool loadTraining, bool loadFormal)
    {
        //TODO：在startUI中interactable禁掉
        
        ParticipantID = participantId;

        BuildPaths(participantId);

        loadSuccess = true;

        // ===============================================================
        // 初始化 trainingTrials
        // ===============================================================
        
        if (loadTraining)
        {
            trainingTrials = new TrialInfo[1][];
            trainingTrials[TrainingSequenceIndex] = new TrialInfo[trainingCountPB];

            // Training is one sequence of trials, independent of the four formal blocks.
            yield return LoadTrainingTrialOrder(
                participantId,
                trainingConfig.participantCsv,
                trainingConfig.orderListCsv,
                trainingTrials[TrainingSequenceIndex]);
            if (!loadSuccess) { yield break; }
            yield return LoadTrialInfos(trainingConfig.sceneCsv, trainingTrials);
            if (!loadSuccess) { yield break; }
            yield return LoadStimuli(trainingConfig.stimuliFolder, trainingTrials);
            if (!loadSuccess) { yield break; }
        }
        


        // ===============================================================
        // 初始化 formalTrials
        // ===============================================================

        if (loadFormal)
        {
            formalTrials = new TrialInfo[blockCount][];
            for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
            {
                formalTrials[blockIndex] = new TrialInfo[trialCountPB];
            }

            // 加载对应的 formal 信息
            yield return LoadTrialOrder(participantId, formalConfig.participantCsv, formalConfig.orderListCsv, formalTrials);
            if (!loadSuccess) { yield break; }
            yield return LoadTrialInfos(formalConfig.sceneCsv, formalTrials);
            if (!loadSuccess) { yield break; }
            ConfigureResultFolderNames(formalTrials);
            if (!loadSuccess) { yield break; }
            yield return LoadStimuli(formalConfig.stimuliFolder, formalTrials);
            if (!loadSuccess) { yield break; }
        }
        
        
        
    }

    private void ConfigureResultFolderNames(TrialInfo[][] trialInfos)
    {
        HashSet<string> usedFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int blockIndex = 0; blockIndex < trialInfos.Length; blockIndex++)
        {
            if (trialInfos[blockIndex] == null || trialInfos[blockIndex].Length == 0)
            {
                ShowWarning($"Block {blockIndex + 1} does not contain any formal trials.");
                loadSuccess = false;
                return;
            }

            float blockDistance = trialInfos[blockIndex][0].Distance;
            for (int trialIndex = 1; trialIndex < trialInfos[blockIndex].Length; trialIndex++)
            {
                if (!Mathf.Approximately(trialInfos[blockIndex][trialIndex].Distance, blockDistance))
                {
                    ShowWarning($"Block {blockIndex + 1} contains more than one Distance and cannot use a single result folder.");
                    loadSuccess = false;
                    return;
                }
            }

            string folderName = "Distance_" + Mathf.Abs(blockDistance).ToString("0.###", CultureInfo.InvariantCulture);
            if (!usedFolderNames.Add(folderName))
            {
                ShowWarning($"More than one formal block uses {folderName}. Each block must use a unique Distance.");
                loadSuccess = false;
                return;
            }

            blockFolderName[blockIndex] = folderName;
        }
    }

    private string ResolveResultFilePath(string folderPath, int blockIndex, int participantId, bool createFolder)
    {
        string participantFileName = "P_" + participantId + ".csv";
        string preferredFolder = folderPath + "/" + blockFolderName[blockIndex];
        string preferredFile = preferredFolder + "/" + participantFileName;

        // Do not fall back to the legacy DensityJND_Block* folders. Those files
        // belong to the earlier 20-trial protocol and must not be interpreted as
        // progress for the current 240-trial, distance-based protocol.

        if (createFolder && !Directory.Exists(preferredFolder))
        {
            Directory.CreateDirectory(preferredFolder);
        }

        return preferredFile;
    }

    private static bool LegacyResultFileExists(string folderPath, int blockIndex, int participantId)
    {
        string legacyFile = folderPath + "/DensityJND_Block" + (blockIndex + 1) +
                            "/P_" + participantId + ".csv";
        return File.Exists(legacyFile);
    }


    // Training has one participant-specific trial sequence and no block dimension.
    private IEnumerator LoadTrainingTrialOrder(
        int participantId,
        string participantCsv,
        string orderCsv,
        TrialInfo[] trialInfos)
    {
        string participantText;
        string orderText;

        using (UnityWebRequest request = UnityWebRequest.Get(ToRequestUrl(participantCsv)))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                ShowWarning($"Unable to load {Path.GetFileName(participantCsv)}.\nPlease add the missing configuration file and try again.");
                loadSuccess = false;
                yield break;
            }
            participantText = request.downloadHandler.text;
        }

        using (UnityWebRequest request = UnityWebRequest.Get(ToRequestUrl(orderCsv)))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                ShowWarning($"Unable to load {Path.GetFileName(orderCsv)}.\nPlease add the missing configuration file and try again.");
                loadSuccess = false;
                yield break;
            }
            orderText = request.downloadHandler.text;
        }

        string[] participantLines = participantText.Split('\n');
        string[] participantHeader = participantLines[0].Trim().Split(',');
        if (!ValidateColumns(
                participantHeader,
                Path.GetFileName(participantCsv),
                "ParticipantID"))
        {
            yield break;
        }

        int participantIdColumn = FindColumnIndex(participantHeader, "ParticipantID");
        int trainingOrderColumn = FindColumnIndex(participantHeader, "Training_Order");
        if (trainingOrderColumn < 0)
        {
            // The final participant configuration uses the same block/order
            // schema for training and formal data. Training runs once before
            // formal testing, so it follows the participant's first block order.
            trainingOrderColumn = FindColumnIndex(participantHeader, "Block1_Order");
        }

        if (trainingOrderColumn < 0)
        {
            ShowWarning($"{Path.GetFileName(participantCsv)} is missing the required column 'Block1_Order'.");
            loadSuccess = false;
            yield break;
        }

        int trainingOrderId = -1;

        for (int lineIndex = 1; lineIndex < participantLines.Length; lineIndex++)
        {
            string line = participantLines[lineIndex].Trim();
            if (line == "") continue;

            string[] cells = line.Split(',');
            if (int.Parse(cells[participantIdColumn].Trim()) == participantId)
            {
                trainingOrderId = int.Parse(cells[trainingOrderColumn].Trim());
                break;
            }
        }

        if (trainingOrderId < 0)
        {
            ShowWarning("Participant ID not found.\nPlease check the entered participant ID.");
            loadSuccess = false;
            yield break;
        }

        string[] orderLines = orderText.Split('\n');
        for (int lineIndex = 1; lineIndex < orderLines.Length; lineIndex++)
        {
            string line = orderLines[lineIndex].Trim();
            if (line == "") continue;

            string[] cells = line.Split(',');
            if (int.Parse(cells[0].Trim()) != trainingOrderId) continue;

            if (cells.Length < trialInfos.Length + 1)
            {
                ShowWarning($"{Path.GetFileName(orderCsv)} does not contain all {trialInfos.Length} training trials.");
                loadSuccess = false;
                yield break;
            }

            for (int trialIndex = 0; trialIndex < trialInfos.Length; trialIndex++)
            {
                trialInfos[trialIndex].SceneID = int.Parse(cells[trialIndex + 1].Trim());
            }
            yield break;
        }

        ShowWarning("Training order configuration not found.\nPlease check the Training_OrderList CSV.");
        loadSuccess = false;
    }

    // 根据参与者的 Id 读取正式实验 block 顺序
    private IEnumerator LoadTrialOrder(
        int participantId,
        string participantCsv,
        string orderCsv,
        TrialInfo[][] trialInfos,
        bool repeatFirstBlockOrder = false)
    {
        string participantText = "";
        string orderText = "";

        // 读取 Participant CSV
        using (UnityWebRequest request = UnityWebRequest.Get(ToRequestUrl(participantCsv)))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                ShowWarning($"Unable to load {Path.GetFileName(participantCsv)}.\nPlease add the missing configuration file and try again.");
                loadSuccess = false;
                yield break;
            }

            participantText = request.downloadHandler.text;
        }

        // 读取 OrderList CSV
        using (UnityWebRequest request = UnityWebRequest.Get(ToRequestUrl(orderCsv)))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                ShowWarning($"Unable to load {Path.GetFileName(orderCsv)}.\nPlease add the missing configuration file and try again.");
                loadSuccess = false;
                yield break;
            }

            orderText = request.downloadHandler.text;
        }

        // ===============================================================
        // 1. 从 Participant CSV 找到该参与者这一行，并读取每个 block 的 orderID
        // ===============================================================

        string[] participantLines = participantText.Split('\n');
        string[] participantHeader = participantLines[0].Trim().Split(',');

        if (!ValidateColumns(participantHeader, Path.GetFileName(participantCsv), "ParticipantID"))
        {
            yield break;
        }

        int participantIdColumn = FindColumnIndex(participantHeader, "ParticipantID");


        bool findParticipant = false;
        int[] blockOrders = null;
        for (int i = 1; i < participantLines.Length; i++)
        {
            string line = participantLines[i].Trim();
            
            if (line != "")
            {
                string[] cells = line.Split(',');

                int rowParticipantID = int.Parse(cells[participantIdColumn].Trim());

                if (rowParticipantID == participantId)
                {
                    findParticipant = true;
                    blockOrders = new int[trialInfos.Length];

                    int firstBlockOrderColumn = FindColumnIndex(participantHeader, "Block1_Order");
                    for (int blockIndex = 1; blockIndex <= trialInfos.Length; blockIndex++)
                    {
                        string blockOrderColumnName = "Block" + blockIndex + "_Order";
                        int blockOrderColumn = FindColumnIndex(participantHeader, blockOrderColumnName);

                        if (blockOrderColumn < 0 && repeatFirstBlockOrder)
                        {
                            blockOrderColumn = firstBlockOrderColumn;
                        }

                        if (blockOrderColumn < 0)
                        {
                            ShowWarning($"{Path.GetFileName(participantCsv)} is missing the required column '{blockOrderColumnName}'.");
                            loadSuccess = false;
                            yield break;
                        }

                        blockOrders[blockIndex - 1] = int.Parse(cells[blockOrderColumn].Trim());
                    }

                    break;
                }
            }
        }
        // 如果文件读完都没有找到对应的用户序号 -> 提示 ParticipantID 输错了
        if (findParticipant == false)
        {
            // TODO: Log 文档
            ShowWarning("Participant ID not found.\nPlease check the entered participant ID.");
            
            loadSuccess = false;
            yield break;
        }

        // ===============================================================
        // 2. 根据每个 block 的 orderID，从 OrderList CSV 读取 SceneID 顺序
        // ===============================================================

        string[] orderLines = orderText.Split('\n');
        
        // 逐个 block 读取对应的顺序
        for (int blockIndex = 0; blockIndex < trialInfos.Length; blockIndex++)
        {
            int orderID = blockOrders[blockIndex];
            
            bool foundOrder = false;
            
            for (int i = 1; i < orderLines.Length; i++)
            {
                string line = orderLines[i].Trim();

                if (line != "")
                {
                    string[] cells = line.Split(',');
                    
                    int rowOrderID = int.Parse(cells[0].Trim());
                    
                    if (rowOrderID == orderID)
                    {
                        foundOrder = true;
                        
                        for (int j = 0; j < trialInfos[blockIndex].Length; j++)
                        {
                            trialInfos[blockIndex][j].SceneID = int.Parse(cells[j + 1].Trim());
                        }

                        break;
                    }
                }
            }

            // 如果 OrderList 里没有找到这个 orderID -> 配置文件有问题
            if (!foundOrder)
            {
                // TODO: Log 文档
                
                ShowWarning("Trial order configuration not found.\nPlease check the OrderList CSV and participant assignment.");
                
                loadSuccess = false;
                yield break;
            }
        }
    }
    

    // 根据 SceneID 读取具体的 trial 信息
    private IEnumerator LoadTrialInfos(string sceneCsv, TrialInfo[][] trialInfos)
    {
        string sceneText = "";

        // 读取 Scene CSV
        using (UnityWebRequest request = UnityWebRequest.Get(ToRequestUrl(sceneCsv)))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                // TODO: Log 文档
                
                ShowWarning("Unable to load scene information.\nPlease check the Scene CSV path.");
                
                loadSuccess = false;
                yield break;
            }

            sceneText = request.downloadHandler.text;
        }

        string[] sceneLines = sceneText.Split('\n');

        // 按表头找列
        string[] sceneHeader = sceneLines[0].Trim().Split(',');

        // SceneID and RepetitionID are supported when present, but remain optional
        // so both formal scene-table variants can use the same loader.
        if (!ValidateColumns(
                sceneHeader,
                Path.GetFileName(sceneCsv),
                "ConditionID",
                "DatasetName",
                "Distance",
                "DensityRatio",
                "CorrectAnswer"))
        {
            yield break;
        }

        int sceneIdColumn = FindColumnIndex(sceneHeader, "SceneID");
        int conditionIdColumn = FindColumnIndex(sceneHeader, "ConditionID");
        int repetitionIdColumn = FindColumnIndex(sceneHeader, "RepetitionID");
        int datasetNameColumn = FindColumnIndex(sceneHeader, "DatasetName");
        int sourceLabelColumn = FindColumnIndex(sceneHeader, "SourceLabel");
        int samplingSeedColumn = FindColumnIndex(sceneHeader, "SamplingSeed");
        int distanceColumn = FindColumnIndex(sceneHeader, "Distance");
        int densityRatioColumn = FindColumnIndex(sceneHeader, "DensityRatio");
        int correctAnswerColumn = FindColumnIndex(sceneHeader, "CorrectAnswer");

        // 遍历每个 block
        for (int blockIndex = 0; blockIndex < trialInfos.Length; blockIndex++)
        {
            // 遍历当前 block 里的每个 trial
            for (int trialIndex = 0; trialIndex < trialInfos[blockIndex].Length; trialIndex++)
            {
                int targetSceneID = trialInfos[blockIndex][trialIndex].SceneID;
                bool foundScene = false;

                // 从第 1 行开始读，因为第 0 行是表头
                for (int i = 1; i < sceneLines.Length; i++)
                {
                    string line = sceneLines[i].Trim();

                    if (line != "")
                    {
                        string[] cells = line.Split(',');

                        // Without an explicit SceneID column, the 1-based data-row
                        // number is the scene ID used by Formal_OrderList.csv.
                        int sceneID = sceneIdColumn >= 0
                            ? int.Parse(cells[sceneIdColumn].Trim())
                            : i;

                        if (sceneID == targetSceneID)
                        {
                            TrialInfo trial = trialInfos[blockIndex][trialIndex];

                            trial.SceneID = sceneID;
                            trial.ConditionID = int.Parse(cells[conditionIdColumn].Trim());
                            trial.RepetitionID = repetitionIdColumn >= 0
                                ? int.Parse(cells[repetitionIdColumn].Trim())
                                : 0;
                            // SourceLabel 存在时，从单个源点云生成稀疏对（训练格式）。
                            // 不存在时，CSV 中的 label 1/2 已经是要直接显示的左右刺激（新正式格式）。
                            trial.SourceLabel = sourceLabelColumn >= 0
                                ? int.Parse(cells[sourceLabelColumn].Trim())
                                : 0;
                            trial.SamplingSeed = samplingSeedColumn >= 0
                                ? int.Parse(cells[samplingSeedColumn].Trim())
                                : trial.ConditionID;
                            trial.StimuliName = cells[datasetNameColumn].Trim();
                            trial.Distance = float.Parse(cells[distanceColumn].Trim(), CultureInfo.InvariantCulture);
                            trial.DensityRatio = float.Parse(cells[densityRatioColumn].Trim(), CultureInfo.InvariantCulture);
                            trial.CorrectAnswer = int.Parse(cells[correctAnswerColumn].Trim());

                            trialInfos[blockIndex][trialIndex] = trial;

                            foundScene = true;
                            break;
                        }
                    }
                }

                if (!foundScene)
                {
                    // TODO: Log 文档
                
                    ShowWarning("Scene information not found.\nPlease check the Scene CSV and trial order configuration.");

                    loadSuccess = false;
                    yield break;
                }
            }
        }
    }


    // 根据 StimuliName 读取具体的点数据
    private IEnumerator LoadStimuli(
        string stimuliFolder,
        TrialInfo[][] trialInfos)
    {
        Dictionary<string, string> stimulusTextCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for(int blockIndex =  0; blockIndex < trialInfos.Length; blockIndex++)
        {
            for (int trialIndex = 0; trialIndex < trialInfos[blockIndex].Length; trialIndex++)
            {
                TrialInfo trial = trialInfos[blockIndex][trialIndex];

                string stimuliFileName = GetStimulusFileName(trial.StimuliName);
                string stimuliPath = stimuliFolder + "/" + stimuliFileName;
                string stimuliText = "";

                // Multiple conditions can share one source dataset. Read it once and
                // reuse the text while deterministic sampling creates each trial.
                if (!stimulusTextCache.TryGetValue(stimuliFileName, out stimuliText))
                {
                    using (UnityWebRequest request = UnityWebRequest.Get(ToRequestUrl(stimuliPath)))
                    {
                        yield return request.SendWebRequest();

                        if (request.result != UnityWebRequest.Result.Success)
                        {
                            ShowWarning($"Unable to load {stimuliFileName}.\nPlease check the Formal/Stimuli folder and DatasetName.");
                            loadSuccess = false;
                            yield break;
                        }
                        else
                        {
                            stimuliText = request.downloadHandler.text;
                            stimulusTextCache[stimuliFileName] = stimuliText;
                        }
                    }
                }

                string[] lines = stimuliText.Split('\n');
                string[] header = lines[0].Trim().Split(',');

                if (!ValidateColumns(header, Path.GetFileName(stimuliPath), "x", "y", "z", "label"))
                {
                    yield break;
                }

                int xColumn = FindColumnIndex(header, "x");
                int yColumn = FindColumnIndex(header, "y");
                int zColumn = FindColumnIndex(header, "z");
                int labelColumn = FindColumnIndex(header, "label");

                Dictionary<int, List<Vector3>> pointsByLabel = new Dictionary<int, List<Vector3>>();

                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();

                    if (line == "")
                    {
                        break;
                    }

                    string[] cells = line.Split(',');

                    float x = float.Parse(cells[xColumn].Trim(), CultureInfo.InvariantCulture);
                    float y = float.Parse(cells[yColumn].Trim(), CultureInfo.InvariantCulture);
                    float z = float.Parse(cells[zColumn].Trim(), CultureInfo.InvariantCulture);
                    int label = int.Parse(cells[labelColumn].Trim());

                    Vector3 point = new Vector3(x, y, z);

                    if (!pointsByLabel.TryGetValue(label, out List<Vector3> labelPoints))
                    {
                        labelPoints = new List<Vector3>();
                        pointsByLabel.Add(label, labelPoints);
                    }

                    labelPoints.Add(point);
                }

                if (trial.SourceLabel > 0)
                {
                    if (!pointsByLabel.TryGetValue(trial.SourceLabel, out List<Vector3> sourcePoints) ||
                        sourcePoints.Count == 0)
                    {
                        ShowWarning($"{stimuliFileName} does not contain label {trial.SourceLabel} for condition {trial.ConditionID}.");
                        loadSuccess = false;
                        yield break;
                    }

                    Vector3[] denseStimulus = sourcePoints.ToArray();
                    Vector3[] sparseStimulus = CreateDensitySubset(
                        denseStimulus,
                        trial.DensityRatio,
                        trial.SamplingSeed);

                    if (trial.CorrectAnswer == 1)
                    {
                        trial.Stimulus1 = denseStimulus;
                        trial.Stimulus2 = sparseStimulus;
                    }
                    else if (trial.CorrectAnswer == 2)
                    {
                        trial.Stimulus1 = sparseStimulus;
                        trial.Stimulus2 = denseStimulus;
                    }
                    else
                    {
                        ShowWarning($"Condition {trial.ConditionID} has an invalid CorrectAnswer. Use 1 for Greater or 2 for Smaller.");
                        loadSuccess = false;
                        yield break;
                    }
                }
                else
                {
                    if (!pointsByLabel.TryGetValue(1, out List<Vector3> stimulus1) ||
                        !pointsByLabel.TryGetValue(2, out List<Vector3> stimulus2))
                    {
                        ShowWarning($"{stimuliFileName} must contain both label 1 and label 2.");
                        loadSuccess = false;
                        yield break;
                    }

                    trial.Stimulus1 = stimulus1.ToArray();
                    trial.Stimulus2 = stimulus2.ToArray();
                }

                trialInfos[blockIndex][trialIndex] = trial;
            }
        }
    }

    private Vector3[] CreateDensitySubset(Vector3[] source, float densityDifference, int seed)
    {
        float clampedDifference = Mathf.Clamp01(densityDifference);
        int keepCount = Mathf.Clamp(
            Mathf.RoundToInt(source.Length * (1f - clampedDifference)),
            1,
            source.Length);

        int[] indices = new int[source.Length];
        for (int i = 0; i < indices.Length; i++)
        {
            indices[i] = i;
        }

        System.Random random = new System.Random(seed);
        Vector3[] subset = new Vector3[keepCount];

        // Partial Fisher-Yates shuffle: deterministic for a condition while avoiding
        // a spatial bias from simply taking the first rows of the CSV.
        for (int i = 0; i < keepCount; i++)
        {
            int swapIndex = random.Next(i, indices.Length);
            int temp = indices[i];
            indices[i] = indices[swapIndex];
            indices[swapIndex] = temp;
            subset[i] = source[indices[i]];
        }

        return subset;
    }

    private string GetDatasetFileName(string sceneTitle)
    {
        const string marker = "Dataset";
        int markerIndex = sceneTitle.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

        if (markerIndex < 0)
        {
            return sceneTitle;
        }

        int endIndex = markerIndex + marker.Length;
        while (endIndex < sceneTitle.Length && char.IsDigit(sceneTitle[endIndex]))
        {
            endIndex++;
        }

        return endIndex > markerIndex + marker.Length
            ? sceneTitle.Substring(markerIndex, endIndex - markerIndex)
            : sceneTitle;
    }

    private string GetStimulusFileName(string sceneTitle)
    {
        string trimmedTitle = sceneTitle.Trim();
        if (trimmedTitle.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return trimmedTitle;
        }

        return GetDatasetFileName(trimmedTitle) + ".csv";
    }


    // Csv 读取工具函数
    private string ToRequestUrl(string path)
    {
        // StreamingAssets is a normal filesystem path in the Editor/desktop build,
        // but UnityWebRequest requires a file:// URL there. Android already returns
        // a jar:file:// URL, so leave any existing URL scheme unchanged.
        return path.Contains("://") ? path : new Uri(path).AbsoluteUri;
    }

    private int FindColumnIndex(string[] header, string columnName)
    {
        for (int i = 0; i < header.Length; i++)
        {
            if (header[i].Trim() == columnName)
            {
                return i;
            }
        }

        return -1;
    }

    private bool ValidateColumns(string[] header, string fileName, params string[] requiredColumns)
    {
        foreach (string columnName in requiredColumns)
        {
            if (FindColumnIndex(header, columnName) >= 0)
            {
                continue;
            }

            ShowWarning($"{fileName} is missing the required column '{columnName}'.");
            loadSuccess = false;
            return false;
        }

        return true;
    }

    
    #endregion =========================================================================================================


    private void ShowWarning(string message)
    {
        if (uiController != null)
        {
            uiController.ShowWarning(message);
        }
        else if (warningUI != null)
        {
            warningUI.SetActive(true);
        }

        Debug.LogWarning(message);
    }




    #region ======================================= Stimuli Management =================================================
    
    
    private Vector3[] TransformStimulusToWorld(Vector3[] rawPoints, Vector3 targetCenter, Quaternion rotation)
    {
        Vector3 rawCenter = GetPointCenter(rawPoints);

        Vector3[] worldPoints = new Vector3[rawPoints.Length];

        for (int i = 0; i < rawPoints.Length; i++)
        {
            Vector3 localPoint = rawPoints[i] - rawCenter;
            worldPoints[i] = targetCenter + rotation * localPoint;
        }

        return worldPoints;
    }


    private Vector3[] MergeStimuli(Vector3[] stimulus1, Vector3[] stimulus2)
    {
        Vector3[] merged = new Vector3[stimulus1.Length + stimulus2.Length];

        for (int i = 0; i < stimulus1.Length; i++)
        {
            merged[i] = stimulus1[i];
        }

        for (int i = 0; i < stimulus2.Length; i++)
        {
            merged[stimulus1.Length + i] = stimulus2[i];
        }

        return merged;
    }


    private Vector3 GetPointCenter(Vector3[] points)
    {
        Vector3 center = Vector3.zero;

        for (int i = 0; i < points.Length; i++)
        {
            center += points[i];
        }

        center /= points.Length;

        return center;
    }
    
    
    private IEnumerator HideStimulusAfterDelay()
    {
        // 记录当前 Trial 开始时间
        trialStartTime = Time.time;
        
        yield return new WaitForSeconds(stimulusVisibleSeconds);

        if(currentPhase == StudyPhase.Training || currentPhase == StudyPhase.Formal || currentPhase == StudyPhase.Redo)
        {
            stimuliRender.ReleaseForReinit();
        }
        
        trialStartTime = -1f;
        stimulusTimerCoroutine = null;
    }


    private void StopStimulusTimer()
    {
        if (stimulusTimerCoroutine != null)
        {
            StopCoroutine(stimulusTimerCoroutine);
            stimulusTimerCoroutine = null;
            trialStartTime = -1f;
        }
    }

    
    #endregion =========================================================================================================
    
    
}
