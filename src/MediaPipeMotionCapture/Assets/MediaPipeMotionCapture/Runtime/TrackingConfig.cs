using System;
using UnityEngine;

namespace MediaPipeMotionCapture
{
    [Serializable]
    public class TrackingConfig
    {
        [Header("Pose")]
        public string PoseModelName = "pose_landmarker_full.bytes";
        public int NumPoses = 1;
        public float MinPoseDetectionConfidence = 0.5f;
        public float MinPosePresenceConfidence = 0.5f;
        public float MinPoseTrackingConfidence = 0.5f;

        [Header("Hand")]
        public string HandModelName = "hand_landmarker.bytes";
        public int NumHands = 2;
        public float MinHandDetectionConfidence = 0.5f;
        public float MinHandPresenceConfidence = 0.5f;
        public float MinHandTrackingConfidence = 0.5f;

        [Header("Face")]
        public string FaceModelName = "face_landmarker_v2_with_blendshapes.bytes";
        public int NumFaces = 1;
        public float MinFaceDetectionConfidence = 0.5f;
        public float MinFacePresenceConfidence = 0.5f;
        public float MinFaceTrackingConfidence = 0.5f;

        [Header("Segmentation")]
        public bool OutputSegmentationMasks = false;

        [Header("Smoothing")]
        public bool EnableSmoothing = true;
        public bool UsePerBodyPartParams = true;
        public float SmoothingMinCutoff = 1.0f;
        public float SmoothingBeta = 0.1f;
    }
}
