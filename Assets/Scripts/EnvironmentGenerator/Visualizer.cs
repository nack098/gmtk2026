using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class HeightMapVisualizer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TerrainGenerator _generator;

    [Header("3D Mesh Settings")]
    [SerializeField] private float _heightScale = 15.0f;
    [SerializeField] private float _meshSize = 50.0f;

    [Header("Material & Texturing")]
    [SerializeField] private Material _terrainMaterial;
    [SerializeField] private float _uvTiling = 5.0f;

    [Header("Debug Display")]
    [SerializeField] private bool _showWireframe = true;
    [SerializeField] private Color _wireframeColor = new Color(0.2f, 0.8f, 1.0f, 0.3f);
    
    public float HeightScale => _heightScale;
    public float MeshSize => _meshSize;

    private Mesh _generatedMesh;

    private void Start()
    {
        if (_generator == null)
            _generator = GetComponent<TerrainGenerator>();

        Generate3DMesh();
    }

    [ContextMenu("Generate 3D Preview Mesh")]
    public void Generate3DMesh()
    {
        if (_generator == null || _generator.FinalHeightMap == null)
        {
            Debug.LogWarning("[HeightMapVisualizer] Generator or HeightMap is missing! Run the scene first.");
            return;
        }

        float[,] map = _generator.FinalHeightMap;
        int resX = map.GetLength(0);
        int resY = map.GetLength(1);

        MeshFilter mf = GetComponent<MeshFilter>();
        MeshRenderer mr = GetComponent<MeshRenderer>();

        if (_terrainMaterial != null)
        {
            mr.sharedMaterial = _terrainMaterial;
        }

        _generatedMesh = new Mesh();
        _generatedMesh.name = "TrashHeap_3D_Preview";

        Vector3[] vertices = new Vector3[resX * resY];
        Vector2[] uvs = new Vector2[resX * resY];
        int[] triangles = new int[(resX - 1) * (resY - 1) * 6];

        float stepX = _meshSize / (resX - 1);
        float stepY = _meshSize / (resY - 1);

        // 1. Build Vertices & Scaled UVs
        for (int y = 0; y < resY; ++y)
        {
            for (int x = 0; x < resX; ++x)
            {
                int index = y * resX + x;
                float height = map[x, y] * _heightScale;

                vertices[index] = new Vector3(x * stepX - (_meshSize * 0.5f), height, y * stepY - (_meshSize * 0.5f));
                
                // Scale UVs using tiling factor so textures don't look stretched out over the 50x50 grid
                uvs[index] = new Vector2((float)x / (resX - 1) * _uvTiling, (float)y / (resY - 1) * _uvTiling);
            }
        }

        // 2. Build Triangles
        int triIndex = 0;
        for (int y = 0; y < resY - 1; ++y)
        {
            for (int x = 0; x < resX - 1; ++x)
            {
                int current = y * resX + x;
                int next = current + 1;
                int above = (y + 1) * resX + x;
                int aboveNext = above + 1;

                // First Triangle
                triangles[triIndex++] = current;
                triangles[triIndex++] = above;
                triangles[triIndex++] = next;

                // Second Triangle
                triangles[triIndex++] = next;
                triangles[triIndex++] = above;
                triangles[triIndex++] = aboveNext;
            }
        }

        _generatedMesh.vertices = vertices;
        _generatedMesh.uv = uvs;
        _generatedMesh.triangles = triangles;

        // Recalculate lighting normals, bounds, and tangents for proper PBR shading/normal maps!
        _generatedMesh.RecalculateNormals();
        _generatedMesh.RecalculateTangents();
        _generatedMesh.RecalculateBounds();

        mf.sharedMesh = _generatedMesh;
    }

    private void OnDrawGizmosSelected()
    {
        if (!_showWireframe || _generatedMesh == null) return;

        Gizmos.color = _wireframeColor;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireMesh(_generatedMesh);
    }
}