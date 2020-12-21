using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class EasyARRendererFeature : ScriptableRendererFeature
{
    class EasyARRenderPass : ScriptableRenderPass
    {
        const string RenderPassTag = "EasyAR Camera Blit";

        static readonly ProfilingSampler sampler = new ProfilingSampler(RenderPassTag);

        readonly EasyARRendererData data;

        public EasyARRenderPass(EasyARRendererData data)
        {
            this.data = data;
            renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!data)
                return;
            var buffer = CommandBufferPool.Get(RenderPassTag);

            using (new ProfilingScope(buffer, sampler))
            {
                RenderTargetIdentifier src = BuiltinRenderTextureType.CameraTarget;
                RenderTargetIdentifier dst = BuiltinRenderTextureType.CurrentActive;
                buffer.Blit(src, dst, data.material);
                buffer.SetRenderTarget(dst);
            }
            context.ExecuteCommandBuffer(buffer);
            CommandBufferPool.Release(buffer);
        }
    }

    [SerializeField]
    EasyARRendererData data;

    EasyARRenderPass renderPass;

    public override void Create()
    {
        renderPass = new EasyARRenderPass(data);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(renderPass);
    }
}
