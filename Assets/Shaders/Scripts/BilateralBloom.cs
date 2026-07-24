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
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_mat == null) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            if (resourceData == null || cameraData == null) return;

            TextureHandle activeColor = resourceData.activeColorTexture;

            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            TextureHandle tempTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_StylizedPostProcessTemp", false);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Stylized Post Process Pass", out var passData))
            {
                passData.material = _mat;
                passData.src = activeColor;
                passData.dst = tempTexture;

                builder.UseTexture(passData.src, AccessFlags.Read);
                builder.SetRenderAttachment(passData.dst, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.src, new Vector4(1f, 1f, 0f, 0f), data.material, 0);
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
                    Blitter.BlitTexture(context.cmd, data.src, new Vector4(1f, 1f, 0f, 0f), 0, false);
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
        if (_pass != null && _settings.postProcessMaterial != null)
        {
            renderer.EnqueuePass(_pass);
        }
    }
}