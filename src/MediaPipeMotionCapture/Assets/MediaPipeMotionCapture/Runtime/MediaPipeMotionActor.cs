using System.Collections.Generic;
using UnityEngine;

namespace MediaPipeMotionCapture
{
    public class MediaPipeMotionActor : MonoBehaviour
    {
        [Header("Tracking")]
        [SerializeField] private MotionCaptureTaskRunner _motionCapturaTaskRunner;

        [Header("Settings")]
        [SerializeField] private bool _showDebugLog;
        [SerializeField] private bool _showDebugSkeleton;

        private readonly float[] _blendshapes = new float[FaceData.BlendshapeCount];
        private readonly string[] _blendshapeNames = new string[FaceData.BlendshapeCount];

        private bool _initialized;

        private PoseSynchronizer _poseSynchronizer;
        private HandSynchronizer _handSynchronizer;

        private TrackingData _latestData;
        private bool _hasNewData;

        private Transform _headBone;
        private Transform _neckBone;
        private MediaPipeSkeletonBuilder.BuildResult _skeleton;
        private HumanPoseHandler _humanPoseHandler;
        private HumanPose _humanPose;

        public float[] Blendshapes => _blendshapes;
        public string[] BlendshapeNames => _blendshapeNames;

        void Awake()
        {
            Initialize();
        }

        void OnDestroy()
        {
            _humanPoseHandler?.Dispose();
            _humanPoseHandler = null;

            if (_skeleton?.SkeletonRoot != null)
            {
                Destroy(_skeleton.SkeletonRoot);
            }

            if (_skeleton?.Avatar != null)
            {
                Destroy(_skeleton.Avatar);
            }
        }

        void OnEnable()
        {
            if (_motionCapturaTaskRunner != null)
            {
                _motionCapturaTaskRunner.OnTrackingDataUpdated += OnTrackingDataUpdated;
            }
        }

        void OnDisable()
        {
            if (_motionCapturaTaskRunner != null)
            {
                _motionCapturaTaskRunner.OnTrackingDataUpdated -= OnTrackingDataUpdated;
            }
        }

        void LateUpdate()
        {
            UpdatePose();
        }
        
        public bool TryGetHumanPose(ref HumanPose humanPose)
        {
            if (_humanPoseHandler == null)
            {
                humanPose = default;
                return false;
            }

            _humanPoseHandler.GetHumanPose(ref humanPose);
            return true;
        }

        private void Initialize()
        {
            if (_initialized) return;

            _skeleton = MediaPipeSkeletonBuilder.Build();
            if (_skeleton == null)
            {
                DebugLogger.LogError("[MediaPipeMotionActor] Failed to build MediaPipe skeleton.");
                return;
            }

            _skeleton.SkeletonRoot.transform.SetParent(transform, false);

            _humanPoseHandler = new HumanPoseHandler(_skeleton.Avatar, _skeleton.SkeletonRoot.transform);

            _poseSynchronizer = new PoseSynchronizer();
            _poseSynchronizer.Initialize(_skeleton.BoneMap);

            _handSynchronizer = new HandSynchronizer();
            _handSynchronizer.Initialize(_skeleton.BoneMap);

            _headBone = _skeleton.BoneMap.GetValueOrDefault(HumanBodyBones.Head);
            _neckBone = _skeleton.BoneMap.GetValueOrDefault(HumanBodyBones.Neck);

            if (_showDebugSkeleton)
            {
                _skeleton.SkeletonRoot.AddComponent<SkeletonDebugVisualizer>();
            }

            _initialized = true;

            DebugLogger.Log($"[MediaPipeMotionActor] Initialized. Skeleton bones: {_skeleton.BoneMap.Count}");
        }

        private void OnTrackingDataUpdated(TrackingData data)
        {
            if (!_initialized) return;
            _latestData = data;
            _hasNewData = true;
        }

        private void UpdatePose()
        {
            if (!_hasNewData || !_initialized) return;
            _hasNewData = false;

            if (_latestData.HasPose)
            {
                _poseSynchronizer.Apply(_latestData.Pose);
            }

            if (_latestData.HasLeftHand)
            {
                _handSynchronizer.Apply(_latestData.LeftHand);
            }
            if (_latestData.HasRightHand)
            {
                _handSynchronizer.Apply(_latestData.RightHand);
            }

            if (_latestData.HasFace)
            {
                // 親ボーン (Neck) の回転を考慮してローカル回転に変換
                if (_neckBone != null)
                {
                    _headBone.localRotation = Quaternion.Inverse(_neckBone.rotation) * _latestData.Face.HeadRotation;
                }
                else
                {
                    _headBone.rotation = _latestData.Face.HeadRotation;
                }

                // Blendshapes
                for (var i = 0; i < FaceData.BlendshapeCount; i++)
                {
                    _blendshapes[i] = _latestData.Face.Blendshapes[i] * 100f; // [0,1] → [0,100]
                    _blendshapeNames[i] = _latestData.Face.BlendshapeNames[i];
                }
            }

            _humanPoseHandler.GetHumanPose(ref _humanPose);
        }
    }
}
