using System;
using Mediapipe;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Unity;
using BaseOptions = Mediapipe.Tasks.Core.BaseOptions;

namespace MediaPipeMotionCapture
{
    public class FaceTrackingTask : IDisposable
    {
        private FaceLandmarker _landmarker;
        private ImageProcessingOptions _imageProcessingOptions;
        private bool _isDisposed;
        private Action<FaceLandmarkerResult> _resultCallback;

        public int NumFaces { get; }
        public float MinDetectionConfidence { get; }
        public float MinPresenceConfidence { get; }
        public float MinTrackingConfidence { get; }
        public bool OutputFaceBlendshapes { get; }
        public bool OutputFaceTransformationMatrixes { get; }

        public FaceTrackingTask(
            int numFaces = 1,
            float minDetectionConfidence = 0.5f,
            float minPresenceConfidence = 0.5f,
            float minTrackingConfidence = 0.5f,
            bool outputFaceBlendshapes = true,
            bool outputFaceTransformationMatrixes = true)
        {
            NumFaces = numFaces;
            MinDetectionConfidence = minDetectionConfidence;
            MinPresenceConfidence = minPresenceConfidence;
            MinTrackingConfidence = minTrackingConfidence;
            OutputFaceBlendshapes = outputFaceBlendshapes;
            OutputFaceTransformationMatrixes = outputFaceTransformationMatrixes;
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

        public void Initialize(string modelPath = "face_landmarker_v2_with_blendshapes.bytes",
                               Action<FaceLandmarkerResult> resultCallback = null)
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

            var options = new FaceLandmarkerOptions(
                baseOptions: baseOptions,
                runningMode: RunningMode.LIVE_STREAM,
                numFaces: NumFaces,
                minFaceDetectionConfidence: MinDetectionConfidence,
                minFacePresenceConfidence: MinPresenceConfidence,
                minTrackingConfidence: MinTrackingConfidence,
                outputFaceBlendshapes: OutputFaceBlendshapes,
                outputFaceTransformationMatrixes: OutputFaceTransformationMatrixes,
                resultCallback: OnDetectionResult
            );

            _landmarker = FaceLandmarker.CreateFromOptions(options, GpuManager.GpuResources);
            _imageProcessingOptions = new ImageProcessingOptions(rotationDegrees: 0);

            DebugLogger.Log($"[FaceTrackingTask] Initialized (LIVE_STREAM). Delegate={delegateType}, Model={modelPath}, Blendshapes={OutputFaceBlendshapes}, TransformMatrix={OutputFaceTransformationMatrixes}");
        }

        public void DetectAsync(Image image, long timestampMs)
        {
            _landmarker?.DetectAsync(image, timestampMs, _imageProcessingOptions);
        }

        private void OnDetectionResult(FaceLandmarkerResult result, Image image, long timestampMs)
        {
            _resultCallback?.Invoke(result);
        }
    }
}
