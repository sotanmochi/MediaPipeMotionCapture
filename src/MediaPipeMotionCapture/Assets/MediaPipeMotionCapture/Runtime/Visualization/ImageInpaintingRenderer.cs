using UnityEngine;
using UnityEngine.UI;

namespace MediaPipeMotionCapture
{
    /// <summary>
    /// 画像修復（Inpainting）処理のレンダラー
    /// </summary>
    public class ImageInpaintingRenderer : MonoBehaviour
    {
        [SerializeField] private MotionCaptureTaskRunner _motionCaptureTaskRunner;
        [SerializeField] private DeviceCameraBase _deviceCameraComponent;
        [SerializeField] private RawImage _outputImage;
        [SerializeField] private Shader _inpaintingShader;

        [Header("Temporal Median")]
        [SerializeField] private ComputeShader _medianComputeShader;
        [SerializeField] private int _medianBufferSize = 5;
        [SerializeField] private float _sampleIntervalSec = 0.1f;
        [SerializeField] private float _medianIntervalSec = 0.3f;

        private IDeviceCamera _deviceCamera;
        private TemporalMedianBackground _temporalMedian;
        private Material _material;
        private Material _originalMaterial;
        private float[] _currentMask;
        private int _maskWidth;
        private int _maskHeight;
        private bool _hasMask;
        private bool _initialized;

        void Start()
        {
            Initialize();
        }

        void OnDestroy()
        {
            _temporalMedian?.Dispose();
            _temporalMedian = null;

            if (_outputImage != null && _originalMaterial != null)
            {
                _outputImage.material = _originalMaterial;
            }

            if (_material != null)
            {
                Destroy(_material);
                _material = null;
            }
        }

        void OnEnable()
        {
            if (_motionCaptureTaskRunner != null)
                _motionCaptureTaskRunner.OnTrackingDataUpdated += OnTrackingDataUpdated;
        }

        void OnDisable()
        {
            if (_motionCaptureTaskRunner != null)
                _motionCaptureTaskRunner.OnTrackingDataUpdated -= OnTrackingDataUpdated;
        }

        void LateUpdate()
        {
            if (!_initialized || !_hasMask) return;

            var cameraTexture = _deviceCamera.ImageTexture;
            if (cameraTexture == null) return;

            // 時間的メディアン背景の更新（マスク膨張も内部で実行される）
            _temporalMedian.UpdateBackground(cameraTexture, _currentMask, _maskWidth, _maskHeight);

            // モードに応じたマスクバッファとスケールを合成シェーダーに設定
            var activeMask = _temporalMedian.MaskBuffer;
            if (activeMask != null)
            {
                _material.SetBuffer("_MaskBuffer", activeMask);
            }

            // シェーダーパラメータの設定
            _material.SetTexture("_CameraTex", cameraTexture);
            _material.SetTexture("_BackgroundTex", _temporalMedian.BackgroundTexture);
            _material.SetInt("_Width", _maskWidth);
            _material.SetInt("_Height", _maskHeight);

            // RawImage にマテリアルを適用
            _outputImage.texture = cameraTexture;
            if (_outputImage.material != _material)
            {
                _originalMaterial = _outputImage.material;
                _outputImage.material = _material;
            }
        }

        private void Initialize()
        {
            if (_initialized) return;

            _deviceCamera = _deviceCameraComponent.GetComponent<IDeviceCamera>();
            if (_deviceCamera == null)
            {
                DebugLogger.LogError($"[{nameof(ImageInpaintingRenderer)}] DeviceCamera component does not implement IDeviceCamera.");
                enabled = false;
                return;
            }

            if (_outputImage == null)
            {
                DebugLogger.LogError($"[{nameof(ImageInpaintingRenderer)}] Output image is not assigned.");
                enabled = false;
                return;
            }

            if (_inpaintingShader == null)
            {
                DebugLogger.LogError($"[{nameof(ImageInpaintingRenderer)}] Inpainting shader is not assigned.");
                enabled = false;
                return;
            }

            if (_medianComputeShader == null)
            {
                DebugLogger.LogError($"[{nameof(ImageInpaintingRenderer)}] Compute shader is not assigned.");
                enabled = false;
                return;
            }

            _material = new Material(_inpaintingShader);
            _temporalMedian = new TemporalMedianBackground(
                _medianComputeShader, _medianBufferSize, _sampleIntervalSec, _medianIntervalSec);

            _initialized = true;
        }

        private void OnTrackingDataUpdated(TrackingData data)
        {
            if (!_initialized) return;

            if (data.HasSegmentationMask && data.SegmentationMask != null)
            {
                _currentMask = data.SegmentationMask;
                _maskWidth = data.SegmentationMaskWidth;
                _maskHeight = data.SegmentationMaskHeight;
                _hasMask = true;
            }
            else
            {
                _hasMask = false;
            }
        }

    }
}
