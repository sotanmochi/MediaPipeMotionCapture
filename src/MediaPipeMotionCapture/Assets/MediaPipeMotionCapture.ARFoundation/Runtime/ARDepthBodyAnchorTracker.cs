using System.Collections.Generic;
using UnityEngine;

namespace MediaPipeMotionCapture.ARFoundation
{
    /// <summary>
    /// 複数の主要関節位置をデプスマップで3D化し、
    /// カメラに映っている状況に応じて最適なアンカーを自動選択するコンポーネント。
    /// </summary>
    public class ARDepthBodyAnchorTracker : MonoBehaviour
    {
        private const int AnchorCount = 3; // Head, Shoulder, Hips

        [Header("References")]
        [SerializeField] private MotionCaptureTaskRunner _motionCaptureTaskRunner;
        [SerializeField] private DeviceCameraBase _deviceCamera;

        [Header("Settings")]
        [SerializeField] private float _minVisibility = 0.5f;

        [Header("Anchor Selection")]
        [SerializeField] private float _anchorSwitchHysteresis = 0.3f;

        [Header("Debug Log")]
        [SerializeField] private bool _showDebugLog;

        /// <summary>
        /// 現在選択されているアンカーのワールド座標。
        /// </summary>
        public Vector3 AnchorWorldPosition { get; private set; }

        /// <summary>
        /// 現在選択されているアンカーの種別。
        /// </summary>
        public BodyAnchorType ActiveAnchorType { get; private set; }

        /// <summary>
        /// トラッキングが有効かどうか。
        /// </summary>
        public bool IsTracking { get; private set; }

        /// <summary>
        /// 各関節の個別トラッキング結果。
        /// </summary>
        public IReadOnlyList<BodyAnchorResult> AnchorResults => _anchorResults;

        // トラッキングデータ（イベントで受信）
        private bool _hasPose;
        private bool _hasFace;
        private PoseData _poseSnapshot;
        private Vector3 _headPosition;

        // アンカー結果
        private readonly BodyAnchorResult[] _anchorResults = new BodyAnchorResult[AnchorCount];

        // ヒステリシス
        private BodyAnchorType _currentAnchor = BodyAnchorType.None;
        private float _lastAnchorSwitchTime;

        void OnEnable()
        {
            if (_motionCaptureTaskRunner != null)
            {
                _motionCaptureTaskRunner.OnTrackingDataUpdated += OnTrackingDataUpdated;
            }
        }

        void OnDisable()
        {
            if (_motionCaptureTaskRunner != null)
            {
                _motionCaptureTaskRunner.OnTrackingDataUpdated -= OnTrackingDataUpdated;
            }
        }

        private void OnTrackingDataUpdated(TrackingData data)
        {
            _hasPose = data.HasPose;
            _hasFace = data.HasFace;

            if (data.HasPose)
            {
                _poseSnapshot = data.Pose;
            }

            if (data.HasFace)
            {
                _headPosition = data.Face.HeadPosition;
            }
        }

        void LateUpdate()
        {
            var camera = _deviceCamera;

            if (camera.IsDepthAvailable && _hasPose)
            {
                ComputeAllAnchors(camera);
                var bestAnchor = SelectBestAnchor();
                bestAnchor = ApplyHysteresis(bestAnchor);
                UpdateResult(bestAnchor);
            }
            else if (_hasFace)
            {
                AnchorWorldPosition = camera.Position + camera.Rotation * _headPosition;
                ActiveAnchorType = BodyAnchorType.Head;
                IsTracking = true;

                if (_showDebugLog)
                {
                    DebugLogger.Log($"[{nameof(ARDepthBodyAnchorTracker)}] Face fallback Pos={AnchorWorldPosition:F3}");
                }
            }
            else
            {
                SetTrackingLost();
            }
        }

        private void ComputeAllAnchors(IDeviceCamera camera)
        {
            var pose = _poseSnapshot;
            TryComputeHead(pose, camera, out _anchorResults[0]);
            TryComputeShoulderMidpoint(pose, camera, out _anchorResults[1]);
            TryComputeHipsMidpoint(pose, camera, out _anchorResults[2]);
        }

        private bool TryComputeHead(PoseData pose, IDeviceCamera camera, out BodyAnchorResult result)
        {
            result = default;
            var visibility = pose.Visibility[0];
            if (visibility < _minVisibility) return false;

            var noseNorm = pose.NormalizedLandmarks[0];
            if (!camera.TryNormalizedToWorldPoint(noseNorm.x, noseNorm.y, out var worldPos))
            {
                return false;
            }

            result = new BodyAnchorResult
            {
                AnchorType = BodyAnchorType.Head,
                WorldPosition = worldPos,
                IsValid = true,
                Confidence = visibility,
            };
            return true;
        }

        private bool TryComputeShoulderMidpoint(PoseData pose, IDeviceCamera camera, out BodyAnchorResult result)
        {
            return TryComputePairMidpoint(
                pose, camera,
                Converter.PoseLeftShoulderIndex,
                Converter.PoseRightShoulderIndex,
                BodyAnchorType.Shoulder,
                out result);
        }

        private bool TryComputeHipsMidpoint(PoseData pose, IDeviceCamera camera, out BodyAnchorResult result)
        {
            return TryComputePairMidpoint(
                pose, camera,
                Converter.PoseLeftHipIndex,
                Converter.PoseRightHipIndex,
                BodyAnchorType.Hips,
                out result);
        }

        private bool TryComputePairMidpoint(PoseData pose, IDeviceCamera camera, int leftIndex, int rightIndex, 
            BodyAnchorType anchorType, out BodyAnchorResult result)
        {
            result = default;
            var visL = pose.Visibility[leftIndex];
            var visR = pose.Visibility[rightIndex];
            var hasLeft = visL >= _minVisibility;
            var hasRight = visR >= _minVisibility;

            if (!hasLeft && !hasRight) return false;

            Vector3 worldPos;

            if (hasLeft && hasRight)
            {
                var leftNorm = pose.NormalizedLandmarks[leftIndex];
                var rightNorm = pose.NormalizedLandmarks[rightIndex];
                if (!camera.TryNormalizedToWorldPoint(leftNorm.x, leftNorm.y, out var worldL)) return false;
                if (!camera.TryNormalizedToWorldPoint(rightNorm.x, rightNorm.y, out var worldR)) return false;
                worldPos = (worldL + worldR) * 0.5f;
            }
            else
            {
                int idx = hasLeft ? leftIndex : rightIndex;
                var norm = pose.NormalizedLandmarks[idx];
                if (!camera.TryNormalizedToWorldPoint(norm.x, norm.y, out worldPos)) return false;
            }

            result = new BodyAnchorResult
            {
                AnchorType = anchorType,
                WorldPosition = worldPos,
                IsValid = true,
                Confidence = hasLeft && hasRight ? Mathf.Min(visL, visR) : (hasLeft ? visL : visR),
            };
            return true;
        }

        private BodyAnchorType SelectBestAnchor()
        {
            if (GetResult(BodyAnchorType.Hips).IsValid)
                return BodyAnchorType.Hips;

            if (GetResult(BodyAnchorType.Shoulder).IsValid)
                return BodyAnchorType.Shoulder;

            if (GetResult(BodyAnchorType.Head).IsValid)
                return BodyAnchorType.Head;

            return BodyAnchorType.None;
        }

        private BodyAnchorType ApplyHysteresis(BodyAnchorType bestAnchor)
        {
            if (bestAnchor == _currentAnchor)
                return _currentAnchor;

            // より優先度の高いアンカーが利用可能になった場合は即座に切り替え
            if (bestAnchor > _currentAnchor)
            {
                _currentAnchor = bestAnchor;
                _lastAnchorSwitchTime = Time.time;
                return _currentAnchor;
            }

            // 現在のアンカーがまだ有効かつヒステリシス期間内なら維持
            if (_currentAnchor != BodyAnchorType.None
                && GetResult(_currentAnchor).IsValid
                && Time.time - _lastAnchorSwitchTime < _anchorSwitchHysteresis)
            {
                return _currentAnchor;
            }

            // 優先度の低いアンカーへフォールバック
            _currentAnchor = bestAnchor;
            _lastAnchorSwitchTime = Time.time;
            return _currentAnchor;
        }

        private BodyAnchorResult GetResult(BodyAnchorType anchorType)
        {
            return anchorType switch
            {
                BodyAnchorType.Head => _anchorResults[0],
                BodyAnchorType.Shoulder => _anchorResults[1],
                BodyAnchorType.Hips => _anchorResults[2],
                _ => default,
            };
        }

        private void UpdateResult(BodyAnchorType anchorType)
        {
            if (anchorType == BodyAnchorType.None)
            {
                SetTrackingLost();
                return;
            }

            var result = GetResult(anchorType);
            AnchorWorldPosition = result.WorldPosition;
            ActiveAnchorType = anchorType;
            IsTracking = true;

            if (_showDebugLog)
            {
                DebugLogger.Log(
                    $"[{nameof(ARDepthBodyAnchorTracker)}] Anchor={anchorType}, " +
                    $"Pos={result.WorldPosition:F3}, Confidence={result.Confidence:F2}");
            }
        }

        private void SetTrackingLost()
        {
            IsTracking = false;
            ActiveAnchorType = BodyAnchorType.None;
            _currentAnchor = BodyAnchorType.None;
        }
    }
}
