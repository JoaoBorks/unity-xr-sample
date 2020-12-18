using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace easyar
{
    [RequireComponent(typeof(RenderCameraController))]
    public class CameraImageRendererURP : MonoBehaviour
    {
        private RenderCameraController controller;
        private CommandBuffer commandBuffer;
        private CameraImageMaterial arMaterial;
        private Material material;
        private CameraParameters cameraParameters;
        private bool renderImageHFlip;
        private UserRequest request;

        /// <summary>
        /// <para xml:lang="en">Camera image rendering update event. This event will pass out the Material and texture size of current camera image rendering. This event only indicates a new render happens, while the camera image itself may not change.</para>
        /// </summary>
        public event Action<Material, Vector2> OnFrameRenderUpdate;
        private event Action<Camera, RenderTexture> TargetTextureChange;

        protected virtual void Awake()
        {
            controller = GetComponent<RenderCameraController>();
            arMaterial = new CameraImageMaterial();
        }

        protected virtual void OnEnable() => UpdateCommandBuffer(controller ? controller.TargetCamera : null, material);

        protected virtual void OnDisable() => RemoveCommandBuffer(controller ? controller.TargetCamera : null);

        protected virtual void OnDestroy()
        {
            arMaterial.Dispose();
            if (request != null) 
                request.Dispose();
            if (cameraParameters != null)
                cameraParameters.Dispose();
        }

        /// <summary>
        /// <para xml:lang="en">Get the <see cref="RenderTexture"/> of camera image.</para>
        /// <para xml:lang="en">The texture is a full sized image from <see cref="OutputFrame"/>, not cropped by the screen. The action <paramref name="targetTextureEventHandler"/> will pass out the <see cref="RenderTexture"/> and the <see cref="Camera"/> drawing the texture when the texture created or changed, will not call every frame or when the camera image data change. Calling this method will create external resources, and will trigger render when necessary, so make sure to release the resource using <see cref="DropTargetTexture"/> when not use.</para>
        /// </summary>
        public void RequestTargetTexture(Action<Camera, RenderTexture> targetTextureEventHandler)
        {
            if (request == null)
            {
                request = new UserRequest();
            }
            TargetTextureChange += targetTextureEventHandler;
            request.UpdateTexture(controller ? controller.TargetCamera : null, material, out var texture);
            if (TargetTextureChange != null && texture)
            {
                TargetTextureChange(controller.TargetCamera, texture);
            }
        }

        /// <summary>
        /// <para xml:lang="en">Release the <see cref="RenderTexture"/> of camera image. Internal resources will be released when all holders release.</para>
        /// </summary>
        public void DropTargetTexture(Action<Camera, RenderTexture> targetTextureEventHandler)
        {
            if (controller)
            {
                targetTextureEventHandler(controller.TargetCamera, null);
            }
            TargetTextureChange -= targetTextureEventHandler;
            if (TargetTextureChange == null && request != null)
            {
                request.RemoveCommandBuffer(controller ? controller.TargetCamera : null);
                request.Dispose();
                request = null;
            }
        }

        /// <summary>
        /// <para xml:lang="en">Usually only for internal assemble use. Assemble response.</para>
        /// </summary>
        public void OnAssemble(ARSession session)
        {
            session.FrameChange += OnFrameChange;
            session.FrameUpdate += OnFrameUpdate;
        }

        /// <summary>
        /// <para xml:lang="en">Set render image horizontal flip.</para>
        /// </summary>
        public void SetHFilp(bool hFlip)
        {
            renderImageHFlip = hFlip;
        }

        private void OnFrameChange(OutputFrame outputFrame, Matrix4x4 displayCompensation)
        {
            if (outputFrame == null)
            {
                material = null;
                UpdateCommandBuffer(controller ? controller.TargetCamera : null, material);
                if (request != null)
                {
                    request.UpdateCommandBuffer(controller ? controller.TargetCamera : null, material);
                    RenderTexture texture;
                    if (TargetTextureChange != null && request.UpdateTexture(controller.TargetCamera, material, out texture))
                    {
                        TargetTextureChange(controller.TargetCamera, texture);
                    }
                }
                return;
            }
            if (!enabled && request == null && OnFrameRenderUpdate == null)
            {
                return;
            }
            using (var frame = outputFrame.inputFrame())
            {
                using (var image = frame.image())
                {
                    var materialUpdated = arMaterial.UpdateByImage(image);
                    if (material != materialUpdated)
                    {
                        material = materialUpdated;
                        UpdateCommandBuffer(controller ? controller.TargetCamera : null, material);
                        if (request != null) { request.UpdateCommandBuffer(controller ? controller.TargetCamera : null, material); }
                    }
                }
                if (cameraParameters != null)
                {
                    cameraParameters.Dispose();
                }
                cameraParameters = frame.cameraParameters();
            }
        }

        private void OnFrameUpdate(OutputFrame outputFrame)
        {
            if (!controller || (!enabled && request == null && OnFrameRenderUpdate == null))
            {
                return;
            }

            if (request != null)
            {
                RenderTexture texture;
                if (TargetTextureChange != null && request.UpdateTexture(controller.TargetCamera, material, out texture))
                {
                    TargetTextureChange(controller.TargetCamera, texture);
                }
            }

            if (!material)
            {
                return;
            }

            bool cameraFront = cameraParameters.cameraDeviceType() == CameraDeviceType.Front ? true : false;
            var imageProjection = cameraParameters.imageProjection(controller.TargetCamera.aspect, EasyARController.Instance.Display.Rotation, false, cameraFront? !renderImageHFlip : renderImageHFlip).ToUnityMatrix();
            if (renderImageHFlip)
            {
                var translateMatrix = Matrix4x4.identity;
                translateMatrix.m00 = -1;
                imageProjection = translateMatrix * imageProjection;
            }
            material.SetMatrix("_TextureRotation", imageProjection);
            if (OnFrameRenderUpdate != null)
            {
                OnFrameRenderUpdate(material, new Vector2(Screen.width * controller.TargetCamera.rect.width, Screen.height * controller.TargetCamera.rect.height));
            }
        }

        private void UpdateCommandBuffer(Camera cam, Material material)
        {
            RemoveCommandBuffer(cam);
            if (!cam || !material)
            {
                return;
            }
            if (enabled)
            {
                commandBuffer = new CommandBuffer();
                commandBuffer.Blit(null, BuiltinRenderTextureType.CameraTarget, material);
                cam.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, commandBuffer);
            }
        }

        private void RemoveCommandBuffer(Camera cam)
        {
            if (commandBuffer != null)
            {
                if (cam)
                {
                    cam.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, commandBuffer);
                }
                commandBuffer.Dispose();
                commandBuffer = null;
            }
        }

        private class UserRequest : IDisposable
        {
            private RenderTexture texture;
            private CommandBuffer commandBuffer;

            ~UserRequest()
            {
                if (commandBuffer != null) { commandBuffer.Dispose(); }
                if (texture) { Destroy(texture); }
            }

            public void Dispose()
            {
                if (commandBuffer != null) { commandBuffer.Dispose(); }
                if (texture) { Destroy(texture); }
                GC.SuppressFinalize(this);
            }

            public bool UpdateTexture(Camera cam, Material material, out RenderTexture tex)
            {
                tex = texture;
                if (!cam || !material)
                {
                    if (texture)
                    {
                        Destroy(texture);
                        tex = texture = null;
                        return true;
                    }
                    return false;
                }
                int w = (int)(Screen.width * cam.rect.width);
                int h = (int)(Screen.height * cam.rect.height);
                if (texture && (texture.width != w || texture.height != h))
                {
                    Destroy(texture);
                }

                if (texture)
                {
                    return false;
                }
                else
                {
                    texture = new RenderTexture(w, h, 0);
                    UpdateCommandBuffer(cam, material);
                    tex = texture;
                    return true;
                }
            }

            public void UpdateCommandBuffer(Camera cam, Material material)
            {
                RemoveCommandBuffer(cam);
                if (!cam || !material)
                {
                    return;
                }
                if (texture)
                {
                    commandBuffer = new CommandBuffer();
                    commandBuffer.Blit(null, texture, material);
                    cam.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, commandBuffer);
                }
            }

            public void RemoveCommandBuffer(Camera cam)
            {
                if (commandBuffer != null)
                {
                    if (cam)
                    {
                        cam.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, commandBuffer);
                    }
                    commandBuffer.Dispose();
                    commandBuffer = null;
                }
            }
        }
    }
}
