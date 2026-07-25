using System.Collections.Generic;
using UnityEngine;
using Takayama.Math;
using Random = Takayama.Math.Random;

public class TerrainGenerator : MonoBehaviour
{
    [Header("GPU Compute Reference")]
    [SerializeField] private ComputeShader _terrainCS;

    [Header("Dynamic Grid Scaling")]
    [SerializeField] private int _baseStartRes = 16;
    [SerializeField] private int _targetRes = 128;
    [SerializeField] private ushort _pileAmount = 7;

    [Header("1. Low-Res Seed & CA Settings")]
    [SerializeField] private float _minSeedDistance = 3.5f;
    [SerializeField] private int _caIterations = 2;

    [Header("2. Mountain Cone Settings")]
    [SerializeField] private float _maxInfluenceRadius = 24f;
    [SerializeField] private float _pileSteepness = 1.8f;

    [Header("3. Perlin Domain Warping")]
    [SerializeField] private float _warpFrequency = 0.025f;
    [SerializeField] private float _warpAmplitude = 14.0f;

    [Header("4. High-Freq Debris Roughness")]
    [SerializeField] private float _debrisFrequency = 0.18f;
    [SerializeField] private float _debrisStrength = 0.18f;

    public RenderTexture FinalHeightMapRT { get; private set; }

    private int _kStepCA, _kUpscaleCA, _kSeedJFA, _kStepJFA, _kFinalizeDistanceCones, _kApplyWarpAndRoughness;

    private void Awake()
    {
        CacheKernels();
    }

    private void CacheKernels()
    {
        _kStepCA = _terrainCS.FindKernel("K_StepCA");
        _kUpscaleCA = _terrainCS.FindKernel("K_UpscaleCA");
        _kSeedJFA = _terrainCS.FindKernel("K_SeedJFA");
        _kStepJFA = _terrainCS.FindKernel("K_StepJFA");
        _kFinalizeDistanceCones = _terrainCS.FindKernel("K_FinalizeDistanceCones");
        _kApplyWarpAndRoughness = _terrainCS.FindKernel("K_ApplyWarpAndRoughness");
    }

    [ContextMenu("Generate Terrain GPU")]
    public void GenerateTerrainGPU()
    {
        int startRes = _baseStartRes;
        while ((startRes * startRes) / (float)_pileAmount < 18f && startRes < _targetRes)
        {
            startRes *= 2;
        }

        // 1. CPU-side Poisson/Spaced Seed Placement
        float scaledMinDist = (startRes / 16f) * _minSeedDistance;
        List<Vector2Int> lowResSeeds = GenerateSpacedSeeds(_pileAmount, startRes, scaledMinDist);

        // 2. Prepare RenderTextures
        RenderTexture gridA = CreateRT(startRes, RenderTextureFormat.RFloat);
        RenderTexture gridB = CreateRT(startRes, RenderTextureFormat.RFloat);

        // Upload initial seed pixels to GridA
        Texture2D initialSeedTex = new Texture2D(startRes, startRes, TextureFormat.RFloat, false);
        Color[] seedColors = new Color[startRes * startRes];
        foreach (var s in lowResSeeds)
        {
            seedColors[s.y * startRes + s.x] = Color.white;
            if (s.x + 1 < startRes) seedColors[s.y * startRes + (s.x + 1)] = Color.white;
            if (s.y + 1 < startRes) seedColors[(s.y + 1) * startRes + s.x] = Color.white;
            if (s.x + 1 < startRes && s.y + 1 < startRes) seedColors[(s.y + 1) * startRes + (s.x + 1)] = Color.white;
        }
        initialSeedTex.SetPixels(seedColors);
        initialSeedTex.Apply();

        // FIX: Copy directly on GPU without touching RenderTexture.active or running a Blit pass
        Graphics.CopyTexture(initialSeedTex, gridA);
        DestroyImmediate(initialSeedTex);

        // 3. CA Loop with Dynamic Upscaling on GPU
        int currentRes = startRes;

        while (currentRes <= _targetRes)
        {
            int caIters = (currentRes == _targetRes) ? 1 : _caIterations;
            int threshold = (currentRes <= 32) ? 3 : 4;

            for (int i = 0; i < caIters; ++i)
            {
                DispatchPass(_kStepCA, gridA, gridB, currentRes, threshold);
                Swap(ref gridA, ref gridB);
            }

            if (currentRes < _targetRes)
            {
                int nextRes = currentRes * 2;
                RenderTexture nextGridA = CreateRT(nextRes, RenderTextureFormat.RFloat);
                RenderTexture nextGridB = CreateRT(nextRes, RenderTextureFormat.RFloat);

                _terrainCS.SetTexture(_kUpscaleCA, "_GridA", gridA);
                _terrainCS.SetTexture(_kUpscaleCA, "_GridB", nextGridA);
                _terrainCS.SetInt("_Res", nextRes);
                _terrainCS.Dispatch(_kUpscaleCA, Mathf.CeilToInt(nextRes / 8f), Mathf.CeilToInt(nextRes / 8f), 1);

                gridA.Release();
                gridB.Release();
                gridA = nextGridA;
                gridB = nextGridB;

                currentRes = nextRes;
            }
            else
            {
                break;
            }
        }

        // 4. Jump Flooding Algorithm (JFA)
        RenderTexture jfaA = CreateRT(_targetRes, RenderTextureFormat.ARGBFloat);
        RenderTexture jfaB = CreateRT(_targetRes, RenderTextureFormat.ARGBFloat);

        _terrainCS.SetTexture(_kSeedJFA, "_GridA", gridA);
        _terrainCS.SetTexture(_kSeedJFA, "_JFA_A", jfaA);
        _terrainCS.SetInt("_Res", _targetRes);
        _terrainCS.Dispatch(_kSeedJFA, Mathf.CeilToInt(_targetRes / 8f), Mathf.CeilToInt(_targetRes / 8f), 1);

        int step = _targetRes / 2;
        while (step >= 1)
        {
            _terrainCS.SetTexture(_kStepJFA, "_JFA_A", jfaA);
            _terrainCS.SetTexture(_kStepJFA, "_JFA_B", jfaB);
            _terrainCS.SetInt("_Res", _targetRes);
            _terrainCS.SetInt("_StepDistance", step);
            _terrainCS.Dispatch(_kStepJFA, Mathf.CeilToInt(_targetRes / 8f), Mathf.CeilToInt(_targetRes / 8f), 1);

            Swap(ref jfaA, ref jfaB);
            step /= 2;
        }

        // 5. Build Distance Cones Pass
        _terrainCS.SetTexture(_kFinalizeDistanceCones, "_GridA", gridA);
        _terrainCS.SetTexture(_kFinalizeDistanceCones, "_JFA_A", jfaA);
        _terrainCS.SetTexture(_kFinalizeDistanceCones, "_GridB", gridB);
        _terrainCS.SetInt("_Res", _targetRes);
        _terrainCS.SetFloat("_MaxInfluenceRadius", _maxInfluenceRadius);
        _terrainCS.SetFloat("_PileSteepness", _pileSteepness);
        _terrainCS.Dispatch(_kFinalizeDistanceCones, Mathf.CeilToInt(_targetRes / 8f), Mathf.CeilToInt(_targetRes / 8f), 1);

        Swap(ref gridA, ref gridB);

        // 6. Domain Warping & Debris Roughness Pass
        if (FinalHeightMapRT != null) FinalHeightMapRT.Release();
        FinalHeightMapRT = CreateRT(_targetRes, RenderTextureFormat.RFloat, FilterMode.Bilinear);

        _terrainCS.SetTexture(_kApplyWarpAndRoughness, "_GridA", gridA);
        _terrainCS.SetTexture(_kApplyWarpAndRoughness, "_FinalHeightmap", FinalHeightMapRT);
        _terrainCS.SetInt("_Res", _targetRes);
        _terrainCS.SetFloat("_WarpFrequency", _warpFrequency);
        _terrainCS.SetFloat("_WarpAmplitude", _warpAmplitude);
        _terrainCS.SetFloat("_DebrisFrequency", _debrisFrequency);
        _terrainCS.SetFloat("_DebrisStrength", _debrisStrength);
        _terrainCS.SetVector("_NoiseOffset", new Vector2(Random.Range(0f, 1000f), Random.Range(0f, 1000f)));

        _terrainCS.Dispatch(_kApplyWarpAndRoughness, Mathf.CeilToInt(_targetRes / 8f), Mathf.CeilToInt(_targetRes / 8f), 1);

        // FIX: Ensure no active RT remains set before destroying intermediate resources
        RenderTexture.active = null;

        // Cleanup intermediate buffers
        gridA?.Release();
        gridB?.Release();
        jfaA?.Release();
        jfaB?.Release();

        Debug.Log("<color=cyan>[TerrainGenerator]</color> Fast GPU Heightmap Generated Successfully!");
    }

    private void DispatchPass(int kernel, RenderTexture src, RenderTexture dst, int res, int threshold)
    {
        _terrainCS.SetTexture(kernel, "_GridA", src);
        _terrainCS.SetTexture(kernel, "_GridB", dst);
        _terrainCS.SetInt("_Res", res);
        _terrainCS.SetInt("_Threshold", threshold);
        _terrainCS.Dispatch(kernel, Mathf.CeilToInt(res / 8f), Mathf.CeilToInt(res / 8f), 1);
    }

    private RenderTexture CreateRT(int res, RenderTextureFormat format, FilterMode filterMode = FilterMode.Point)
    {
        RenderTexture rt = new RenderTexture(res, res, 0, format, RenderTextureReadWrite.Linear)
        {
            enableRandomWrite = true,
            filterMode = filterMode,
            wrapMode = TextureWrapMode.Clamp
        };
        rt.Create();
        return rt;
    }

    private void Swap(ref RenderTexture a, ref RenderTexture b)
    {
        RenderTexture temp = a;
        a = b;
        b = temp;
    }

    private List<Vector2Int> GenerateSpacedSeeds(int count, int res, float minDist)
    {
        List<Vector2Int> seeds = new List<Vector2Int>();
        int attempts = 1000;

        while (seeds.Count < count && attempts > 0)
        {
            --attempts;
            Vector2Int candidate = new Vector2Int((int)Random.Range(2, res - 2), (int)Random.Range(2, res - 2));

            bool valid = true;
            foreach (Vector2Int s in seeds)
            {
                if (Vector2Int.Distance(candidate, s) < minDist)
                {
                    valid = false;
                    break;
                }
            }

            if (valid) seeds.Add(candidate);
        }
        return seeds;
    }
}