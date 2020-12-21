using System;
using UnityEngine;

namespace easyar
{
    [RequireComponent(typeof(RenderCameraController))]
    public class CameraImageRendererURP : MonoBehaviour
    {
        [SerializeField]
        EasyARRendererData data;

        RenderCameraController controller;
        CameraImageMaterial arMaterial;
        Material material;
        CameraParameters cameraParameters;
        bool renderImageHFlip;

        /// <summary>
        /// <para xml:lang="en">Camera image rendering update event. This event will pass out the Material and texture size of current camera image rendering. This event only indicates a new render happens, while the camera image itself may not change.</para>
        /// </summary>
        public event Action<Material, Vector2> OnFrameRenderUpdate;
        event Action<Camera, RenderTexture> TargetTextureChange;

        protected virtual void Awake()
        {
            controller = GetComponent<RenderCameraController>();
            arMaterial = new CameraImageMaterial();
        }

        protected virtual void OnDestroy()
        {
            arMaterial.Dispose();
            if (cameraParameters != null)
                cameraParameters.Dispose();
        }

        /// <summary>
        /// <para xml:lang="en">Get the <see cref="RenderTexture"/> of camera image.</para>
        /// <para xml:lang="en">The texture is a full sized image from <see cref="OutputFrame"/>, not cropped by the screen. The action <paramref name="targetTextureEventHandler"/> will pass out the <see cref="RenderTexture"/> and the <see cref="Camera"/> drawing the texture when the texture created or changed, will not call every frame or when the camera image data change. Calling this method will create external resources, and will trigger render when necessary, so make sure to release the resource using <see cref="DropTargetTexture"/> when not use.</para>
        /// </summary>
        public void RequestTargetTexture(Action<Camera, RenderTexture> targetTextureEventHandler)
        {
            TargetTextureChange += targetTextureEventHandler;
            data.UpdateTexture(controller ? controller.TargetCamera : null, material, out var texture);
            if (TargetTextureChange != null && texture)
                TargetTextureChange(controller.TargetCamera, texture);
        }

        /// <summary>
        /// <para xml:lang="en">Release the <see cref="RenderTexture"/> of camera image. Internal resources will be released when all holders release.</para>
        /// </summary>
        public void DropTargetTexture(Action<Camera, RenderTexture> targetTextureEventHandler)
        {
            if (controller)
                targetTextureEventHandler(controller.TargetCamera, null);
            TargetTextureChange -= targetTextureEventHandler;
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
        public void SetHFilp(bool hFlip) => renderImageHFlip = hFlip;

        void OnFrameChange(OutputFrame outputFrame, Matrix4x4 displayCompensation)
        {
            if (outputFrame == null)
            {
                material = null;
                if (data != null)
                {
                    if (TargetTextureChange != null && data.UpdateTexture(controller.TargetCamera, material, out var texture))
                        TargetTextureChange(controller.TargetCamera, texture);
                }
                return;
            }
            if (!enabled && data == null && OnFrameRenderUpdate == null)
                return;
            using (var frame = outputFrame.inputFrame())
            {
                using (var image = frame.image())
                {
                    var materialUpdated = arMaterial.UpdateByImage(image);
                    if (material != materialUpdated)
                    {
                        material = materialUpdated;
                        data.material = material;
                    }
                }
                if (cameraParameters != null)
                    cameraParameters.Dispose();
                cameraParameters = frame.cameraParameters();
            }
        }

        void OnFrameUpdate(OutputFrame outputFrame)
        {
            if (!controller || (!enabled && data == null && OnFrameRenderUpdate == null))
                return;

            if (data != null)
            {
                if (TargetTextureChange != null && data.UpdateTexture(controller.TargetCamera, material, out var texture))
                    TargetTextureChange(controller.TargetCamera, texture);
            }

            if (!material)
                return;

            bool cameraFront = cameraParameters.cameraDeviceType() == CameraDeviceType.Front;
            var imageProjection = cameraParameters.imageProjection(controller.TargetCamera.aspect, EasyARController.Instance.Display.Rotation, false, cameraFront? !renderImageHFlip : renderImageHFlip).ToUnityMatrix();
            if (renderImageHFlip)
            {
                var translateMatrix = Matrix4x4.identity;
                translateMatrix.m00 = -1;
                imageProjection = translateMatrix * imageProjection;
            }
            material.SetMatrix("_TextureRotation", imageProjection);
            OnFrameRenderUpdate?.Invoke(material, new Vector2(Screen.width * controller.TargetCamera.rect.width, Screen.height * controller.TargetCamera.rect.height));
        }
    }
}