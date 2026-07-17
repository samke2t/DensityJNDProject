#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class TemporaryAnswerPreviewEditor
{
    public const string PreviewName = "AnswerUIView_TEMP_PREVIEW_DELETE_BEFORE_BUILD";
    private const float PreviewScale = 0.28f;
    private const float TrainingAnswerButtonY = 35f;

    [MenuItem("Tools/Density JND/Temporary Answer Preview/Show Training Answer Page In Play Mode")]
    public static void ShowTrainingAnswerPageInPlayMode()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Temporary Answer Preview",
                "Enter Play Mode before opening the Training answer page.",
                "OK");
            return;
        }

        StudyManager manager = Object.FindObjectOfType<StudyManager>(true);
        StudyUIController controller = Object.FindObjectOfType<StudyUIController>(true);
        UI_AnswerInput input = Object.FindObjectOfType<UI_AnswerInput>(true);
        if (manager == null || controller == null || input == null)
        {
            Debug.LogWarning("[DensityJND] The Training answer preview could not find its UI components.");
            return;
        }

        manager.currentPhase = StudyManager.StudyPhase.Training;
        manager.currentBlock = 0;
        manager.currentTrainingTrial = 0;
        manager.trialStartTime = -1f;
        input.RefreshTrialInformation();
        controller.ShowAnswering();
        Debug.Log("[DensityJND] Showing the Training answer page for visual preview.");
    }

    [MenuItem("Tools/Density JND/Temporary Answer Preview/Show Formal Answer Page In Play Mode")]
    public static void ShowFormalAnswerPageInPlayMode()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Temporary Answer Preview",
                "Enter Play Mode before opening the Formal answer page.",
                "OK");
            return;
        }

        StudyManager manager = Object.FindObjectOfType<StudyManager>(true);
        StudyUIController controller = Object.FindObjectOfType<StudyUIController>(true);
        UI_AnswerInput input = Object.FindObjectOfType<UI_AnswerInput>(true);
        if (manager == null || controller == null || input == null)
        {
            Debug.LogWarning("[DensityJND] The Formal answer preview could not find its UI components.");
            return;
        }

        manager.currentPhase = StudyManager.StudyPhase.Formal;
        manager.currentBlock = 0;
        manager.currentFormalTrial = 0;
        manager.trialStartTime = -1f;
        input.RefreshTrialInformation();
        controller.ShowAnswering();
        Debug.Log("[DensityJND] Showing the Formal answer page for visual preview.");
    }

    [MenuItem("Tools/Density JND/Temporary Answer Preview/Show Training Complete Page In Play Mode")]
    public static void ShowTrainingCompletePageInPlayMode()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Temporary Answer Preview",
                "Enter Play Mode before opening the Training Complete page.",
                "OK");
            return;
        }

        StudyManager manager = Object.FindObjectOfType<StudyManager>(true);
        StudyUIController controller = Object.FindObjectOfType<StudyUIController>(true);
        UI_AnswerInput input = Object.FindObjectOfType<UI_AnswerInput>(true);
        if (manager == null || controller == null || input == null)
        {
            Debug.LogWarning("[DensityJND] The Training Complete preview could not find its UI components.");
            return;
        }

        manager.currentPhase = StudyManager.StudyPhase.Training;
        manager.currentBlock = 0;
        manager.currentTrainingTrial = Mathf.Max(0, manager.TrainingTrialCount - 1);
        manager.trialStartTime = -1f;
        input.RefreshTrialInformation();
        input.ShowTrainingReadyPageForPreview();
        controller.ShowAnswering();
        Debug.Log("[DensityJND] Showing the Training Complete page for visual preview.");
    }

    [MenuItem("Tools/Density JND/Temporary Answer Preview/Create Under StudyCanvas")]
    public static void CreatePreview()
    {
        GameObject studyCanvas = FindSceneObject("StudyCanvas");
        GameObject source = FindSceneObject("AnswerUIView");
        if (studyCanvas == null || source == null)
        {
            EditorUtility.DisplayDialog(
                "Temporary Answer Preview",
                "StudyCanvas or the original AnswerUIView could not be found.",
                "OK");
            return;
        }

        RemoveExistingPreview();

        GameObject preview = Object.Instantiate(source, studyCanvas.transform, false);
        preview.name = PreviewName;
        Undo.RegisterCreatedObjectUndo(preview, "Create temporary Answer UI preview");
        Undo.RecordObject(source, "Hide original Answer UI while previewing");
        source.SetActive(false);

        // Ensure the duplicate renders through StudyCanvas like the live Answer UI.
        Component[] rootComponents = preview.GetComponents<Component>();
        foreach (Component component in rootComponents.Reverse())
        {
            if (component is GraphicRaycaster ||
                component.GetType().FullName ==
                "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster")
            {
                Object.DestroyImmediate(component);
            }
        }

        CanvasScaler scaler = preview.GetComponent<CanvasScaler>();
        if (scaler != null) Object.DestroyImmediate(scaler);
        Canvas canvas = preview.GetComponent<Canvas>();
        if (canvas != null) Object.DestroyImmediate(canvas);

        RectTransform rect = preview.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition3D = Vector3.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one * PreviewScale;
        rect.sizeDelta = new Vector2(1200f, 800f);
        rect.SetSiblingIndex(Mathf.Min(1, studyCanvas.transform.childCount - 1));

        ApplyTrainingLayout(preview);

        preview.SetActive(true);
        Selection.activeGameObject = preview;
        EditorGUIUtility.PingObject(preview);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("[DensityJND] Temporary Answer UI preview created under StudyCanvas. " +
                  "Remove it before the final build.");
    }

    [MenuItem("Tools/Density JND/Temporary Answer Preview/Apply Training Layout")]
    public static void ApplyTrainingLayoutToExistingPreview()
    {
        GameObject preview = FindSceneObject(PreviewName);
        if (preview == null)
        {
            EditorUtility.DisplayDialog(
                "Temporary Answer Preview",
                "No temporary Answer UI preview is present.",
                "OK");
            return;
        }

        ApplyTrainingLayout(preview);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[DensityJND] Applied the Training button layout to the temporary Answer UI preview.");
    }

    [MenuItem("Tools/Density JND/Temporary Answer Preview/Remove Before Build")]
    public static void RemovePreview()
    {
        if (!RemoveExistingPreview())
        {
            EditorUtility.DisplayDialog(
                "Temporary Answer Preview",
                "No temporary Answer UI preview is present.",
                "OK");
            return;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[DensityJND] Temporary Answer UI preview removed.");
    }

    private static bool RemoveExistingPreview()
    {
        GameObject existing = FindSceneObject(PreviewName);
        if (existing == null)
        {
            return false;
        }

        Undo.DestroyObjectImmediate(existing);
        return true;
    }

    private static void ApplyTrainingLayout(GameObject preview)
    {
        RectTransform previewRect = preview.GetComponent<RectTransform>();
        if (previewRect != null)
        {
            Undo.RecordObject(previewRect, "Resize temporary Answer UI preview");
            previewRect.localScale = Vector3.one * PreviewScale;
        }

        foreach (string buttonName in new[] { "LeftButton", "RightButton" })
        {
            RectTransform button = preview.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(item => item.name == buttonName);
            if (button == null)
            {
                continue;
            }

            Undo.RecordObject(button, "Move Training answer button");
            Vector2 position = button.anchoredPosition;
            position.y = TrainingAnswerButtonY;
            button.anchoredPosition = position;
        }
    }

    private static GameObject FindSceneObject(string objectName)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(item => item.scene.IsValid() && item.name == objectName);
    }
}
#endif
