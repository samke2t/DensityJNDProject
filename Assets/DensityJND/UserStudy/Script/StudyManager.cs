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

    #region =============== Data Definition ===============

    public enum StudyPhase
    {
        Idle,
        Training,
        Formal,
        Finished,
        Redo
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
    public float trialStartTime;               // 可用 stimulusVisibleSeconds - trialStartTime 得到倒计时

    public bool IsLastTrainingTrial =>
        currentPhase == StudyPhase.Training && currentTrainingTrial >= trainingCountPB - 1;
    public int BlockCount => blockCount;
    
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
    public string[] blockFolderName;           // 每个 block 的文件名设置

    
    // 该参与者实验程序中使用的所有 Trial Stimuli 信息的列表
    public TrialInfo[][] trainingTrials;       // 按该用户的训练顺序记录的所有 trial 的具体信息
    public TrialInfo[][] formalTrials;         // 按该用户的实验顺序记录的所有 trial 的具体信息
    public RedoTrial[] redoList;               // 数据有问题，重做的列表
    
    
    // 正在运行的 Trial 的相关信息
    private Vector3 stimuliOrigin;                     // 刺激渲染的中点
    private TrialInfo currentTrial;                    // 当下的trial 的信息
    private Coroutine stimulusTimerCoroutine;          // 正在运行的倒计时任务
    
    
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
        
        
        
        trialCountPB = 32; // Formal_OrderList / Formal_Scene 当前各包含 32 个 trial
        trainingCountPB = 2; // TODO: 按实际情况修改

        blockCount = 1; // TODO：按实际情况修改
        blockFolderName = new String[blockCount];
        blockFolderName[0] = "DensityJND_Equal"; // TODO：按实际情况修改

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

    private void EnterTrial(int blockId, int trialId, StudyPhase phase)
    {
        currentBlock = blockId;
        currentPhase = phase;

        if (phase == StudyPhase.Training)
        {
            currentTrainingTrial = trialId;
            currentTrial = trainingTrials[currentBlock][currentTrainingTrial];
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

        currentPhase = StudyPhase.Redo;

        endUI.SetActive(false);
        answerUI.SetActive(true);

        reTrialId = 0;

        currentBlock = redoList[reTrialId].blockIndex;
        currentFormalTrial = redoList[reTrialId].trialIndex;

        currentTrial = formalTrials[currentBlock][currentFormalTrial];

        SetupTrial(currentTrial);
    }
    
    
    #endregion =========================================================================================================


    

    #region ==================================== Experiment Phase Control ==============================================
    
    
    // TODO：对接 training 模式下， training again 的 Button 调用的函数
    public void RestartTraining()
    {
        currentTrainingTrial = 0;
        currentTrial = trainingTrials[currentBlock][currentTrainingTrial];
        
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

        EnterTrial(currentBlock, 0, StudyPhase.Formal);
    }
    
    // 开始训练部分
    public void StartTraining()
    {
        currentPhase = StudyPhase.Training;
        
        currentTrainingTrial = 0;
        currentTrial = trainingTrials[currentBlock][currentTrainingTrial];

        StopStimulusTimer();
        SetupTrial(currentTrial);
    }
    
    
    // 开始正式的实验
    public void StartFormal()
    {
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
        // ===============================================================
        // 1. 计算用户当前水平 forward 和 right
        // ===============================================================

        Vector3 forward = Vector3.ProjectOnPlane(playerPosition.forward, Vector3.up).normalized;

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        Quaternion stimulusRotation = Quaternion.LookRotation(forward, Vector3.up);

        // ===============================================================
        // 2. 根据距离和视角夹角计算两个团中心
        // ===============================================================

        float configuredDistance = Mathf.Abs(currTrial.Distance);
        float distance = configuredDistance > 0.01f
            ? configuredDistance
            : defaultStimulusDistance;

        Vector3 datasetCenter = playerPosition.position + forward * distance;

        float halfAngleRad = clusterCenterAngleDeg * Mathf.Deg2Rad * 0.5f;
        float halfGap = Mathf.Tan(halfAngleRad) * distance;

        Vector3 stimulus1Center = datasetCenter - right * halfGap; // label 1 在左边
        Vector3 stimulus2Center = datasetCenter + right * halfGap; // label 2 在右边

        // ===============================================================
        // 3. 分别变换两个团
        // ===============================================================

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

        // ===============================================================
        // 4. 合并成一个 Vector3[]
        // ===============================================================

        Vector3[] allWorldPoints = MergeStimuli(stimulus1World, stimulus2World);

        // ===============================================================
        // 5. 只渲染一次
        // ===============================================================

        stimuliRender.Render(allWorldPoints); // 数据渲染
        
        answerInput.RefreshTrialInformation(); // 同步这个Trial的信息
        
        stimulusTimerCoroutine = StartCoroutine(HideStimulusAfterDelay());
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
                currentTrial = trainingTrials[currentBlock][currentTrainingTrial];
                
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
                    // 如果还有下一个 block，进入下一个 block 的 training
                    currentBlock++;
                    StartTraining();
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
                currentPhase = StudyPhase.Finished;
                    
                answerUI.SetActive(false);
                stimuliRender.ReleaseForReinit();

                ResultsValidator();
                    
                endUI.SetActive(true); // TODO：同步数据扫描的信息, 结束且呈现一个 Summary
            }
            else
            {
                reTrialId++;
                currentBlock = redoList[reTrialId].blockIndex;
                currentFormalTrial = redoList[reTrialId].trialIndex;
                
                currentTrial = formalTrials[currentBlock][currentFormalTrial];
                
                SetupTrial(currentTrial);
            }
        }
    }
    

    // Todo：在实验过程中直接重新开当下的 trial 调用的函数（UI 或者手柄上特定的按键交互）
    public void ReloadTrial()
    {
        StopStimulusTimer();
        stimuliRender.ReleaseForReinit();
        
        SetupTrial(currentTrial);
    }
    

    #endregion =========================================================================================================


    
    
    #region ======================================= Results Management =================================================
    
    
    // 实验结果记录
    private void AnswerOutput(string folderPath, int block, int participantID, int trialId, int pAnswer, TrialInfo trialInfo)
    {
        // ===============================================================
        // 1. 创建 block 文件夹
        // ===============================================================

        string blockFolder = folderPath + "/" + blockFolderName[block];

        if (!Directory.Exists(blockFolder))
        {
            Directory.CreateDirectory(blockFolder);
        }

        // ===============================================================
        // 2. 一个参与者一个 csv 文件
        // ===============================================================

        string filePath = blockFolder + "/P_" + participantID + ".csv";

        // ===============================================================
        // 3. 如果文件不存在，先写表头
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
        // 4. 计算正确性
        // ===============================================================

        int accuracy = 0;

        if (pAnswer == trialInfo.CorrectAnswer)
        {
            accuracy = 1;
        }

        // ===============================================================
        // 5. 写入当前 trial 的一行结果
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
            trialInfo.DensityRatio.ToString(CultureInfo.InvariantCulture) + "\n";

        UpdateTrialResult(filePath, trialId, line);
    }
    
    
    private void UpdateTrialResult(string filePath, int trialId, string newLine)
    {
        List<string> lines = new List<string>(File.ReadAllLines(filePath));
        
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
        redoList = Array.Empty<RedoTrial>();
        redoUI.SetActive(false);
        
        List<RedoTrial> tempRedoList = new List<RedoTrial>();

        for(int blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            string filePath = resultFolder + "/" + blockFolderName[blockIndex] + "/P_" + ParticipantID + ".csv";
            
            // ===============================================================
            // 1. 检查结果文件是否存在
            // ===============================================================

            if(!File.Exists(filePath))
            {
                ShowWarning("Result file missing.\nPlease check the data output folder.");

                return;
            }
            

            string[] lines = File.ReadAllLines(filePath);
            
            // ===============================================================
            // 2. 记录当前 block 已完成的 trial
            // ===============================================================

            bool[] finishedTrial = new bool[trialCountPB];

            // ===============================================================
            // 3. 检查每一行结果
            // ===============================================================

            for(int i = 1; i < lines.Length; i++)
            {
                // 空行跳过
                if(lines[i].Trim() == "")
                {
                    continue;
                }
                
                string[] cells = lines[i].Split(',');
                
                // 列数量异常
                if(cells.Length < 9)
                {
                    continue;
                }

                // TrialID
                if(!int.TryParse(cells[0], out int trialID))
                {
                    continue;
                }
                
                // 超出当前block范围
                if(trialID < 0 || trialID >= trialCountPB)
                {
                    continue;
                }
                
                // 标记该trial已经存在
                finishedTrial[trialID] = true;

                // ===========================================================
                // 检查数值异常
                // ===========================================================

                bool invalid = false;
                
                for(int j = 1; j < cells.Length; j++)
                {
                    if(cells[j].Contains("NaN") || cells[j].Contains("Infinity"))
                    {
                        invalid = true;
                        break;
                    }
                }

                if(invalid)
                {
                    tempRedoList.Add(new RedoTrial() { blockIndex = blockIndex, trialIndex = trialID });
                }

            }
            
            // ===============================================================
            // 4. 检查缺失 trial
            // ===============================================================

            for(int trialIndex = 0; trialIndex < trialCountPB; trialIndex++)
            {
                if(!finishedTrial[trialIndex])
                {
                    tempRedoList.Add(new RedoTrial() { blockIndex = blockIndex, trialIndex = trialIndex });
                }

            }

        }



        // ===============================================================
        // 5. 保存需要 redo 的列表
        // ===============================================================


        redoList = tempRedoList.ToArray();
        redoUI.SetActive(redoList.Length > 0);
        
        
    }
    
    
    #endregion =========================================================================================================
    
    
    
    
    #region ==================================== Study Preparation Pipeline ============================================
    
    
    // 初始化所有相关的路径
    private void BuildPaths(int participantId)
    {
        string trainingConfigPath = Application.streamingAssetsPath + "/Config" + "/Training";
        trainingConfig = new ConfigInfo();
        trainingConfig.stimuliFolder = trainingConfigPath+ "/Stimuli";
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
            trainingTrials = new TrialInfo[blockCount][];
            for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
            {
                trainingTrials[blockIndex] = new TrialInfo[trainingCountPB];
            }

            // 加载对应的 training 信息
            yield return LoadTrialOrder(participantId, trainingConfig.participantCsv, trainingConfig.orderListCsv, trainingTrials);
            if (!loadSuccess) { yield break; }
            yield return LoadTrialInfos(trainingConfig.sceneCsv, trainingTrials);
            if (!loadSuccess) { yield break; }
            yield return LoadStimuli(trainingConfig.stimuliFolder, trainingTrials, false);
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
            yield return LoadStimuli(formalConfig.stimuliFolder, formalTrials, true);
            if (!loadSuccess) { yield break; }
        }
        
        
        
    }

    
    // 根据参与者的 Id 读取实验顺序
    private IEnumerator LoadTrialOrder(int participantId, string participantCsv, string orderCsv, TrialInfo[][] trialInfos)
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
                    blockOrders = new int[blockCount];

                    for (int blockIndex = 1; blockIndex <= blockCount; blockIndex++)
                    {
                        string blockOrderColumnName = "Block" + blockIndex + "_Order";
                        int blockOrderColumn = FindColumnIndex(participantHeader, blockOrderColumnName);

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
        for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
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

        // Formal_Scene.csv already defines its rows with the existing five columns.
        // SceneID and RepetitionID remain supported when present, but are optional.
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
        TrialInfo[][] trialInfos,
        bool allowFormalPreviewFallback)
    {
        string formalPreviewText = null;

        for(int blockIndex =  0; blockIndex < trialInfos.Length; blockIndex++)
        {
            for (int trialIndex = 0; trialIndex < trialInfos[blockIndex].Length; trialIndex++)
            {
                TrialInfo trial = trialInfos[blockIndex][trialIndex];

                string stimuliFileName = allowFormalPreviewFallback
                    ? GetDatasetFileName(trial.StimuliName)
                    : trial.StimuliName;
                string stimuliPath = stimuliFolder + "/" + stimuliFileName + ".csv";
                string stimuliText = "";

                // 读取当前 trial 对应的 stimuli csv
                using (UnityWebRequest request = UnityWebRequest.Get(ToRequestUrl(stimuliPath)))
                {
                    yield return request.SendWebRequest();

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        if (!allowFormalPreviewFallback)
                        {
                            ShowWarning("Unable to load stimulus data.\nPlease check the stimulus file path and dataset name.");
                            loadSuccess = false;
                            yield break;
                        }
                    }
                    else
                    {
                        stimuliText = request.downloadHandler.text;
                    }
                }

                // Formal_Scene.csv uses descriptive titles such as
                // T1_Low_Dataset7_.... The Dataset7 part selects Dataset7.csv while
                // the complete title remains untouched in TrialInfo. The repository
                // currently only has Dataset1.csv, so use it for previewing missing
                // formal datasets until the remaining files arrive.
                if (string.IsNullOrEmpty(stimuliText) && allowFormalPreviewFallback)
                {
                    if (formalPreviewText == null)
                    {
                        string previewPath = stimuliFolder + "/Dataset1.csv";
                        using (UnityWebRequest previewRequest = UnityWebRequest.Get(ToRequestUrl(previewPath)))
                        {
                            yield return previewRequest.SendWebRequest();

                            if (previewRequest.result != UnityWebRequest.Result.Success)
                            {
                                ShowWarning("Unable to load formal preview stimulus Dataset1.csv.\nPlease check the Formal/Stimuli folder.");
                                loadSuccess = false;
                                yield break;
                            }

                            formalPreviewText = previewRequest.downloadHandler.text;
                        }
                    }

                    stimuliText = formalPreviewText;
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

                List<Vector3> stimulus1 = new List<Vector3>();
                List<Vector3> stimulus2 = new List<Vector3>();

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

                    if (label == 1)
                    {
                        stimulus1.Add(point);
                    }
                    else if (label == 2)
                    {
                        stimulus2.Add(point);
                    }
                }

                trial.Stimulus1 = stimulus1.ToArray();
                trial.Stimulus2 = stimulus2.ToArray();

                trialInfos[blockIndex][trialIndex] = trial;
            }
        }
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

        if(currentPhase == StudyPhase.Formal  || currentPhase == StudyPhase.Redo)
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
