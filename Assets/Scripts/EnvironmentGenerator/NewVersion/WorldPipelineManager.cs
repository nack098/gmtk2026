using System.Collections;
using UnityEngine;

public class WorldPipelineManager : MonoBehaviour
{
    [Header("Pipeline Stages")]
    [SerializeField] private TerrainGenerator _terrainGenerator;
    [SerializeField] private HeightMapToMesh _heightMapToMesh;
    [SerializeField] private GrassGeneratorBatched _grassGenerator; // Integrated here!
    [SerializeField] private GpuJunkyardPopulator _junkPopulator;
    [SerializeField] private GpuJunkyardColliderPool _junkColliderPool; // Physics Proximity Pool!
    [SerializeField] private GpuPickableScrapScatter _scrapPopulator; // Integrated here!

    private IEnumerator Start()
    {
        yield return StartCoroutine(RunInitializationPipeline());
    }

    [ContextMenu("Rebuild World Pipeline")]
    public void ExecutePipelineManual()
    {
        StartCoroutine(RunInitializationPipeline());
    }

    private IEnumerator RunInitializationPipeline()
    {
        Debug.Log("<color=yellow>[PipelineManager]</color> Starting World Generation Pipeline...");

        // Stage 1: Terrain GPU HeightMap Generation
        if (_terrainGenerator != null)
        {
            _terrainGenerator.GenerateTerrainGPU();
        }
        else
        {
            Debug.LogError("[PipelineManager] TerrainGenerator reference missing!");
            yield break;
        }

        // Wait 1 frame to ensure GPU textures and RenderTextures settle properly
        yield return null;

        // Stage 2: Mesh Visualization
        if (_heightMapToMesh != null)
        {
            _heightMapToMesh.GenerateMeshFromHeightMap();
        }
        else
        {
            Debug.LogError("[PipelineManager] HeightMapToMesh reference missing!");
            yield break;
        }

        // Stage 3: Batched Grass Scattering (Executed AFTER HeightMap is guaranteed valid!)
        if (_grassGenerator != null)
        {
            _grassGenerator.GenerateGrassBatchedGPU();
        }
        else
        {
            Debug.LogWarning("[PipelineManager] GrassGeneratorBatched reference missing! Skipping grass generation stage.");
        }

        // Stage 4: Junk Populator
        if (_junkPopulator != null)
        {
            _junkPopulator.GenerateJunkGPU();
        }
        else
        {
            Debug.LogError("[PipelineManager] JunkPopulator reference missing!");
            yield break;
        }

        if (_scrapPopulator != null)
        {
            _scrapPopulator.ScatterPropsGPU();
        }
        else
        {
            Debug.LogWarning("[PipelineManager] GpuPickableScrapScatter reference missing! Skipping scrap objective generation stage.");
        }

        Debug.Log("<color=green>[PipelineManager]</color> World Initialization Complete!");
    }
}
