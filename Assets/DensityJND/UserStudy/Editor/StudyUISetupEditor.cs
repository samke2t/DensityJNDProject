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
    private const string MarkerName = "GeneratedCompleteUI_v14";
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
        else if (ConfigureXRIfAvailable())
        {
            EditorSceneManager.SaveScene(scene);
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
        Canvas canvas = UnityEngine.Object.FindObjectOfType<Canvas>(true);
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

        StartWidgets start = BuildStartView(startView.transform, startInput);
        AnswerWidgets answer = BuildAnswerView(answerView.transform, answerInput);
        EndWidgets end = BuildEndView(endView.transform);
        FinishWidgets finish = BuildFinishView(finishView.transform, manager);
        WarningWidgets warning = BuildWarningView(warningView.transform, controller);

        ConfigureStartInput(startInput, manager, controller, start);
        ConfigureAnswerInput(answerInput, manager, controller, answer);
        ConfigureManager(manager, stimuli, startInput, answerInput, startView, answerView, warningView, endView,
            finishView);
        ConfigureController(controller, manager, startInput, startView, answerView, endView, finishView, warningView,
            warning.Message, end.Summary, warning.Retry);

        EnsureEventSystem();
        ConfigureXRIfAvailable();

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

    private static StartWidgets BuildStartView(Transform view, UI_StartInput inputHandler)
    {
        GameObject panel = CreatePanel("StartPanel", view, Background);
        GameObject card = CreateCard("StartCard", panel.transform, Card, new Vector2(1040, 800));
        CreateText("Title", card.transform, "Density Judgment Study", 48, FontStyles.Bold,
            new Vector2(0, 245), new Vector2(900, 76));
        TMP_Text instruction = CreateText("Instruction", card.transform,
            "Enter the participant ID to begin.\nAdvanced Start is for testing and recovery only.", 27,
            FontStyles.Normal, new Vector2(0, 145), new Vector2(920, 90));
        instruction.color = MutedText;
        instruction.lineSpacing = 8f;

        TMP_InputField participant = CreateInputField("ParticipantIDInput", card.transform, "Participant ID");
        SetRect(participant.GetComponent<RectTransform>(), new Vector2(-190, 35), new Vector2(440, 72));
        Button startStudy = CreateButton("StartStudyButton", card.transform, "Start Study", Primary);
        SetRect(startStudy.GetComponent<RectTransform>(), new Vector2(290, 35), new Vector2(320, 72));
        UnityEventTools.AddPersistentListener(startStudy.onClick, inputHandler.OnStartStudyClicked);

        TMP_Text advancedTitle = CreateText("AdvancedTitle", card.transform, "Advanced start", 22,
            FontStyles.Bold, new Vector2(0, -60), new Vector2(900, 42));
        advancedTitle.color = MutedText;

        TMP_InputField block = CreateInputField("BlockIDInput", card.transform, "Block (starts at 1)");
        SetRect(block.GetComponent<RectTransform>(), new Vector2(-350, -145), new Vector2(280, 64));
        TMP_InputField trial = CreateInputField("TrialIDInput", card.transform, "Trial (starts at 1)");
        SetRect(trial.GetComponent<RectTransform>(), new Vector2(-40, -145), new Vector2(280, 64));

        ToggleGroup phaseGroup = card.AddComponent<ToggleGroup>();
        Toggle training = CreateToggle("TrainingToggle", card.transform, "Training", phaseGroup, true);
        SetRect(training.GetComponent<RectTransform>(), new Vector2(250, -145), new Vector2(190, 56));
        Toggle formal = CreateToggle("FormalToggle", card.transform, "Formal", phaseGroup, false);
        SetRect(formal.GetComponent<RectTransform>(), new Vector2(430, -145), new Vector2(170, 56));

        Button startTrial = CreateButton("StartTrialButton", card.transform, "Start Specific Trial", Secondary);
        SetRect(startTrial.GetComponent<RectTransform>(), new Vector2(0, -275), new Vector2(380, 68));
        UnityEventTools.AddPersistentListener(startTrial.onClick, inputHandler.OnStartTrialClicked);

        TMP_Text message = CreateText("MessageText", card.transform, "", 25, FontStyles.Normal,
            new Vector2(0, -350), new Vector2(920, 56));
        message.color = Error;

        return new StartWidgets(participant, block, trial, training, formal, message, startStudy, startTrial);
    }

    private static AnswerWidgets BuildAnswerView(Transform view, UI_AnswerInput inputHandler)
    {
        GameObject panel = CreatePanel("AnswerPanel", view, Background);
        GameObject card = CreateCard("AnswerCard", panel.transform, Card, new Vector2(1080, 800));
        const float statusRowY = 300f;
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

        CreateText("Question", card.transform, "Which side appears denser?", 42, FontStyles.Bold,
            new Vector2(0, 155), new Vector2(900, 72));

        Button left = CreateButton("LeftButton", card.transform, "Left", Secondary);
        SetRect(left.GetComponent<RectTransform>(), new Vector2(-245, 25), new Vector2(410, 126));
        UnityEventTools.AddPersistentListener(left.onClick, inputHandler.OnLeftClicked);
        Button right = CreateButton("RightButton", card.transform, "Right", Secondary);
        SetRect(right.GetComponent<RectTransform>(), new Vector2(245, 25), new Vector2(410, 126));
        UnityEventTools.AddPersistentListener(right.onClick, inputHandler.OnRightClicked);

        TMP_Text message = CreateText("AnswerMessage", card.transform, "", 25, FontStyles.Normal,
            new Vector2(0, -110), new Vector2(900, 52));
        message.color = Error;

        Button submit = CreateButton("SubmitButton", card.transform, "Submit Answer", Primary);
        SetRect(submit.GetComponent<RectTransform>(), new Vector2(0, -190), new Vector2(340, 72));
        UnityEventTools.AddPersistentListener(submit.onClick, inputHandler.OnSubmitClicked);
        Button next = CreateButton("NextButton", card.transform, "Next Training Trial", Secondary);
        SetRect(next.GetComponent<RectTransform>(), new Vector2(-350, -325), new Vector2(310, 64));
        UnityEventTools.AddPersistentListener(next.onClick, inputHandler.OnNextClicked);
        Button again = CreateButton("TrainingAgainButton", card.transform, "Restart Training", Secondary);
        SetRect(again.GetComponent<RectTransform>(), new Vector2(0, -325), new Vector2(290, 64));
        UnityEventTools.AddPersistentListener(again.onClick, inputHandler.OnTrainingAgainClicked);
        Button formal = CreateButton("StartFormalButton", card.transform, "Start Formal Study", Primary);
        SetRect(formal.GetComponent<RectTransform>(), new Vector2(350, -325), new Vector2(310, 64));
        UnityEventTools.AddPersistentListener(formal.onClick, inputHandler.OnStartFormalClicked);

        // Keep the training actions visible so the layout remains stable. UI_AnswerInput
        // controls which action is interactable for the current training state.
        next.interactable = false;
        again.interactable = false;
        formal.interactable = false;

        return new AnswerWidgets(phase, block, trial, countdown, message, left, right, submit, next, again, formal);
    }

    private static EndWidgets BuildEndView(Transform view)
    {
        GameObject panel = CreatePanel("EndPanel", view, Background);
        GameObject card = CreateCard("EndCard", panel.transform, Card, new Vector2(920, 430));
        CreateText("EndTitle", card.transform, "Study Complete", 50, FontStyles.Bold,
            new Vector2(0, 110), new Vector2(820, 80));
        TMP_Text summary = CreateText("EndSummary", card.transform,
            "The study is complete. Results were saved successfully.", 30, FontStyles.Normal,
            new Vector2(0, 5), new Vector2(800, 110));
        summary.color = MutedText;
        CreateText("EndInstruction", card.transform,
            "Please keep the headset on and wait for the researcher.", 25, FontStyles.Normal,
            new Vector2(0, -105), new Vector2(800, 64)).color = MutedText;
        return new EndWidgets(summary);
    }

    private static FinishWidgets BuildFinishView(Transform view, StudyManager manager)
    {
        Image dim = CreatePanel("RedoDim", view, new Color(0f, 0f, 0f, 0.58f)).GetComponent<Image>();
        GameObject card = CreateCard("RedoCard", dim.transform, Card, new Vector2(780, 370));
        CreateText("RedoTitle", card.transform, "Additional Trials Required", 38, FontStyles.Bold,
            new Vector2(0, 95), new Vector2(680, 60));
        CreateText("RedoText", card.transform,
            "Some saved trials are missing or invalid. Select Start Redo to complete them.", 26,
            FontStyles.Normal, new Vector2(0, 15), new Vector2(660, 90)).color = MutedText;
        Button redo = CreateButton("StartRedoButton", card.transform, "Start Redo", Primary);
        SetRect(redo.GetComponent<RectTransform>(), new Vector2(0, -105), new Vector2(280, 62));
        UnityEventTools.AddPersistentListener(redo.onClick, manager.StartRedo);
        return new FinishWidgets(redo);
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
        StartWidgets widgets)
    {
        SerializedObject serialized = new SerializedObject(input);
        SetObject(serialized, "studyManager", manager);
        SetObject(serialized, "uiController", controller);
        SetObject(serialized, "participantIDInput", widgets.Participant);
        SetObject(serialized, "blockIDInput", widgets.Block);
        SetObject(serialized, "trialIDInput", widgets.Trial);
        SetObject(serialized, "trainingToggle", widgets.Training);
        SetObject(serialized, "formalToggle", widgets.Formal);
        SetObject(serialized, "messageText", widgets.Message);
        SetObject(serialized, "startStudyButton", widgets.StartStudy);
        SetObject(serialized, "startTrialButton", widgets.StartTrial);
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
        TMP_Text warningText, TMP_Text endSummary, Button retry)
    {
        SerializedObject serialized = new SerializedObject(controller);
        SetObject(serialized, "studyManager", manager);
        SetObject(serialized, "startInput", startInput);
        SetObject(serialized, "startUI", start);
        SetObject(serialized, "answerUI", answer);
        SetObject(serialized, "endUI", end);
        SetObject(serialized, "finishUI", finish);
        SetObject(serialized, "warningUI", warning);
        SetObject(serialized, "warningText", warningText);
        SetObject(serialized, "endSummaryText", endSummary);
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
        canvas.worldCamera = Camera.main;
        RectTransform rect = canvas.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(1200, 800);
        rect.localScale = Vector3.one * 0.0015f;
        if (Camera.main != null)
        {
            Vector3 canvasPosition = Camera.main.transform.position + Camera.main.transform.forward * 2f;
            canvasPosition.y = Mathf.Max(canvasPosition.y, 1f);
            rect.position = canvasPosition;
            rect.rotation = Camera.main.transform.rotation;
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

    private static Toggle CreateToggle(string name, Transform parent, string label, ToggleGroup group, bool isOn)
    {
        GameObject root = CreateUIObject(name, parent);
        Toggle toggle = root.AddComponent<Toggle>();
        toggle.group = group;

        GameObject boxObject = CreateUIObject("Background", root.transform);
        Image box = boxObject.AddComponent<Image>();
        box.color = InputSurface;
        ApplyRoundedSprite(box);
        SetRect(box.rectTransform, new Vector2(-65, 0), new Vector2(34, 34));
        GameObject checkObject = CreateUIObject("Checkmark", boxObject.transform);
        Image check = checkObject.AddComponent<Image>();
        check.color = Primary;
        Stretch(check.rectTransform);
        check.rectTransform.offsetMin = new Vector2(6, 6);
        check.rectTransform.offsetMax = new Vector2(-6, -6);
        toggle.targetGraphic = box;
        toggle.graphic = check;

        TMP_Text text = CreateText("Label", root.transform, label, 24, FontStyles.Normal,
            new Vector2(25, 0), new Vector2(120, 45));
        text.alignment = TextAlignmentOptions.MidlineLeft;
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

        Canvas canvas = UnityEngine.Object.FindObjectOfType<Canvas>(true);
        if (canvas != null && canvas.GetComponent(trackedRaycasterType) == null)
        {
            GraphicRaycaster standard = canvas.GetComponent<GraphicRaycaster>();
            if (standard != null) Undo.DestroyObjectImmediate(standard);
            Undo.AddComponent(canvas.gameObject, trackedRaycasterType);
            changed = true;
        }

        return changed;
    }

    private static string ValidateScene()
    {
        string[] required =
        {
            "StudyCanvas", "StartUIView", "AnswerUIView", "EndUIView", "FinishUIView", "WarningUIView",
            "EventSystem"
        };
        string missing = string.Join(", ", required.Where(item => FindSceneObject(item) == null));
        StudyManager manager = UnityEngine.Object.FindObjectOfType<StudyManager>(true);
        StudyUIController controller = UnityEngine.Object.FindObjectOfType<StudyUIController>(true);
        int missingManagerReferences = 0;
        if (manager != null)
        {
            SerializedObject serialized = new SerializedObject(manager);
            string[] properties = { "playerPosition", "startUI", "answerUI", "warningUI", "endUI", "redoUI", "stimuliRender", "startInput", "answerInput" };
            missingManagerReferences = properties.Count(property => serialized.FindProperty(property).objectReferenceValue == null);
        }

        bool valid = string.IsNullOrEmpty(missing) && manager != null && controller != null && missingManagerReferences == 0;
        return valid
            ? "PASS: all five UI views, controller, EventSystem, and StudyManager references are configured."
            : "FAIL: missing objects: " + (string.IsNullOrEmpty(missing) ? "none" : missing) +
              "; missing manager references: " + missingManagerReferences + ".";
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

    private readonly struct StartWidgets
    {
        public readonly TMP_InputField Participant, Block, Trial;
        public readonly Toggle Training, Formal;
        public readonly TMP_Text Message;
        public readonly Button StartStudy, StartTrial;
        public StartWidgets(TMP_InputField participant, TMP_InputField block, TMP_InputField trial, Toggle training,
            Toggle formal, TMP_Text message, Button startStudy, Button startTrial)
        {
            Participant = participant; Block = block; Trial = trial; Training = training; Formal = formal;
            Message = message; StartStudy = startStudy; StartTrial = startTrial;
        }
    }

    private readonly struct AnswerWidgets
    {
        public readonly TMP_Text Phase, Block, Trial, Countdown, Message;
        public readonly Button Left, Right, Submit, Next, Again, Formal;
        public AnswerWidgets(TMP_Text phase, TMP_Text block, TMP_Text trial, TMP_Text countdown, TMP_Text message,
            Button left, Button right, Button submit, Button next, Button again, Button formal)
        {
            Phase = phase; Block = block; Trial = trial; Countdown = countdown; Message = message;
            Left = left; Right = right; Submit = submit; Next = next; Again = again; Formal = formal;
        }
    }

    private readonly struct EndWidgets
    {
        public readonly TMP_Text Summary;
        public EndWidgets(TMP_Text summary) { Summary = summary; }
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
