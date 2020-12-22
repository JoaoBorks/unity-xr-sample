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

        public void OnAssemble(ARSession session)
        {
            session.FrameChange += OnFrameChange;
            session.FrameUpdate += OnFrameUpdate;
        }

        public void SetHFilp(bool hFlip) => renderImageHFlip = hFlip;

        void OnFrameChange(OutputFrame outputFrame, Matrix4x4 displayCompensation)
        {
            if (outputFrame == null)
            {
                material = null;
                return;
            }
            if (!enabled && !data)
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
            if (!controller || (!enabled && !data) || !material)
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
        }
    }
}