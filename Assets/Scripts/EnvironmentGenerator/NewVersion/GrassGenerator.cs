using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class GrassGeneratorBatched : MonoBehaviour
{
    #region Structures
    [StructLayout(LayoutKind.Sequential)]
    private struct GrassData
    {
        internal float id;          // 4 bytes
        internal Vector3 rotation;  // 12 bytes
        internal Vector2 scale;     // 8 bytes
        internal Vector3 position;  // 12 bytes
        internal Vector3 normal;    // 12 bytes
        internal float pad;         // 4 bytes (Total = 48 bytes)
    }
    #endregion

    #region Inspector Fields
    [Header("References")]
    [SerializeField] private TerrainGenerator terrainGenerator;
    [SerializeField] private HeightMapToMesh visualizer;
    [SerializeField] private ComputeShader scatterShader;
    [SerializeField] private ComputeShader cullingShader;
    [SerializeField] private Material grassMaterial;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Mesh grassMesh;
    [SerializeField] private HiZGenerator hiZGenerator; // <-- Added Hi-Z Depth Generator

    [Header("Chunk Grid Batching")]
    [SerializeField] private Vector2Int gridDimensions = new Vector2Int(4, 4);
    [Tooltip("Check this if your HeightMapToMesh centers the generated terrain mesh around (0,0,0)!")]
    [SerializeField] private bool centerMeshOrigin = true;

    [Header("Grass Density & Distribution")]
    [SerializeField] private int maxGrassPerChunk = 10000;
    [SerializeField, Range(0.1f, 5.0f)] private float spawnDecayRate = 1.5f;
    [SerializeField, Range(0.0f, 1.0f)] private float heightLimitCutoff = 0.85f;
    [SerializeField, Range(0.0f, 1.0f)] private float flatlandBias = 0.8f;
    [SerializeField] private bool useBetaClustering = false;
    [SerializeField, Range(0.5f, 5.0f)] private float betaAlpha = 2.0f;
    [SerializeField, Range(0.5f, 5.0f)] private float betaBeta = 2.0f;

    [Header("Grass Scale & Dimensions (World Units)")]
    [SerializeField] private Vector2 baseScale = new Vector2(0.05f, 0.3f);
    [SerializeField] private Vector2 scaleRandomness = new Vector2(0.02f, 0.1f);

    [Header("Culling & Distance")]
    [SerializeField] private float boundingRadius = 1.0f;
    [SerializeField, Range(10f, 1000f)] private float maxDrawDistance = 300.0f;
    [SerializeField, Range(1f, 50f)] private float fadeDistance = 20.0f;

    [Header("Debug Controls")]
    [SerializeField] private bool enableDebugConsoleLog = true;
    #endregion

    #region Private State
    private GraphicsBuffer allInstancesBuffer = null;
    private GraphicsBuffer culledInstancesBuffer = null;
    private GraphicsBuffer argumentsBuffer = null;
    private GraphicsBuffer counterBuffer = null;
    private GraphicsBuffer chunkPositionsBuffer = null;

    private int scatterKernel;
    private int resetKernel, cullingKernel;

    private Vector4[] frustumPlanes = new Vector4[6];
    private RenderParams renderParams;
    private bool isInitialized = false;
    private int totalMaxGrass;
    private float chunkSize;

    private static readonly int VPMatrixID = Shader.PropertyToID("_VPMatrix");
    private static readonly int HiZBufferID = Shader.PropertyToID("_HiZBuffer");
    private static readonly int ScreenWidthID = Shader.PropertyToID("_ScreenWidth");
    private static readonly int ScreenHeightID = Shader.PropertyToID("_ScreenHeight");
    private static readonly int CameraPositionID = Shader.PropertyToID("_CameraPosition");
    private static readonly int FrustumPlanesID = Shader.PropertyToID("_FrustumPlanes");
    #endregion

    private void Awake()
    {
        if (!scatterShader) throw new ArgumentNullException(nameof(scatterShader));
        if (!cullingShader) throw new ArgumentNullException(nameof(cullingShader));
        if (!grassMaterial) throw new ArgumentNullException(nameof(grassMaterial));
        if (!grassMesh) throw new ArgumentNullException(nameof(grassMesh));

        if (targetCamera == null) targetCamera = Camera.main;
        if (terrainGenerator == null) terrainGenerator = GetComponent<TerrainGenerator>();
        if (visualizer == null) visualizer = GetComponent<HeightMapToMesh>();

        scatterKernel = scatterShader.FindKernel("CSScatterGrassBatched");
        resetKernel = cullingShader.FindKernel("ResetIndexBatch");
        cullingKernel = cullingShader.FindKernel("CullingBatch");
    }

    [ContextMenu("Generate Batched Grass (GPU)")]
    public void GenerateGrassBatchedGPU()
    {
        if (terrainGenerator == null || terrainGenerator.FinalHeightMapRT == null)
        {
            Debug.LogWarning("[GrassGeneratorBatched] TerrainGenerator or HeightMap missing!");
            return;
        }

        ReleaseBuffers();

        // 1. Calculate terrain dimensions automatically from mesh bounds
        float actualTerrainWidth = 50.0f; // Fallback
        MeshFilter mf = visualizer != null ? visualizer.GetComponent<MeshFilter>() : GetComponent<MeshFilter>();

        if (mf != null && mf.sharedMesh != null)
        {
            actualTerrainWidth = mf.sharedMesh.bounds.size.x * (visualizer != null ? visualizer.transform.lossyScale.x : transform.lossyScale.x);
        }

        // Auto-compute chunkSize to cover the terrain mesh accurately
        chunkSize = actualTerrainWidth / gridDimensions.x;
        Vector2 terrainSize = new Vector2(actualTerrainWidth, actualTerrainWidth);

        Vector3 terrainOrigin = visualizer != null ? visualizer.transform.position : transform.position;
        if (centerMeshOrigin)
        {
            terrainOrigin -= new Vector3(terrainSize.x * 0.5f, 0f, terrainSize.y * 0.5f);
        }

        int totalChunks = gridDimensions.x * gridDimensions.y;
        totalMaxGrass = maxGrassPerChunk * totalChunks;
        float heightScale = visualizer != null ? visualizer.HeightScale : 15.0f;

        // 2. Buffer Allocations
        int stride = Marshal.SizeOf<GrassData>();
        allInstancesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, totalMaxGrass, stride);
        culledInstancesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, totalMaxGrass, stride);

        argumentsBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Structured,
            5,
            sizeof(uint)
        );

        counterBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, sizeof(uint));
        counterBuffer.SetData(new uint[] { 0 });

        chunkPositionsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, totalChunks, Marshal.SizeOf<Vector4>());
        Vector4[] chunkPositions = new Vector4[totalChunks];

        for (int z = 0; z < gridDimensions.y; z++)
        {
            for (int x = 0; x < gridDimensions.x; x++)
            {
                int idx = x + z * gridDimensions.x;
                Vector3 pos = terrainOrigin + new Vector3(x * chunkSize, 0, z * chunkSize);
                chunkPositions[idx] = new Vector4(pos.x, pos.y, pos.z, 1.0f);
            }
        }
        chunkPositionsBuffer.SetData(chunkPositions);

        uint indexCount = grassMesh.GetIndexCount(0);
        uint indexStart = grassMesh.GetIndexStart(0);
        uint baseVertex = grassMesh.GetBaseVertex(0);

        uint[] args = { indexCount, 0, indexStart, baseVertex, 0 };
        argumentsBuffer.SetData(args);

        // 3. Set Scatter Shader Properties
        scatterShader.SetBuffer(scatterKernel, "_GrassInstances", allInstancesBuffer);
        scatterShader.SetBuffer(scatterKernel, "_CounterBuffer", counterBuffer);
        scatterShader.SetBuffer(scatterKernel, "_ChunkPositions", chunkPositionsBuffer);
        scatterShader.SetTexture(scatterKernel, "_HeightMap", terrainGenerator.FinalHeightMapRT);

        scatterShader.SetInt("_MaxGrassPerChunk", maxGrassPerChunk);
        scatterShader.SetInt("_TotalMaxGrass", totalMaxGrass);
        scatterShader.SetInt("_GridX", gridDimensions.x);
        scatterShader.SetInt("_GridY", gridDimensions.y);
        scatterShader.SetFloat("_ChunkSize", chunkSize);

        scatterShader.SetVector("_TerrainOrigin", terrainOrigin);
        scatterShader.SetVector("_TerrainSize", terrainSize);

        scatterShader.SetFloat("_SpawnDecayRate", spawnDecayRate);
        scatterShader.SetFloat("_HeightLimitCutoff", heightLimitCutoff);
        scatterShader.SetFloat("_FlatlandBias", flatlandBias);
        scatterShader.SetBool("_UseBetaClustering", useBetaClustering);
        scatterShader.SetFloat("_BetaAlpha", betaAlpha);
        scatterShader.SetFloat("_BetaBeta", betaBeta);
        scatterShader.SetVector("_BaseScale", baseScale);
        scatterShader.SetVector("_ScaleRandomness", scaleRandomness);
        scatterShader.SetFloat("_HeightScale", heightScale);

        // Dispatch Compute Shader
        int threadsPerChunk = Mathf.CeilToInt(maxGrassPerChunk / 64.0f);
        scatterShader.Dispatch(scatterKernel, threadsPerChunk, gridDimensions.x, gridDimensions.y);

        renderParams = new RenderParams(grassMaterial)
        {
            worldBounds = new Bounds(Vector3.zero, Vector3.one * 100000.0f)
        };

        grassMaterial.SetBuffer("_DataBuffer", culledInstancesBuffer);
        isInitialized = true;

        if (enableDebugConsoleLog)
        {
            Debug.Log($"<color=lime>[GrassGeneratorBatched]</color> Successfully initialized grass covering {actualTerrainWidth}x{actualTerrainWidth} units!");
        }
    }

    private void Update()
    {
        if (!isInitialized) return;
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) return;

        if (hiZGenerator == null && targetCamera != null)
        {
            hiZGenerator = targetCamera.GetComponent<HiZGenerator>();
            if (hiZGenerator == null) hiZGenerator = FindAnyObjectByType<HiZGenerator>();
            if (hiZGenerator == null) hiZGenerator = targetCamera.gameObject.AddComponent<HiZGenerator>();
        }

        // 1. Calculate Frustum Planes
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(targetCamera);
        for (int i = 0; i < 6; ++i)
        {
            frustumPlanes[i] = new Vector4(planes[i].normal.x, planes[i].normal.y, planes[i].normal.z, planes[i].distance);
        }

        // 2. Correct Projection Matrix with Platform UV Flipping
        Matrix4x4 gpuProj = GL.GetGPUProjectionMatrix(targetCamera.projectionMatrix, SystemInfo.graphicsUVStartsAtTop);
        Matrix4x4 vpMatrix = gpuProj * targetCamera.worldToCameraMatrix;
        int threadGroups = Mathf.CeilToInt(totalMaxGrass / 64.0f);

        // 3. Reset Counter / Instance Count in Argument Buffer
        cullingShader.SetBuffer(resetKernel, "_GlobalArgsBuffer", argumentsBuffer);
        cullingShader.SetInt("_ScrapTypeCount", 1);
        cullingShader.Dispatch(resetKernel, 1, 1, 1);

        // 4. Pass Uniforms to Culling Kernel
        cullingShader.SetMatrix(VPMatrixID, vpMatrix);
        cullingShader.SetVectorArray(FrustumPlanesID, frustumPlanes);
        cullingShader.SetVector(CameraPositionID, targetCamera.transform.position);
        cullingShader.SetFloat("_MaxDrawDistance", maxDrawDistance);
        cullingShader.SetFloat("_FadeDistance", fadeDistance);

        // Feed real screen dimensions to Compute Shader
        cullingShader.SetFloat(ScreenWidthID, (float)targetCamera.pixelWidth);
        cullingShader.SetFloat(ScreenHeightID, (float)targetCamera.pixelHeight);

        bool hasHiZ = hiZGenerator != null && hiZGenerator.HiZTexture != null && hiZGenerator.HiZTexture.IsCreated();
        Texture activeHiZ = hasHiZ ? (Texture)hiZGenerator.HiZTexture : Texture2D.blackTexture;
        int maxMipLevel = hasHiZ ? Mathf.Max(0, hiZGenerator.HiZTexture.mipmapCount - 1) : 0;

        cullingShader.SetTexture(cullingKernel, HiZBufferID, activeHiZ);
        cullingShader.SetInt("_MaxMipLevel", maxMipLevel);
        cullingShader.SetInt("_HiZEnabled", hasHiZ ? 1 : 0);

        cullingShader.SetBuffer(cullingKernel, "_GlobalArgsBuffer", argumentsBuffer);
        cullingShader.SetBuffer(cullingKernel, "_AllInstancesBuffer", allInstancesBuffer);
        cullingShader.SetBuffer(cullingKernel, "_CulledInstancesBuffer", culledInstancesBuffer);

        cullingShader.SetInt("_TotalInstances", totalMaxGrass);
        cullingShader.SetInt("_MaxInstancesPerType", totalMaxGrass);
        cullingShader.SetInt("_ScrapTypeCount", 1);
        cullingShader.SetFloat("_BoundingRadius", boundingRadius);

        // 5. Dispatch Culling & Render Indirect
        cullingShader.Dispatch(cullingKernel, threadGroups, 1, 1);
        Graphics.RenderMeshIndirect(renderParams, grassMesh, argumentsBuffer, 1, 0);
    }

    private void OnDisable() => ReleaseBuffers();
    private void OnDestroy() => ReleaseBuffers();

    private void ReleaseBuffers()
    {
        isInitialized = false;
        allInstancesBuffer?.Release(); allInstancesBuffer = null;
        culledInstancesBuffer?.Release(); culledInstancesBuffer = null;
        argumentsBuffer?.Release(); argumentsBuffer = null;
        counterBuffer?.Release(); counterBuffer = null;
        chunkPositionsBuffer?.Release(); chunkPositionsBuffer = null;
    }
}
