using System;
using Mediapipe;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity;
using BaseOptions = Mediapipe.Tasks.Core.BaseOptions;

namespace MediaPipeMotionCapture
{
    public class PoseTrackingTask : IDisposable
    {
        private PoseLandmarker _landmarker;
        private bool _isDisposed;
        private Action<PoseLandmarkerResult> _resultCallback;

        public int NumPoses { get; }
        public float MinDetectionConfidence { get; }
        public float MinPresenceConfidence { get; }
        public float MinTrackingConfidence { get; }
        public bool OutputSegmentationMasks { get; }

        public PoseTrackingTask(
            int numPoses = 1,
            float minDetectionConfidence = 0.5f,
            float minPresenceConfidence = 0.5f,
            float minTrackingConfidence = 0.5f,
            bool outputSegmentationMasks = false)
        {
            NumPoses = numPoses;
            MinDetectionConfidence = minDetectionConfidence;
            MinPresenceConfidence = minPresenceConfidence;
            MinTrackingConfidence = minTrackingConfidence;
            OutputSegmentationMasks = outputSegmentationMasks;
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

        public void Initialize(string modelPath = "pose_landmarker_full.bytes",
                               Action<PoseLandmarkerResult> resultCallback = null)
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

            var options = new PoseLandmarkerOptions(
                baseOptions: baseOptions,
                runningMode: RunningMode.LIVE_STREAM,
                numPoses: NumPoses,
                minPoseDetectionConfidence: MinDetectionConfidence,
                minPosePresenceConfidence: MinPresenceConfidence,
                minTrackingConfidence: MinTrackingConfidence,
                outputSegmentationMasks: OutputSegmentationMasks,
                resultCallback: OnDetectionResult
            );

            _landmarker = PoseLandmarker.CreateFromOptions(options, GpuManager.GpuResources);

            DebugLogger.Log($"[PoseTrackingTask] Initialized (LIVE_STREAM). Delegate={delegateType}, Model={modelPath}");
        }

        public void DetectAsync(Image image, long timestampMs, int rotationDegrees = 0)
        {
            _landmarker?.DetectAsync(image, timestampMs, new ImageProcessingOptions(rotationDegrees: rotationDegrees));
        }

        private void OnDetectionResult(PoseLandmarkerResult result, Image image, long timestampMs)
        {
            _resultCallback?.Invoke(result);
        }
    }
}
