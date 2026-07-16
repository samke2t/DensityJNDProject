#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class TemporaryAnswerPreviewEditor
{
    public const string PreviewName = "AnswerUIView_TEMP_PREVIEW_DELETE_BEFORE_BUILD";

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
        Undo.RecordObject(source, "Hide hand-mounted Answer UI while previewing");
        source.SetActive(false);

        // Let the duplicate render through StudyCanvas instead of keeping the hand-mounted canvas.
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
        rect.localScale = Vector3.one;
        rect.sizeDelta = new Vector2(1200f, 800f);
        rect.SetSiblingIndex(Mathf.Min(1, studyCanvas.transform.childCount - 1));

        preview.SetActive(true);
        Selection.activeGameObject = preview;
        EditorGUIUtility.PingObject(preview);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("[DensityJND] Temporary Answer UI preview created under StudyCanvas. " +
                  "Remove it before the final build.");
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

    private static GameObject FindSceneObject(string objectName)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(item => item.scene.IsValid() && item.name == objectName);
    }
}
#endif
