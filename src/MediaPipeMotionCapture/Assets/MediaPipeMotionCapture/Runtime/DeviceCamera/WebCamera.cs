using Mediapipe;
using Unity.Collections;
using UnityEngine;

namespace MediaPipeMotionCapture
{
    public sealed class WebCamera : DeviceCameraBase
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private int _requestedWidth = 1280;
        [SerializeField] private int _requestedHeight = 720;
        [SerializeField] private int _requestedFps = 30;
        [SerializeField] private string _deviceName = "";

        private WebCamTexture _webCamTexture;
        private Color32[] _pixelBuffer;
        private NativeArray<Color32> _nativePixelBuffer;

        public override Vector3 Position => _camera != null ? _camera.transform.position : Vector3.zero;
        public override Quaternion Rotation => _camera != null ? _camera.transform.rotation : Quaternion.identity;
        public override float VerticalFieldOfView => _camera != null ? _camera.fieldOfView : 0f;
        public override Texture ImageTexture => _webCamTexture;
        public override int? ImageWidth => _webCamTexture != null ? _webCamTexture.width : null;
        public override int? ImageHeight => _webCamTexture != null ? _webCamTexture.height : null;
        public override bool IsReady => _webCamTexture != null && _webCamTexture.isPlaying;

        void Start()
        {
            if (string.IsNullOrEmpty(_deviceName))
            {
                _webCamTexture = new WebCamTexture(_requestedWidth, _requestedHeight, _requestedFps);
            }
            else
            {
                _webCamTexture = new WebCamTexture(_deviceName, _requestedWidth, _requestedHeight, _requestedFps);
            }

            _webCamTexture.Play();

            DebugLogger.Log($"[WebCamera] Started. Device={_webCamTexture.deviceName}, Resolution={_webCamTexture.width}x{_webCamTexture.height}");
        }

        void OnDestroy()
        {
            if (_webCamTexture != null)
            {
                _webCamTexture.Stop();
                Destroy(_webCamTexture);
                _webCamTexture = null;
            }
            if (_nativePixelBuffer.IsCreated)
            {
                _nativePixelBuffer.Dispose();
            }
        }

        public override void SetPositionAndRotation(Vector3 position, Quaternion rotation)
        {
            if (_camera == null)
            {
                DebugLogger.LogError("Camera reference is not set. Cannot set position and rotation.");
                return;
            }

            _camera.transform.SetPositionAndRotation(position, rotation);
        }

        public override void SetFieldOfView(float fov, FieldOfViewType type)
        {
            if (_camera == null)
            {
                DebugLogger.LogError("Camera reference is not set. Cannot set field of view.");
                return;
            }

            if (ImageWidth == null || ImageHeight == null)
            {
                DebugLogger.LogError("Camera does not have valid image dimensions. Cannot set field of view.");
                return;
            }

            var imageWidth = ImageWidth.Value;
            var imageHeight = ImageHeight.Value;

            switch (type)
            {
                case FieldOfViewType.Vertical:
                    _camera.fieldOfView = fov;
                    break;
                case FieldOfViewType.Horizontal:
                    var aspectRatio = (float)imageWidth / imageHeight;
                    _camera.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(fov * Mathf.Deg2Rad / 2f) / aspectRatio) * Mathf.Rad2Deg;
                    break;
                case FieldOfViewType.Diagonal:
                    var diagonalAspect = Mathf.Sqrt(imageWidth * imageWidth + imageHeight * imageHeight) / imageHeight;
                    _camera.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(fov * Mathf.Deg2Rad / 2f) / diagonalAspect) * Mathf.Rad2Deg;
                    break;
            }
        }

        public override bool TryAcquireMediaPipeImage(out Image image, out int width, out int height)
        {
            image = null;
            width = 0;
            height = 0;

            if (_webCamTexture == null || !_webCamTexture.isPlaying || !_webCamTexture.didUpdateThisFrame)
            {
                return false;
            }

            width = _webCamTexture.width;
            height = _webCamTexture.height;

            EnsureNativeBuffer(width, height);

            // WebCamTexture → Color32[] → NativeArray<Color32> (copy with vertical flip) → Reinterpret<byte>
            // GetPixels32 returns with the origin at the bottom-left (Y-axis up), but MediaPipe expects the origin at the top-left (Y-axis down).
            _webCamTexture.GetPixels32(_pixelBuffer);
            CopyFlippedVertically(_pixelBuffer, _nativePixelBuffer, width, height);

            image = new Image(
                ImageFormat.Types.Format.Srgba,
                width,
                height,
                widthStep: width * 4,
                pixelData: _nativePixelBuffer.Reinterpret<byte>(4)
            );

            return true;
        }

        public override bool TryCreateImageFromCurrentBuffer(out Image image, out int width, out int height)
        {
            image = null;
            width = 0;
            height = 0;

            if (!_nativePixelBuffer.IsCreated || _webCamTexture == null)
            {
                return false;
            }

            width = _webCamTexture.width;
            height = _webCamTexture.height;
            image = new Image(
                ImageFormat.Types.Format.Srgba,
                width,
                height,
                widthStep: width * 4,
                pixelData: _nativePixelBuffer.Reinterpret<byte>(4)
            );

            return true;
        }

        private static void CopyFlippedVertically(Color32[] src, NativeArray<Color32> dst, int width, int height)
        {
            for (var y = 0; y < height; y++)
            {
                var srcRow = (height - 1 - y) * width;
                var dstRow = y * width;
                NativeArray<Color32>.Copy(src, srcRow, dst, dstRow, width);
            }
        }

        private void EnsureNativeBuffer(int width, int height)
        {
            var pixelCount = width * height;
            if (!_nativePixelBuffer.IsCreated || _nativePixelBuffer.Length != pixelCount)
            {
                if (_nativePixelBuffer.IsCreated)
                {
                    _nativePixelBuffer.Dispose();
                }
                _nativePixelBuffer = new NativeArray<Color32>(pixelCount, Allocator.Persistent);
                _pixelBuffer = new Color32[pixelCount];
            }
        }
    }
}
