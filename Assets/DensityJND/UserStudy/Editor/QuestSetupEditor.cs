#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;
using UnityEngine.UI;
using UnityEngine.Rendering;

public static class QuestSetupEditor
{
    private const string PackageVersion = "2.5.4";
    private const string SetupName = "Quest XR Interaction Setup";
    private const string StarterAssetsRoot = "Assets/Samples/XR Interaction Toolkit/2.5.4/Starter Assets";

    [MenuItem("Tools/Density JND/Configure Quest XR")]
    public static void ConfigureFromMenu()
    {
        Configure(true);
    }

    public static void ConfigureBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/DensityJND_Equal.unity", OpenSceneMode.Single);
        Configure(false);
    }

    public static void BuildAndConfigureBatch()
    {
        StudyUISetupEditor.BuildCompleteUIBatch();
        Configure(false);
    }

    public static void BuildQuestApkBatch()
    {
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/DensityJND_Equal.unity" },
            locationPathName = "/tmp/DensityJNDQuest.apk",
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.Development
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new System.InvalidOperationException("Quest APK build failed: " + report.summary.result);
        }
        Debug.Log("Density JND Quest APK build PASS: " + report.summary.totalSize + " bytes.");
    }

    private static void Configure(bool showDialog)
    {
        ImportStarterAssets();
        bool openXrReady = ConfigureOpenXR();
        bool rigReady = ConfigureRigAndUI();
        ConfigureAndroidPlayer();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        if (!openXrReady || !rigReady)
        {
            throw new System.InvalidOperationException("Density JND Quest setup did not complete. Check the preceding errors.");
        }

        Debug.Log("Density JND Quest setup complete: OpenXR Android loader, Meta Quest support, Oculus Touch profile, XR Origin, controller rays, and XR UI input are configured.");
        if (showDialog)
        {
            EditorUtility.DisplayDialog("Density JND", "Quest XR scene setup completed. Install the Android Build Support module before building an APK.", "OK");
        }
    }

    private static void ImportStarterAssets()
    {
        Sample sample = Sample.FindByPackage("com.unity.xr.interaction.toolkit", PackageVersion)
            .FirstOrDefault(item => item.displayName == "Starter Assets");
        if (string.IsNullOrEmpty(sample.displayName))
        {
            Debug.LogError("XR Interaction Toolkit Starter Assets sample was not found.");
            return;
        }

        string prefabPath = StarterAssetsRoot + "/Prefabs/XR Interaction Setup.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            sample.Import(Sample.ImportOptions.OverridePreviousImports);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
    }

    private static bool ConfigureOpenXR()
    {
        XRGeneralSettingsPerBuildTarget perTarget;
        EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out perTarget);
        if (perTarget == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/XR"))
            {
                AssetDatabase.CreateFolder("Assets", "XR");
            }
            perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
        }

        if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
        {
            perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
        }

        XRManagerSettings androidManager = perTarget.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
        XRPackageMetadataStore.AssignLoader(androidManager, typeof(OpenXRLoader).FullName, BuildTargetGroup.Android);

        OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        if (settings == null)
        {
            Debug.LogError("Android OpenXR settings were not created.");
            return false;
        }

        MetaQuestFeature metaQuest = settings.GetFeature<MetaQuestFeature>();
        if (metaQuest != null)
        {
            metaQuest.enabled = true;
            EditorUtility.SetDirty(metaQuest);
        }

        OculusTouchControllerProfile touch = settings.GetFeature<OculusTouchControllerProfile>();
        if (touch != null)
        {
            touch.enabled = true;
            EditorUtility.SetDirty(touch);
        }
        return metaQuest != null && touch != null;
    }

    private static bool ConfigureRigAndUI()
    {
        Sample sample = Sample.FindByPackage("com.unity.xr.interaction.toolkit", PackageVersion)
            .FirstOrDefault(item => item.displayName == "Starter Assets");
        if (string.IsNullOrEmpty(sample.displayName)) return false;

        GameObject existing = FindSceneObject(SetupName);
        if (existing == null)
        {
            string prefabPath = StarterAssetsRoot + "/Prefabs/XR Interaction Setup.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError("XR Interaction Setup prefab could not be imported.");
                return false;
            }

            existing = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            existing.name = SetupName;
            Undo.RegisterCreatedObjectUndo(existing, "Create Quest XR setup");
        }

        Camera xrCamera = existing.GetComponentInChildren<Camera>(true);
        if (xrCamera != null)
        {
            xrCamera.clearFlags = CameraClearFlags.SolidColor;
            xrCamera.backgroundColor = Color.black;
            EditorUtility.SetDirty(xrCamera);
        }

        Camera oldCamera = Resources.FindObjectsOfTypeAll<Camera>()
            .FirstOrDefault(item => item != xrCamera && item.gameObject.scene.IsValid() && item.gameObject.name == "Main Camera");
        if (oldCamera != null && xrCamera != null)
        {
            Vector3 cameraOffset = xrCamera.transform.position - existing.transform.position;
            existing.transform.position = oldCamera.transform.position - cameraOffset;
            oldCamera.gameObject.SetActive(false);
        }

        foreach (Renderer renderer in existing.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
            {
                renderer.enabled = false;
            }
        }

        foreach (EventSystem eventSystem in Object.FindObjectsOfType<EventSystem>(true))
        {
            if (!eventSystem.transform.IsChildOf(existing.transform))
            {
                Object.DestroyImmediate(eventSystem.gameObject);
            }
        }

        Canvas canvas = Object.FindObjectOfType<Canvas>(true);
        if (canvas != null)
        {
            canvas.worldCamera = xrCamera;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
            canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
            canvasRect.anchoredPosition = Vector2.zero;
            GraphicRaycaster standard = canvas.GetComponent<GraphicRaycaster>();
            if (standard != null) Object.DestroyImmediate(standard);
            if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            }
            if (xrCamera != null)
            {
                Transform previewPose = oldCamera != null ? oldCamera.transform : xrCamera.transform;
                canvas.transform.position = previewPose.position + previewPose.forward * 2f;
                canvas.transform.rotation = previewPose.rotation;
                canvas.transform.localPosition = new Vector3(0f, 1f, -8f);
            }
        }

        StudyManager manager = Object.FindObjectOfType<StudyManager>(true);
        if (manager != null && xrCamera != null)
        {
            manager.playerPosition = xrCamera.transform;
            EditorUtility.SetDirty(manager);
        }
        return existing != null && xrCamera != null && manager != null && canvas != null;
    }

    private static void ConfigureAndroidPlayer()
    {
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.densityjnd.study");
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });

        UnityEngine.Object playerSettingsAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")
            .FirstOrDefault();
        if (playerSettingsAsset != null)
        {
            SerializedObject serialized = new SerializedObject(playerSettingsAsset);
            SerializedProperty activeInputHandler = serialized.FindProperty("activeInputHandler");
            if (activeInputHandler != null) activeInputHandler.intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/DensityJND_Equal.unity", true)
        };
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(item => item.scene.IsValid() && item.name == name);
    }
}
#endif
