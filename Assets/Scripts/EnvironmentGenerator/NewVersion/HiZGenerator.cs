using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
public class HiZGenerator : MonoBehaviour
{
    [Header("Compute Shader")]
    [SerializeField] private ComputeShader hiZCullingShader;

    private RenderTexture hiZTexture;
    private int kernelMip0 = -1;
    private int kernelMips = -1;
    private Camera attachedCamera;

    public RenderTexture HiZTexture => hiZTexture;

    private static readonly int InputSourceID = Shader.PropertyToID("_InputSource");
    private static readonly int InputMipID = Shader.PropertyToID("_InputMip");
    private static readonly int OutputMipID = Shader.PropertyToID("_OutputMip");
    private static readonly int OutputSizeID = Shader.PropertyToID("_OutputSize");

    private void OnEnable()
    {
        attachedCamera = GetComponent<Camera>();
        if (attachedCamera != null)
        {
            // Force Unity / URP to generate depth texture pass
            attachedCamera.depthTextureMode |= DepthTextureMode.Depth;
        }

        // FIX: Switched from beginCameraRendering to endCameraRendering!
        // Depth texture is only populated AFTER opaque geometry/depth prepass executes!
        RenderPipelineManager.endCameraRendering += OnEndCamera;
    }

    private void OnDisable()
    {
        RenderPipelineManager.endCameraRendering -= OnEndCamera;
        ReleaseTexture();
    }

    private void Awake()
    {
        InitKernels();
    }

    private void InitKernels()
    {
        if (hiZCullingShader == null) return;
        kernelMip0 = hiZCullingShader.FindKernel("K_DownsampleMip0");
        kernelMips = hiZCullingShader.FindKernel("K_DownsampleMips");
    }

    private void OnEndCamera(ScriptableRenderContext context, Camera cam)
    {
        if (attachedCamera != null && cam != attachedCamera) return;
        if (hiZCullingShader == null) return;

        // Grab real Camera Depth Texture from Unity rendering pipeline
        Texture depthTex = Shader.GetGlobalTexture("_CameraDepthTexture");
        if (depthTex == null) depthTex = Texture2D.blackTexture;

        ExecuteDepthDownsample(cam.pixelWidth, cam.pixelHeight, depthTex);
    }

    public void ExecuteDepthDownsample(int width, int height, Texture sourceDepth)
    {
        if (width <= 0 || height <= 0 || hiZCullingShader == null) return;

        if (kernelMip0 < 0 || kernelMips < 0) InitKernels();

        EnsureTexture(ref hiZTexture, width, height);

        if (sourceDepth == null) sourceDepth = Texture2D.blackTexture;

        // 1. Mip 0 Generation
        int mipWidth = Mathf.Max(1, width / 2);
        int mipHeight = Mathf.Max(1, height / 2);

        hiZCullingShader.SetTexture(kernelMip0, InputSourceID, sourceDepth);
        hiZCullingShader.SetTexture(kernelMip0, OutputMipID, hiZTexture, 0);
        hiZCullingShader.SetInts(OutputSizeID, mipWidth, mipHeight);

        hiZCullingShader.Dispatch(kernelMip0, Mathf.CeilToInt(mipWidth / 8.0f), Mathf.CeilToInt(mipHeight / 8.0f), 1);

        // 2. Sub-Mips Generation
        int numMips = hiZTexture.mipmapCount;
        for (int i = 1; i < numMips; i++)
        {
            mipWidth = Mathf.Max(1, mipWidth / 2);
            mipHeight = Mathf.Max(1, mipHeight / 2);

            hiZCullingShader.SetTexture(kernelMips, InputMipID, hiZTexture, i - 1);
            hiZCullingShader.SetTexture(kernelMips, OutputMipID, hiZTexture, i);
            hiZCullingShader.SetInts(OutputSizeID, mipWidth, mipHeight);

            hiZCullingShader.Dispatch(kernelMips, Mathf.CeilToInt(mipWidth / 8.0f), Mathf.CeilToInt(mipHeight / 8.0f), 1);
        }
    }

    private void EnsureTexture(ref RenderTexture rt, int width, int height)
    {
        if (rt != null && rt.width == width && rt.height == height) return;

        ReleaseTexture();

        rt = new RenderTexture(width, height, 0, RenderTextureFormat.RFloat)
        {
            useMipMap = true,
            autoGenerateMips = false,
            enableRandomWrite = true,
            filterMode = FilterMode.Point
        };
        rt.Create();
    }

    private void ReleaseTexture()
    {
        if (hiZTexture != null)
        {
            hiZTexture.Release();
            hiZTexture = null;
        }
    }
}