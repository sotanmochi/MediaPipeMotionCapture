using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniGLTF;
using UnityEngine;
using UniVRM10;

namespace MediaPipeMotionCapture.Samples
{
    public sealed class MotionActorSampleApp : MonoBehaviour
    {
        [SerializeField] private string _vrmModel = "VRM/Sample_Alpha_PerfectSync.vrm";
        [SerializeField] private MediaPipeMotionActor _motionActor;

        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly Dictionary<string, FaceExpressionKey> _blendshapeNameToKey = new();

        private GameObject _target;
        private HumanPoseHandler _targetPoseHandler;
        private HumanPose _humanPose;
        private FaceExpressionHandler _faceExpressionHandler;
        private FaceExpressionKey[] _indexToKeyMap;
        private bool _indexToKeyMapBuilt;

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
        }

        private async UniTaskVoid LoadVrmModelAsync()
        {
            var modelPath = Path.Combine(Application.streamingAssetsPath, _vrmModel);
            var vrmInstance = await Vrm10.LoadPathAsync(modelPath);

            var gltfInstance = vrmInstance.GetComponent<RuntimeGltfInstance>();
            gltfInstance.EnableUpdateWhenOffscreen();

            await UniTask.DelayFrame(1, cancellationToken: _cancellationTokenSource.Token); // NOTE: Wait for ControlRig to be applied.

            if (gltfInstance != null && gltfInstance.TryGetComponent<Animator>(out var animator))
            {
                _targetPoseHandler?.Dispose();
                _targetPoseHandler = null;

                UnityEngine.Object.Destroy(_target);
                _target = null;

                _targetPoseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
                _target = gltfInstance.gameObject;

                _faceExpressionHandler = new FaceExpressionHandler(gltfInstance.Root.transform);
            }
        }

        private void UpdateBlendshapes()
        {
            if (!_indexToKeyMapBuilt)
            {
                TryBuildIndexToKeyMap();
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

        private void TryBuildIndexToKeyMap()
        {
            var names = _motionActor.BlendshapeNames;
            if (names == null || names[0] == null) return;

            _indexToKeyMap = new FaceExpressionKey[FaceData.BlendshapeCount];
            for (var i = 0; i < FaceData.BlendshapeCount; i++)
            {
                _indexToKeyMap[i] = _blendshapeNameToKey.TryGetValue(names[i], out var key) ? key : (FaceExpressionKey)(-1);
            }
            _indexToKeyMapBuilt = true;
        }
    }
}
