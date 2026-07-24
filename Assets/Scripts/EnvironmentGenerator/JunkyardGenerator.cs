using UnityEngine;
using System.Collections.Generic;

using Random = Takayama.Math.Random;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class JunkyardTerrainSurfaceModifier : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TerrainGenerator _terrainGenerator;
    [SerializeField] private HeightMapVisualizer _visualizer;

    [Header("Scrap Visual Library (Unpickable Terrain Junk)")]
    [SerializeField] private GameObject[] _terrainScrapPrefabs;

    [Header("Poisson Clustering Settings")]
    [SerializeField] private int _scrapDensityGrid = 30;     
    [SerializeField] private float _basePoissonLambda = 3.0f; 
    [SerializeField] private float _positionJitter = 1.5f;
    [SerializeField] private Vector2 _scaleRange = new Vector2(0.8f, 2.0f);

    [Header("Container")]
    [SerializeField] private Transform _terrainScrapContainer;

    private void Start()
    {
        if (_terrainGenerator == null)
            _terrainGenerator = GetComponent<TerrainGenerator>();
        if (_visualizer == null)
            _visualizer = GetComponent<HeightMapVisualizer>();

        BuildJunkyardSurfaceMesh();
    }

    [ContextMenu("Build Junkyard Surface Mesh")]
    public void BuildJunkyardSurfaceMesh()
    {
        if (_terrainGenerator == null || _terrainGenerator.FinalHeightMap == null)
        {
            Debug.LogWarning("[JunkyardTerrainSurfaceModifier] TerrainGenerator heightmap is missing!");
            return;
        }

        if (_visualizer == null)
        {
            Debug.LogWarning("[JunkyardTerrainSurfaceModifier] HeightMapVisualizer reference is missing!");
            return;
        }

        if (_terrainScrapPrefabs == null || _terrainScrapPrefabs.Length == 0)
        {
            Debug.LogWarning("[JunkyardTerrainSurfaceModifier] Assign some unpickable terrain scrap prefabs!");
            return;
        }

        ClearTerrainScrap();

        GameObject containerObj = new GameObject("Terrain_Scrap_Visuals_Container");
        containerObj.transform.SetParent(transform);
        containerObj.transform.localPosition = Vector3.zero;
        containerObj.transform.localRotation = Quaternion.identity;
        _terrainScrapContainer = containerObj.transform;

        float[,] heightMap = _terrainGenerator.FinalHeightMap;
        int res = heightMap.GetLength(0);

        // Fetch exact dimensions from the Visualizer so we don't mismatch scales!
        float meshSize = _visualizer.MeshSize;
        float heightScale = _visualizer.HeightScale;
        float stepSize = meshSize / _scrapDensityGrid;

        int spawnedCount = 0;

        for (int y = 0; y < _scrapDensityGrid; ++y)
        {
            for (int x = 0; x < _scrapDensityGrid; ++x)
            {
                float worldX = (x * stepSize) - (meshSize * 0.5f);
                float worldZ = (y * stepSize) - (meshSize * 0.5f);

                float normX = Mathf.Clamp01((worldX + (meshSize * 0.5f)) / meshSize);
                float normY = Mathf.Clamp01((worldZ + (meshSize * 0.5f)) / meshSize);

                int mapX = Mathf.Clamp(Mathf.FloorToInt(normX * (res - 1)), 0, res - 1);
                int mapY = Mathf.Clamp(Mathf.FloorToInt(normY * (res - 1)), 0, res - 1);

                float heightVal = heightMap[mapX, mapY];
                if (heightVal < 0.01f) continue;

                float currentLambda = _basePoissonLambda * heightVal;
                int scrapClusterCount = Random.Poisson(currentLambda);

                for (int i = 0; i < scrapClusterCount; ++i)
                {
                    float jitterX = Random.Range(-_positionJitter, _positionJitter);
                    float jitterZ = Random.Range(-_positionJitter, _positionJitter);

                    float finalWorldX = worldX + jitterX;
                    float finalWorldZ = worldZ + jitterZ;

                    // FIXED: Match vertex generation logic exactly: local height uses visualizer scale, centered via TransformPoint
                    float localY = heightVal * heightScale;
                    Vector3 localSpawnPos = new Vector3(finalWorldX, localY, finalWorldZ);
                    Vector3 spawnPos = transform.TransformPoint(localSpawnPos);

                    GameObject prefab = _terrainScrapPrefabs[(int)Random.Range(0, _terrainScrapPrefabs.Length)];
                    GameObject scrapPiece = Instantiate(prefab, spawnPos, Quaternion.Euler(Random.Range(0, 360f), Random.Range(0, 360f), Random.Range(0, 360f)), _terrainScrapContainer);

                    Collider col = scrapPiece.GetComponent<Collider>();
                    if (col != null) Destroy(col);

                    float scale = Random.Range(_scaleRange.x, _scaleRange.y);
                    scrapPiece.transform.localScale = new Vector3(scale, scale * Random.Range(0.6f, 1.4f), scale);

                    ++spawnedCount;
                }
            }
        }

        Debug.Log($"[JunkyardTerrainSurfaceModifier] Successfully spawned {spawnedCount} clustered scrap meshes using Poisson distribution!");
    }

    [ContextMenu("Clear Terrain Scrap")]
    public void ClearTerrainScrap()
    {
        if (_terrainScrapContainer != null)
        {
            DestroyImmediate(_terrainScrapContainer.gameObject);
            _terrainScrapContainer = null;
        }
    }
}