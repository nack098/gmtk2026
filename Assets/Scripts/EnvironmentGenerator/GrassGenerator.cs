using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Takayama.Math;

namespace Takayama.Environment
{
    public class GrassGenerator : MonoBehaviour
    {
    #region Fields
    #region Unity Visible
    
        [SerializeField] private ComputeShader culling;
        [SerializeField] private Material grassMaterial;
        [SerializeField] private Camera camera;
        [SerializeField] private Mesh grassMesh;
        [SerializeField] private int grassCount = 10000;
        [SerializeField] private float grassValue = 0f;
        [SerializeField] private float boundingRadius = 1.0f;
        
    #endregion
    
        private GrassData[] grassData = null;
        private GraphicsBuffer allInstancesBuffer = null;
        private GraphicsBuffer culledInstancesBuffer = null;
        private GraphicsBuffer argumentsBuffer = null;
        private int resetKernel, cullingKernel;
        private Vector4[] frustumPlanes = new Vector4[6];
        private RenderParams renderParams;
        
    #endregion
    
    #region Methods
    #region Unity Life Cycle
    
        private void Awake()
        {
            if (!culling) throw new ArgumentNullException("Culling Compute Shader is null");
            if (!grassMaterial) throw new ArgumentNullException("Grass Material is null");
            if (!camera) throw new ArgumentNullException("Camera is null");
            if (!grassMesh) throw new ArgumentNullException("Grass Mesh is null");
            
            resetKernel = culling.FindKernel("ResetIndex");
            cullingKernel = culling.FindKernel("Culling");
            
            renderParams = new (grassMaterial);
            renderParams.worldBounds = new Bounds(Vector3.zero, Vector3.one * 5000f);
        }
        
        private void OnEnable()
        {
            var stride = sizeof(float) * 8;
            var args = new uint[] {
                grassMesh.GetIndexCount(0),
                0,
                grassMesh.GetIndexStart(0),
                grassMesh.GetBaseVertex(0),
                0
            };
            
            grassData = new GrassData[grassCount];
            allInstancesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, grassCount, stride);
            culledInstancesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, grassCount, stride);
            argumentsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, sizeof(uint) * 5);
            
            
            for (int i = 0; i < grassCount; ++i)
            {
                grassData[i].id = i;
                grassData[i].value = grassValue;
                grassData[i].position.x = (i % 100) * 2f;
                grassData[i].position.y = 0f;
                grassData[i].position.z = (i / 100) * 2f;
            }
            
            allInstancesBuffer.SetData(grassData);
            argumentsBuffer.SetData(args);
            
            grassMaterial.SetBuffer("_DataBuffer", culledInstancesBuffer);
            culling.SetBuffer(resetKernel,"_ArgumentsBuffer", argumentsBuffer);
            
            culling.SetBuffer(cullingKernel,"_ArgumentsBuffer", argumentsBuffer);
            culling.SetBuffer(cullingKernel, "_AllInstancesBuffer", allInstancesBuffer);
            culling.SetBuffer(cullingKernel, "_CulledInstancesBuffer", culledInstancesBuffer);
            culling.SetInt("_TotalInstances", grassCount);
            culling.SetFloat("_BoundingRadius", boundingRadius);
        }

        private void Update()
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
            int threadGroups = Mathf.CeilToInt(grassCount / 64.0f);
            
            for (int i = 0; i < 6; ++i)
                frustumPlanes[i] = new Vector4(planes[i].normal.x, planes[i].normal.y, planes[i].normal.z, planes[i].distance);
            culling.SetVectorArray("_FrustumPlanes", frustumPlanes);
            
            culling.Dispatch(resetKernel, 1, 1, 1);
            culling.Dispatch(cullingKernel, threadGroups, 1, 1);
            
            Graphics.RenderMeshIndirect(renderParams, grassMesh, argumentsBuffer);
        }
        
        private void OnDisable()
        {
            grassData = null;
            allInstancesBuffer?.Dispose();
            culledInstancesBuffer?.Dispose();
            argumentsBuffer?.Dispose();
        }
        
    #endregion
    
    
    
    #endregion
    
    #region Parameter Structure
    
        [StructLayout(LayoutKind.Sequential)]
        private struct GrassData 
        {
            internal float id;      
            internal float value;       
            internal Vector2 pad0;      
            internal Vector3 position;
            internal float pad1;        
        }
    
    #endregion
    }
}