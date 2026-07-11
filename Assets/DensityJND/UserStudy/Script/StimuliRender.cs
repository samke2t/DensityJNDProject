using System.Collections.Generic;
using UnityEngine;

public class StimuliRender : MonoBehaviour
{
    [Header("Render Settings")]
    public Material pointMaterial;
    public GameObject spherePrefab;
    public float pointSize = 0.01f;
    public Color stimulusColor = Color.white;

    private const int BatchSize = 1023;

    private Mesh sphereMesh;
    private bool isRender = false;

    private readonly List<RenderBatch> renderBatches = new List<RenderBatch>();

    private sealed class RenderBatch
    {
        public int Count;
        public Matrix4x4[] Matrices;
    }

    // 初始化渲染资源
    public void Awake()
    {
        MeshFilter meshFilter = spherePrefab.GetComponent<MeshFilter>();
        sphereMesh = meshFilter.sharedMesh;

        pointMaterial.enableInstancing = true;
        SetMaterialColor(pointMaterial, stimulusColor);

        isRender = false;
    }

    // 渲染已经转换好的 world position
    public void Render(Vector3[] worldPoints)
    {
        BuildMatrixCache(worldPoints);
        isRender = true;
    }

    // 建立当前 trial 的 Matrix 缓存
    private void BuildMatrixCache(Vector3[] worldPoints)
    {
        renderBatches.Clear();

        Vector3 scale = Vector3.one * pointSize;

        int offset = 0;

        while (offset < worldPoints.Length)
        {
            int count = Mathf.Min(BatchSize, worldPoints.Length - offset);

            RenderBatch batch = new RenderBatch();
            batch.Count = count;
            batch.Matrices = new Matrix4x4[count];

            for (int i = 0; i < count; i++)
            {
                Vector3 position = worldPoints[offset + i];
                batch.Matrices[i] = Matrix4x4.TRS(position, Quaternion.identity, scale);
            }

            renderBatches.Add(batch);

            offset += count;
        }
    }

    // 每帧绘制当前缓存好的点
    private void LateUpdate()
    {
        if (!isRender)
        {
            return;
        }

        for (int i = 0; i < renderBatches.Count; i++)
        {
            RenderBatch batch = renderBatches[i];

            Graphics.DrawMeshInstanced(
                sphereMesh,
                0,
                pointMaterial,
                batch.Matrices,
                batch.Count
            );
        }
    }

    // 清空当前 trial 的刺激
    public void ReleaseForReinit()
    {
        if(isRender == true)
        {
            renderBatches.Clear();
        }
        isRender = false;
    }

    // 设置材质颜色
    private void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        else if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private void OnDestroy()
    {
        ReleaseForReinit();
        sphereMesh = null;
    }
}