using System;
using Mediapipe;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Unity;
using BaseOptions = Mediapipe.Tasks.Core.BaseOptions;

namespace MediaPipeMotionCapture
{
    public class HandTrackingTask : IDisposable
    {
        private HandLandmarker _landmarker;
        private bool _isDisposed;
        private Action<HandLandmarkerResult> _resultCallback;

        public int NumHands { get; }
        public float MinDetectionConfidence { get; }
        public float MinPresenceConfidence { get; }
        public float MinTrackingConfidence { get; }

        public HandTrackingTask(
            int numHands = 2,
            float minDetectionConfidence = 0.5f,
            float minPresenceConfidence = 0.5f,
            float minTrackingConfidence = 0.5f)
        {
            NumHands = numHands;
            MinDetectionConfidence = minDetectionConfidence;
            MinPresenceConfidence = minPresenceConfidence;
            MinTrackingConfidence = minTrackingConfidence;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _landmarker?.Close();
                _landmarker = null;
                _isDisposed = true;
            }
        }

        public void Initialize(string modelPath = "hand_landmarker.bytes",
                               Action<HandLandmarkerResult> resultCallback = null)
        {
            _resultCallback = resultCallback;

            var delegateType = BaseOptions.Delegate.CPU;
#if !UNITY_EDITOR
            delegateType = BaseOptions.Delegate.GPU;
#endif

            var baseOptions = new BaseOptions(
                delegateCase: delegateType,
                modelAssetPath: modelPath
            );

            var options = new HandLandmarkerOptions(
                baseOptions: baseOptions,
                runningMode: RunningMode.LIVE_STREAM,
                numHands: NumHands,
                minHandDetectionConfidence: MinDetectionConfidence,
                minHandPresenceConfidence: MinPresenceConfidence,
                minTrackingConfidence: MinTrackingConfidence,
                resultCallback: OnDetectionResult
            );

            _landmarker = HandLandmarker.CreateFromOptions(options, GpuManager.GpuResources);

            DebugLogger.Log($"[HandTrackingTask] Initialized (LIVE_STREAM). Delegate={delegateType}, Model={modelPath}");
        }

        public void DetectAsync(Image image, long timestampMs, int rotationDegrees = 0)
        {
            _landmarker?.DetectAsync(image, timestampMs, new ImageProcessingOptions(rotationDegrees: rotationDegrees));
        }

        private void OnDetectionResult(HandLandmarkerResult result, Image image, long timestampMs)
        {
            _resultCallback?.Invoke(result);
        }
    }
}
