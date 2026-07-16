#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

[InitializeOnLoad]
public static class BilingualAnswerFontAssetEditor
{
    private const string SourceFontPath = "Assets/Resources/Fonts/NotoSansSC-AnswerSubset.ttf";
    private const string FontAssetPath = "Assets/Resources/Fonts/NotoSansSC-AnswerTMP.asset";

    static BilingualAnswerFontAssetEditor()
    {
        EditorApplication.delayCall += EnsureFontAsset;
    }

    [MenuItem("Tools/Density JND/Ensure Bilingual Answer Font")]
    public static void EnsureFontAsset()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath) != null)
        {
            return;
        }

        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (sourceFont == null)
        {
            Debug.LogError("[DensityJND] Cannot create the bilingual Answer UI font: source font is missing.");
            return;
        }

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            90,
            9,
            GlyphRenderMode.SDFAA,
            1024,
            1024,
            AtlasPopulationMode.Dynamic,
            true);
        if (fontAsset == null)
        {
            Debug.LogError("[DensityJND] Failed to create the bilingual Answer UI TMP font asset.");
            return;
        }

        fontAsset.name = "NotoSansSC-AnswerTMP";
        AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

        if (fontAsset.atlasTexture != null)
        {
            fontAsset.atlasTexture.name = "NotoSansSC-AnswerTMP Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
        }
        if (fontAsset.material != null)
        {
            fontAsset.material.name = "NotoSansSC-AnswerTMP Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceUpdate);
        Debug.Log("[DensityJND] Persistent bilingual Answer UI TMP font asset created.");
    }
}
#endif
