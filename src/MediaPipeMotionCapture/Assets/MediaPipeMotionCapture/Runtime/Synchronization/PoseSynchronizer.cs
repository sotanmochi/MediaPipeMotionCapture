using System.Collections.Generic;
using UnityEngine;

namespace MediaPipeMotionCapture
{
    /// <summary>
    /// MediaPipe PoseData のランドマークから仮想スケルトンのボーン回転を計算し適用する。
    /// T-Pose での各ボーン方向を基準に、ランドマーク間のベクトルから回転差分を計算する。
    /// </summary>
    public class PoseSynchronizer
    {
        private const float DefaultVisibilityThreshold = 0.5f;

        private Dictionary<HumanBodyBones, Transform> _boneMap;
        private Dictionary<HumanBodyBones, Vector3> _tposeDirections;
        private Dictionary<HumanBodyBones, Quaternion> _tposeLocalRotations;
        private Transform _hips;
        private float _visibilityThreshold;

        private LandmarkBoneMapping[] _activeMappings;

        public void Initialize(
            Dictionary<HumanBodyBones, Transform> boneMap,
            float visibilityThreshold = DefaultVisibilityThreshold)
        {
            _boneMap = boneMap;
            _visibilityThreshold = visibilityThreshold;

            _hips = boneMap[HumanBodyBones.Hips];

            // 上半身 + 下半身のマッピングを結合
            // NOTE: 親ボーン → 子ボーンの順序であること (例: UpperArm → LowerArm)。
            // ApplyLimbBones で親ボーンのワールド回転を基準に子ボーンの回転を計算するため、
            // 親が先に更新されている必要がある。
            var mappings = new List<LandmarkBoneMapping>();
            mappings.AddRange(BoneMappingTable.UpperBody);
            mappings.AddRange(BoneMappingTable.LowerBody);
            _activeMappings = mappings.ToArray();

            RecordTPoseState();
        }

        /// <summary>
        /// T-Pose でのボーン方向とローカル回転を記録する。
        /// Initialize 時に1回呼ばれる。
        /// </summary>
        private void RecordTPoseState()
        {
            _tposeDirections = new Dictionary<HumanBodyBones, Vector3>();
            _tposeLocalRotations = new Dictionary<HumanBodyBones, Quaternion>();

            foreach (var mapping in _activeMappings)
            {
                if (!_boneMap.TryGetValue(mapping.Bone, out var boneTransform))
                    continue;

                // T-Pose でのボーン方向を親ボーンのローカル空間で記録する。
                // 親ボーンのローカル空間で保持することで、ランタイムに親の回転変化を自然に反映できる。
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
        /// PoseData からスケルトンの各ボーン Transform を更新する。
        /// </summary>
        public void Apply(PoseData poseData)
        {
            if (poseData == null) return;

            var worldLandmarks = poseData.WorldLandmarks;
            var visibility = poseData.Visibility;

            ApplyHips(poseData);

            ApplyLimbBones(worldLandmarks, visibility);
        }

        private void ApplyHips(PoseData poseData)
        {
            var worldLandmarks = poseData.WorldLandmarks;

            var leftHip = worldLandmarks[BoneMappingTable.LeftHip];
            var rightHip = worldLandmarks[BoneMappingTable.RightHip];
            var hipsCenter = (leftHip + rightHip) * 0.5f;

            // Hips のワールド位置を設定（カメラ空間基準の推定位置を使用）
            _hips.position = poseData.HipWorldPosition;

            // Hips の回転: 脚左右方向 + Spine 方向から計算
            var leftShoulder = worldLandmarks[BoneMappingTable.LeftShoulder];
            var rightShoulder = worldLandmarks[BoneMappingTable.RightShoulder];
            var shoulderCenter = (leftShoulder + rightShoulder) * 0.5f;

            var upDir = (shoulderCenter - hipsCenter).normalized;
            var rightDir = (rightHip - leftHip).normalized;
            var forwardDir = Vector3.Cross(rightDir, upDir).normalized;

            if (forwardDir.sqrMagnitude > 0.001f && upDir.sqrMagnitude > 0.001f)
            {
                _hips.rotation = Quaternion.LookRotation(forwardDir, upDir);
            }
        }

        private void ApplyLimbBones(Vector3[] worldLandmarks, float[] visibility)
        {
            foreach (var mapping in _activeMappings)
            {
                // Visibility チェック
                if (visibility[mapping.ParentLandmarkIndex] < _visibilityThreshold ||
                    visibility[mapping.ChildLandmarkIndex] < _visibilityThreshold)
                {
                    continue;
                }

                if (!_boneMap.TryGetValue(mapping.Bone, out var boneTransform))
                    continue;

                if (!_tposeDirections.TryGetValue(mapping.Bone, out var tposeDir))
                    continue;

                if (!_tposeLocalRotations.TryGetValue(mapping.Bone, out var tposeLocalRot))
                    continue;

                // 現在のランドマーク方向 (ワールド空間)
                var currentDir = (worldLandmarks[mapping.ChildLandmarkIndex] - worldLandmarks[mapping.ParentLandmarkIndex]).normalized;
                if (currentDir.sqrMagnitude < 0.001f) continue;

                // 親ボーンのローカル空間で回転差分を計算する。
                // tposeDir は親ボーンのローカル空間で記録されているので、
                // currentDir も親ボーンのローカル空間に変換して比較する。
                // 親ボーンが既にこのフレームで更新済みであれば、子ボーンも正しく計算される。
                var parentRotation = boneTransform.parent != null ? boneTransform.parent.rotation : Quaternion.identity;
                var currentDirInParentSpace = Quaternion.Inverse(parentRotation) * currentDir;

                var deltaRotation = Quaternion.FromToRotation(tposeDir, currentDirInParentSpace);

                boneTransform.localRotation = deltaRotation * tposeLocalRot;
            }
        }
    }
}
