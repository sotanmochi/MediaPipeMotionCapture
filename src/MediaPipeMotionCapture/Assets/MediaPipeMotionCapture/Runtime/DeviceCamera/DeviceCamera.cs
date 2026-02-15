using Mediapipe;
using UnityEngine;

namespace MediaPipeMotionCapture
{
    public interface IDeviceCamera
    {
        Vector3 Position { get; }
        Quaternion Rotation { get; }
        float VerticalFieldOfView { get; }
        Texture ImageTexture { get; }
        int? ImageWidth { get; }
        int? ImageHeight { get; }
        bool IsReady { get; }

        void SetPositionAndRotation(Vector3 position, Quaternion rotation);
        void SetFieldOfView(float fov, FieldOfViewType type);
        bool TryAcquireMediaPipeImage(out Image image, out int width, out int height);
        bool TryCreateImageFromCurrentBuffer(out Image image, out int width, out int height);
    }

    public static class DeviceCameraExtensions
    {
        public static float GetFieldOfView(this IDeviceCamera deviceCamera, FieldOfViewType type)
        {
            if (deviceCamera.ImageWidth == null || deviceCamera.ImageHeight == null)
            {
                DebugLogger.LogError("Camera does not have valid image dimensions. Returning vertical FOV as default.");
                return deviceCamera.VerticalFieldOfView;
            }

            var imageWidth = deviceCamera.ImageWidth.Value;
            var imageHeight = deviceCamera.ImageHeight.Value;

            switch (type)
            {
                case FieldOfViewType.Vertical:
                    return deviceCamera.VerticalFieldOfView;
                case FieldOfViewType.Horizontal:
                    // Calculate horizontal FOV based on vertical FOV and aspect ratio
                    var aspectRatio = (float)imageWidth / imageHeight;
                    return 2f * Mathf.Atan(Mathf.Tan(deviceCamera.VerticalFieldOfView * Mathf.Deg2Rad / 2f) * aspectRatio) * Mathf.Rad2Deg;
                case FieldOfViewType.Diagonal:
                    // Calculate diagonal FOV based on vertical FOV and aspect ratio
                    var diagonalAspect = Mathf.Sqrt(imageWidth * imageWidth + imageHeight * imageHeight) / imageHeight;
                    return 2f * Mathf.Atan(Mathf.Tan(deviceCamera.VerticalFieldOfView * Mathf.Deg2Rad / 2f) * diagonalAspect) * Mathf.Rad2Deg;
                default:
                    return deviceCamera.VerticalFieldOfView;
            }
        }
    }

    public enum FieldOfViewType
    {
        Vertical,
        Horizontal,
        Diagonal,
    }
}
