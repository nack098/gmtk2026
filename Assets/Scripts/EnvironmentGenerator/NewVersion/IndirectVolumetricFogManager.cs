using UnityEngine;

[ExecuteAlways]
public class IndirectVolumetricFogManager : MonoBehaviour
{
    [Header("Mesh & Material Configuration")]
    public Mesh volumeProxyMesh;
    public Material fogMaterial;

    [Header("Volume Placement")]
    public Vector3 volumeCenter = new Vector3(0f, 10f, 0f);
    public Vector3 volumeSize = new Vector3(1500f, 300f, 1500f);

    private ComputeBuffer argsBuffer;
    private ComputeBuffer transformBuffer;
    private ComputeBuffer invTransformBuffer;

    private Bounds renderBounds;

    void OnEnable()
    {
        InitializeBuffers();
    }

    void OnDisable()
    {
        ReleaseBuffers();
    }

    private static Mesh defaultCubeMesh;

    void EnsureResources()
    {
        if (volumeProxyMesh == null)
        {
            if (defaultCubeMesh == null)
            {
                GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                defaultCubeMesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
                if (Application.isPlaying) Destroy(tempCube);
                else DestroyImmediate(tempCube);
            }
            volumeProxyMesh = defaultCubeMesh;
        }

        if (fogMaterial == null)
        {
            Shader shader = Shader.Find("Custom/URP_IndirectVolumetricFog_Smooth");
            if (shader != null)
            {
                fogMaterial = new Material(shader) { name = "IndirectVolumetricFog_Material_Runtime" };
            }
        }
    }

    void InitializeBuffers()
    {
        EnsureResources();
        if (volumeProxyMesh == null || fogMaterial == null) return;

        ReleaseBuffers();

        // 1. Indirect Draw Arguments Setup
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        args[0] = (uint)volumeProxyMesh.GetIndexCount(0);
        args[1] = 1;
        args[2] = (uint)volumeProxyMesh.GetIndexStart(0);
        args[3] = (uint)volumeProxyMesh.GetBaseVertex(0);

        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);

        // 2. Matrix Transforms Setup
        Matrix4x4[] transforms = new Matrix4x4[1];
        Matrix4x4[] invTransforms = new Matrix4x4[1];

        Matrix4x4 mat = Matrix4x4.TRS(volumeCenter, Quaternion.identity, volumeSize);
        transforms[0] = mat;
        invTransforms[0] = mat.inverse;

        transformBuffer = new ComputeBuffer(1, sizeof(float) * 16);
        invTransformBuffer = new ComputeBuffer(1, sizeof(float) * 16);

        transformBuffer.SetData(transforms);
        invTransformBuffer.SetData(invTransforms);

        fogMaterial.SetBuffer("_ObjectToWorldBuffer", transformBuffer);
        fogMaterial.SetBuffer("_WorldToObjectBuffer", invTransformBuffer);

        renderBounds = new Bounds(volumeCenter, volumeSize * 2.0f);
    }

    void Update()
    {
        EnsureResources();
        if (volumeProxyMesh == null || fogMaterial == null) return;

        if (argsBuffer == null || transformBuffer == null || invTransformBuffer == null)
        {
            InitializeBuffers();
        }

        if (argsBuffer == null || fogMaterial == null || volumeProxyMesh == null) return;

        // Draw via indirect instancing
        Graphics.DrawMeshInstancedIndirect(
            volumeProxyMesh,
            0,
            fogMaterial,
            renderBounds,
            argsBuffer
        );
    }

    void ReleaseBuffers()
    {
        if (argsBuffer != null) { argsBuffer.Release(); argsBuffer = null; }
        if (transformBuffer != null) { transformBuffer.Release(); transformBuffer = null; }
        if (invTransformBuffer != null) { invTransformBuffer.Release(); invTransformBuffer = null; }
    }
}
