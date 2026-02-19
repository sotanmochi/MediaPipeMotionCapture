using System.Threading;
using Cysharp.Threading.Tasks;
using Mediapipe;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MediaPipeMotionCapture.ARFoundation
{
    /// <summary>
    /// AR Foundation の ARCameraManager を利用した IDeviceCamera 実装。
    /// XRCpuImage を RGBA32 に変換し、MediaPipe の入力として使用する。
    /// </summary>
    public sealed class ARCamera : DeviceCameraBase
    {
        [SerializeField] private ARCameraManager _arCameraManager;
        [SerializeField] private Camera _camera;

        [Header("Performance")]
        [Tooltip("入力画像の解像度を下げる倍率 (1 = 等倍, 2 = 半分, 3 = 1/3)")]
        [SerializeField] private int _downscaleFactor = 2;
        [Tooltip("N フレームに1回だけ新しいフレームを取得する (1 = 毎フレーム)")]
        [SerializeField] private int _processEveryNthFrame = 2;
        [Tooltip("出力画像の最大幅。これを超える場合は自動でダウンスケールする。0 = 制限なし")]
        [SerializeField] private int _maxOutputWidth = 640;

        [Header("Depth")]
        [Tooltip("AROcclusionManager への参照。null の場合はデプス機能を無効化する。")]
        [SerializeField] private AROcclusionManager _occlusionManager;
        [SerializeField] private bool _useSmoothedDepth = true;
        [SerializeField] private float _minDepthMeters = 0.1f;
        [SerializeField] private float _maxDepthMeters = 5.0f;

        private NativeArray<byte> _nativePixelBuffer;
        private Texture2D _cpuTexture;
        private int _lastFrameWidth;
        private int _lastFrameHeight;
        private bool _hasNewFrame;
        private int _frameCount;

        // Depth
        private NativeArray<byte> _depthRawBuffer;
        private int _depthWidth;
        private int _depthHeight;
        private bool _hasDepth;
        private XRCpuImage.Format _depthFormat;

        public override Vector3 Position => _camera != null ? _camera.transform.position : Vector3.zero;
        public override Quaternion Rotation => _camera != null ? _camera.transform.rotation : Quaternion.identity;
        public override float VerticalFieldOfView => _camera != null ? _camera.fieldOfView : 0f;

        public override Texture ImageTexture
        {
            get
            {
                if (!_hasNewFrame || !_nativePixelBuffer.IsCreated) return null;
                EnsureCpuTexture(_lastFrameWidth, _lastFrameHeight);
                _cpuTexture.LoadRawTextureData(_nativePixelBuffer);
                _cpuTexture.Apply();
                return _cpuTexture;
            }
        }

        public override int? ImageWidth => _hasNewFrame ? _lastFrameWidth : null;
        public override int? ImageHeight => _hasNewFrame ? _lastFrameHeight : null;
        public override bool IsReady => _arCameraManager != null && _arCameraManager.enabled;
        public override bool IsDepthAvailable => _occlusionManager != null && _hasDepth;

        public bool IsFrontFacing => CurrentFacingDirection == CameraFacingDirection.User;
        public CameraFacingDirection CurrentFacingDirection =>
            _arCameraManager != null ? _arCameraManager.currentFacingDirection : CameraFacingDirection.None;

        void Start()
        {
            if (_arCameraManager == null)
            {
                DebugLogger.LogError($"[{nameof(ARCamera)}] ARCameraManager is not assigned.");
                enabled = false;
                return;
            }

            if (_camera == null)
            {
                _camera = _arCameraManager.GetComponent<Camera>();
            }

            if (_camera == null)
            {
                DebugLogger.LogError($"[{nameof(ARCamera)}] Camera component not found on ARCameraManager GameObject.");
                enabled = false;
                return;
            }

            _downscaleFactor = Mathf.Max(1, _downscaleFactor);
            _processEveryNthFrame = Mathf.Max(1, _processEveryNthFrame);

            DebugLogger.Log($"[{nameof(ARCamera)}] Initialized. Downscale={_downscaleFactor}, ProcessEveryNthFrame={_processEveryNthFrame}");
        }

        void OnDestroy()
        {
            if (_nativePixelBuffer.IsCreated)
            {
                _nativePixelBuffer.Dispose();
            }

            if (_depthRawBuffer.IsCreated)
            {
                _depthRawBuffer.Dispose();
            }

            if (_cpuTexture != null)
            {
                Destroy(_cpuTexture);
                _cpuTexture = null;
            }
        }

        public override void SetPositionAndRotation(Vector3 position, Quaternion rotation)
        {
            // AR カメラの位置・回転は AR トラッキングで自動更新されるため、通常は外部から設定しない。
            DebugLogger.Log($"[{nameof(ARCamera)}] SetPositionAndRotation is not supported for AR camera. Position and rotation are managed by AR tracking.");
        }

        public override void SetFieldOfView(float fov, FieldOfViewType type)
        {
            // AR カメラの FOV はデバイスのカメラ intrinsics から自動設定されるため、外部から設定しない。
            DebugLogger.Log($"[{nameof(ARCamera)}] SetFieldOfView is not supported for AR camera. FOV is managed by AR Foundation.");
        }

        public bool TryAcquireLatestCpuImage(out XRCpuImage cpuImage)
        {
            cpuImage = default;
            if (_arCameraManager == null) return false;
            return _arCameraManager.TryAcquireLatestCpuImage(out cpuImage);
        }

        public override bool TryAcquireMediaPipeImage(out Image image, out int width, out int height)
        {
            image = null;
            width = 0;
            height = 0;

            if (_arCameraManager == null) return false;

            // フレームスキップ
            _frameCount++;
            if (_frameCount % _processEveryNthFrame != 0) return false;

            if (!_arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage cpuImage)) return false;

            try
            {
                // センサーネイティブ解像度で取得する（Portrait時もswapしない）。
                // 回転は MediaPipe の ImageProcessingOptions.rotationDegrees で対応する。
                width = cpuImage.width / _downscaleFactor;
                height = cpuImage.height / _downscaleFactor;

                // 最大幅の制限を適用
                if (_maxOutputWidth > 0 && width > _maxOutputWidth)
                {
                    var scale = (float)_maxOutputWidth / width;
                    width = _maxOutputWidth;
                    height = Mathf.Max(1, (int)(height * scale));
                }

                EnsureNativeBuffer(width, height);

                // MirrorX: XRCpuImage.Convert() の出力は左下原点（Unity Texture2D 規約）だが
                // MediaPipe は左上原点を期待するため、垂直反転が必須。
                // MirrorY: フロントカメラ使用時は追加で水平反転（鏡像表示）。
                var transformation = XRCpuImage.Transformation.MirrorX;
                if (_arCameraManager.currentFacingDirection == CameraFacingDirection.User)
                {
                    transformation |= XRCpuImage.Transformation.MirrorY;
                }

                var conversionParams = new XRCpuImage.ConversionParams
                {
                    inputRect = new RectInt(0, 0, cpuImage.width, cpuImage.height),
                    outputDimensions = new Vector2Int(width, height),
                    outputFormat = TextureFormat.RGBA32,
                    transformation = transformation,
                };

                unsafe
                {
                    cpuImage.Convert(conversionParams,
                        new System.IntPtr(NativeArrayUnsafeUtility.GetUnsafePtr(_nativePixelBuffer)),
                        _nativePixelBuffer.Length);
                }

                _lastFrameWidth = width;
                _lastFrameHeight = height;
                _hasNewFrame = true;

                TryUpdateDepthMap();

                image = new Image(
                    ImageFormat.Types.Format.Srgba,
                    width,
                    height,
                    widthStep: width * 4,
                    pixelData: _nativePixelBuffer
                );

                return true;
            }
            finally
            {
                cpuImage.Dispose();
            }
        }

        public override bool TryCreateImageFromCurrentBuffer(out Image image, out int width, out int height)
        {
            image = null;
            width = 0;
            height = 0;

            if (!_nativePixelBuffer.IsCreated || !_hasNewFrame) return false;

            width = _lastFrameWidth;
            height = _lastFrameHeight;

            image = new Image(
                ImageFormat.Types.Format.Srgba,
                width,
                height,
                widthStep: width * 4,
                pixelData: _nativePixelBuffer
            );

            return true;
        }

        // --- Depth ---

        public override bool TryGetDepthAt(float normalizedX, float normalizedY, out float depthMeters)
        {
            depthMeters = 0f;
            if (!_hasDepth || _depthWidth == 0 || _depthHeight == 0) return false;

            int x = Mathf.Clamp((int)(normalizedX * (_depthWidth - 1)), 0, _depthWidth - 1);
            int y = Mathf.Clamp((int)(normalizedY * (_depthHeight - 1)), 0, _depthHeight - 1);

            depthMeters = ReadDepthValue(x, y);

            return depthMeters >= _minDepthMeters && depthMeters <= _maxDepthMeters;
        }

        public override bool TryNormalizedToWorldPoint(float normalizedX, float normalizedY, out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;

            if (!TryGetDepthAt(normalizedX, normalizedY, out float depthMeters))
                return false;

            if (_camera == null) return false;

            // センサー座標系 → スクリーン座標系の変換
            float screenNx = normalizedX;
            float screenNy = normalizedY;

            // WIP
            TransformSensorToScreenCoordinates(ref screenNx, ref screenNy, IsFrontFacing);

            // スクリーン正規化座標 (左上原点, Y下向き) → Viewport 座標系 (左下原点, Y上向き)
            Vector3 viewportPoint = new Vector3(screenNx, 1f - screenNy, depthMeters);
            worldPosition = _camera.ViewportToWorldPoint(viewportPoint);

            return true;
        }

        /// <summary>
        /// TODO: Fix bugs
        /// </summary>
        private static void TransformSensorToScreenCoordinates(ref float nx, ref float ny, bool isFrontCamera)
        {
            if (isFrontCamera)
            {
                switch (Screen.orientation)
                {
                    case ScreenOrientation.LandscapeRight:
                        nx = 1f - nx;
                        ny = ny;
                        break;
                    case ScreenOrientation.Portrait:
                        var tmpP = nx;
                        nx = ny;
                        ny = tmpP;
                        break;
                    case ScreenOrientation.LandscapeLeft:
                        nx = nx;
                        ny = 1f - ny;
                        break;
                    case ScreenOrientation.PortraitUpsideDown:
                        nx = 1f - ny;
                        ny = 1f - nx;
                        break;
                    default:
                        break;
                }
            }
            else
            {
                switch (Screen.orientation)
                {
                    case ScreenOrientation.LandscapeLeft:
                        break;
                    case ScreenOrientation.Portrait:
                        var tmpP = nx;
                        nx = 1f - ny;
                        ny = tmpP;
                        break;
                    case ScreenOrientation.LandscapeRight:
                        nx = 1f - nx;
                        ny = 1f - ny;
                        break;
                    case ScreenOrientation.PortraitUpsideDown:
                        var tmpU = nx;
                        nx = ny;
                        ny = 1f - tmpU;
                        break;
                    default:
                        break;
                }
            }
        }

        private void TryUpdateDepthMap()
        {
            if (_occlusionManager == null) return;

            XRCpuImage depthImage;
            bool acquired = _useSmoothedDepth
                ? _occlusionManager.TryAcquireSmoothedEnvironmentDepthCpuImage(out depthImage)
                : _occlusionManager.TryAcquireEnvironmentDepthCpuImage(out depthImage);

            if (!acquired) return;

            try
            {
                _depthWidth = depthImage.width;
                _depthHeight = depthImage.height;
                _depthFormat = depthImage.format;

                var plane = depthImage.GetPlane(0);
                int dataSize = plane.data.Length;
                EnsureDepthBuffer(dataSize);

                NativeArray<byte>.Copy(plane.data, _depthRawBuffer, dataSize);
                _hasDepth = true;
            }
            finally
            {
                depthImage.Dispose();
            }
        }

        private float ReadDepthValue(int x, int y)
        {
            int index = y * _depthWidth + x;

            switch (_depthFormat)
            {
                case XRCpuImage.Format.DepthUint16:
                {
                    var shortBuffer = _depthRawBuffer.Reinterpret<ushort>(1);
                    return shortBuffer[index] * 0.001f; // mm → m
                }
                case XRCpuImage.Format.DepthFloat32:
                {
                    var floatBuffer = _depthRawBuffer.Reinterpret<float>(1);
                    return floatBuffer[index]; // already in meters
                }
                default:
                    DebugLogger.LogError($"[{nameof(ARCamera)}] Unsupported depth format: {_depthFormat}");
                    return 0f;
            }
        }

        private void EnsureDepthBuffer(int requiredSize)
        {
            if (!_depthRawBuffer.IsCreated || _depthRawBuffer.Length != requiredSize)
            {
                if (_depthRawBuffer.IsCreated)
                {
                    _depthRawBuffer.Dispose();
                }
                _depthRawBuffer = new NativeArray<byte>(requiredSize, Allocator.Persistent);
            }
        }

        // --- Buffers ---

        private void EnsureNativeBuffer(int width, int height)
        {
            var requiredSize = width * height * 4; // RGBA32: 4 bytes per pixel
            if (!_nativePixelBuffer.IsCreated || _nativePixelBuffer.Length != requiredSize)
            {
                if (_nativePixelBuffer.IsCreated)
                {
                    _nativePixelBuffer.Dispose();
                }
                _nativePixelBuffer = new NativeArray<byte>(requiredSize, Allocator.Persistent);
            }
        }

        private void EnsureCpuTexture(int width, int height)
        {
            if (_cpuTexture == null || _cpuTexture.width != width || _cpuTexture.height != height)
            {
                if (_cpuTexture != null)
                {
                    Destroy(_cpuTexture);
                }
                _cpuTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            }
        }
    }
}
