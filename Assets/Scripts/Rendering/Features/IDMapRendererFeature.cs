using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class IDMapRendererFeature : ScriptableRendererFeature
{
    class PixelOutlinePass : ScriptableRenderPass
    {
        public Material idMaterial;
        public Material outlineMaterial;
        public LayerMask layerMask;
        
        private static readonly int IDMapID = Shader.PropertyToID("_IDMap");
        private static readonly int NormalMapID = Shader.PropertyToID("_CustomNormalMap");
        private List<ShaderTagId> shaderTagIds;

        public PixelOutlinePass(Material idMat, Material outMat, LayerMask mask)
        {
            this.idMaterial = idMat;
            this.outlineMaterial = outMat;
            this.layerMask = mask;
            this.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing; 
            
            shaderTagIds = new List<ShaderTagId> {
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("LightweightForward"),
                new ShaderTagId("SRPDefaultUnlit")
            };
        }

        private class RenderIDData
        {
            public RendererListHandle rendererList;
        }

        private class CopyData
        {
            public TextureHandle src;
        }

        private class BlitData
        {
            public Material material;
            public TextureHandle src;
            public TextureHandle idMap;
            public TextureHandle normalMap;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (idMaterial == null || outlineMaterial == null) return;

            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            TextureHandle cameraColor = resourceData.activeColorTexture;
            if (!cameraColor.IsValid()) return;

            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.msaaSamples = 1; // Force 1 for ID and Normal Maps
            desc.depthBufferBits = 0;
            desc.colorFormat = RenderTextureFormat.ARGB32;

            // 1. Create ID and Normal Map textures (Render Graph handles lifecycle automatically)
            TextureHandle idMapTex = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_IDMapRT", false, FilterMode.Point);
            TextureHandle normalMapTex = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_CustomNormalMapRT", false, FilterMode.Point);

            // 2. Render ID & Normals Pass
            DrawingSettings drawingSettings = new DrawingSettings(shaderTagIds[0], new SortingSettings(cameraData.camera) { criteria = cameraData.defaultOpaqueSortFlags });
            for (int i = 1; i < shaderTagIds.Count; i++)
                drawingSettings.SetShaderPassName(i, shaderTagIds[i]);

            drawingSettings.overrideMaterial = idMaterial;
            drawingSettings.overrideMaterialPassIndex = 0;
            
            FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.opaque, layerMask);
            RendererListParams listParams = new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings);
            RendererListHandle rendererList = renderGraph.CreateRendererList(listParams);

            using (var builder = renderGraph.AddRasterRenderPass<RenderIDData>("IDMap Render", out var passData))
            {
                passData.rendererList = rendererList;
                builder.SetRenderAttachment(idMapTex, 0, AccessFlags.Write);
                builder.SetRenderAttachment(normalMapTex, 1, AccessFlags.Write);
                
                TextureHandle depthTex = resourceData.activeDepthTexture;
                if (depthTex.IsValid())
                    builder.SetRenderAttachmentDepth(depthTex, AccessFlags.Read);

                builder.UseRendererList(rendererList);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((RenderIDData data, RasterGraphContext context) =>
                {
                    context.cmd.ClearRenderTarget(false, true, Color.clear);
                    context.cmd.DrawRendererList(data.rendererList);
                });
            }

            // 3. Copy Camera Color to Temp Texture
            RenderTextureDescriptor copyDesc = cameraData.cameraTargetDescriptor;
            copyDesc.depthBufferBits = 0;
            TextureHandle tempColorTex = UniversalRenderer.CreateRenderGraphTexture(renderGraph, copyDesc, "_TempCameraColor", false);

            using (var builder = renderGraph.AddRasterRenderPass<CopyData>("Copy Camera Color", out var passData))
            {
                passData.src = cameraColor;
                builder.UseTexture(cameraColor, AccessFlags.Read);
                builder.SetRenderAttachment(tempColorTex, 0, AccessFlags.Write);
                
                builder.SetRenderFunc((CopyData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), 0, false);
                });
            }

            // 4. Blit Outline Fullscreen Pass explicitly depending on the maps we generated
            using (var builder = renderGraph.AddRasterRenderPass<BlitData>("Pixel Outline Blit", out var passData))
            {
                passData.material = outlineMaterial;
                passData.idMap = idMapTex;
                passData.normalMap = normalMapTex;
                passData.src = tempColorTex;

                builder.UseTexture(tempColorTex, AccessFlags.Read);
                builder.UseTexture(idMapTex, AccessFlags.Read);
                builder.UseTexture(normalMapTex, AccessFlags.Read);
                
                // Write directly back to camera color
                builder.SetRenderAttachment(cameraColor, 0, AccessFlags.Write);

                builder.SetRenderFunc((BlitData data, RasterGraphContext context) =>
                {
                    data.material.SetTexture(IDMapID, data.idMap);
                    data.material.SetTexture(NormalMapID, data.normalMap);
                    // BlitTexture automatically binds data.src to "_BlitTexture", exactly what URP Sample Buffer expects!
                    Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }
        }
    }

    public Material idAndNormalMaterial;
    public Material outlineMaterial;
    public LayerMask layerMask;
    
    private PixelOutlinePass outlinePass;

    public override void Create()
    {
        outlinePass = new PixelOutlinePass(idAndNormalMaterial, outlineMaterial, layerMask);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (idAndNormalMaterial != null && outlineMaterial != null && 
           (renderingData.cameraData.cameraType == CameraType.Game || renderingData.cameraData.cameraType == CameraType.SceneView))
        {
            renderer.EnqueuePass(outlinePass);
        }
    }
}
