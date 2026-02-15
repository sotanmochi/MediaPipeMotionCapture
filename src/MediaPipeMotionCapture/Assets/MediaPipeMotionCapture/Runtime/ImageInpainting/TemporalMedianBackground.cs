using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MediaPipeMotionCapture
{
    /// <summary>
    /// 時間的メディアンによる背景モデル構築（コンピュートシェーダー版）。
    /// 過去 N フレームのうち「人物がいない」ピクセルの中央値を背景として使う。
    /// 事前撮影した背景を初期値として使用し、時間経過とともに照明変化に適応する。
    /// 全ての重い処理は GPU 上で行い、CPU↔GPU 間のテクスチャ転送を排除する。
    /// サンプリングとメディアン計算は時間ベースで制御し、フレームレートに依存しない。
    /// </summary>
    public class TemporalMedianBackground : IDisposable
    {
        private const float PersonMaskThreshold = 0.3f;

        private readonly ComputeShader _computeShader;
        private readonly int _bufferSize;
        private readonly float _sampleIntervalSec;
        private readonly float _medianIntervalSec;

        // カーネル ID
        private readonly int _updateKernel;
        private readonly int _medianKernel;
        private readonly int _captureKernel;
        private readonly int _maskDilateHKernel;
        private readonly int _maskDilateVKernel;

        // GPU リソース
        private RenderTexture _backgroundRT;
        private GraphicsBuffer _ringBuffer;
        private GraphicsBuffer _validCount;
        private GraphicsBuffer _maskBuffer;
        private GraphicsBuffer _tempMask;
        private GraphicsBuffer _dilatedMask;
        private GraphicsBuffer _gaussWeights;
        private RenderTexture _cameraRT; // WebCamTexture 用の中間バッファ

        // 状態
        private int _width;
        private int _height;
        private int _maskWidth;
        private int _maskHeight;
        private int _currentSlot;
        private int _totalFramesAdded;
        private float _lastSampleTime;
        private float _lastMedianTime;
        private bool _isInitialized;

        public bool IsInitialized => _isInitialized;
        public Texture BackgroundTexture => _backgroundRT;
        public GraphicsBuffer MaskBuffer => _maskBuffer;

        public TemporalMedianBackground(
            ComputeShader computeShader,
            int bufferSize = 5,
            float sampleIntervalSec = 0.1f,
            float medianIntervalSec = 0.3f)
        {
            _computeShader = computeShader;
            _bufferSize = bufferSize;
            _sampleIntervalSec = sampleIntervalSec;
            _medianIntervalSec = medianIntervalSec;

            _updateKernel = _computeShader.FindKernel("UpdateBuffer");
            _medianKernel = _computeShader.FindKernel("ComputeMedian");
            _captureKernel = _computeShader.FindKernel("CaptureBackground");
            _maskDilateHKernel = _computeShader.FindKernel("MaskDilateH");
            _maskDilateVKernel = _computeShader.FindKernel("MaskDilateV");
        }

        /// <summary>
        /// 事前撮影した背景をキャプチャする。
        /// カメラテクスチャの現在のフレームを初期背景として使用する。
        /// </summary>
        public void CaptureInitialBackground(Texture cameraTexture)
        {
            if (cameraTexture == null) return;

            var width = cameraTexture.width;
            var height = cameraTexture.height;

            EnsureInitialized(width, height);

            var readableTex = GetReadableTexture(cameraTexture);

            _computeShader.SetTexture(_captureKernel, "_CameraTex", readableTex);
            _computeShader.SetTexture(_captureKernel, "_BackgroundTex", _backgroundRT);

            _computeShader.SetInt("_Width", width);
            _computeShader.SetInt("_Height", height);
            _computeShader.SetInt("_BufferSize", _bufferSize);

            var groupsX = Mathf.CeilToInt(width / 8f);
            var groupsY = Mathf.CeilToInt(height / 8f);
            _computeShader.Dispatch(_captureKernel, groupsX, groupsY, 1);

            _totalFramesAdded = _bufferSize;
            _currentSlot = 0;
        }

        /// <summary>
        /// 毎フレーム呼び出し。セグメンテーションマスクを GPU に転送し、
        /// 時間ベースの間隔でリングバッファへのサンプル蓄積とメディアン計算を行う。
        /// </summary>
        public void UpdateBackground(Texture cameraTexture, float[] mask, int maskW, int maskH)
        {
            if (cameraTexture == null || mask == null) return;

            var width = cameraTexture.width;
            var height = cameraTexture.height;

            EnsureInitialized(width, height);
            EnsureMaskBuffer(maskW, maskH);

            // マスクデータを GPU に転送（レンダリングシェーダーが毎フレーム参照するため常に実行）
            _maskBuffer.SetData(mask);

            var readableTex = GetReadableTexture(cameraTexture);

            _computeShader.SetInt("_MaskWidth", maskW);
            _computeShader.SetInt("_MaskHeight", maskH);
            _computeShader.SetFloat("_MaskScale", 1.0f);

            _computeShader.SetInt("_Width", width);
            _computeShader.SetInt("_Height", height);
            _computeShader.SetInt("_BufferSize", _bufferSize);
            _computeShader.SetFloat("_MaskThreshold", PersonMaskThreshold);

            var groupsX = Mathf.CeilToInt(width / 8f);
            var groupsY = Mathf.CeilToInt(height / 8f);

            var now = Time.time;

            // 一定時間ごとにリングバッファへサンプルを蓄積
            if (now - _lastSampleTime >= _sampleIntervalSec)
            {
                _lastSampleTime = now;

                _computeShader.SetBuffer(_updateKernel, "_MaskBuffer", _maskBuffer);
                _computeShader.SetInt("_CurrentSlot", _currentSlot % _bufferSize);
                _computeShader.SetInt("_TotalFramesAdded", _totalFramesAdded);

                _computeShader.SetTexture(_updateKernel, "_CameraTex", readableTex);
                _computeShader.Dispatch(_updateKernel, groupsX, groupsY, 1);

                _currentSlot++;
                _totalFramesAdded++;
            }

            // 一定時間ごとにメディアン計算
            if (now - _lastMedianTime >= _medianIntervalSec)
            {
                _lastMedianTime = now;

                _computeShader.SetInt("_TotalFramesAdded", _totalFramesAdded);
                _computeShader.SetTexture(_medianKernel, "_BackgroundTex", _backgroundRT);
                _computeShader.Dispatch(_medianKernel, groupsX, groupsY, 1);
            }
        }

        private void EnsureInitialized(int width, int height)
        {
            if (_isInitialized && _width == width && _height == height) return;

            ReleaseGpuResources();

            _width = width;
            _height = height;
            var pixelCount = width * height;

            _backgroundRT = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            _backgroundRT.Create();

            // リングバッファ: bufferSize * pixelCount 個の uint
            var stride = Marshal.SizeOf(typeof(uint));
            _ringBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _bufferSize * pixelCount, stride);

            var intStride = Marshal.SizeOf(typeof(int));
            _validCount = new GraphicsBuffer(GraphicsBuffer.Target.Structured, pixelCount, intStride);
            _validCount.SetData(new int[pixelCount]);

            _computeShader.SetBuffer(_updateKernel, "_RingBuffer", _ringBuffer);
            _computeShader.SetBuffer(_updateKernel, "_ValidCount", _validCount);
            _computeShader.SetBuffer(_medianKernel, "_RingBuffer", _ringBuffer);
            _computeShader.SetBuffer(_medianKernel, "_ValidCount", _validCount);
            _computeShader.SetBuffer(_captureKernel, "_RingBuffer", _ringBuffer);
            _computeShader.SetBuffer(_captureKernel, "_ValidCount", _validCount);

            _currentSlot = 0;
            _totalFramesAdded = 0;
            _lastSampleTime = 0f;
            _lastMedianTime = 0f;
            _isInitialized = true;
        }

        private void EnsureMaskBuffer(int maskW, int maskH)
        {
            var requiredSize = maskW * maskH;
            if (_maskBuffer != null && _maskBuffer.count == requiredSize
                && _maskWidth == maskW && _maskHeight == maskH) return;

            _maskWidth = maskW;
            _maskHeight = maskH;

            var stride = Marshal.SizeOf(typeof(float));

            _maskBuffer?.Release();
            _maskBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, requiredSize, stride);
        }

        private static float[] ComputeGaussianWeights(float sigma, int radius)
        {
            var weights = new float[radius + 1];
            var twoSigmaSq = 2f * sigma * sigma;
            for (var i = 0; i <= radius; i++)
            {
                weights[i] = Mathf.Exp(-(i * i) / twoSigmaSq);
            }
            return weights;
        }

        /// <summary>
        /// カメラテクスチャをコンピュートシェーダーで読み取り可能な形式に変換する。
        /// Texture2D/RenderTexture はそのまま使用。WebCamTexture は RenderTexture に Blit する。
        /// </summary>
        private Texture GetReadableTexture(Texture source)
        {
            if (source is RenderTexture || source is Texture2D)
            {
                return source;
            }

            // WebCamTexture → RenderTexture に Blit (GPU→GPU、CPU ストールなし)
            if (_cameraRT == null || _cameraRT.width != source.width || _cameraRT.height != source.height)
            {
                _cameraRT?.Release();
                _cameraRT = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            }
            Graphics.Blit(source, _cameraRT);
            return _cameraRT;
        }

        private void ReleaseGpuResources()
        {
            if (_backgroundRT != null)
            {
                _backgroundRT.Release();
                UnityEngine.Object.Destroy(_backgroundRT);
                _backgroundRT = null;
            }

            _ringBuffer?.Release();
            _ringBuffer = null;

            _validCount?.Release();
            _validCount = null;

            _maskBuffer?.Release();
            _maskBuffer = null;

            _tempMask?.Release();
            _tempMask = null;

            _dilatedMask?.Release();
            _dilatedMask = null;

            _gaussWeights?.Release();
            _gaussWeights = null;

            if (_cameraRT != null)
            {
                _cameraRT.Release();
                UnityEngine.Object.Destroy(_cameraRT);
                _cameraRT = null;
            }
        }

        public void Dispose()
        {
            ReleaseGpuResources();
            _isInitialized = false;
        }
    }
}
