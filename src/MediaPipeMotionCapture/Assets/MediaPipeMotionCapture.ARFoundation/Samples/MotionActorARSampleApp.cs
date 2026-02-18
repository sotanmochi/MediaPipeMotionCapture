using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MediaPipeMotionCapture.ARFoundation;
using UniGLTF;
using UnityEngine;
using UniVRM10;

namespace MediaPipeMotionCapture.Samples
{
    /// <summary>
    /// AR カメラ環境向けの MotionActor サンプル。
    /// MotionActorSampleApp の機能に加え、ARDepthBodyAnchorTracker のワールド位置で
    /// VRM モデルを実空間に配置する。
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class MotionActorARSampleApp : MonoBehaviour
    {
        [SerializeField] private string _vrmModel = "VRM/Sample_Alpha_PerfectSync.vrm";
        [SerializeField] private MediaPipeMotionActor _motionActor;

        [Header("AR Positioning")]
        [SerializeField] private ARDepthBodyAnchorTracker _bodyAnchorTracker;

        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly Dictionary<string, FaceExpressionKey> _blendshapeNameToKey = new();

        private GameObject _target;
        private Animator _targetAnimator;
        private HumanPoseHandler _targetPoseHandler;
        private HumanPose _humanPose;
        private FaceExpressionHandler _faceExpressionHandler;
        private FaceExpressionKey[] _indexToKeyMap;

        void Start()
        {
            foreach (var key in Enum.GetValues(typeof(FaceExpressionKey)).Cast<FaceExpressionKey>())
            {
                _blendshapeNameToKey[key.ToString()] = key;
            }
            LoadVrmModelAsync().Forget();
        }

        void OnDestroy()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _targetPoseHandler?.Dispose();
            _targetPoseHandler = null;
            _faceExpressionHandler = null;
        }

        void LateUpdate()
        {
            if (_motionActor == null || _targetPoseHandler == null)
            {
                return;
            }

            if (_motionActor.TryGetHumanPose(ref _humanPose))
            {
                _targetPoseHandler.SetHumanPose(ref _humanPose);
            }

            UpdateBlendshapes();
            ApplyBodyAnchorPosition();
        }

        private async UniTaskVoid LoadVrmModelAsync()
        {
            var modelPath = Path.Combine(Application.streamingAssetsPath, _vrmModel);
            var vrmInstance = await Vrm10.LoadPathAsync(modelPath);

            var gltfInstance = vrmInstance.GetComponent<RuntimeGltfInstance>();
            gltfInstance.EnableUpdateWhenOffscreen();

            await UniTask.DelayFrame(1, cancellationToken: _cancellationTokenSource.Token);

            if (gltfInstance != null && gltfInstance.TryGetComponent<Animator>(out var animator))
            {
                _targetPoseHandler?.Dispose();
                _targetPoseHandler = null;

                UnityEngine.Object.Destroy(_target);
                _target = null;

                _targetPoseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
                _target = gltfInstance.gameObject;
                _targetAnimator = animator;

                _faceExpressionHandler = new FaceExpressionHandler(gltfInstance.Root.transform);
            }
        }

        private void ApplyBodyAnchorPosition()
        {
            if (_bodyAnchorTracker == null || !_bodyAnchorTracker.IsTracking)
            {
                return;
            }

            if (_target == null || _targetAnimator == null)
            {
                return;
            }

            var referenceBone = GetReferenceBone(_targetAnimator, _bodyAnchorTracker.ActiveAnchorType);
            if (referenceBone == null)
            {
                return;
            }

            var offset = _bodyAnchorTracker.AnchorWorldPosition - referenceBone.position;
            _target.transform.position += offset;
        }

        private static Transform GetReferenceBone(Animator animator, BodyAnchorType anchorType)
        {
            return anchorType switch
            {
                BodyAnchorType.Hips => animator.GetBoneTransform(HumanBodyBones.Hips),
                BodyAnchorType.Shoulder => animator.GetBoneTransform(HumanBodyBones.UpperChest)
                                       ?? animator.GetBoneTransform(HumanBodyBones.Chest),
                BodyAnchorType.Head => animator.GetBoneTransform(HumanBodyBones.Head),
                _ => null,
            };
        }

        private void UpdateBlendshapes()
        {
            if (_indexToKeyMap == null && !TryBuildIndexToKeyMap())
            {
                return;
            }

            if (_faceExpressionHandler == null)
            {
                return;
            }

            var blendshapes = _motionActor.Blendshapes;
            if (blendshapes == null)
            {
                return;
            }

            for (var i = 0; i < blendshapes.Length && i < FaceData.BlendshapeCount; i++)
            {
                var key = _indexToKeyMap[i];
                if (key != (FaceExpressionKey)(-1))
                {
                    _faceExpressionHandler.SetWeight(key, blendshapes[i]);
                }
            }
        }

        private bool TryBuildIndexToKeyMap()
        {
            var names = _motionActor.BlendshapeNames;
            if (names == null || names[0] == null) return false;

            _indexToKeyMap = new FaceExpressionKey[FaceData.BlendshapeCount];
            for (var i = 0; i < FaceData.BlendshapeCount; i++)
            {
                _indexToKeyMap[i] = _blendshapeNameToKey.TryGetValue(names[i], out var key) ? key : (FaceExpressionKey)(-1);
            }
            return true;
        }
    }
}
