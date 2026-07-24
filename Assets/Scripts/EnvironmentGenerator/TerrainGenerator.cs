using UnityEngine;
using Takayama.Math;
using System.Collections.Generic;

using Random = Takayama.Math.Random;

public class TerrainGenerator : MonoBehaviour
{
    [Header("Dynamic Grid Scaling")]
    [SerializeField] private int _baseStartRes = 16;
    [SerializeField] private int _targetRes = 128;
    [SerializeField] private ushort _pileAmount = 7;

    [Header("1. Low-Res Seed & CA Settings")]
    [SerializeField] private float _minSeedDistance = 3.5f;
    [SerializeField] private int _caIterations = 2;

    [Header("2. Mountain Cone Settings")]
    [SerializeField] private float _maxInfluenceRadius = 24f; // Max reach of heap base
    [SerializeField] private float _pileSteepness = 1.8f;      // Power curve for sharp mountain apex point

    [Header("3. Perlin Domain Warping")]
    [SerializeField] private float _warpFrequency = 0.025f;
    [SerializeField] private float _warpAmplitude = 14.0f;

    [Header("4. High-Freq Debris Roughness")]
    [SerializeField] private float _debrisFrequency = 0.18f;
    [SerializeField] private float _debrisStrength = 0.18f;

    [SerializeField] private Texture2D _image;

    public float[,] FinalHeightMap { get; private set; }

    private void Awake()
    {
        _image = new Texture2D(_targetRes, _targetRes);

        int startRes = _baseStartRes;
        while ((startRes * startRes) / (float)_pileAmount < 18f && startRes < _targetRes)
        {
            startRes *= 2;
        }

        float scaledMinDist = (startRes / 16f) * _minSeedDistance;
        List<Vector2Int> lowResSeeds = GenerateSpacedSeeds(_pileAmount, startRes, scaledMinDist);

        bool[,] caFootprints = GenerateAndUpscaleCA(lowResSeeds, startRes, _targetRes, _caIterations);
        float[,] rawCones = ComputeIsolatedCADistanceCones(caFootprints, _targetRes, _maxInfluenceRadius, _pileSteepness);
        float[,] warpedCones = ApplyDomainWarping(rawCones, _targetRes, _warpFrequency, _warpAmplitude);
        
        FinalHeightMap = ApplyDebrisRoughness(warpedCones, _targetRes, _debrisFrequency, _debrisStrength);
        RenderHeightMapToTexture(FinalHeightMap, _targetRes);
    }

    private List<Vector2Int> GenerateSpacedSeeds(int count, int res, float minDist)
    {
        List<Vector2Int> seeds = new List<Vector2Int>();
        int attempts = 1000;

        while (seeds.Count < count && attempts > 0)
        {
            --attempts;
            Vector2Int candidate = new Vector2Int(
                (int)Random.Range(2, res - 2),
                (int)Random.Range(2, res - 2)
            );

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

    private bool[,] GenerateAndUpscaleCA(List<Vector2Int> seeds, int startRes, int targetRes, int caIters)
    {
        bool[,] grid = new bool[startRes, startRes];

        foreach (Vector2Int s in seeds)
        {
            grid[s.x, s.y] = true;
            if (s.x + 1 < startRes) grid[s.x + 1, s.y] = true;
            if (s.y + 1 < startRes) grid[s.x, s.y + 1] = true;
            if (s.x + 1 < startRes && s.y + 1 < startRes) grid[s.x + 1, s.y + 1] = true;
        }

        int currentRes = startRes;

        while (currentRes < targetRes)
        {
            for (int i = 0; i < caIters; ++i)
            {
                int threshold = (currentRes <= 32) ? 3 : 4;
                grid = StepCellularAutomata(grid, currentRes, threshold);
            }
            grid = UpscaleGridFast(grid, currentRes, 2);
            currentRes *= 2;
        }

        grid = StepCellularAutomata(grid, targetRes, 4);
        return grid;
    }

    private bool[,] StepCellularAutomata(bool[,] inputGrid, int res, int threshold)
    {
        bool[,] nextGrid = new bool[res, res];

        for (int y = 0; y < res; ++y)
        {
            for (int x = 0; x < res; ++x)
            {
                int neighbors = 0;
                for (int cy = -1; cy <= 1; ++cy)
                {
                    for (int cx = -1; cx <= 1; ++cx)
                    {
                        if (cx == 0 && cy == 0) continue;
                        int nx = x + cx;
                        int ny = y + cy;

                        if (nx >= 0 && nx < res && ny >= 0 && ny < res)
                            if (inputGrid[nx, ny]) ++neighbors;
                    }
                }

                if (inputGrid[x, y])
                    nextGrid[x, y] = neighbors >= (threshold - 1);
                else
                    nextGrid[x, y] = neighbors >= threshold;
            }
        }
        return nextGrid;
    }

    private bool[,] UpscaleGridFast(bool[,] inputGrid, int oldRes, int factor)
    {
        int newRes = oldRes * factor;
        bool[,] outputGrid = new bool[newRes, newRes];

        for (int y = 0; y < oldRes; ++y)
        {
            for (int x = 0; x < oldRes; ++x)
            {
                bool val = inputGrid[x, y];
                for (int my = 0; my < factor; ++my)
                {
                    for (int mx = 0; mx < factor; ++mx)
                    {
                        outputGrid[x * factor + mx, y * factor + my] = val;
                    }
                }
            }
        }
        return outputGrid;
    }

    private float[,] ComputeIsolatedCADistanceCones(bool[,] caMask, int res, float maxRadius, float steepness)
    {
        float[,] map = new float[res, res];
        List<Vector2> caActivePixels = new List<Vector2>();

        for (int y = 0; y < res; ++y)
            for (int x = 0; x < res; ++x)
                if (caMask[x, y])
                    caActivePixels.Add(new Vector2(x, y));

        if (caActivePixels.Count == 0) return map;

        float sqrMaxRadius = maxRadius * maxRadius;

        for (int y = 0; y < res; ++y)
        {
            for (int x = 0; x < res; ++x)
            {
                Vector2 pos = new Vector2(x, y);

                if (caMask[x, y])
                {
                    map[x, y] = 1.0f;
                }
                else
                {
                    float minSqDist = float.MaxValue;

                    foreach (Vector2 caPos in caActivePixels)
                    {
                        float sqDist = (pos - caPos).sqrMagnitude;
                        if (sqDist < minSqDist) minSqDist = sqDist;
                    }

                    if (minSqDist < sqrMaxRadius)
                    {
                        float dist = Mathf.Sqrt(minSqDist);
                        float normalizedDist = 1.0f - (dist / maxRadius);

                        map[x, y] = Mathf.Pow(normalizedDist, steepness);
                    }
                    else
                    {
                        map[x, y] = 0.0f;
                    }
                }
            }
        }
        return map;
    }

    private float[,] ApplyDomainWarping(float[,] input, int res, float freq, float amp)
    {
        float[,] output = new float[res, res];
        float offsetX = Random.Range(0f, 1000f);
        float offsetY = Random.Range(0f, 1000f);

        for (int y = 0; y < res; ++y)
        {
            for (int x = 0; x < res; ++x)
            {
                float noiseX = (Mathf.PerlinNoise((x + offsetX) * freq, (y + offsetY) * freq) - 0.5f) * 2f;
                float noiseY = (Mathf.PerlinNoise((x + offsetX + 500f) * freq, (y + offsetY + 500f) * freq) - 0.5f) * 2f;

                float warpedX = Mathf.Clamp(x + noiseX * amp, 0f, res - 1f);
                float warpedY = Mathf.Clamp(y + noiseY * amp, 0f, res - 1f);

                output[x, y] = SampleBilinear(input, res, warpedX, warpedY);
            }
        }
        return output;
    }

    private float[,] ApplyDebrisRoughness(float[,] input, int res, float freq, float strength)
    {
        float[,] output = new float[res, res];
        float seedX = Random.Range(0f, 1000f);
        float seedY = Random.Range(0f, 1000f);

        for (int y = 0; y < res; ++y)
        {
            for (int x = 0; x < res; ++x)
            {
                float baseHeight = input[x, y];

                if (baseHeight > 0.001f)
                {
                    float microNoise = (Mathf.PerlinNoise((x + seedX) * freq, (y + seedY) * freq) - 0.5f) * strength;
                    output[x, y] = Mathf.Clamp01(baseHeight + microNoise * baseHeight);
                }
                else
                {
                    output[x, y] = 0f;
                }
            }
        }
        return output;
    }

    private float SampleBilinear(float[,] map, int res, float x, float y)
    {
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        int x1 = Mathf.Min(x0 + 1, res - 1);
        int y1 = Mathf.Min(y0 + 1, res - 1);

        float tx = x - x0;
        float ty = y - y0;

        float top = Mathf.Lerp(map[x0, y1], map[x1, y1], tx);
        float bottom = Mathf.Lerp(map[x0, y0], map[x1, y0], tx);

        return Mathf.Lerp(bottom, top, ty);
    }

    private void RenderHeightMapToTexture(float[,] map, int res)
    {
        Color32[] pixels = new Color32[res * res];

        for (int y = 0; y < res; ++y)
        {
            for (int x = 0; x < res; ++x)
            {
                float heightVal = map[x, y];
                byte shade = (byte)(heightVal * 255f);
                pixels[y * res + x] = new Color32(shade, shade, shade, 255);
            }
        }

        _image.SetPixels32(pixels);
        _image.Apply();
        _image.filterMode = FilterMode.Point;
    }
}