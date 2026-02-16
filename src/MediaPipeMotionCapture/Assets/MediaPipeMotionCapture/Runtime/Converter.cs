using Mediapipe.Tasks.Components.Containers;
using UnityEngine;

namespace MediaPipeMotionCapture
{
    public static class Converter
    {
        public const int PoseLandmarkCount = 33;
        public const int HandLandmarkCount = 21;
        public const int PoseLeftWristIndex = 15;
        public const int PoseRightWristIndex = 16;
        public const int PoseLeftShoulderIndex = 11;
        public const int PoseRightShoulderIndex = 12;
        public const int PoseLeftHipIndex = 23;
        public const int PoseRightHipIndex = 24;

        /// <summary>
        /// PoseLandmarkerResult の World Landmarks を Unity 座標に変換し、PoseData に書き込む。
        /// visibility は NormalizedLandmarks から取得する。
        /// </summary>
        public static void ConvertPoseResult(
            Landmarks worldLandmarks,
            NormalizedLandmarks normalizedLandmarks,
            PoseData output)
        {
            var count = Mathf.Min(worldLandmarks.landmarks.Count, PoseLandmarkCount);
            for (var i = 0; i < count; i++)
            {
                var lm = worldLandmarks.landmarks[i];
                output.WorldLandmarks[i] = new Vector3(lm.x, -lm.y, lm.z);
                output.Visibility[i] = normalizedLandmarks.landmarks[i].visibility ?? 0f;
            }
        }

        /// <summary>
        /// HandLandmarkerResult の World Landmarks を Unity 座標に変換し、
        /// Pose の手首ワールド座標をアンカーとして HandData に書き込む。
        /// </summary>
        public static void ConvertHandResult(
            Landmarks handWorldLandmarks,
            Vector3 poseWristPosition,
            HandData output)
        {
            var count = Mathf.Min(handWorldLandmarks.landmarks.Count, HandLandmarkCount);
            for (var i = 0; i < count; i++)
            {
                var lm = handWorldLandmarks.landmarks[i];
                output.Landmarks[i] = poseWristPosition + new Vector3(lm.x, -lm.y, lm.z);
            }
        }

        /// <summary>
        /// Facial Transformation Matrix から頭部の位置と回転を抽出し FaceData に書き込む。
        /// MediaPipeUnityPlugin 側で Z 軸反転済みのためそのまま使用。
        /// 位置の単位は cm なので 0.01 を掛けてメートルに変換する。
        /// </summary>
        public static void ExtractHeadPose(Matrix4x4 matrix, FaceData output)
        {
            Vector3 position = matrix.GetColumn(3);
            position *= 0.01f;
            output.HeadPosition = position;
            // カメラに向いている顔の前方方向をキャラクターの前方方向（+Z）に合わせるため、Y軸180°回転で補正する。
            output.HeadRotation = matrix.rotation * Quaternion.Euler(0, 180f, 0);
        }

        /// <summary>
        /// FaceBlendshapes (Classifications) から 52 個の ARKit 互換ブレンドシェイプを
        /// FaceData に書き込む。index=0 の "_neutral" はスキップする。
        /// </summary>
        public static void ConvertFaceBlendshapes(Classifications blendshapes, FaceData output)
        {
            var outIdx = 0;
            for (var i = 0; i < blendshapes.categories.Count && outIdx < FaceData.BlendshapeCount; i++)
            {
                var cat = blendshapes.categories[i];
                if (cat.categoryName == "_neutral") continue;
                output.BlendshapeNames[outIdx] = cat.categoryName;
                output.Blendshapes[outIdx] = cat.score;
                outIdx++;
            }
        }

        /// <summary>
        /// HandLandmarkerResult の NormalizedLandmarks を HandData に格納する。
        /// ポーズと同様に [0,1] 範囲、左上原点・Y下向きでそのまま格納する。
        /// </summary>
        public static void ConvertHandNormalizedLandmarks(NormalizedLandmarks normalizedLandmarks, HandData output)
        {
            var count = Mathf.Min(normalizedLandmarks.landmarks.Count, HandLandmarkCount);
            for (var i = 0; i < count; i++)
            {
                var lm = normalizedLandmarks.landmarks[i];
                output.NormalizedLandmarks[i] = new Vector3(lm.x, lm.y, lm.z);
            }
        }

        /// <summary>
        /// MediaPipe の NormalizedLandmarks を PoseData に格納する。
        /// MediaPipe の Normalized Landmarks は [0,1] 範囲で、左上原点・Y下向き。
        /// そのまま格納する（Y反転はしない。逆射影時に考慮する）。
        /// z は MediaPipe が推定した相対深度。
        /// </summary>
        public static void ConvertNormalizedLandmarks(NormalizedLandmarks normalizedLandmarks, PoseData output)
        {
            var count = Mathf.Min(normalizedLandmarks.landmarks.Count, PoseLandmarkCount);
            for (var i = 0; i < count; i++)
            {
                var lm = normalizedLandmarks.landmarks[i];
                output.NormalizedLandmarks[i] = new Vector3(lm.x, lm.y, lm.z);
            }
        }
    }
}
