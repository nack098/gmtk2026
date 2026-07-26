using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

[System.Serializable]
public struct ScrapTypeConfig
{
    public string name;
    public Mesh mesh;
    public Material material;
}

[System.Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct ScrapInstanceData
{
    public float id;
    public Vector3 rotation;
    public Vector2 scale;
    public Vector3 position;
    public Vector3 normal;
    public float pad;
}

[DisallowMultipleComponent]
public class GpuJunkyardPopulator : MonoBehaviour
{
    [Header("Compute Shader References")]
    [SerializeField] private ComputeShader _scatterComputeShader;
    [SerializeField] private ComputeShader _cullingComputeShader;

    [Header("Generator References")]
    [SerializeField] private TerrainGenerator _terrainGenerator;
    [SerializeField] private HeightMapToMesh _visualizer;
    [SerializeField] private Camera _targetCamera;
    [SerializeField] private HiZGenerator _hiZGenerator;

    [Header("Scrap Mesh Variations")]
    [SerializeField] private ScrapTypeConfig[] _scrapTypes;

    public ScrapTypeConfig[] ScrapTypes => _scrapTypes;
    public ScrapInstanceData[] CachedInstances { get; private set; }
    public bool IsDataReady { get; private set; }

    [Header("Scattering Parameters")]
    [SerializeField] private int _scrapDensityGrid = 80;
    [SerializeField] private float _basePoissonLambda = 7.0f;
    [SerializeField] private float _positionJitter = 0.4f;
    [SerializeField] private Vector2 _scaleRange = new Vector2(0.5f, 1.5f);

    [Header("Height Masking Controls")]
    [SerializeField, Range(0f, 0.9f)] private float _heightCutoff = 0.3f;
    [SerializeField, Range(0.5f, 5f)] private float _heightExponent = 3.0f;

    [Header("Culling & LOD Settings")]
    [SerializeField] private float _maxDrawDistance = 150.0f;
    [SerializeField] private float _boundingRadius = 1.0f;

    // GPU Buffers & Textures
    private GraphicsBuffer _allInstancesBuffer;
    private GraphicsBuffer _culledInstancesBuffer;
    private GraphicsBuffer _globalArgsBuffer;
    private GraphicsBuffer _counterBuffer;

    private RenderParams[] _renderParamsList;
    private readonly Vector4[] _frustumPlanes = new Vector4[6];

    private int _scatterKernel;
    private int _resetKernel;
    private int _cullKernel;

    private int _maxInstanceCapacity;
    private int _maxInstancesPerType;
    private bool _isInitialized = false;

    private static readonly int DataBufferID = Shader.PropertyToID("_DataBuffer");
    private static readonly int VPMatrixID = Shader.PropertyToID("_VPMatrix");
    private static readonly int FrustumPlanesID = Shader.PropertyToID("_FrustumPlanes");
    private static readonly int CameraPositionID = Shader.PropertyToID("_CameraPosition");
    private static readonly int HiZBufferID = Shader.PropertyToID("_HiZBuffer");
    private static readonly int ScreenWidthID = Shader.PropertyToID("_ScreenWidth");
    private static readonly int ScreenHeightID = Shader.PropertyToID("_ScreenHeight");

    private void OnDisable() => ReleaseBuffers();
    private void OnDestroy() => ReleaseBuffers();

    [ContextMenu("Generate Junk (GPU Pipeline)")]
    public void GenerateJunkGPU()
    {
        if (_scatterComputeShader == null || _cullingComputeShader == null) return;
        if (_terrainGenerator == null || _terrainGenerator.FinalHeightMapRT == null) return;
        if (_scrapTypes == null || _scrapTypes.Length == 0) return;

        ReleaseBuffers();

        if (_targetCamera == null) _targetCamera = Camera.main;

        _scatterKernel = _scatterComputeShader.FindKernel("K_ScatterJunk");
        _resetKernel = _cullingComputeShader.FindKernel("ResetIndexBatch");
        _cullKernel = _cullingComputeShader.FindKernel("CullingBatch");

        float meshSize = _visualizer != null ? _visualizer.MeshSize : 50.0f;
        float heightScale = _visualizer != null ? _visualizer.HeightScale : 15.0f;

        _maxInstanceCapacity = _scrapDensityGrid * _scrapDensityGrid * 8;
        _maxInstancesPerType = _maxInstanceCapacity;

        int instanceStride = Marshal.SizeOf<ScrapInstanceData>();
        int totalCapacity = _maxInstancesPerType * _scrapTypes.Length;

        _allInstancesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _maxInstanceCapacity, instanceStride);
        _culledInstancesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, totalCapacity, instanceStride);
        _counterBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, sizeof(uint));
        _counterBuffer.SetData(new uint[] { 0 });

        int totalUintCount = _scrapTypes.Length * 5;
        _globalArgsBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Structured,
            totalUintCount,
            sizeof(uint)
        );

        uint[] initialArgs = new uint[totalUintCount];
        _renderParamsList = new RenderParams[_scrapTypes.Length];
        Bounds bounds = new Bounds(transform.position, new Vector3(meshSize, heightScale * 3f, meshSize));

        for (int i = 0; i < _scrapTypes.Length; i++)
        {
            if (_scrapTypes[i].mesh == null || _scrapTypes[i].material == null)
            {
                ReleaseBuffers();
                return;
            }

            int argsOffset = i * 5;
            initialArgs[argsOffset + 0] = _scrapTypes[i].mesh.GetIndexCount(0);
            initialArgs[argsOffset + 1] = 0;
            initialArgs[argsOffset + 2] = _scrapTypes[i].mesh.GetIndexStart(0);
            initialArgs[argsOffset + 3] = _scrapTypes[i].mesh.GetBaseVertex(0);
            initialArgs[argsOffset + 4] = 0;

            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
            propBlock.SetBuffer(DataBufferID, _culledInstancesBuffer);
            propBlock.SetInt("_BaseInstanceOffset", i * _maxInstancesPerType);

            _renderParamsList[i] = new RenderParams(_scrapTypes[i].material)
            {
                worldBounds = bounds,
                matProps = propBlock
            };
        }
        _globalArgsBuffer.SetData(initialArgs);

        // DISPATCH SCATTER PASS
        _scatterComputeShader.SetTexture(_scatterKernel, "_HeightMap", _terrainGenerator.FinalHeightMapRT);
        _scatterComputeShader.SetBuffer(_scatterKernel, "_InstanceBuffer", _allInstancesBuffer);
        _scatterComputeShader.SetBuffer(_scatterKernel, "_CounterBuffer", _counterBuffer);
        _scatterComputeShader.SetInt("_ScrapDensityGrid", _scrapDensityGrid);
        _scatterComputeShader.SetFloat("_BasePoissonLambda", _basePoissonLambda);
        _scatterComputeShader.SetFloat("_PositionJitter", _positionJitter);
        _scatterComputeShader.SetVector("_ScaleRange", _scaleRange);
        _scatterComputeShader.SetFloat("_MeshSize", meshSize);
        _scatterComputeShader.SetFloat("_HeightScale", heightScale);
        _scatterComputeShader.SetMatrix("_LocalToWorldMatrix", transform.localToWorldMatrix);
        _scatterComputeShader.SetInt("_Seed", UnityEngine.Random.Range(1, 10000));
        _scatterComputeShader.SetInt("_ScrapTypeCount", _scrapTypes.Length);
        _scatterComputeShader.SetInt("_MaxInstanceCapacity", _maxInstanceCapacity);
        _scatterComputeShader.SetFloat("_HeightCutoff", _heightCutoff);
        _scatterComputeShader.SetFloat("_HeightExponent", _heightExponent);

        int scatterThreadGroups = Mathf.Max(1, Mathf.CeilToInt(_scrapDensityGrid / 8f));
        _scatterComputeShader.Dispatch(_scatterKernel, scatterThreadGroups, scatterThreadGroups, 1);

        _isInitialized = true;
        IsDataReady = false;

        AsyncGPUReadback.Request(_allInstancesBuffer, (request) =>
        {
            if (request.hasError || _allInstancesBuffer == null) return;
            var data = request.GetData<ScrapInstanceData>();
            CachedInstances = data.ToArray();
            IsDataReady = true;
        });
    }

    private void Update()
    {
        if (!_isInitialized || _cullingComputeShader == null) return;
        if (_targetCamera == null) _targetCamera = Camera.main;
        if (_targetCamera == null) return;

        if (_hiZGenerator == null && _targetCamera != null)
        {
            _hiZGenerator = _targetCamera.GetComponent<HiZGenerator>();
            if (_hiZGenerator == null) _hiZGenerator = FindAnyObjectByType<HiZGenerator>();
            if (_hiZGenerator == null) _hiZGenerator = _targetCamera.gameObject.AddComponent<HiZGenerator>();
        }

        // 1. Calculate Frustum Planes
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(_targetCamera);
        for (int i = 0; i < 6; i++)
        {
            _frustumPlanes[i] = new Vector4(planes[i].normal.x, planes[i].normal.y, planes[i].normal.z, planes[i].distance);
        }

        // 2. Correct VP Matrix calculation respecting graphics API platform flipping
        Matrix4x4 gpuProj = GL.GetGPUProjectionMatrix(_targetCamera.projectionMatrix, SystemInfo.graphicsUVStartsAtTop);
        Matrix4x4 vpMatrix = gpuProj * _targetCamera.worldToCameraMatrix;

        // 3. Reset Arguments
        _cullingComputeShader.SetBuffer(_resetKernel, "_GlobalArgsBuffer", _globalArgsBuffer);
        _cullingComputeShader.SetInt("_ScrapTypeCount", _scrapTypes.Length);
        _cullingComputeShader.Dispatch(_resetKernel, Mathf.Max(1, Mathf.CeilToInt(_scrapTypes.Length / 64.0f)), 1, 1);

        // 4. Culling Pass
        _cullingComputeShader.SetMatrix(VPMatrixID, vpMatrix);
        _cullingComputeShader.SetVectorArray(FrustumPlanesID, _frustumPlanes);
        _cullingComputeShader.SetVector(CameraPositionID, _targetCamera.transform.position);
        _cullingComputeShader.SetFloat("_MaxDrawDistance", _maxDrawDistance);
        _cullingComputeShader.SetFloat("_BoundingRadius", _boundingRadius);
        _cullingComputeShader.SetInt("_TotalInstances", _maxInstanceCapacity);
        _cullingComputeShader.SetInt("_MaxInstancesPerType", _maxInstancesPerType);
        _cullingComputeShader.SetInt("_ScrapTypeCount", _scrapTypes.Length);

        // Feed real screen dimensions to Compute Shader
        _cullingComputeShader.SetFloat(ScreenWidthID, (float)_targetCamera.pixelWidth);
        _cullingComputeShader.SetFloat(ScreenHeightID, (float)_targetCamera.pixelHeight);

        _cullingComputeShader.SetBuffer(_cullKernel, "_AllInstancesBuffer", _allInstancesBuffer);
        _cullingComputeShader.SetBuffer(_cullKernel, "_CulledInstancesBuffer", _culledInstancesBuffer);
        _cullingComputeShader.SetBuffer(_cullKernel, "_GlobalArgsBuffer", _globalArgsBuffer);
        _cullingComputeShader.SetBuffer(_cullKernel, "_CounterBuffer", _counterBuffer);

        bool hasHiZ = _hiZGenerator != null && _hiZGenerator.HiZTexture != null && _hiZGenerator.HiZTexture.IsCreated();
        Texture activeHiZ = hasHiZ ? (Texture)_hiZGenerator.HiZTexture : Texture2D.blackTexture;
        int maxMipLevel = hasHiZ ? Mathf.Max(0, _hiZGenerator.HiZTexture.mipmapCount - 1) : 0;

        _cullingComputeShader.SetTexture(_cullKernel, HiZBufferID, activeHiZ);
        _cullingComputeShader.SetInt("_MaxMipLevel", maxMipLevel);
        _cullingComputeShader.SetInt("_HiZEnabled", hasHiZ ? 1 : 0);

        int cullThreadGroups = Mathf.Max(1, Mathf.CeilToInt(_maxInstanceCapacity / 64.0f));
        _cullingComputeShader.Dispatch(_cullKernel, cullThreadGroups, 1, 1);

        // 5. Render Indirect (FIX: Command Buffer Offset aligned by 5 uints per scrap type)
        for (int i = 0; i < _scrapTypes.Length; i++)
        {
            Graphics.RenderMeshIndirect(_renderParamsList[i], _scrapTypes[i].mesh, _globalArgsBuffer, 1, i);
        }
    }

    private void ReleaseBuffers()
    {
        _allInstancesBuffer?.Release();
        _culledInstancesBuffer?.Release();
        _globalArgsBuffer?.Release();
        _counterBuffer?.Release();

        _allInstancesBuffer = null;
        _culledInstancesBuffer = null;
        _globalArgsBuffer = null;
        _counterBuffer = null;
        _isInitialized = false;
        IsDataReady = false;
        CachedInstances = null;
    }
}
