using System;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using Mediapipe.Unity;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using MediaPipeMotionCapture.Smoothing;
using UnityEngine;

namespace MediaPipeMotionCapture
{
    public class MotionCaptureTaskRunner : MonoBehaviour
    {
        [SerializeField] private DeviceCameraBase _deviceCameraComponent;
        [SerializeField] private TrackingConfig _config;
        [SerializeField] private bool _showDebugLog;

        private IDeviceCamera _deviceCamera;
        private PoseTrackingTask _poseTask;
        private HandTrackingTask _handTask;
        private FaceTrackingTask _faceTask;
        private LandmarkSmoother _smoother;
        private PoseData _poseData;
        private HandData _leftHandData;
        private HandData _rightHandData;
        private FaceData _faceData;
        private TrackingData _trackingData;
        private Stopwatch _stopwatch;
        private bool _isReady;

        private readonly object _dataLock = new();
        private bool _hasPendingPose;
        private bool _hasPendingLeftHand;
        private bool _hasPendingRightHand;
        private bool _hasPendingFace;
        private bool _hasPendingSegmentationMask;

        public event Action<TrackingData> OnTrackingDataUpdated;

        async void Start()
        {
            await InitializeAsync();
        }

        void Update()
        {
            UpdateTrackingData();
        }

        void OnDestroy()
        {
            _poseTask?.Dispose();
            _handTask?.Dispose();
            _faceTask?.Dispose();
        }

        void OnApplicationQuit()
        {
            _poseTask?.Dispose();
            _poseTask = null;
            _handTask?.Dispose();
            _handTask = null;
            _faceTask?.Dispose();
            _faceTask = null;
            MediaPipeInitializer.Shutdown();
        }

        private async UniTask InitializeAsync()
        {
            _deviceCamera = _deviceCameraComponent.GetComponent<IDeviceCamera>();
            if (_deviceCamera == null)
            {
                DebugLogger.LogError($"[{nameof(MotionCaptureTaskRunner)}] DeviceCamera component does not implement IDeviceCamera.");
                return;
            }

            _stopwatch = new Stopwatch();
            _poseData = new PoseData();
            _leftHandData = new HandData(Handedness.Left);
            _rightHandData = new HandData(Handedness.Right);
            _faceData = new FaceData();
            _trackingData = new TrackingData
            {
                Pose = _poseData,
                LeftHand = _leftHandData,
                RightHand = _rightHandData,
                Face = _faceData,
            };

            DebugLogger.Log($"[{nameof(MotionCaptureTaskRunner)}] Initializing MediaPipe...");
            await MediaPipeInitializer.InitializeAsync();

            DebugLogger.Log($"[{nameof(MotionCaptureTaskRunner)}] Preparing models: {_config.PoseModelName}, {_config.HandModelName}, {_config.FaceModelName}");
            await MediaPipeInitializer.PrepareModelAsync(_config.PoseModelName);
            await MediaPipeInitializer.PrepareModelAsync(_config.HandModelName);
            await MediaPipeInitializer.PrepareModelAsync(_config.FaceModelName);

            _poseTask = new PoseTrackingTask(
                numPoses: _config.NumPoses,
                minDetectionConfidence: _config.MinPoseDetectionConfidence,
                minPresenceConfidence: _config.MinPosePresenceConfidence,
                minTrackingConfidence: _config.MinPoseTrackingConfidence,
                outputSegmentationMasks: _config.OutputSegmentationMasks
            );
            _poseTask.Initialize(_config.PoseModelName, OnPoseResult);

            _handTask = new HandTrackingTask(
                numHands: _config.NumHands,
                minDetectionConfidence: _config.MinHandDetectionConfidence,
                minPresenceConfidence: _config.MinHandPresenceConfidence,
                minTrackingConfidence: _config.MinHandTrackingConfidence
            );
            _handTask.Initialize(_config.HandModelName, OnHandResult);

            _faceTask = new FaceTrackingTask(
                numFaces: _config.NumFaces,
                minDetectionConfidence: _config.MinFaceDetectionConfidence,
                minPresenceConfidence: _config.MinFacePresenceConfidence,
                minTrackingConfidence: _config.MinFaceTrackingConfidence
            );
            _faceTask.Initialize(_config.FaceModelName, OnFaceResult);

            if (_config.EnableSmoothing)
            {
                _smoother = new LandmarkSmoother(
                    _config.UsePerBodyPartParams,
                    _config.SmoothingMinCutoff,
                    _config.SmoothingBeta
                );
            }

            _stopwatch.Start();
            _isReady = true;

            DebugLogger.Log($"[{nameof(MotionCaptureTaskRunner)}] Ready. Starting pose, hand, and face tracking (LIVE_STREAM).");
        }

        private void UpdateTrackingData()
        {
            if (!_isReady) return;

            if (!_deviceCamera.TryAcquireMediaPipeImage(out var image, out int width, out int height))
            {
                return;
            }

            long timestampMs = _stopwatch.ElapsedMilliseconds;
            int rotationDegrees = GetScreenRotationDegrees();
            _poseTask.DetectAsync(image, timestampMs, rotationDegrees);

            if (_deviceCamera.TryCreateImageFromCurrentBuffer(out var handImage, out _, out _))
            {
                _handTask.DetectAsync(handImage, timestampMs, rotationDegrees);
            }

            if (_deviceCamera.TryCreateImageFromCurrentBuffer(out var faceImage, out _, out _))
            {
                _faceTask.DetectAsync(faceImage, timestampMs, rotationDegrees);
            }

            bool hasPose, hasLeftHand, hasRightHand, hasFace, hasSegMask;
            lock (_dataLock)
            {
                hasPose = _hasPendingPose;
                hasLeftHand = _hasPendingLeftHand;
                hasRightHand = _hasPendingRightHand;
                hasFace = _hasPendingFace;
                hasSegMask = _hasPendingSegmentationMask;

                _hasPendingPose = false;
                _hasPendingLeftHand = false;
                _hasPendingRightHand = false;
                _hasPendingFace = false;
                _hasPendingSegmentationMask = false;
            }

            _trackingData.HasPose = hasPose;
            _trackingData.HasLeftHand = hasLeftHand;
            _trackingData.HasRightHand = hasRightHand;
            _trackingData.HasFace = hasFace;
            _trackingData.HasSegmentationMask = hasSegMask;
            _trackingData.TimestampMs = timestampMs;

            if (_smoother != null)
            {
                float timestampSec = timestampMs * 0.001f;
                lock (_dataLock)
                {
                    if (hasPose) _smoother.SmoothPose(_poseData, timestampSec);
                    if (hasLeftHand) _smoother.SmoothHand(_leftHandData, timestampSec);
                    if (hasRightHand) _smoother.SmoothHand(_rightHandData, timestampSec);
                    if (hasFace) _smoother.SmoothFace(_faceData, timestampSec);
                }
            }

            if (hasPose || hasLeftHand || hasRightHand || hasFace)
            {
                OnTrackingDataUpdated?.Invoke(_trackingData);
            }
        }

        private void OnPoseResult(PoseLandmarkerResult poseResult)
        {
            lock (_dataLock)
            {
                if (poseResult.poseWorldLandmarks != null && poseResult.poseWorldLandmarks.Count > 0
                    && poseResult.poseLandmarks != null && poseResult.poseLandmarks.Count > 0)
                {
                    Converter.ConvertPoseResult(
                        poseResult.poseWorldLandmarks[0],
                        poseResult.poseLandmarks[0],
                        _poseData
                    );

                    Converter.ConvertNormalizedLandmarks(
                        poseResult.poseLandmarks[0],
                        _poseData
                    );

                    _hasPendingPose = true;

                    if (poseResult.segmentationMasks != null && poseResult.segmentationMasks.Count > 0)
                    {
                        var mask = poseResult.segmentationMasks[0];
                        var maskW = mask.Width();
                        var maskH = mask.Height();
                        EnsureSegmentationBuffer(maskW, maskH);
                        mask.TryReadChannelNormalized(0, _trackingData.SegmentationMask);
                        _trackingData.SegmentationMaskWidth = maskW;
                        _trackingData.SegmentationMaskHeight = maskH;
                        _hasPendingSegmentationMask = true;
                    }

                    if (_showDebugLog)
                    {
                        DebugLogger.Log($"[{nameof(MotionCaptureTaskRunner)}] Pose detected. Nose: {_poseData.WorldLandmarks[0]}");
                    }
                }
            }
        }

        private void OnHandResult(HandLandmarkerResult handResult)
        {
            lock (_dataLock)
            {
                if (handResult.handWorldLandmarks == null || handResult.handedness == null)
                {
                    return;
                }

                for (int i = 0; i < handResult.handedness.Count; i++)
                {
                    if (i >= handResult.handWorldLandmarks.Count) break;

                    var categories = handResult.handedness[i].categories;
                    if (categories == null || categories.Count == 0) continue;

                    string label = categories[0].categoryName;
                    bool isLeft = label == "Left";

                    Vector3 poseWristPosition = Vector3.zero;
                    if (_hasPendingPose)
                    {
                        int wristIndex = isLeft
                            ? Converter.PoseLeftWristIndex
                            : Converter.PoseRightWristIndex;
                        poseWristPosition = _poseData.WorldLandmarks[wristIndex];
                    }

                    if (isLeft)
                    {
                        Converter.ConvertHandResult(
                            handResult.handWorldLandmarks[i],
                            poseWristPosition,
                            _leftHandData
                        );
                        if (handResult.handLandmarks != null && i < handResult.handLandmarks.Count)
                        {
                            Converter.ConvertHandNormalizedLandmarks(
                                handResult.handLandmarks[i],
                                _leftHandData
                            );
                        }
                        _hasPendingLeftHand = true;
                    }
                    else
                    {
                        Converter.ConvertHandResult(
                            handResult.handWorldLandmarks[i],
                            poseWristPosition,
                            _rightHandData
                        );
                        if (handResult.handLandmarks != null && i < handResult.handLandmarks.Count)
                        {
                            Converter.ConvertHandNormalizedLandmarks(
                                handResult.handLandmarks[i],
                                _rightHandData
                            );
                        }
                        _hasPendingRightHand = true;
                    }
                }
            }
        }

        private void OnFaceResult(FaceLandmarkerResult faceResult)
        {
            lock (_dataLock)
            {
                if (faceResult.faceBlendshapes == null || faceResult.faceBlendshapes.Count == 0)
                {
                    return;
                }

                Converter.ConvertFaceBlendshapes(faceResult.faceBlendshapes[0], _faceData);

                if (faceResult.facialTransformationMatrixes != null && faceResult.facialTransformationMatrixes.Count > 0)
                {
                    Converter.ExtractHeadPose(faceResult.facialTransformationMatrixes[0], _faceData);
                }

                _hasPendingFace = true;

                if (_showDebugLog)
                {
                    DebugLogger.Log($"[{nameof(MotionCaptureTaskRunner)}] Face detected. HeadPos: {_faceData.HeadPosition}, HeadRot: {_faceData.HeadRotation.eulerAngles}");
                }
            }
        }

        /// <summary>
        /// Screen.orientation からセンサー画像に適用すべき回転角度 (度, 時計回り) を返す。
        /// XRCpuImage はセンサーネイティブの Landscape 画像を出力するため、
        /// Portrait 時は 90°、LandscapeRight 時は 180° 等の補正が必要。
        /// ※ 実機検証後に値を調整すること。
        /// </summary>
        private static int GetScreenRotationDegrees()
        {
            return Screen.orientation switch
            {
                ScreenOrientation.Portrait           =>  90,
                ScreenOrientation.LandscapeRight     => 180,
                ScreenOrientation.PortraitUpsideDown => 270,
                _                                    =>   0, // LandscapeLeft
            };
        }

        private void EnsureSegmentationBuffer(int width, int height)
        {
            var requiredSize = width * height;
            if (_trackingData.SegmentationMask == null || _trackingData.SegmentationMask.Length != requiredSize)
            {
                _trackingData.SegmentationMask = new float[requiredSize];
            }
        }
    }
}
