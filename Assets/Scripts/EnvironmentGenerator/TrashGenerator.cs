using UnityEngine;
using Takayama.Math;
using System.Collections.Generic;

using Random = Takayama.Math.Random;

public class TrashScrapScatter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TerrainGenerator _generator;
    [SerializeField] private HeightMapVisualizer _visualizer;

    [Header("Scrap Prefab Library")]
    [SerializeField] private GameObject[] _trashPrefabs;

    [Header("Scatter Density & Distribution")]
    [SerializeField] private int _totalScrapCount = 150;
    [SerializeField] private float _minHeightThreshold = 0.08f;
    [SerializeField] private float _minPropDistance = 2.5f;

    [Header("Randomization Transforms")]
    [SerializeField] private Vector2 _scaleRange = new Vector2(0.8f, 1.5f);
    [SerializeField] private bool _alignToSurfaceNormal = true;
    [SerializeField] private float _maxNormalTiltAngle = 25.0f;

    [Header("Container")]
    [SerializeField] private Transform _scrapContainer;

    private void Start()
    {
        // Auto-hook references if missing
        if (_generator == null) _generator = GetComponent<TerrainGenerator>();
        if (_visualizer == null) _visualizer = GetComponent<HeightMapVisualizer>();

        ScatterProps();
    }

    [ContextMenu("Scatter Trash Props")]
    public void ScatterProps()
    {
        if (_generator == null || _generator.FinalHeightMap == null)
        {
            Debug.LogWarning("[TrashScrapScatter] Heightmap missing! Ensure TerrainGenerator runs first.");
            return;
        }

        if (_visualizer == null)
        {
            Debug.LogWarning("[TrashScrapScatter] HeightMapVisualizer reference missing!");
            return;
        }

        if (_trashPrefabs == null || _trashPrefabs.Length == 0)
        {
            Debug.LogWarning("[TrashScrapScatter] No trash prefabs assigned in the inspector!");
            return;
        }

        ClearScrap();

        if (_scrapContainer == null)
        {
            GameObject containerObj = new GameObject("Trash_Scrap_Container");
            containerObj.transform.SetParent(transform);
            _scrapContainer = containerObj.transform;
        }

        float[,] heightMap = _generator.FinalHeightMap;
        int resX = heightMap.GetLength(0);
        int resY = heightMap.GetLength(1);

        // Dynamically fetch exact mesh dimensions from the Visualizer component!
        float meshSize = _visualizer.MeshSize;
        float heightScale = _visualizer.HeightScale;

        List<Vector3> placedPositions = new List<Vector3>();
        int spawnAttempts = _totalScrapCount * 15;
        int spawnedCount = 0;

        while (spawnedCount < _totalScrapCount && spawnAttempts > 0)
        {
            --spawnAttempts;

            float normX = Random.Range(0.02f, 0.98f);
            float normY = Random.Range(0.02f, 0.98f);

            int mapX = Mathf.Clamp(Mathf.FloorToInt(normX * resX), 0, resX - 1);
            int mapY = Mathf.Clamp(Mathf.FloorToInt(normY * resY), 0, resY - 1);

            float heightVal = heightMap[mapX, mapY];

            if (heightVal < _minHeightThreshold) continue;

            float worldX = (normX * meshSize) - (meshSize * 0.5f);
            float worldZ = (normY * meshSize) - (meshSize * 0.5f);
            float worldY = heightVal * heightScale;

            Vector3 candidatePos = transform.TransformPoint(new Vector3(worldX, worldY, worldZ));

            bool tooClose = false;
            foreach (Vector3 pos in placedPositions)
            {
                if (Vector3.Distance(candidatePos, pos) < _minPropDistance)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            Vector3 surfaceNormal = ComputeHeightMapNormal(heightMap, normX, normY, resX, resY, meshSize, heightScale);

            GameObject selectedPrefab = _trashPrefabs[(int)Random.Range(0, _trashPrefabs.Length)];
            GameObject instantiatedScrap = Instantiate(selectedPrefab, candidatePos, Quaternion.identity, _scrapContainer);

            Quaternion randomYRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            if (_alignToSurfaceNormal)
            {
                Quaternion alignRot = Quaternion.FromToRotation(Vector3.up, surfaceNormal);
                if (Vector3.Angle(Vector3.up, surfaceNormal) > _maxNormalTiltAngle)
                {
                    alignRot = Quaternion.RotateTowards(Quaternion.identity, alignRot, _maxNormalTiltAngle);
                }
                instantiatedScrap.transform.rotation = alignRot * randomYRotation;
            }
            else
            {
                instantiatedScrap.transform.rotation = randomYRotation;
            }

            float randomScale = Random.Range(_scaleRange.x, _scaleRange.y);
            instantiatedScrap.transform.localScale = Vector3.one * randomScale;

            placedPositions.Add(candidatePos);
            ++spawnedCount;
        }

        Debug.Log($"[TrashScrapScatter] Scattered {spawnedCount} trash props across the {meshSize}x{meshSize} mesh!");
    }

    private Vector3 ComputeHeightMapNormal(float[,] map, float normX, float normY, int resX, int resY, float meshSize, float heightScale)
    {
        int x = Mathf.Clamp((int)(normX * resX), 1, resX - 2);
        int y = Mathf.Clamp((int)(normY * resY), 1, resY - 2);

        float stepX = meshSize / resX;
        float stepY = meshSize / resY;

        float hL = map[x - 1, y] * heightScale;
        float hR = map[x + 1, y] * heightScale;
        float hD = map[x, y - 1] * heightScale;
        float hU = map[x, y + 1] * heightScale;

        Vector3 normal = new Vector3(hL - hR, 2.0f * stepX, hD - hU).normalized;
        return transform.TransformDirection(normal);
    }

    [ContextMenu("Clear Scattered Scrap")]
    public void ClearScrap()
    {
        if (_scrapContainer != null)
        {
            DestroyImmediate(_scrapContainer.gameObject);
            _scrapContainer = null;
        }
    }
}