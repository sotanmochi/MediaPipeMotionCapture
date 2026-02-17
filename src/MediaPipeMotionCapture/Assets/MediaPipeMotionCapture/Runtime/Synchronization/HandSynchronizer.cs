using System.Collections.Generic;
using UnityEngine;

namespace MediaPipeMotionCapture
{
    /// <summary>
    /// MediaPipe HandData (21 ランドマーク) から指関節と手首のボーン回転を計算し適用する。
    /// PoseApplicator と同じ親ローカル空間での FromToRotation アルゴリズムを使用する。
    /// </summary>
    public class HandSynchronizer
    {
        private Dictionary<HumanBodyBones, Transform> _boneMap;
        private Dictionary<HumanBodyBones, Vector3> _tposeDirections;
        private Dictionary<HumanBodyBones, Quaternion> _tposeLocalRotations;

        private LandmarkBoneMapping[] _leftFingerMappings;
        private LandmarkBoneMapping[] _rightFingerMappings;

        // 手首の T-Pose 状態
        private Quaternion _leftWristTposeLocalRot;
        private Quaternion _rightWristTposeLocalRot;
        private Vector3 _leftWristTposeForward;
        private Vector3 _leftWristTposeUp;
        private Vector3 _rightWristTposeForward;
        private Vector3 _rightWristTposeUp;

        public void Initialize(Dictionary<HumanBodyBones, Transform> boneMap)
        {
            _boneMap = boneMap;
            _leftFingerMappings = HandBoneMappingTable.LeftFingers;
            _rightFingerMappings = HandBoneMappingTable.RightFingers;

            RecordTPoseState();
        }

        private void RecordTPoseState()
        {
            _tposeDirections = new Dictionary<HumanBodyBones, Vector3>();
            _tposeLocalRotations = new Dictionary<HumanBodyBones, Quaternion>();

            RecordFingerTPose(_leftFingerMappings);
            RecordFingerTPose(_rightFingerMappings);
            RecordWristTPose(HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftIndexProximal, isLeft: true);
            RecordWristTPose(HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightLittleProximal, HumanBodyBones.RightIndexProximal, isLeft: false);
        }

        private void RecordFingerTPose(LandmarkBoneMapping[] mappings)
        {
            foreach (var mapping in mappings)
            {
                if (!_boneMap.TryGetValue(mapping.Bone, out var boneTransform))
                    continue;

                if (boneTransform.childCount > 0)
                {
                    var childPos = boneTransform.GetChild(0).position;
                    var dirWorld = (childPos - boneTransform.position).normalized;
                    if (dirWorld.sqrMagnitude > 0.001f)
                    {
                        var parentRot = boneTransform.parent != null ? boneTransform.parent.rotation : Quaternion.identity;
                        _tposeDirections[mapping.Bone] = Quaternion.Inverse(parentRot) * dirWorld;
                    }
                }

                _tposeLocalRotations[mapping.Bone] = boneTransform.localRotation;
            }
        }

        /// <summary>
        /// 手首の T-Pose 状態を記録する。
        /// MiddleProximal の方向を forward、Index と Little の外積を palm normal (up) として記録する。
        /// </summary>
        private void RecordWristTPose(
            HumanBodyBones handBone, HumanBodyBones middleBone,
            HumanBodyBones littleBone, HumanBodyBones indexBone, bool isLeft)
        {
            if (!_boneMap.TryGetValue(handBone, out var handTransform)) return;
            if (!_boneMap.TryGetValue(middleBone, out var middleTransform)) return;
            if (!_boneMap.TryGetValue(littleBone, out var littleTransform)) return;
            if (!_boneMap.TryGetValue(indexBone, out var indexTransform)) return;

            var parentRot = handTransform.parent != null ? handTransform.parent.rotation : Quaternion.identity;
            var invParent = Quaternion.Inverse(parentRot);
            var handPos = handTransform.position;

            var forwardWorld = (middleTransform.position - handPos).normalized;
            var toLittle = (littleTransform.position - handPos).normalized;
            var toIndex = (indexTransform.position - handPos).normalized;
            // 左手: cross(little, index) → 上向き、右手: cross(index, little) → 上向き
            var palmUpWorld = isLeft
                ? Vector3.Cross(toLittle, toIndex).normalized
                : Vector3.Cross(toIndex, toLittle).normalized;

            if (isLeft)
            {
                _leftWristTposeForward = invParent * forwardWorld;
                _leftWristTposeUp = invParent * palmUpWorld;
                _leftWristTposeLocalRot = handTransform.localRotation;
            }
            else
            {
                _rightWristTposeForward = invParent * forwardWorld;
                _rightWristTposeUp = invParent * palmUpWorld;
                _rightWristTposeLocalRot = handTransform.localRotation;
            }
        }

        /// <summary>
        /// HandData からスケルトンの手首 + 指関節 Transform を更新する。
        /// PoseApplicator の ApplyLimbBones の後に呼ぶこと。
        /// </summary>
        public void Apply(HandData handData)
        {
            if (handData == null) return;

            bool isLeft = handData.Handedness == Handedness.Left;
            var landmarks = handData.Landmarks;
            var fingerMappings = isLeft ? _leftFingerMappings : _rightFingerMappings;
            var handBone = isLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;

            ApplyWrist(handBone, landmarks, isLeft);
            ApplyFingers(fingerMappings, landmarks);
        }

        private void ApplyWrist(HumanBodyBones handBone, Vector3[] landmarks, bool isLeft)
        {
            if (!_boneMap.TryGetValue(handBone, out var handTransform))
                return;

            var wristPos = landmarks[HandBoneMappingTable.Wrist];
            var middleMCP = landmarks[HandBoneMappingTable.MiddleMCP];
            var pinkyMCP = landmarks[HandBoneMappingTable.PinkyMCP];
            var indexMCP = landmarks[HandBoneMappingTable.IndexMCP];

            var forwardWorld = (middleMCP - wristPos).normalized;
            var toPinky = (pinkyMCP - wristPos).normalized;
            var toIndex = (indexMCP - wristPos).normalized;
            var palmUpWorld = isLeft
                ? Vector3.Cross(toPinky, toIndex).normalized
                : Vector3.Cross(toIndex, toPinky).normalized;

            if (forwardWorld.sqrMagnitude < 0.001f || palmUpWorld.sqrMagnitude < 0.001f) return;

            // 親ボーンのローカル空間に変換
            var parentRot = handTransform.parent != null ? handTransform.parent.rotation : Quaternion.identity;
            var invParent = Quaternion.Inverse(parentRot);
            var forwardInParent = invParent * forwardWorld;
            var upInParent = invParent * palmUpWorld;

            // T-Pose の向き → 現在の向きへの回転差分 (親ローカル空間)
            var tposeForward = isLeft ? _leftWristTposeForward : _rightWristTposeForward;
            var tposeUp = isLeft ? _leftWristTposeUp : _rightWristTposeUp;
            var tposeLocalRot = isLeft ? _leftWristTposeLocalRot : _rightWristTposeLocalRot;

            var tposeRot = Quaternion.LookRotation(tposeForward, tposeUp);
            var currentRot = Quaternion.LookRotation(forwardInParent, upInParent);
            var deltaRotation = currentRot * Quaternion.Inverse(tposeRot);

            handTransform.localRotation = deltaRotation * tposeLocalRot;
        }

        private void ApplyFingers(LandmarkBoneMapping[] mappings, Vector3[] landmarks)
        {
            foreach (var mapping in mappings)
            {
                if (!_boneMap.TryGetValue(mapping.Bone, out var boneTransform))
                    continue;

                if (!_tposeDirections.TryGetValue(mapping.Bone, out var tposeDir))
                    continue;

                if (!_tposeLocalRotations.TryGetValue(mapping.Bone, out var tposeLocalRot))
                    continue;

                var currentDir = (landmarks[mapping.ChildLandmarkIndex] - landmarks[mapping.ParentLandmarkIndex]).normalized;
                if (currentDir.sqrMagnitude < 0.001f) continue;

                var parentRotation = boneTransform.parent != null ? boneTransform.parent.rotation : Quaternion.identity;
                var currentDirInParentSpace = Quaternion.Inverse(parentRotation) * currentDir;

                var deltaRotation = Quaternion.FromToRotation(tposeDir, currentDirInParentSpace);
                boneTransform.localRotation = deltaRotation * tposeLocalRot;
            }
        }
    }
}
