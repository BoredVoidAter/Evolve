using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class IDMapRendererFeature : ScriptableRendererFeature
{
    class IDMapPass : ScriptableRenderPass
    {
        private Material overrideMaterial;
        private LayerMask layerMask;
        private ShaderTagId shaderTagId = new ShaderTagId("UniversalForward");

        public IDMapPass(Material material, LayerMask layerMask)
        {
            this.overrideMaterial = material;
            this.layerMask = layerMask;
            // Execute after opaque objects so we can read the depth buffer
            this.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        }

        // A struct to pass data into the Render Graph execution lambda
        private class PassData
        {
            public RendererListHandle rendererList;
            public TextureHandle idMapTexture;
        }

        // --- THE UNITY 6 RENDER GRAPH API ---
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (overrideMaterial == null) return;

            // Extract the data we need from the URP context
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            // 1. Create a descriptor for our ID Map texture
            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.colorFormat = RenderTextureFormat.ARGB32;
            desc.depthBufferBits = 0; // Don't allocate depth, we'll borrow the camera's!
            
            // 2. Allocate the texture in the Render Graph using URP's helper method
            TextureHandle idMapTex = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_IDMap", false);

            // 3. Set up how to draw the objects
            SortingCriteria sortingCriteria = cameraData.defaultOpaqueSortFlags;
            DrawingSettings drawingSettings = new DrawingSettings(shaderTagId, new SortingSettings(cameraData.camera) { criteria = sortingCriteria });
            drawingSettings.overrideMaterial = overrideMaterial;
            
            FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.opaque, layerMask);
            
            // Create the Renderer List
            RendererListParams listParams = new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings);
            RendererListHandle rendererList = renderGraph.CreateRendererList(listParams);

            // 4. Build the Raster Render Pass
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("IDMapRenderPass", out var passData))
            {
                passData.rendererList = rendererList;
                passData.idMapTexture = idMapTex;

                // Set our custom texture as the color target
                builder.SetRenderAttachment(idMapTex, 0, AccessFlags.Write);
                
                // Bind the main camera's depth buffer (Read-Only) for accurate occlusion
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
                
                builder.UseRendererList(rendererList);
                
                // Allow setting global textures for the Shader Graph
                builder.AllowGlobalStateModification(true);

                // Register the global texture safely AFTER the pass finishes writing to it
                builder.SetGlobalTextureAfterPass(idMapTex, Shader.PropertyToID("_IDMap"));

                // 5. Execute the drawing commands
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    // Clear background to black (ID 0)
                    context.cmd.ClearRenderTarget(false, true, Color.black);
                    
                    // Draw the creatures/objects
                    context.cmd.DrawRendererList(data.rendererList);
                });
            }
        }
    }

    public Material overrideMaterial;
    public LayerMask layerMask;
    private IDMapPass idMapPass;

    public override void Create()
    {
        idMapPass = new IDMapPass(overrideMaterial, layerMask);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // Add only for Game and Scene views
        if (overrideMaterial != null && (renderingData.cameraData.cameraType == CameraType.Game || renderingData.cameraData.cameraType == CameraType.SceneView))
        {
            renderer.EnqueuePass(idMapPass);
        }
    }
}
