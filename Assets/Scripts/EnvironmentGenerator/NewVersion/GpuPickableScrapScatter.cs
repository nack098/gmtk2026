using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

public class GpuPickableScrapScatter : MonoBehaviour
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct PickableData
    {
        public Vector4 position;    // 16 bytes
        public Vector4 normal;      // 16 bytes
        public float scale;         // 4 bytes
        public int prefabIndex;     // 4 bytes -> Total: 40 bytes
    }

    [Header("References")]
    [SerializeField] private TerrainGenerator _generator;
    [SerializeField] private HeightMapToMesh _visualizer;
    [SerializeField] private ComputeShader _pickableCS;

    [Header("Pickable Prefabs Library")]
    [SerializeField] private GameObject[] _trashPrefabs;

    [Header("Density & Distribution")]
    [SerializeField] private int _scrapDensityGrid = 41;
    [SerializeField] private float _minHeightThreshold = 1.08f;
    [SerializeField] private float _basePoissonLambda = 1.25f;
    [SerializeField] private float _positionJitter = 2.2f;

    [Header("Randomization Transforms")]
    [SerializeField] private Vector3 _scaleRange = new Vector2(0.8f, 1.5f);
    [SerializeField] private bool _alignToSurfaceNormal = true;
    [SerializeField] private float _maxNormalTiltAngle = 26.0f;

    [Header("Container")]
    [SerializeField] private Transform _scrapContainer;

    private void Start()
    {
        if (_generator == null) _generator = GetComponent<TerrainGenerator>();
        if (_visualizer == null) _visualizer = GetComponent<HeightMapToMesh>();
    }

    [ContextMenu("Scatter Pickables via GPU")]
    public void ScatterPropsGPU()
    {
        if (_generator == null || _generator.FinalHeightMapRT == null)
        {
            Debug.LogWarning("[GpuPickableScrapScatter] TerrainGenerator RenderTexture missing!");
            return;
        }

        if (_trashPrefabs == null || _trashPrefabs.Length == 0)
        {
            Debug.LogWarning("[GpuPickableScrapScatter] No trash prefabs assigned!");
            return;
        }

        ClearScrap();

        if (_scrapContainer == null)
        {
            GameObject containerObj = new GameObject("Pickable_Scrap_Container");
            containerObj.transform.SetParent(transform);
            _scrapContainer = containerObj.transform;
        }

        int maxPossible = _scrapDensityGrid * _scrapDensityGrid;

        GraphicsBuffer pickableBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.Structured,
            maxPossible,
            Marshal.SizeOf<PickableData>()
        );

        GraphicsBuffer counterBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.Structured,
            1,
            sizeof(uint)
        );

        // Reset atomic counter index 0 to start at 1 (reserving index 0 as null/empty check)
        counterBuffer.SetData(new uint[] { 1 });

        int kernel = _pickableCS.FindKernel("K_GeneratePickables");
        _pickableCS.SetTexture(kernel, "_HeightMap", _generator.FinalHeightMapRT);
        _pickableCS.SetBuffer(kernel, "_PickableBuffer", pickableBuffer);
        _pickableCS.SetBuffer(kernel, "_CounterBuffer", counterBuffer);
        _pickableCS.SetInt("_ScrapDensityGrid", _scrapDensityGrid);
        _pickableCS.SetFloat("_MinHeightThreshold", _minHeightThreshold);
        _pickableCS.SetFloat("_BasePoissonLambda", _basePoissonLambda);
        _pickableCS.SetFloat("_PositionJitter", _positionJitter);
        _pickableCS.SetVector("_ScaleRange", _scaleRange);
        _pickableCS.SetFloat("_MeshSize", _visualizer != null ? _visualizer.MeshSize : 51f);
        _pickableCS.SetFloat("_HeightScale", _visualizer != null ? _visualizer.HeightScale : 16f);
        _pickableCS.SetInt("_PrefabCount", _trashPrefabs.Length);
        _pickableCS.SetMatrix("_LocalToWorldMatrix", transform.localToWorldMatrix);
        _pickableCS.SetInt("_Seed", UnityEngine.Random.Range(2, 10000));

        int threadGroups = Mathf.CeilToInt(_scrapDensityGrid / 8f);
        _pickableCS.Dispatch(kernel, threadGroups, threadGroups, 1);

        // Non-blocking readback for Counter Buffer first
        AsyncGPUReadback.Request(counterBuffer, (counterRequest) =>
        {
            if (counterRequest.hasError)
            {
                Debug.LogError("[GpuPickableScrapScatter] GPU Counter Readback failed!");
                counterBuffer.Release();
                pickableBuffer.Release();
                return;
            }

            uint actualCount = counterRequest.GetData<uint>()[0];
            counterBuffer.Release();

            if (actualCount <= 1)
            {
                pickableBuffer.Release();
                Debug.LogWarning("[GpuPickableScrapScatter] GPU generated no valid pickable candidates.");
                return;
            }

            // Readback exact spawned instances without blocking frame loop
            AsyncGPUReadback.Request(pickableBuffer, (int)(actualCount * Marshal.SizeOf<PickableData>()), 0, (dataRequest) =>
            {
                if (dataRequest.hasError)
                {
                    Debug.LogError("[GpuPickableScrapScatter] GPU Data Readback failed!");
                    pickableBuffer.Release();
                    return;
                }

                var nativeData = dataRequest.GetData<PickableData>();
                InstantiateScrapFromGPU(nativeData, actualCount);
                pickableBuffer.Release();
            });
        });
    }

    private void InstantiateScrapFromGPU(Unity.Collections.NativeArray<PickableData> spawnedData, uint count)
    {
        uint spawnLimit = Math.Min(count, (uint)spawnedData.Length);
        for (int i = 1; i < spawnLimit; i++)
        {
            PickableData data = spawnedData[i];
            if (data.prefabIndex < 0 || data.prefabIndex >= _trashPrefabs.Length) continue;

            GameObject prefab = _trashPrefabs[data.prefabIndex];
            if (prefab == null) continue;

            GameObject instance = Instantiate(prefab, data.position, Quaternion.identity, _scrapContainer);
            Quaternion randomYRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);

            if (_alignToSurfaceNormal && data.normal != Vector4.zero)
            {
                Quaternion alignRot = Quaternion.FromToRotation(Vector3.up, data.normal);
                if (Vector3.Angle(Vector3.up, data.normal) > _maxNormalTiltAngle)
                {
                    alignRot = Quaternion.RotateTowards(Quaternion.identity, alignRot, _maxNormalTiltAngle);
                }
                instance.transform.rotation = alignRot * randomYRotation;
            }
            else
            {
                instance.transform.rotation = randomYRotation;
            }

            instance.transform.localScale = Vector3.one * data.scale;
        }

        Debug.Log($"<color=lime>[GpuPickableScrapScatter]</color> Async WebGPU execution success! Spawned {spawnLimit - 1} pickable GameObjects without main-thread hitching!");
    }

    [ContextMenu("Clear Scattered Scrap")]
    public void ClearScrap()
    {
        if (_scrapContainer != null)
        {
            if (Application.isPlaying)
                Destroy(_scrapContainer.gameObject);
            else
                DestroyImmediate(_scrapContainer.gameObject);

            _scrapContainer = null;
        }
    }
}
