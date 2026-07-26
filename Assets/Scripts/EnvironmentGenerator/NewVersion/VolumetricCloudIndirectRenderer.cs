using UnityEngine;

[ExecuteAlways]
public class VolumetricCloudIndirectRenderer : MonoBehaviour
{
    [SerializeField] private Mesh _cubeMesh;
    [SerializeField] private Material _cloudMaterial;
    [SerializeField] private Vector3 _cloudVolumeSize = new Vector3(1500f, 200f, 1500f);
    [SerializeField] private float _heightOffsetFromCamera = 60f;
    [SerializeField] private Transform _targetCamera;
    [SerializeField] private bool _lockToCameraXZ = true;

    private GraphicsBuffer _argsBuffer;
    private ComputeBuffer _objectToWorldBuffer;
    private ComputeBuffer _worldToObjectBuffer;

    private readonly Matrix4x4[] _matrices = new Matrix4x4[1];
    private readonly Matrix4x4[] _inverseMatrices = new Matrix4x4[1];

    private void OnEnable()
    {
        InitializeBuffers();
    }

    private void OnDisable()
    {
        ReleaseBuffers();
    }

    private static Mesh _defaultCubeMesh;

    private void EnsureResources()
    {
        if (_cubeMesh == null)
        {
            if (_defaultCubeMesh == null)
            {
                GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _defaultCubeMesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
                if (Application.isPlaying) Destroy(tempCube);
                else DestroyImmediate(tempCube);
            }
            _cubeMesh = _defaultCubeMesh;
        }

        if (_cloudMaterial == null)
        {
            Shader shader = Shader.Find("Custom/URP_AdvancedVolumetricCloudsWebGPU");
            if (shader != null)
            {
                _cloudMaterial = new Material(shader) { name = "VolumetricClouds_Material_Runtime" };
            }
        }
    }

    private void InitializeBuffers()
    {
        EnsureResources();
        if (_cubeMesh == null || _cloudMaterial == null) return;

        _argsBuffer?.Release();
        _argsBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.IndirectArguments, 
            1, 
            GraphicsBuffer.IndirectDrawIndexedArgs.size
        );

        var args = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        args[0].indexCountPerInstance = _cubeMesh.GetIndexCount(0);
        args[0].instanceCount = 1;
        args[0].startIndex = _cubeMesh.GetIndexStart(0);
        args[0].baseVertexIndex = _cubeMesh.GetBaseVertex(0);
        args[0].startInstance = 0;
        
        _argsBuffer.SetData(args);

        _objectToWorldBuffer?.Release();
        _worldToObjectBuffer?.Release();
        _objectToWorldBuffer = new ComputeBuffer(1, sizeof(float) * 16);
        _worldToObjectBuffer = new ComputeBuffer(1, sizeof(float) * 16);
    }

    private void Update()
    {
        EnsureResources();
        if (_cubeMesh == null || _cloudMaterial == null) return;

        if (_targetCamera == null)
        {
            if (Camera.main != null) _targetCamera = Camera.main.transform;
            else return;
        }

        if (_argsBuffer == null || !_argsBuffer.IsValid() || _objectToWorldBuffer == null || _worldToObjectBuffer == null)
        {
            InitializeBuffers();
        }

        if (_argsBuffer == null || !_argsBuffer.IsValid() || _objectToWorldBuffer == null || _worldToObjectBuffer == null) return;

        Vector3 targetPos = transform.position;
        if (_lockToCameraXZ)
        {
            targetPos.x = _targetCamera.position.x;
            targetPos.z = _targetCamera.position.z;
        }
        targetPos.y = _targetCamera.position.y + _heightOffsetFromCamera + (_cloudVolumeSize.y * 0.5f);

        Matrix4x4 matrix = Matrix4x4.TRS(targetPos, Quaternion.identity, _cloudVolumeSize);
        _matrices[0] = matrix;
        _inverseMatrices[0] = matrix.inverse;

        _objectToWorldBuffer.SetData(_matrices);
        _worldToObjectBuffer.SetData(_inverseMatrices);

        _cloudMaterial.SetBuffer("_ObjectToWorldBuffer", _objectToWorldBuffer);
        _cloudMaterial.SetBuffer("_WorldToObjectBuffer", _worldToObjectBuffer);

        Graphics.DrawMeshInstancedIndirect(
            _cubeMesh,
            0,
            _cloudMaterial,
            new Bounds(targetPos, _cloudVolumeSize * 2.0f),
            _argsBuffer
        );
    }

    private void ReleaseBuffers()
    {
        _argsBuffer?.Release();
        _argsBuffer = null;
        _objectToWorldBuffer?.Release();
        _objectToWorldBuffer = null;
        _worldToObjectBuffer?.Release();
        _worldToObjectBuffer = null;
    }
}