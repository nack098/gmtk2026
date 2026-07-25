using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class StylizedPostProcessFeature : ScriptableRendererFeature
{
    private class StylizedPostProcessPass : ScriptableRenderPass
    {
        private Material _mat;

        private class PassData
        {
            public Material material;
            public TextureHandle src;
            public TextureHandle dst;
        }

        public StylizedPostProcessPass(Material mat)
        {
            _mat = mat;
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            
            // Require depth texture so URP binds _CameraDepthTexture for bilateral sampling
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_mat == null) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            if (resourceData == null || cameraData == null) return;

            // GUARD 1: Ignore Preview (Material Inspector / Asset Thumbnails) and Reflection cameras!
            if (cameraData.cameraType == CameraType.Preview || cameraData.cameraType == CameraType.Reflection)
            {
                return;
            }

            TextureHandle activeColor = resourceData.activeColorTexture;

            // GUARD 2: Ensure active color handle is valid before building render passes
            if (!activeColor.IsValid()) return;

            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            TextureHandle tempTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_StylizedPostProcessTemp", false);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Stylized Post Process Pass", out var passData))
            {
                passData.material = _mat;
                passData.src = activeColor;
                passData.dst = tempTexture;

                builder.UseTexture(passData.src, AccessFlags.Read);
                
                // Read camera depth texture for bilateral depth calculations
                if (resourceData.cameraDepthTexture.IsValid())
                {
                    builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                }

                builder.SetRenderAttachment(passData.dst, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    if (data.src.IsValid() && data.material != null)
                    {
                        Blitter.BlitTexture(context.cmd, data.src, new Vector4(1f, 1f, 0f, 0f), data.material, 0);
                    }
                });
            }

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Stylized Post Process Copy Back", out var passData))
            {
                passData.src = tempTexture;
                passData.dst = activeColor;

                builder.UseTexture(passData.src, AccessFlags.Read);
                builder.SetRenderAttachment(passData.dst, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    if (data.src.IsValid())
                    {
                        Blitter.BlitTexture(context.cmd, data.src, new Vector4(1f, 1f, 0f, 0f), 0, false);
                    }
                });
            }
        }
    }

    [System.Serializable]
    public class Settings
    {
        public Material postProcessMaterial;
    }

    [SerializeField] private Settings _settings = new Settings();
    private StylizedPostProcessPass _pass;

    public override void Create()
    {
        if (_settings.postProcessMaterial != null)
        {
            _pass = new StylizedPostProcessPass(_settings.postProcessMaterial);
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // GUARD 3: Don't enqueue pass during inspector camera rendering
        if (renderingData.cameraData.cameraType == CameraType.Preview || renderingData.cameraData.cameraType == CameraType.Reflection)
        {
            return;
        }

        if (_pass != null && _settings.postProcessMaterial != null)
        {
            renderer.EnqueuePass(_pass);
        }
    }
}