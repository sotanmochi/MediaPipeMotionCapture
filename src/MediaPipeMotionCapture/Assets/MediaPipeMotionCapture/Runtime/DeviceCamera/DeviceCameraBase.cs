using Mediapipe;
using UnityEngine;

namespace MediaPipeMotionCapture
{
    public abstract class DeviceCameraBase : MonoBehaviour, IDeviceCamera
    {
        public abstract Vector3 Position { get; }
        public abstract Quaternion Rotation { get; }
        public abstract float VerticalFieldOfView { get; }
        public abstract Texture ImageTexture { get; }
        public abstract int? ImageWidth { get; }
        public abstract int? ImageHeight { get; }
        public abstract bool IsReady { get; }

        public abstract void SetPositionAndRotation(Vector3 position, Quaternion rotation);
        public abstract void SetFieldOfView(float fov, FieldOfViewType type);
        public abstract bool TryAcquireMediaPipeImage(out Image image, out int width, out int height);
        public abstract bool TryCreateImageFromCurrentBuffer(out Image image, out int width, out int height);
    }
}
