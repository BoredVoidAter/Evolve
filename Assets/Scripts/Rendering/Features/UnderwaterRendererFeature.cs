using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class UnderwaterRendererFeature : ScriptableRendererFeature
{
    // Global static toggle easily accessed by our scripts
    public static bool IsActive = false; 
    
    public Material underwaterMaterial;

    class UnderwaterPass : ScriptableRenderPass
    {
        public Material material;

        private class PassData
        {
            public TextureHandle source;
            public Material material;
        }

        public UnderwaterPass(Material mat)
        {
            this.material = mat;
            // Draw AFTER other post processing to ensure it sits on top cleanly
            this.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing; 
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // Only execute if active
            if (material == null || !UnderwaterRendererFeature.IsActive) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            TextureHandle src = resourceData.activeColorTexture;
            if (!src.IsValid()) return;

            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0; // Color only

            // Create temporary texture
            TextureHandle dst = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_UnderwaterTemp", false);

            // Pass 1: Blit from Camera -> Temp (Applying our Shader)
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Underwater FX", out var passData))
            {
                passData.source = src;
                passData.material = material;

                builder.UseTexture(src, AccessFlags.Read);
                builder.SetRenderAttachment(dst, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            // Pass 2: Blit from Temp -> Camera (Returning the altered image)
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Underwater FX Copy Back", out var passData))
            {
                passData.source = dst;

                builder.UseTexture(dst, AccessFlags.Read);
                builder.SetRenderAttachment(src, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0, false);
                });
            }
        }
    }

    private UnderwaterPass _pass;

    public override void Create()
    {
        if (underwaterMaterial != null)
        {
            _pass = new UnderwaterPass(underwaterMaterial);
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // Only render underwater FX on the Game Camera (skip Scene view so editing isn't annoying)
        if (underwaterMaterial != null && renderingData.cameraData.cameraType == CameraType.Game)
        {
            renderer.EnqueuePass(_pass);
        }
    }
}
