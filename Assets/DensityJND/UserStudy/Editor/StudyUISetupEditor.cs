#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class StudyUISetupEditor
{
    private const string SceneName = "DensityJND_Equal";
    private const string MarkerName = "GeneratedCompleteUI_v53";
    private const float StartViewScale = 0.78f;
    private const float EndViewScale = 0.78f;
    private const float AnswerHandScale = 0.00032f;
    private const float AnswerHandForwardOffset = 0.30f;
    private const string RoundedSpritePath =
        "Assets/Samples/XR Interaction Toolkit/2.5.4/Starter Assets/DemoSceneAssets/Sprites/Round Radius 4.png";
    // Horizon-inspired semantic palette: neutral surfaces, soft text and one restrained action blue.
    private static readonly Color Background = new Color(0.102f, 0.102f, 0.102f, 0.94f); // #1A1A1A
    private static readonly Color Card = new Color(0.157f, 0.157f, 0.169f, 0.98f);         // #28282B
    private static readonly Color InputSurface = new Color(0.216f, 0.216f, 0.231f, 1f);    // #37373B
    private static readonly Color Primary = new Color(0.071f, 0.412f, 0.788f, 1f);         // #1269C9
    private static readonly Color Secondary = new Color(0.204f, 0.204f, 0.216f, 1f);       // #343437
    private static readonly Color PrimaryText = new Color(0.914f, 0.914f, 0.925f, 1f);     // #E9E9EC
    private static readonly Color MutedText = new Color(0.690f, 0.690f, 0.714f, 1f);       // #B0B0B6
    private static readonly Color Error = new Color(0.878f, 0.337f, 0.380f, 1f);           // #E05661

    [InitializeOnLoadMethod]
    private static void ScheduleAutomaticBuild()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        EditorApplication.delayCall += AutomaticBuild;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += AutomaticBuild;
        }
    }

    private static void AutomaticBuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            EditorApplication.delayCall += AutomaticBuild;
            return;
        }

        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != SceneName)
        {
            return;
        }

        if (FindSceneObject(MarkerName) == null)
        {
            BuildCompleteUI(false);
        }
        else
        {
            bool changed = ConfigureXRIfAvailable();
            changed |= ConfigureAnswerHandUIIfAvailable();
            changed |= EnsureNextBlockUI();
            if (changed)
            {
                EditorSceneManager.SaveScene(scene);
            }
        }
    }

    [MenuItem("Tools/Density JND/Build Complete Study UI")]
    public static void BuildCompleteUIFromMenu()
    {
        BuildCompleteUI(true);
    }

    public static void BuildCompleteUIBatch()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/DensityJND_Equal.unity", OpenSceneMode.Single);
        BuildCompleteUI(false);
        EditorSceneManager.SaveScene(scene);
    }

    public static void CaptureStartPreviewBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/DensityJND_Equal.unity", OpenSceneMode.Single);
        GameObject start = FindSceneObject("StartUIView");
        GameObject answer = FindSceneObject("AnswerUIView");
        GameObject end = FindSceneObject("EndUIView");
        GameObject finish = FindSceneObject("FinishUIView");
        GameObject warning = FindSceneObject("WarningUIView");
        if (start != null) start.SetActive(true);
        if (answer != null) answer.SetActive(false);
        if (end != null) end.SetActive(false);
        if (finish != null) finish.SetActive(false);
        if (warning != null) warning.SetActive(false);

        Camera camera = Resources.FindObjectsOfTypeAll<Camera>()
            .FirstOrDefault(item => item.gameObject.scene.IsValid() && item.gameObject.name == "Main Camera" &&
                                    !item.transform.IsChildOf(FindSceneObject("Quest XR Interaction Setup")?.transform));
        if (camera == null)
        {
            camera = UnityEngine.Object.FindObjectsOfType<Camera>(true)
                .FirstOrDefault(item => item.gameObject.activeInHierarchy);
        }
        if (camera == null) throw new InvalidOperationException("No active camera was found for the UI preview.");

        bool cameraWasActive = camera.gameObject.activeSelf;
        camera.gameObject.SetActive(true);
        GameObject studyCanvasObject = FindSceneObject("StudyCanvas");
        Canvas canvas = studyCanvasObject != null ? studyCanvasObject.GetComponent<Canvas>() : null;
        if (canvas != null)
        {
            canvas.worldCamera = camera;
            canvas.transform.position = camera.transform.position + camera.transform.forward * 2f;
            canvas.transform.rotation = camera.transform.rotation;
        }

        RenderTexture target = new RenderTexture(1200, 800, 24);
        Texture2D image = new Texture2D(1200, 800, TextureFormat.RGB24, false);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0);
        image.Apply();
        File.WriteAllBytes("/tmp/DensityJND-StartUI.png", image.EncodeToPNG());
        camera.targetTexture = null;
        camera.gameObject.SetActive(cameraWasActive);
        RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(image);
        Debug.Log("Density JND Start UI preview captured.");
    }

    [MenuItem("Tools/Density JND/Validate Study UI")]
    public static void ValidateFromMenu()
    {
        string report = ValidateScene();
        EditorUtility.DisplayDialog("Density JND Validation", report, "OK");
    }

    private static void BuildCompleteUI(bool showDialog)
    {
        EnsureTmpEssentials();

        StudyManager manager = UnityEngine.Object.FindObjectOfType<StudyManager>(true);
        StimuliRender stimuli = UnityEngine.Object.FindObjectOfType<StimuliRender>(true);
        UI_StartInput startInput = UnityEngine.Object.FindObjectOfType<UI_StartInput>(true);
        UI_AnswerInput answerInput = UnityEngine.Object.FindObjectOfType<UI_AnswerInput>(true);

        GameObject uiGroup = FindSceneObject("UIGroup");
        GameObject startPlaceholder = FindSceneObject("StartUI");
        GameObject answerPlaceholder = FindSceneObject("AnswerUI");
        GameObject endPlaceholder = FindSceneObject("EndUI");
        GameObject finishPlaceholder = FindSceneObject("FinishUI");
        GameObject warningPlaceholder = FindSceneObject("WarningUI") ?? FindSceneObject("WarrningUI");

        if (manager == null || stimuli == null || startInput == null || answerInput == null || uiGroup == null ||
            startPlaceholder == null || answerPlaceholder == null || endPlaceholder == null ||
            finishPlaceholder == null || warningPlaceholder == null)
        {
            Debug.LogError("Density JND UI build failed: the scene framework is incomplete.");
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Density JND", "Open DensityJND_Equal and ensure all UI placeholders exist.", "OK");
            }
            return;
        }

        ClearPlaceholderVisuals(startPlaceholder);
        ClearPlaceholderVisuals(answerPlaceholder);
        ClearPlaceholderVisuals(endPlaceholder);
        ClearPlaceholderVisuals(finishPlaceholder);
        ClearPlaceholderVisuals(warningPlaceholder);

        Canvas canvas = EnsureCanvas(uiGroup);
        StudyUIController controller = GetOrAdd<StudyUIController>(uiGroup);

        GameObject startView = CreateView(canvas.transform, startPlaceholder, "StartUIView");
        GameObject answerView = CreateView(canvas.transform, answerPlaceholder, "AnswerUIView");
        GameObject endView = CreateView(canvas.transform, endPlaceholder, "EndUIView");
        GameObject finishView = CreateView(canvas.transform, finishPlaceholder, "FinishUIView");
        GameObject warningView = CreateView(canvas.transform, warningPlaceholder, "WarningUIView");

        StartViewWidgets start = BuildStartView(startView.transform, startInput, controller);
        AnswerWidgets answer = BuildAnswerView(answerView.transform, answerInput);
        EndWidgets end = BuildEndView(endView.transform, controller);
        FinishWidgets finish = BuildFinishView(finishView.transform, manager);
        WarningWidgets warning = BuildWarningView(warningView.transform, controller);

        ConfigureStartInput(startInput, manager, controller, start);
        ConfigureAnswerInput(answerInput, manager, controller, answer);
        ConfigureManager(manager, stimuli, startInput, answerInput, startView, answerView, warningView, endView,
            finishView);
        ConfigureController(controller, manager, startInput, startView, answerView, endView, finishView, warningView,
            warning.Message, end, warning.Retry, start);

        EnsureEventSystem();
        ConfigureXRIfAvailable();
        ConfigureAnswerHandUIIfAvailable();

        startView.SetActive(true);
        answerView.SetActive(false);
        endView.SetActive(false);
        finishView.SetActive(false);
        warningView.SetActive(false);

        GameObject[] oldMarkers = Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(item => item.scene == EditorSceneManager.GetActiveScene() &&
                           item.name.StartsWith("GeneratedCompleteUI_v", StringComparison.Ordinal) &&
                           item.name != MarkerName)
            .ToArray();
        foreach (GameObject oldMarker in oldMarkers)
        {
            Undo.DestroyObjectImmediate(oldMarker);
        }

        GameObject marker = FindSceneObject(MarkerName);
        if (marker == null)
        {
            marker = new GameObject(MarkerName);
            Undo.RegisterCreatedObjectUndo(marker, "Create UI build marker");
            marker.transform.SetParent(uiGroup.transform, false);
            marker.hideFlags = HideFlags.HideInHierarchy;
        }

        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(startInput);
        EditorUtility.SetDirty(answerInput);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = startView;

        string report = ValidateScene();
        Debug.Log("Density JND complete UI build finished.\n" + report);
        if (showDialog)
        {
            EditorUtility.DisplayDialog("Density JND", "Complete UI created and saved.\n\n" + report, "OK");
        }
    }

    private static StartViewWidgets BuildStartView(Transform view, UI_StartInput inputHandler,
        StudyUIController controller)
    {
        GameObject panel = CreatePanel("StartPanel", view, Background);
        panel.transform.localScale = Vector3.one * StartViewScale;
        GameObject experimenterPage = CreateUIObject("ExperimenterStartPage", panel.transform);
        Stretch(experimenterPage.GetComponent<RectTransform>());
        GameObject developerPage = CreateUIObject("DeveloperStartPage", panel.transform);
        Stretch(developerPage.GetComponent<RectTransform>());

        StartWidgets experimenter = BuildStartPage(experimenterPage.transform, inputHandler, false);
        StartWidgets developer = BuildStartPage(developerPage.transform, inputHandler, true);

        Button experimenterTab = CreateIconButton("ExperimenterTabButton", panel.transform, false, true);
        SetRect(experimenterTab.GetComponent<RectTransform>(), new Vector2(480, 340), new Vector2(64, 64));
        UnityEventTools.AddPersistentListener(experimenterTab.onClick, controller.ShowExperimenterStartPage);

        Button developerTab = CreateIconButton("DeveloperTabButton", panel.transform, true, false);
        SetRect(developerTab.GetComponent<RectTransform>(), new Vector2(480, 266), new Vector2(64, 64));
        UnityEventTools.AddPersistentListener(developerTab.onClick, controller.ShowDeveloperStartPage);

        developerPage.SetActive(false);
        return new StartViewWidgets(experimenterPage, developerPage, experimenterTab, developerTab,
            experimenter, developer);
    }

    private static StartWidgets BuildStartPage(Transform page, UI_StartInput inputHandler, bool isDeveloper)
    {
        string prefix = isDeveloper ? "Developer" : "";
        GameObject card = CreateCard(prefix + "StartCard", page, Card, new Vector2(1040, 800));
        CreateText("Title", card.transform, isDeveloper ? "Formal Trial Recovery" : "Density Judgment Study",
            48, FontStyles.Bold, new Vector2(0, 270), new Vector2(900, 70));
        TMP_Text instruction = CreateText("Instruction", card.transform,
            isDeveloper
                ? "Resume a Formal study from a selected block and trial."
                : "Select a field, then enter numbers with the VR keypad.", 25,
            FontStyles.Normal, new Vector2(0, 205), new Vector2(920, 55));
        instruction.color = MutedText;

        TMP_InputField participant;
        TMP_InputField block = null;
        TMP_InputField trial = null;
        TMP_Text message;
        Button startStudy;
        Button repairTrial = null;
        Toggle training = null;
        Toggle formal = null;

        if (isDeveloper)
        {
            participant = CreateInputField(prefix + "ParticipantIDInput", card.transform, "Participant ID");
            SetRect(participant.GetComponent<RectTransform>(), new Vector2(-255, 110), new Vector2(420, 66));

            startStudy = CreateButton(prefix + "ResumeStudyButton", card.transform, "Resume Study", Primary);
            SetRect(startStudy.GetComponent<RectTransform>(), new Vector2(-255, 20), new Vector2(420, 62));
            UnityEventTools.AddPersistentListener(startStudy.onClick, inputHandler.OnDeveloperResumeStudyClicked);

            CreateText("StatusLabel", card.transform, "Status:", 20, FontStyles.Bold,
                new Vector2(-255, -40), new Vector2(420, 30)).color = MutedText;
            message = CreateText(prefix + "StatusText", card.transform, "", 22, FontStyles.Normal,
                new Vector2(-255, -85), new Vector2(450, 90));
            message.color = MutedText;

            block = CreateInputField(prefix + "BlockIDInput", card.transform, "Block");
            SetRect(block.GetComponent<RectTransform>(), new Vector2(-365, -185), new Vector2(200, 58));
            trial = CreateInputField(prefix + "TrialIDInput", card.transform, "Trial");
            SetRect(trial.GetComponent<RectTransform>(), new Vector2(-145, -185), new Vector2(200, 58));

            repairTrial = CreateButton(prefix + "RepairTrialButton", card.transform,
                "Repair Specific Trial", Secondary);
            SetRect(repairTrial.GetComponent<RectTransform>(), new Vector2(-255, -255),
                new Vector2(420, 62));
            UnityEventTools.AddPersistentListener(repairTrial.onClick,
                inputHandler.OnDeveloperRepairTrialClicked);
        }
        else
        {
            participant = CreateInputField(prefix + "ParticipantIDInput", card.transform, "Participant ID");
            SetRect(participant.GetComponent<RectTransform>(), new Vector2(-255, 50), new Vector2(420, 72));
            startStudy = CreateButton(prefix + "StartStudyButton", card.transform, "Start Study", Primary);
            SetRect(startStudy.GetComponent<RectTransform>(), new Vector2(-255, -50), new Vector2(420, 72));
            UnityEventTools.AddPersistentListener(startStudy.onClick, inputHandler.OnStartStudyClicked);

            ToggleGroup phaseGroup = card.AddComponent<ToggleGroup>();
            phaseGroup.allowSwitchOff = false;
            CreateText("PhaseSelectorLabel", card.transform, "Study mode", 20, FontStyles.Bold,
                new Vector2(-255, -165), new Vector2(420, 30)).color = MutedText;
            training = CreateToggle(prefix + "TrainingToggle", card.transform, "Training", phaseGroup, false);
            SetRect(training.GetComponent<RectTransform>(), new Vector2(-365, -235), new Vector2(200, 68));
            formal = CreateToggle(prefix + "FormalToggle", card.transform, "Formal", phaseGroup, true);
            SetRect(formal.GetComponent<RectTransform>(), new Vector2(-145, -235), new Vector2(200, 68));

            message = CreateText(prefix + "MessageText", card.transform, "", 25, FontStyles.Normal,
                new Vector2(-255, -345), new Vector2(450, 80));
            message.color = Error;
        }

        CreateText("KeypadTitle", card.transform, "VR numeric keypad", 22, FontStyles.Bold,
            new Vector2(270, 105), new Vector2(380, 42)).color = MutedText;

        string[,] keys =
        {
            { "1", "2", "3" },
            { "4", "5", "6" },
            { "7", "8", "9" }
        };
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                string digit = keys[row, column];
                Button key = CreateButton(prefix + "Key" + digit, card.transform, digit, Secondary);
                SetRect(key.GetComponent<RectTransform>(),
                    new Vector2(150 + column * 120, 25 - row * 92), new Vector2(100, 72));
                UnityEventTools.AddStringPersistentListener(key.onClick, inputHandler.AppendDigit, digit);
            }
        }

        Button clear = CreateButton(prefix + "KeyClear", card.transform, "Clear", Secondary);
        SetRect(clear.GetComponent<RectTransform>(), new Vector2(150, -251), new Vector2(100, 72));
        UnityEventTools.AddPersistentListener(clear.onClick, inputHandler.ClearNumericInput);

        Button zero = CreateButton(prefix + "Key0", card.transform, "0", Secondary);
        SetRect(zero.GetComponent<RectTransform>(), new Vector2(270, -251), new Vector2(100, 72));
        UnityEventTools.AddStringPersistentListener(zero.onClick, inputHandler.AppendDigit, "0");

        Button back = CreateButton(prefix + "KeyBackspace", card.transform, "Back", Secondary);
        SetRect(back.GetComponent<RectTransform>(), new Vector2(390, -251), new Vector2(100, 72));
        UnityEventTools.AddPersistentListener(back.onClick, inputHandler.BackspaceNumericInput);

        return new StartWidgets(participant, block, trial, training, formal, message, startStudy,
            repairTrial);
    }

    private static AnswerWidgets BuildAnswerView(Transform view, UI_AnswerInput inputHandler)
    {
        GameObject panel = CreatePanel("AnswerPanel", view, Background);
        GameObject card = CreateCard("AnswerCard", panel.transform, Card, new Vector2(1080, 800));
        const float statusRowY = 330f;
        TMP_Text phase = CreateText("PhaseText", card.transform, "Training", 24, FontStyles.Bold,
            new Vector2(-360, statusRowY), new Vector2(180, 52));
        TMP_Text block = CreateText("BlockText", card.transform, "Block 1", 24, FontStyles.Normal,
            new Vector2(240, statusRowY), new Vector2(130, 52));
        block.color = MutedText;
        TMP_Text trial = CreateText("TrialText", card.transform, "Trial 1", 24, FontStyles.Normal,
            new Vector2(360, statusRowY), new Vector2(130, 52));
        trial.color = MutedText;
        TMP_Text countdown = CreateText("CountdownText", card.transform, "", 32, FontStyles.Bold,
            new Vector2(500, statusRowY), new Vector2(60, 52));

        TMP_Text question = CreateText("Question", card.transform,
            "Is the left cluster denser or less dense than the right?\n左边这个团的密度比右边更大还是更小？",
            32, FontStyles.Bold, new Vector2(0, 185), new Vector2(960, 110));

        Button left = CreateButton("LeftButton", card.transform, "Greater\n更大", Secondary);
        SetRect(left.GetComponent<RectTransform>(), new Vector2(-245, 55), new Vector2(410, 126));
        left.GetComponentInChildren<TMP_Text>().fontSize = 30;
        UnityEventTools.AddPersistentListener(left.onClick, inputHandler.OnLeftClicked);
        Button right = CreateButton("RightButton", card.transform, "Smaller\n更小", Secondary);
        SetRect(right.GetComponent<RectTransform>(), new Vector2(245, 55), new Vector2(410, 126));
        right.GetComponentInChildren<TMP_Text>().fontSize = 30;
        UnityEventTools.AddPersistentListener(right.onClick, inputHandler.OnRightClicked);

        TMP_Text message = CreateText("AnswerMessage", card.transform, "", 25, FontStyles.Normal,
            new Vector2(0, -55), new Vector2(900, 52));
        message.color = Error;

        Button submit = CreateButton("SubmitButton", card.transform, "Submit Answer", Primary);
        SetRect(submit.GetComponent<RectTransform>(), new Vector2(0, -130), new Vector2(400, 108));
        submit.GetComponentInChildren<TMP_Text>().fontSize = 32;
        UnityEventTools.AddPersistentListener(submit.onClick, inputHandler.OnSubmitClicked);
        Button next = CreateButton("NextButton", card.transform, "Next Training Trial", Secondary);
        SetRect(next.GetComponent<RectTransform>(), new Vector2(-350, -295), new Vector2(310, 64));
        UnityEventTools.AddPersistentListener(next.onClick, inputHandler.OnNextClicked);
        Button again = CreateButton("TrainingAgainButton", card.transform, "Restart Training", Secondary);
        SetRect(again.GetComponent<RectTransform>(), new Vector2(0, -295), new Vector2(290, 64));
        UnityEventTools.AddPersistentListener(again.onClick, inputHandler.OnTrainingAgainClicked);
        Button formal = CreateButton("StartFormalButton", card.transform, "Start Formal Study", Primary);
        SetRect(formal.GetComponent<RectTransform>(), new Vector2(350, -295), new Vector2(310, 64));
        UnityEventTools.AddPersistentListener(formal.onClick, inputHandler.OnStartFormalClicked);

        // Keep the training actions visible so the layout remains stable. Starting the
        // formal study is always available; the other actions follow training progress.
        next.interactable = false;
        again.interactable = false;
        formal.interactable = true;

        return new AnswerWidgets(phase, block, trial, countdown, question, message, left, right, submit, next, again,
            formal);
    }

    private static EndWidgets BuildEndView(Transform view, StudyUIController controller)
    {
        GameObject panel = CreatePanel("EndPanel", view, Background);
        panel.transform.localScale = Vector3.one * EndViewScale;
        GameObject card = CreateCard("EndCard", panel.transform, Card, new Vector2(920, 520));
        TMP_Text title = CreateText("StudyCompleteReturnToStartButton", card.transform, "Study Complete", 50,
            FontStyles.Bold, new Vector2(0, 140), new Vector2(820, 80));
        Button returnToStart = title.gameObject.AddComponent<Button>();
        returnToStart.targetGraphic = title;
        returnToStart.transition = Selectable.Transition.None;
        Navigation navigation = returnToStart.navigation;
        navigation.mode = Navigation.Mode.None;
        returnToStart.navigation = navigation;
        UnityEventTools.AddPersistentListener(returnToStart.onClick,
            controller.ReturnToStartFromStudyComplete);
        TMP_Text summary = CreateText("EndSummary", card.transform,
            "The study is complete. Results were saved successfully.", 30, FontStyles.Normal,
            new Vector2(0, 35), new Vector2(800, 110));
        summary.color = MutedText;
        CreateText("EndInstruction", card.transform,
            "Please keep the headset on and wait for the researcher.", 25, FontStyles.Normal,
            new Vector2(0, -55), new Vector2(800, 64)).color = MutedText;
        Button nextBlock = CreateButton("NextBlockButton", card.transform, "Next Block", Primary);
        SetRect(nextBlock.GetComponent<RectTransform>(), new Vector2(0, -155), new Vector2(360, 82));
        nextBlock.GetComponentInChildren<TMP_Text>().fontSize = 30;
        UnityEventTools.AddPersistentListener(nextBlock.onClick, controller.OnNextBlockClicked);
        nextBlock.gameObject.SetActive(false);
        return new EndWidgets(title, summary, nextBlock);
    }

    private static FinishWidgets BuildFinishView(Transform view, StudyManager manager)
    {
        Image dim = CreatePanel("RedoDim", view, new Color(0f, 0f, 0f, 0.58f)).GetComponent<Image>();
        GameObject card = CreateCard("RedoCard", dim.transform, Card, new Vector2(780, 370));
        CreateText("RedoTitle", card.transform, "Researcher Review Required", 38, FontStyles.Bold,
            new Vector2(0, 95), new Vector2(680, 60));
        CreateText("RedoText", card.transform,
            "Open Study Recovery Tools from the Developer page to review the saved trials.", 26,
            FontStyles.Normal, new Vector2(0, 15), new Vector2(660, 90)).color = MutedText;
        return new FinishWidgets(null);
    }

    private static WarningWidgets BuildWarningView(Transform view, StudyUIController controller)
    {
        Image dim = CreatePanel("WarningDim", view, new Color(0f, 0f, 0f, 0.7f)).GetComponent<Image>();
        GameObject card = CreateCard("WarningCard", dim.transform, Card, new Vector2(800, 440));
        CreateText("WarningTitle", card.transform, "Unable to Continue", 40, FontStyles.Bold,
            new Vector2(0, 135), new Vector2(700, 65));
        TMP_Text message = CreateText("WarningMessage", card.transform,
            "Please check the experiment configuration.", 27, FontStyles.Normal,
            new Vector2(0, 35), new Vector2(690, 130));
        message.color = MutedText;
        Button dismiss = CreateButton("DismissButton", card.transform, "OK", Secondary);
        SetRect(dismiss.GetComponent<RectTransform>(), new Vector2(-165, -130), new Vector2(250, 62));
        UnityEventTools.AddPersistentListener(dismiss.onClick, controller.DismissWarning);
        Button retry = CreateButton("RetryButton", card.transform, "Retry", Primary);
        SetRect(retry.GetComponent<RectTransform>(), new Vector2(165, -130), new Vector2(250, 62));
        UnityEventTools.AddPersistentListener(retry.onClick, controller.RetryLastAction);
        return new WarningWidgets(message, retry);
    }

    private static void ConfigureStartInput(UI_StartInput input, StudyManager manager, StudyUIController controller,
        StartViewWidgets widgets)
    {
        SerializedObject serialized = new SerializedObject(input);
        SetObject(serialized, "studyManager", manager);
        SetObject(serialized, "uiController", controller);
        SetObject(serialized, "participantIDInput", widgets.Experimenter.Participant);
        SetObject(serialized, "trainingToggle", widgets.Experimenter.Training);
        SetObject(serialized, "formalToggle", widgets.Experimenter.Formal);
        SetObject(serialized, "messageText", widgets.Experimenter.Message);
        SetObject(serialized, "startStudyButton", widgets.Experimenter.StartStudy);
        SetObject(serialized, "developerParticipantIDInput", widgets.Developer.Participant);
        SetObject(serialized, "developerBlockIDInput", widgets.Developer.Block);
        SetObject(serialized, "developerTrialIDInput", widgets.Developer.Trial);
        SetObject(serialized, "developerMessageText", widgets.Developer.Message);
        SetObject(serialized, "developerResumeStudyButton", widgets.Developer.StartStudy);
        SetObject(serialized, "developerRepairTrialButton", widgets.Developer.RepairTrial);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureAnswerInput(UI_AnswerInput input, StudyManager manager, StudyUIController controller,
        AnswerWidgets widgets)
    {
        SerializedObject serialized = new SerializedObject(input);
        SetObject(serialized, "studyManager", manager);
        SetObject(serialized, "uiController", controller);
        SetObject(serialized, "phaseText", widgets.Phase);
        SetObject(serialized, "blockText", widgets.Block);
        SetObject(serialized, "trialText", widgets.Trial);
        SetObject(serialized, "countdownText", widgets.Countdown);
        SetObject(serialized, "questionText", widgets.Question);
        SetObject(serialized, "messageText", widgets.Message);
        SetObject(serialized, "leftButton", widgets.Left);
        SetObject(serialized, "rightButton", widgets.Right);
        SetObject(serialized, "submitButton", widgets.Submit);
        SetObject(serialized, "nextButton", widgets.Next);
        SetObject(serialized, "trainingAgainButton", widgets.Again);
        SetObject(serialized, "startFormalButton", widgets.Formal);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureManager(StudyManager manager, StimuliRender stimuli, UI_StartInput startInput,
        UI_AnswerInput answerInput, GameObject start, GameObject answer, GameObject warning, GameObject end,
        GameObject finish)
    {
        SerializedObject serialized = new SerializedObject(manager);
        SetObject(serialized, "playerPosition", Camera.main != null ? Camera.main.transform : null);
        SetObject(serialized, "startUI", start);
        SetObject(serialized, "answerUI", answer);
        SetObject(serialized, "warningUI", warning);
        SetObject(serialized, "endUI", end);
        SetObject(serialized, "redoUI", finish);
        SetObject(serialized, "stimuliRender", stimuli);
        SetObject(serialized, "startInput", startInput);
        SetObject(serialized, "answerInput", answerInput);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureController(StudyUIController controller, StudyManager manager, UI_StartInput startInput,
        GameObject start, GameObject answer, GameObject end, GameObject finish, GameObject warning,
        TMP_Text warningText, EndWidgets endWidgets, Button retry, StartViewWidgets startWidgets)
    {
        SerializedObject serialized = new SerializedObject(controller);
        SetObject(serialized, "studyManager", manager);
        SetObject(serialized, "startInput", startInput);
        SetObject(serialized, "startUI", start);
        SetObject(serialized, "answerUI", answer);
        SetObject(serialized, "endUI", end);
        SetObject(serialized, "finishUI", finish);
        SetObject(serialized, "warningUI", warning);
        SetObject(serialized, "experimenterStartPage", startWidgets.ExperimenterPage);
        SetObject(serialized, "developerStartPage", startWidgets.DeveloperPage);
        SetObject(serialized, "experimenterTabButton", startWidgets.ExperimenterTab);
        SetObject(serialized, "developerTabButton", startWidgets.DeveloperTab);
        SetObject(serialized, "warningText", warningText);
        SetObject(serialized, "endTitleText", endWidgets.Title);
        SetObject(serialized, "endSummaryText", endWidgets.Summary);
        SetObject(serialized, "nextBlockButton", endWidgets.NextBlock);
        SetObject(serialized, "retryButton", retry);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Canvas EnsureCanvas(GameObject uiGroup)
    {
        Canvas canvas = uiGroup.GetComponentInChildren<Canvas>(true);
        if (canvas == null)
        {
            GameObject canvasObject = CreateUIObject("StudyCanvas", uiGroup.transform);
            canvas = canvasObject.AddComponent<Canvas>();
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        canvas.renderMode = RenderMode.WorldSpace;
        Camera presentationCamera = Camera.main != null ? Camera.main : canvas.worldCamera;
        if (presentationCamera == null)
        {
            presentationCamera = Resources.FindObjectsOfTypeAll<Camera>()
                .FirstOrDefault(item => item.gameObject.scene == EditorSceneManager.GetActiveScene() &&
                                        item.gameObject.activeInHierarchy && item.enabled);
        }
        canvas.worldCamera = presentationCamera;
        RectTransform rect = canvas.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(1200, 800);
        rect.localScale = Vector3.one * 0.0015f;
        if (presentationCamera != null)
        {
            Transform cameraTransform = presentationCamera.transform;
            rect.SetPositionAndRotation(
                cameraTransform.position + cameraTransform.forward * 2f,
                cameraTransform.rotation);
        }

        CanvasScaler scaler = GetOrAdd<CanvasScaler>(canvas.gameObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.dynamicPixelsPerUnit = 16f;
        GetOrAdd<GraphicRaycaster>(canvas.gameObject);
        return canvas;
    }

    private static GameObject CreateView(Transform canvas, GameObject placeholder, string viewName)
    {
        GameObject oldView = FindSceneObject(viewName);
        if (oldView != null)
        {
            if (placeholder.transform.IsChildOf(oldView.transform))
            {
                placeholder.transform.SetParent(canvas, false);
            }
            Undo.DestroyObjectImmediate(oldView);
        }

        GameObject view = CreateUIObject(viewName, canvas);
        Stretch(view.GetComponent<RectTransform>());
        placeholder.transform.SetParent(view.transform, false);
        placeholder.transform.localPosition = Vector3.zero;
        placeholder.transform.localRotation = Quaternion.identity;
        placeholder.transform.localScale = Vector3.one;
        return view;
    }

    private static void ClearPlaceholderVisuals(GameObject placeholder)
    {
        for (int i = placeholder.transform.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(placeholder.transform.GetChild(i).gameObject);
        }

        foreach (Graphic graphic in placeholder.GetComponents<Graphic>())
        {
            Undo.DestroyObjectImmediate(graphic);
        }

        foreach (Selectable selectable in placeholder.GetComponents<Selectable>())
        {
            Undo.DestroyObjectImmediate(selectable);
        }
    }

    private static GameObject CreatePanel(string name, Transform parent, Color color)
    {
        GameObject panel = CreateUIObject(name, parent);
        Stretch(panel.GetComponent<RectTransform>());
        Image image = panel.AddComponent<Image>();
        image.color = color;
        return panel;
    }

    private static GameObject CreateCard(string name, Transform parent, Color color, Vector2 size)
    {
        GameObject card = CreateUIObject(name, parent);
        Image image = card.AddComponent<Image>();
        image.color = color;
        ApplyRoundedSprite(image);
        SetRect(card.GetComponent<RectTransform>(), Vector2.zero, size);
        return card;
    }

    private static void ApplyRoundedSprite(Image image)
    {
        Sprite rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
        if (rounded == null) return;
        image.sprite = rounded;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
    }

    private static TMP_Text CreateText(string name, Transform parent, string value, float size, FontStyles style,
        Vector2 position, Vector2 dimensions)
    {
        GameObject item = CreateUIObject(name, parent);
        TextMeshProUGUI text = item.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (font != null) text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.color = PrimaryText;
        text.enableWordWrapping = true;
        SetRect(text.rectTransform, position, dimensions);
        return text;
    }

    private static TMP_InputField CreateInputField(string name, Transform parent, string placeholderValue)
    {
        GameObject root = CreateUIObject(name, parent);
        Image background = root.AddComponent<Image>();
        background.color = InputSurface;
        ApplyRoundedSprite(background);
        TMP_InputField input = root.AddComponent<TMP_InputField>();
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 6;
        input.shouldHideSoftKeyboard = true;

        GameObject viewport = CreateUIObject("Text Area", root.transform);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(20, 7);
        viewportRect.offsetMax = new Vector2(-20, -7);
        viewport.AddComponent<RectMask2D>();

        TextMeshProUGUI placeholder = (TextMeshProUGUI)CreateText("Placeholder", viewport.transform,
            placeholderValue, 24, FontStyles.Italic, Vector2.zero, Vector2.zero);
        placeholder.color = MutedText;
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        placeholder.enableWordWrapping = false;
        Stretch(placeholder.rectTransform);

        TextMeshProUGUI valueText = (TextMeshProUGUI)CreateText("Text", viewport.transform, "", 28,
            FontStyles.Normal, Vector2.zero, Vector2.zero);
        valueText.alignment = TextAlignmentOptions.MidlineLeft;
        Stretch(valueText.rectTransform);

        input.textViewport = viewportRect;
        input.textComponent = valueText;
        input.placeholder = placeholder;
        return input;
    }

    private static Button CreateButton(string name, Transform parent, string label, Color normal)
    {
        GameObject root = CreateUIObject(name, parent);
        Image image = root.AddComponent<Image>();
        // Selectable applies its ColorBlock as a multiplier. Keep the graphic white so
        // semantic colors are displayed once instead of being unintentionally squared.
        image.color = Color.white;
        ApplyRoundedSprite(image);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.highlightedColor = Color.Lerp(normal, Color.white, 0.16f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = Color.Lerp(normal, Color.black, 0.2f);
        colors.disabledColor = new Color(normal.r, normal.g, normal.b, 0.35f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        TMP_Text text = CreateText("Label", root.transform, label, 26, FontStyles.Bold, Vector2.zero, Vector2.zero);
        Stretch(text.rectTransform);
        return button;
    }

    private static Button CreateIconButton(string name, Transform parent, bool developerIcon, bool selected)
    {
        Button button = CreateButton(name, parent, developerIcon ? "</>" : "", selected ? Primary : Secondary);
        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.fontSize = developerIcon ? 20 : 1;
            label.raycastTarget = false;
        }

        if (!developerIcon)
        {
            GameObject head = CreateUIObject("PersonHead", button.transform);
            Image headImage = head.AddComponent<Image>();
            headImage.color = PrimaryText;
            ApplyRoundedSprite(headImage);
            SetRect(head.GetComponent<RectTransform>(), new Vector2(0, 8), new Vector2(14, 14));
            headImage.raycastTarget = false;

            GameObject shoulders = CreateUIObject("PersonShoulders", button.transform);
            Image shouldersImage = shoulders.AddComponent<Image>();
            shouldersImage.color = PrimaryText;
            ApplyRoundedSprite(shouldersImage);
            SetRect(shoulders.GetComponent<RectTransform>(), new Vector2(0, -10), new Vector2(28, 14));
            shouldersImage.raycastTarget = false;
        }

        return button;
    }

    private static Toggle CreateToggle(string name, Transform parent, string label, ToggleGroup group, bool isOn)
    {
        GameObject root = CreateUIObject(name, parent);
        Toggle toggle = root.AddComponent<Toggle>();
        toggle.group = group;

        GameObject backgroundObject = CreateUIObject("Background", root.transform);
        Image background = backgroundObject.AddComponent<Image>();
        background.color = Color.white;
        ApplyRoundedSprite(background);
        Stretch(background.rectTransform);

        ColorBlock colors = toggle.colors;
        colors.normalColor = Secondary;
        colors.highlightedColor = Color.Lerp(Secondary, Color.white, 0.12f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = Color.Lerp(Secondary, Color.black, 0.18f);
        colors.disabledColor = new Color(Secondary.r, Secondary.g, Secondary.b, 0.35f);
        colors.colorMultiplier = 1f;
        toggle.colors = colors;

        GameObject selectionObject = CreateUIObject("Selection", root.transform);
        Image selection = selectionObject.AddComponent<Image>();
        selection.color = Primary;
        ApplyRoundedSprite(selection);
        Stretch(selection.rectTransform);
        selection.rectTransform.offsetMin = new Vector2(3, 3);
        selection.rectTransform.offsetMax = new Vector2(-3, -3);
        toggle.targetGraphic = background;
        toggle.graphic = selection;

        TMP_Text text = CreateText("Label", root.transform, label, 24, FontStyles.Bold,
            Vector2.zero, Vector2.zero);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        Stretch(text.rectTransform);
        toggle.SetIsOnWithoutNotify(isOn);
        return toggle;
    }

    private static void EnsureEventSystem()
    {
        EventSystem eventSystem = UnityEngine.Object.FindObjectOfType<EventSystem>(true);
        if (eventSystem == null)
        {
            GameObject item = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(item, "Create EventSystem");
        }
    }

    private static bool ConfigureXRIfAvailable()
    {
        Type xrInputType = FindType("UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule");
        Type trackedRaycasterType = FindType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster");
        if (xrInputType == null || trackedRaycasterType == null)
        {
            return false;
        }

        bool changed = false;
        EventSystem eventSystem = UnityEngine.Object.FindObjectOfType<EventSystem>(true);
        if (eventSystem != null && eventSystem.GetComponent(xrInputType) == null)
        {
            foreach (BaseInputModule module in eventSystem.GetComponents<BaseInputModule>())
            {
                Undo.DestroyObjectImmediate(module);
            }
            Undo.AddComponent(eventSystem.gameObject, xrInputType);
            changed = true;
        }

        GameObject studyCanvasObject = FindSceneObject("StudyCanvas");
        Canvas canvas = studyCanvasObject != null ? studyCanvasObject.GetComponent<Canvas>() : null;
        if (canvas != null)
        {
            GraphicRaycaster standard = canvas.GetComponent<GraphicRaycaster>();
            if (standard == null)
            {
                standard = Undo.AddComponent<GraphicRaycaster>(canvas.gameObject);
                changed = true;
            }
            if (standard.ignoreReversedGraphics)
            {
                standard.ignoreReversedGraphics = false;
                changed = true;
            }
            if (standard.enabled)
            {
                standard.enabled = false;
                changed = true;
            }
            if (canvas.GetComponent(trackedRaycasterType) == null)
            {
                Undo.AddComponent(canvas.gameObject, trackedRaycasterType);
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Keeps the live answer view on its own world-space canvas under the tracked left controller.
    /// The other study views remain on StudyCanvas in front of the participant.
    /// </summary>
    public static bool ConfigureAnswerHandUIIfAvailable()
    {
        GameObject xrSetup = FindSceneObject("Quest XR Interaction Setup");
        GameObject answerView = FindSceneObject("AnswerUIView");
        Type trackedRaycasterType =
            FindType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster");
        if (xrSetup == null || answerView == null || trackedRaycasterType == null)
        {
            return false;
        }

        bool changed = false;

        // Remove the obsolete camera-attached copy left by the earlier setup attempt.
        GameObject[] duplicateViews = Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(item => item.scene == EditorSceneManager.GetActiveScene() &&
                           item.name.StartsWith("AnswerUIView (", StringComparison.Ordinal))
            .ToArray();
        foreach (GameObject duplicateView in duplicateViews)
        {
            Undo.DestroyObjectImmediate(duplicateView);
            changed = true;
        }

        Transform leftController = xrSetup.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(item => item.name == "Left Controller");
        Transform rightController = xrSetup.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(item => item.name == "Right Controller");
        Camera xrCamera = xrSetup.GetComponentInChildren<Camera>(true);
        if (leftController == null || rightController == null || xrCamera == null)
        {
            return changed;
        }

        // The answer panel sits close to the left controller. Keep the right-hand ray's UI path
        // active even if its physics cast also touches a nearby controller/interactable.
        Type xrRayInteractorType = FindType("UnityEngine.XR.Interaction.Toolkit.XRRayInteractor");
        Component rightRay = xrRayInteractorType == null
            ? null
            : rightController.GetComponentsInChildren(xrRayInteractorType, true)
                .FirstOrDefault(item => item.gameObject.name == "Ray Interactor");
        if (rightRay != null)
        {
            if (!rightRay.gameObject.activeSelf)
            {
                rightRay.gameObject.SetActive(true);
                changed = true;
            }
            if (rightRay is Behaviour rightRayBehaviour && !rightRayBehaviour.enabled)
            {
                rightRayBehaviour.enabled = true;
                changed = true;
            }

            SerializedObject rightRayProperties = new SerializedObject(rightRay);
            SerializedProperty enableUI = rightRayProperties.FindProperty("m_EnableUIInteraction");
            SerializedProperty blockUI = rightRayProperties.FindProperty("m_BlockUIOnInteractableSelection");
            if (enableUI != null && !enableUI.boolValue)
            {
                enableUI.boolValue = true;
                changed = true;
            }
            if (blockUI != null && blockUI.boolValue)
            {
                blockUI.boolValue = false;
                changed = true;
            }
            rightRayProperties.ApplyModifiedPropertiesWithoutUndo();
        }

        RectTransform rect = answerView.GetComponent<RectTransform>();
        bool newlyAttached = rect.parent != leftController;
        if (newlyAttached)
        {
            Undo.SetTransformParent(rect, leftController, "Attach Answer UI to left controller");
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1200f, 800f);
            rect.localRotation = Quaternion.identity;
            changed = true;
        }

        Vector3 targetPosition = new Vector3(0f, 0.10f, AnswerHandForwardOffset);
        if ((rect.anchoredPosition3D - targetPosition).sqrMagnitude > Mathf.Epsilon)
        {
            Undo.RecordObject(rect, "Move Answer UI away from left controller");
            rect.anchoredPosition3D = targetPosition;
            changed = true;
        }

        Vector3 targetScale = Vector3.one * AnswerHandScale;
        if ((rect.localScale - targetScale).sqrMagnitude > Mathf.Epsilon)
        {
            Undo.RecordObject(rect, "Resize Answer UI");
            rect.localScale = targetScale;
            changed = true;
        }

        Canvas answerCanvas = answerView.GetComponent<Canvas>();
        if (answerCanvas == null)
        {
            answerCanvas = Undo.AddComponent<Canvas>(answerView);
            changed = true;
        }
        answerCanvas.renderMode = RenderMode.WorldSpace;
        if (answerCanvas.worldCamera != xrCamera)
        {
            answerCanvas.worldCamera = xrCamera;
            changed = true;
        }
        answerCanvas.overrideSorting = true;
        answerCanvas.sortingOrder = 1;

        CanvasScaler scaler = answerView.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = Undo.AddComponent<CanvasScaler>(answerView);
            changed = true;
        }
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.dynamicPixelsPerUnit = 16f;

        GraphicRaycaster standard = answerView.GetComponent<GraphicRaycaster>();
        if (standard == null)
        {
            standard = Undo.AddComponent<GraphicRaycaster>(answerView);
            changed = true;
        }
        if (standard.ignoreReversedGraphics)
        {
            standard.ignoreReversedGraphics = false;
            changed = true;
        }
        if (standard.enabled)
        {
            standard.enabled = false;
            changed = true;
        }
        if (answerView.GetComponent(trackedRaycasterType) == null)
        {
            Undo.AddComponent(answerView, trackedRaycasterType);
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(answerView);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
        return changed;
    }

    private static bool EnsureNextBlockUI()
    {
        GameObject endView = FindSceneObject("EndUIView");
        StudyUIController controller = UnityEngine.Object.FindObjectOfType<StudyUIController>(true);
        if (endView == null || controller == null)
        {
            return false;
        }

        Transform card = endView.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(item => item.name == "EndCard");
        TMP_Text title = endView.GetComponentsInChildren<TMP_Text>(true)
            .FirstOrDefault(item => item.name == "StudyCompleteReturnToStartButton");
        TMP_Text summary = endView.GetComponentsInChildren<TMP_Text>(true)
            .FirstOrDefault(item => item.name == "EndSummary");
        TMP_Text instruction = endView.GetComponentsInChildren<TMP_Text>(true)
            .FirstOrDefault(item => item.name == "EndInstruction");
        if (card == null || title == null || summary == null)
        {
            return false;
        }

        bool changed = false;
        Button nextBlock = endView.GetComponentsInChildren<Button>(true)
            .FirstOrDefault(item => item.name == "NextBlockButton");
        if (nextBlock == null)
        {
            RectTransform cardRect = card.GetComponent<RectTransform>();
            Undo.RecordObject(cardRect, "Resize block end card");
            cardRect.sizeDelta = new Vector2(920, 520);
            SetRect(title.rectTransform, new Vector2(0, 140), new Vector2(820, 80));
            SetRect(summary.rectTransform, new Vector2(0, 35), new Vector2(800, 110));
            if (instruction != null)
            {
                SetRect(instruction.rectTransform, new Vector2(0, -55), new Vector2(800, 64));
            }

            nextBlock = CreateButton("NextBlockButton", card, "Next Block", Primary);
            SetRect(nextBlock.GetComponent<RectTransform>(), new Vector2(0, -155), new Vector2(360, 82));
            nextBlock.GetComponentInChildren<TMP_Text>().fontSize = 30;
            UnityEventTools.AddPersistentListener(nextBlock.onClick, controller.OnNextBlockClicked);
            nextBlock.gameObject.SetActive(false);
            changed = true;
        }

        SerializedObject serialized = new SerializedObject(controller);
        SerializedProperty titleProperty = serialized.FindProperty("endTitleText");
        SerializedProperty summaryProperty = serialized.FindProperty("endSummaryText");
        SerializedProperty buttonProperty = serialized.FindProperty("nextBlockButton");
        if (titleProperty != null && titleProperty.objectReferenceValue != title)
        {
            titleProperty.objectReferenceValue = title;
            changed = true;
        }
        if (summaryProperty != null && summaryProperty.objectReferenceValue != summary)
        {
            summaryProperty.objectReferenceValue = summary;
            changed = true;
        }
        if (buttonProperty != null && buttonProperty.objectReferenceValue != nextBlock)
        {
            buttonProperty.objectReferenceValue = nextBlock;
            changed = true;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return changed;
    }

    private static string ValidateScene()
    {
        string[] required =
        {
            "StudyCanvas", "StartUIView", "AnswerUIView", "EndUIView", "FinishUIView", "WarningUIView",
            "ExperimenterStartPage", "DeveloperStartPage", "ExperimenterTabButton", "DeveloperTabButton",
            "EventSystem"
        };
        string missing = string.Join(", ", required.Where(item => FindSceneObject(item) == null));
        StudyManager manager = UnityEngine.Object.FindObjectOfType<StudyManager>(true);
        StudyUIController controller = UnityEngine.Object.FindObjectOfType<StudyUIController>(true);
        GameObject answerView = FindSceneObject("AnswerUIView");
        bool answerAttachedToLeftController = answerView != null && answerView.GetComponent<Canvas>() != null &&
                                              answerView.transform.parent != null &&
                                              answerView.transform.parent.name == "Left Controller";
        GameObject xrSetup = FindSceneObject("Quest XR Interaction Setup");
        Transform rightController = xrSetup == null
            ? null
            : xrSetup.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == "Right Controller");
        Type xrRayInteractorType = FindType("UnityEngine.XR.Interaction.Toolkit.XRRayInteractor");
        Component rightRay = rightController == null || xrRayInteractorType == null
            ? null
            : rightController.GetComponentsInChildren(xrRayInteractorType, true)
                .FirstOrDefault(item => item.gameObject.name == "Ray Interactor");
        bool rightRaySupportsUI = false;
        if (rightRay != null)
        {
            SerializedObject rightRayProperties = new SerializedObject(rightRay);
            SerializedProperty enableUI = rightRayProperties.FindProperty("m_EnableUIInteraction");
            SerializedProperty blockUI = rightRayProperties.FindProperty("m_BlockUIOnInteractableSelection");
            rightRaySupportsUI = rightRay.gameObject.activeInHierarchy &&
                                 (!(rightRay is Behaviour behaviour) || behaviour.enabled) &&
                                 enableUI != null && enableUI.boolValue &&
                                 blockUI != null && !blockUI.boolValue;
        }
        int missingManagerReferences = 0;
        if (manager != null)
        {
            SerializedObject serialized = new SerializedObject(manager);
            string[] properties = { "playerPosition", "startUI", "answerUI", "warningUI", "endUI", "redoUI", "stimuliRender", "startInput", "answerInput" };
            missingManagerReferences = properties.Count(property => serialized.FindProperty(property).objectReferenceValue == null);
        }

        bool valid = string.IsNullOrEmpty(missing) && manager != null && controller != null &&
                     missingManagerReferences == 0 && answerAttachedToLeftController && rightRaySupportsUI;
        return valid
            ? "PASS: all UI views, StudyManager references, the left-controller Answer UI, and right-hand UI selection are configured."
            : "FAIL: missing objects: " + (string.IsNullOrEmpty(missing) ? "none" : missing) +
              "; missing manager references: " + missingManagerReferences +
              "; Answer UI attached to Left Controller: " + answerAttachedToLeftController +
              "; right-hand UI ray ready: " + rightRaySupportsUI + ".";
    }

    private static void EnsureTmpEssentials()
    {
        const string fontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath) != null)
        {
            return;
        }

        string packagePath = Directory.GetFiles("Library/PackageCache", "TMP Essential Resources.unitypackage",
            SearchOption.AllDirectories).FirstOrDefault();
        if (string.IsNullOrEmpty(packagePath))
        {
            Debug.LogError("TextMesh Pro Essential Resources package was not found.");
            return;
        }
        AssetDatabase.ImportPackage(Path.GetFullPath(packagePath), false);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static Type FindType(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName, false);
            if (type != null) return type;
        }
        return null;
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(item => item.scene.IsValid() && item.name == name);
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(target);
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject item = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(item, "Create " + name);
        item.transform.SetParent(parent, false);
        return item;
    }

    private static void SetObject(SerializedObject serialized, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null) property.objectReferenceValue = value;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private readonly struct StartViewWidgets
    {
        public readonly GameObject ExperimenterPage, DeveloperPage;
        public readonly Button ExperimenterTab, DeveloperTab;
        public readonly StartWidgets Experimenter, Developer;

        public StartViewWidgets(GameObject experimenterPage, GameObject developerPage,
            Button experimenterTab, Button developerTab, StartWidgets experimenter, StartWidgets developer)
        {
            ExperimenterPage = experimenterPage;
            DeveloperPage = developerPage;
            ExperimenterTab = experimenterTab;
            DeveloperTab = developerTab;
            Experimenter = experimenter;
            Developer = developer;
        }
    }

    private readonly struct StartWidgets
    {
        public readonly TMP_InputField Participant, Block, Trial;
        public readonly Toggle Training, Formal;
        public readonly TMP_Text Message;
        public readonly Button StartStudy, RepairTrial;
        public StartWidgets(TMP_InputField participant, TMP_InputField block, TMP_InputField trial, Toggle training,
            Toggle formal, TMP_Text message, Button startStudy, Button repairTrial)
        {
            Participant = participant; Block = block; Trial = trial; Training = training; Formal = formal;
            Message = message; StartStudy = startStudy; RepairTrial = repairTrial;
        }
    }

    private readonly struct AnswerWidgets
    {
        public readonly TMP_Text Phase, Block, Trial, Countdown, Question, Message;
        public readonly Button Left, Right, Submit, Next, Again, Formal;
        public AnswerWidgets(TMP_Text phase, TMP_Text block, TMP_Text trial, TMP_Text countdown, TMP_Text question,
            TMP_Text message, Button left, Button right, Button submit, Button next, Button again, Button formal)
        {
            Phase = phase; Block = block; Trial = trial; Countdown = countdown; Question = question; Message = message;
            Left = left; Right = right; Submit = submit; Next = next; Again = again; Formal = formal;
        }
    }

    private readonly struct EndWidgets
    {
        public readonly TMP_Text Title, Summary;
        public readonly Button NextBlock;
        public EndWidgets(TMP_Text title, TMP_Text summary, Button nextBlock)
        {
            Title = title;
            Summary = summary;
            NextBlock = nextBlock;
        }
    }

    private readonly struct FinishWidgets
    {
        public readonly Button Redo;
        public FinishWidgets(Button redo) { Redo = redo; }
    }

    private readonly struct WarningWidgets
    {
        public readonly TMP_Text Message;
        public readonly Button Retry;
        public WarningWidgets(TMP_Text message, Button retry) { Message = message; Retry = retry; }
    }
}
#endif
