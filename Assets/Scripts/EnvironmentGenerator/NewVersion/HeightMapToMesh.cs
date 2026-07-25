using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class HeightMapToMesh : MonoBehaviour
{
    public enum DisplacementMode
    {
        GPU_ShaderDisplacement, // Fast: Flat plane + Vertex Displacement in Custom/TerrainTriplanar
        CPU_AsyncReadback       // For Physics: Physical vertices displaced via GPU readback (MeshCollider support)
    }

    [Header("References")]
    [SerializeField] private TerrainGenerator _generator;

    [Header("3D Mesh Settings")]
    [SerializeField] private DisplacementMode _mode = DisplacementMode.GPU_ShaderDisplacement;
    [SerializeField] private float _heightScale = 15.0f;
    [SerializeField] private float _meshSize = 50.0f;
    [SerializeField] private int _meshResolution = 128; // Grid resolution (X & Y)

    [Header("Material & Texturing")]
    [SerializeField] private Material _terrainMaterial;
    [SerializeField] private float _uvTiling = 5.0f;

    [Header("Debug Display")]
    [SerializeField] private bool _showWireframe = true;
    [SerializeField] private Color _wireframeColor = new Color(0.2f, 0.8f, 1.0f, 0.3f);

    public float HeightScale => _heightScale;
    public float MeshSize => _meshSize;

    private Mesh _generatedMesh;

    private static readonly int HeightMapTexID = Shader.PropertyToID("_HeightMap");
    private static readonly int HeightScaleID = Shader.PropertyToID("_HeightScale");

    private void Awake()
    {
        if (_generator == null)
            _generator = GetComponent<TerrainGenerator>();
    }

    [ContextMenu("Generate Mesh From Heightmap")]
    public void GenerateMeshFromHeightMap()
    {
        if (_generator == null)
            _generator = GetComponent<TerrainGenerator>();

        // Ensure generator has run first if RT is missing
        if (_generator != null && _generator.FinalHeightMapRT == null)
        {
            _generator.GenerateTerrainGPU();
        }

        if (_mode == DisplacementMode.GPU_ShaderDisplacement)
        {
            GenerateGPUMesh();
            GeneratePhysicsColliderFromGPU();
        }
        else
        {
            GenerateCPUMeshFromGPU();
        }
    }

    /// <summary>
    /// Fast Path: Generates a flat quad grid and delegates vertex displacement & shading 
    /// completely to the GPU Shader via FinalHeightMapRT.
    /// </summary>
    [ContextMenu("Generate GPU Displaced Mesh")]
    public void GenerateGPUMesh()
    {
        if (_generator == null || _generator.FinalHeightMapRT == null)
        {
            Debug.LogWarning("[HeightMapToMesh] Generator or FinalHeightMapRT is missing! Run TerrainGenerator first.");
            return;
        }

        BuildFlatGridMesh(_meshResolution, _meshResolution);

        // Assign RenderTexture and scale to Material Shader
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (_terrainMaterial != null)
        {
            mr.sharedMaterial = _terrainMaterial;
            mr.sharedMaterial.SetTexture(HeightMapTexID, _generator.FinalHeightMapRT);
            mr.sharedMaterial.SetFloat(HeightScaleID, _heightScale);
        }
    }

    /// <summary>
    /// Async Readback Path: Reads GPU RenderTexture back to CPU to displace mesh vertices physically on C#.
    /// Useful if physics MeshColliders are required!
    /// </summary>
    [ContextMenu("Generate CPU Displaced Mesh (Async Readback)")]
    public void GenerateCPUMeshFromGPU()
    {
        if (_generator == null || _generator.FinalHeightMapRT == null)
        {
            Debug.LogWarning("[HeightMapToMesh] Generator or FinalHeightMapRT is missing!");
            return;
        }

        RenderTexture rt = _generator.FinalHeightMapRT;
        AsyncGPUReadback.Request(rt, 0, TextureFormat.RFloat, (AsyncGPUReadbackRequest request) =>
        {
            if (request.hasError)
            {
                Debug.LogError("[HeightMapToMesh] GPU Readback failed!");
                return;
            }

            var heightData = request.GetData<float>();
            BuildDisplacedCPUMesh(heightData, rt.width, rt.height);
        });
    }

    public void GeneratePhysicsColliderFromGPU()
    {
        if (_generator == null || _generator.FinalHeightMapRT == null) return;

        RenderTexture rt = _generator.FinalHeightMapRT;
        AsyncGPUReadback.Request(rt, 0, TextureFormat.RFloat, (AsyncGPUReadbackRequest request) =>
        {
            if (request.hasError)
            {
                Debug.LogError("[HeightMapToMesh] Physics Readback failed!");
                return;
            }

            var heightData = request.GetData<float>();
            Mesh collisionMesh = BuildDisplacedMeshOnly(heightData, rt.width, rt.height);

            if (!TryGetComponent(out MeshCollider collider))
            {
                collider = gameObject.AddComponent<MeshCollider>();
            }
            collider.sharedMesh = collisionMesh;

            // Ensure player is snapped onto terrain surface if stuck below terrain mesh
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                Ray ray = new Ray(player.transform.position + Vector3.up * 200f, Vector3.down);
                if (collider.Raycast(ray, out RaycastHit hit, 400f))
                {
                    if (player.transform.position.y < hit.point.y + 0.1f)
                    {
                        if (player.TryGetComponent(out CharacterController cc))
                        {
                            cc.enabled = false;
                            player.transform.position = hit.point + Vector3.up * 0.5f;
                            cc.enabled = true;
                        }
                        else
                        {
                            player.transform.position = hit.point + Vector3.up * 0.5f;
                        }
                    }
                }
            }
        });
    }

    private void BuildFlatGridMesh(int resX, int resY)
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        MeshRenderer mr = GetComponent<MeshRenderer>();

        if (_terrainMaterial != null) mr.sharedMaterial = _terrainMaterial;

        _generatedMesh = new Mesh { name = "TerrainGrid_GPU_Plane" };

        Vector3[] vertices = new Vector3[resX * resY];
        Vector2[] uvs = new Vector2[resX * resY];
        int[] triangles = new int[(resX - 1) * (resY - 1) * 6];

        float stepX = _meshSize / (resX - 1);
        float stepY = _meshSize / (resY - 1);

        for (int y = 0; y < resY; ++y)
        {
            for (int x = 0; x < resX; ++x)
            {
                int index = y * resX + x;
                vertices[index] = new Vector3(x * stepX - (_meshSize * 0.5f), 0f, y * stepY - (_meshSize * 0.5f));
                
                // Map UVs cleanly from 0..1 across the grid mesh for heightmap sampling
                uvs[index] = new Vector2((float)x / (resX - 1), (float)y / (resY - 1));
            }
        }

        int triIndex = 0;
        for (int y = 0; y < resY - 1; ++y)
        {
            for (int x = 0; x < resX - 1; ++x)
            {
                int current = y * resX + x;
                int next = current + 1;
                int above = (y + 1) * resX + x;
                int aboveNext = above + 1;

                triangles[triIndex++] = current;
                triangles[triIndex++] = above;
                triangles[triIndex++] = next;

                triangles[triIndex++] = next;
                triangles[triIndex++] = above;
                triangles[triIndex++] = aboveNext;
            }
        }

        _generatedMesh.vertices = vertices;
        _generatedMesh.uv = uvs;
        _generatedMesh.triangles = triangles;
        _generatedMesh.RecalculateNormals();

        // Expand bounds vertically so Unity doesn't frustum-cull the displaced mesh
        Bounds bounds = _generatedMesh.bounds;
        bounds.extents = new Vector3(bounds.extents.x, _heightScale, bounds.extents.z);
        _generatedMesh.bounds = bounds;

        mf.sharedMesh = _generatedMesh;
    }

    private Mesh BuildDisplacedMeshOnly(Unity.Collections.NativeArray<float> heightData, int resX, int resY)
    {
        Mesh mesh = new Mesh { name = "TerrainGrid_Displaced_Physics" };

        Vector3[] vertices = new Vector3[resX * resY];
        Vector2[] uvs = new Vector2[resX * resY];
        int[] triangles = new int[(resX - 1) * (resY - 1) * 6];

        float stepX = _meshSize / (resX - 1);
        float stepY = _meshSize / (resY - 1);

        for (int y = 0; y < resY; ++y)
        {
            for (int x = 0; x < resX; ++x)
            {
                int index = y * resX + x;
                float height = heightData[index] * _heightScale;

                vertices[index] = new Vector3(x * stepX - (_meshSize * 0.5f), height, y * stepY - (_meshSize * 0.5f));
                uvs[index] = new Vector2((float)x / (resX - 1) * _uvTiling, (float)y / (resY - 1) * _uvTiling);
            }
        }

        int triIndex = 0;
        for (int y = 0; y < resY - 1; ++y)
        {
            for (int x = 0; x < resX - 1; ++x)
            {
                int current = y * resX + x;
                int next = current + 1;
                int above = (y + 1) * resX + x;
                int aboveNext = above + 1;

                triangles[triIndex++] = current;
                triangles[triIndex++] = above;
                triangles[triIndex++] = next;

                triangles[triIndex++] = next;
                triangles[triIndex++] = above;
                triangles[triIndex++] = aboveNext;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        return mesh;
    }

    private void BuildDisplacedCPUMesh(Unity.Collections.NativeArray<float> heightData, int resX, int resY)
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        _generatedMesh = BuildDisplacedMeshOnly(heightData, resX, resY);
        mf.sharedMesh = _generatedMesh;

        if (!TryGetComponent(out MeshCollider collider))
        {
            collider = gameObject.AddComponent<MeshCollider>();
        }
        collider.sharedMesh = _generatedMesh;
    }

    private void OnDrawGizmosSelected()
    {
        if (!_showWireframe || _generatedMesh == null) return;

        Gizmos.color = _wireframeColor;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireMesh(_generatedMesh);
    }
}