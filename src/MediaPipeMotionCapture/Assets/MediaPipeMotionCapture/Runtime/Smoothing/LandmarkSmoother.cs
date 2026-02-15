using System.Collections.Generic;
using UnityEngine;

namespace MediaPipeMotionCapture.Smoothing
{
    public class LandmarkSmoother
    {
        private const int PoseLandmarkCount = 33;
        private const int HandLandmarkCount = 21;
        private const int BlendshapeCount = FaceData.BlendshapeCount;
        private const int AxesPerLandmark = 3; // x, y, z

        // Pose ランドマーク分類
        // 体幹: 肩 (11,12), 腰 (23,24)
        private static readonly HashSet<int> TorsoIndices = new() { 11, 12, 23, 24 };
        // 顔: 0-10
        private static readonly HashSet<int> FaceIndices = new() { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        // 手先端: 17-22
        private static readonly HashSet<int> HandTipIndices = new() { 17, 18, 19, 20, 21, 22 };
        // 足先端: 29-32
        private static readonly HashSet<int> FootTipIndices = new() { 29, 30, 31, 32 };

        private readonly OneEuroFilter[] _poseFilters;       // [33 * 3]
        private readonly OneEuroFilter[] _leftHandFilters;   // [21 * 3]
        private readonly OneEuroFilter[] _rightHandFilters;  // [21 * 3]
        private readonly OneEuroFilter[] _blendshapeFilters; // [52]
        private readonly OneEuroFilter[] _headPosFilters;    // [3]
        private readonly OneEuroFilter[] _headRotFilters;    // [4]

        public LandmarkSmoother(bool usePerBodyPartParams, float defaultMinCutoff = 1.0f, float defaultBeta = 0.1f)
        {
            _poseFilters = new OneEuroFilter[PoseLandmarkCount * AxesPerLandmark];
            _leftHandFilters = new OneEuroFilter[HandLandmarkCount * AxesPerLandmark];
            _rightHandFilters = new OneEuroFilter[HandLandmarkCount * AxesPerLandmark];
            _blendshapeFilters = new OneEuroFilter[BlendshapeCount];
            _headPosFilters = new OneEuroFilter[3];
            _headRotFilters = new OneEuroFilter[4];

            if (usePerBodyPartParams)
            {
                InitializePoseFiltersPerBodyPart();
                InitializeHandFilters(0.8f, 0.5f);
                InitializeBlendshapeFilters(1.2f, 0.1f);
                InitializeHeadFilters(1.5f, 0.2f);
            }
            else
            {
                InitializePoseFiltersUniform(defaultMinCutoff, defaultBeta);
                InitializeHandFilters(defaultMinCutoff, defaultBeta);
                InitializeBlendshapeFilters(defaultMinCutoff, defaultBeta);
                InitializeHeadFilters(defaultMinCutoff, defaultBeta);
            }
        }

        public void SmoothPose(PoseData pose, float timestampSec)
        {
            for (int i = 0; i < PoseLandmarkCount; i++)
            {
                int baseIdx = i * AxesPerLandmark;
                var lm = pose.WorldLandmarks[i];
                pose.WorldLandmarks[i] = new Vector3(
                    _poseFilters[baseIdx + 0].Filter(lm.x, timestampSec),
                    _poseFilters[baseIdx + 1].Filter(lm.y, timestampSec),
                    _poseFilters[baseIdx + 2].Filter(lm.z, timestampSec)
                );
            }
        }

        public void SmoothHand(HandData hand, float timestampSec)
        {
            var filters = hand.Handedness == Handedness.Left ? _leftHandFilters : _rightHandFilters;
            for (int i = 0; i < HandLandmarkCount; i++)
            {
                int baseIdx = i * AxesPerLandmark;
                var lm = hand.Landmarks[i];
                hand.Landmarks[i] = new Vector3(
                    filters[baseIdx + 0].Filter(lm.x, timestampSec),
                    filters[baseIdx + 1].Filter(lm.y, timestampSec),
                    filters[baseIdx + 2].Filter(lm.z, timestampSec)
                );
            }
        }

        public void SmoothFace(FaceData face, float timestampSec)
        {
            // Blendshapes
            for (int i = 0; i < BlendshapeCount; i++)
            {
                face.Blendshapes[i] = _blendshapeFilters[i].Filter(face.Blendshapes[i], timestampSec);
            }

            // Head Position
            var hp = face.HeadPosition;
            face.HeadPosition = new Vector3(
                _headPosFilters[0].Filter(hp.x, timestampSec),
                _headPosFilters[1].Filter(hp.y, timestampSec),
                _headPosFilters[2].Filter(hp.z, timestampSec)
            );

            // Head Rotation (quaternion components)
            var hr = face.HeadRotation;
            float qx = _headRotFilters[0].Filter(hr.x, timestampSec);
            float qy = _headRotFilters[1].Filter(hr.y, timestampSec);
            float qz = _headRotFilters[2].Filter(hr.z, timestampSec);
            float qw = _headRotFilters[3].Filter(hr.w, timestampSec);
            // 正規化してクォータニオンの妥当性を維持
            face.HeadRotation = NormalizeQuaternion(new Quaternion(qx, qy, qz, qw));
        }

        public void Reset()
        {
            ResetFilters(_poseFilters);
            ResetFilters(_leftHandFilters);
            ResetFilters(_rightHandFilters);
            ResetFilters(_blendshapeFilters);
            ResetFilters(_headPosFilters);
            ResetFilters(_headRotFilters);
        }

        private void InitializePoseFiltersPerBodyPart()
        {
            for (int i = 0; i < PoseLandmarkCount; i++)
            {
                float minCutoff;
                float beta;

                if (TorsoIndices.Contains(i))
                {
                    // 体幹: 安定性重視
                    minCutoff = 1.5f;
                    beta = 0.0f;
                }
                else if (FaceIndices.Contains(i))
                {
                    // 顔ランドマーク: 安定性重視
                    minCutoff = 1.5f;
                    beta = 0.0f;
                }
                else if (HandTipIndices.Contains(i) || FootTipIndices.Contains(i))
                {
                    // 手先・足先: 追従性重視
                    minCutoff = 0.8f;
                    beta = 0.5f;
                }
                else
                {
                    // 四肢 (上腕・前腕・大腿・下腿): 適度な追従性
                    minCutoff = 1.0f;
                    beta = 0.3f;
                }

                int baseIdx = i * AxesPerLandmark;
                _poseFilters[baseIdx + 0] = new OneEuroFilter(minCutoff, beta);
                _poseFilters[baseIdx + 1] = new OneEuroFilter(minCutoff, beta);
                _poseFilters[baseIdx + 2] = new OneEuroFilter(minCutoff, beta);
            }
        }

        private void InitializePoseFiltersUniform(float minCutoff, float beta)
        {
            for (int i = 0; i < _poseFilters.Length; i++)
            {
                _poseFilters[i] = new OneEuroFilter(minCutoff, beta);
            }
        }

        private void InitializeHandFilters(float minCutoff, float beta)
        {
            for (int i = 0; i < _leftHandFilters.Length; i++)
            {
                _leftHandFilters[i] = new OneEuroFilter(minCutoff, beta);
            }
            for (int i = 0; i < _rightHandFilters.Length; i++)
            {
                _rightHandFilters[i] = new OneEuroFilter(minCutoff, beta);
            }
        }

        private void InitializeBlendshapeFilters(float minCutoff, float beta)
        {
            for (int i = 0; i < _blendshapeFilters.Length; i++)
            {
                _blendshapeFilters[i] = new OneEuroFilter(minCutoff, beta);
            }
        }

        private void InitializeHeadFilters(float minCutoff, float beta)
        {
            for (int i = 0; i < _headPosFilters.Length; i++)
            {
                _headPosFilters[i] = new OneEuroFilter(minCutoff, beta);
            }
            for (int i = 0; i < _headRotFilters.Length; i++)
            {
                _headRotFilters[i] = new OneEuroFilter(minCutoff, beta);
            }
        }

        private static void ResetFilters(OneEuroFilter[] filters)
        {
            for (int i = 0; i < filters.Length; i++)
            {
                filters[i].Reset();
            }
        }

        private static Quaternion NormalizeQuaternion(Quaternion q)
        {
            float magnitude = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (magnitude < 1e-6f) return Quaternion.identity;
            return new Quaternion(q.x / magnitude, q.y / magnitude, q.z / magnitude, q.w / magnitude);
        }
    }
}
