using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class VolumetricCloudRenderer : MonoBehaviour
{
    [Header("Compute Shader")]
    [SerializeField] private ComputeShader _cloudCS;

    [Header("Cloud Layer Box Bounds")]
    [SerializeField] private Vector3 _boxCenter = new Vector3(0, 250f, 0);
    [SerializeField] private Vector3 _boxSize = new Vector3(1000f, 80f, 1000f);

    [Header("Sun & Sky Settings")]
    [SerializeField] private Transform _sunLight;
    [SerializeField] private Color _sunColor = new Color(1f, 0.95f, 0.85f);
    [SerializeField] private Color _skyColor = new Color(0.4f, 0.65f, 0.95f);

    [Header("Cloud Tuning")]
    [Range(0.001f, 0.05f)][SerializeField] private float _cloudScale = 0.012f;
    [Range(0.0f, 0.8f)][SerializeField] private float _cloudThreshold = 0.42f;
    [Range(0.5f, 10f)][SerializeField] private float _cloudDensityMultiplier = 3.5f;
    [Range(16, 128)][SerializeField] private int _maxSteps = 64;
    [Range(2, 16)][SerializeField] private int _lightSteps = 6;

    private Camera _cam;
    private RenderTexture _cloudRT;
    private int _kernelHandle;

    private void OnEnable()
    {
        _cam = GetComponent<Camera>();
        if (_cloudCS != null)
        {
            _kernelHandle = _cloudCS.FindKernel("CSMain");
        }
    }

    private void OnDisable()
    {
        if (_cloudRT != null)
        {
            _cloudRT.Release();
            _cloudRT = null;
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (_cloudCS == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        EnsureRenderTexture(source.width, source.height);

        // Pass Camera matrices for world-space ray reconstruction
        Matrix4x4 invProj = _cam.projectionMatrix.inverse;
        Matrix4x4 invView = _cam.cameraToWorldMatrix;

        _cloudCS.SetMatrix("_InvProjectionMatrix", invProj);
        _cloudCS.SetMatrix("_InvViewMatrix", invView);
        _cloudCS.SetVector("_WorldSpaceCameraPos", _cam.transform.position);

        // Sun & Sky
        Vector3 sunDir = _sunLight != null ? -_sunLight.forward : new Vector3(0.5f, 1f, 0.3f).normalized;
        _cloudCS.SetVector("_SunDir", sunDir);
        _cloudCS.SetVector("_SunColor", new Vector4(_sunColor.r, _sunColor.g, _sunColor.b, 1f));
        _cloudCS.SetVector("_SkyColor", new Vector4(_skyColor.r, _skyColor.g, _skyColor.b, 1f));

        // Volume Box
        Vector3 boundsMin = _boxCenter - _boxSize * 0.5f;
        Vector3 boundsMax = _boxCenter + _boxSize * 0.5f;
        _cloudCS.SetVector("_BoundsMin", boundsMin);
        _cloudCS.SetVector("_BoundsMax", boundsMax);

        // Density & Quality Controls
        _cloudCS.SetFloat("_CloudScale", _cloudScale);
        _cloudCS.SetFloat("_CloudThreshold", _cloudThreshold);
        _cloudCS.SetFloat("_CloudDensityMultiplier", _cloudDensityMultiplier);
        _cloudCS.SetInt("_MaxSteps", _maxSteps);
        _cloudCS.SetInt("_LightSteps", _lightSteps);

        // Dispatch Compute Shader
        _cloudCS.SetTexture(_kernelHandle, "_Result", _cloudRT);
        int threadGroupsX = Mathf.CeilToInt(source.width / 8f);
        int threadGroupsY = Mathf.CeilToInt(source.height / 8f);
        _cloudCS.Dispatch(_kernelHandle, threadGroupsX, threadGroupsY, 1);

        // Blit final output to screen
        Graphics.Blit(_cloudRT, destination);
    }

    private void EnsureRenderTexture(int width, int height)
    {
        if (_cloudRT == null || _cloudRT.width != width || _cloudRT.height != height)
        {
            if (_cloudRT != null) _cloudRT.Release();

            _cloudRT = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf)
            {
                enableRandomWrite = true
            };
            _cloudRT.Create();
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Draw the cloud layer box in scene view
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.3f);
        Gizmos.DrawCube(_boxCenter, _boxSize);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(_boxCenter, _boxSize);
    }
}
