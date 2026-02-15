using UnityEngine;

namespace MediaPipeMotionCapture
{
    public class PoseData
    {
        public Vector3[] WorldLandmarks { get; set; }
        public Vector3[] NormalizedLandmarks { get; set; }
        public float[] Visibility { get; set; }
        public Vector3 HipWorldPosition { get; set; }

        public PoseData()
        {
            WorldLandmarks = new Vector3[33];
            NormalizedLandmarks = new Vector3[33];
            Visibility = new float[33];
            HipWorldPosition = Vector3.zero;
        }
    }

    public class HandData
    {
        public Vector3[] Landmarks { get; set; }
        public Handedness Handedness { get; set; }

        public HandData(Handedness handedness)
        {
            Landmarks = new Vector3[21];
            Handedness = handedness;
        }
    }

    public class FaceData
    {
        public const int BlendshapeCount = 52;

        public float[] Blendshapes { get; set; }
        public string[] BlendshapeNames { get; set; }
        public Quaternion HeadRotation { get; set; }
        public Vector3 HeadPosition { get; set; }

        public FaceData()
        {
            Blendshapes = new float[BlendshapeCount];
            BlendshapeNames = new string[BlendshapeCount];
            HeadRotation = Quaternion.identity;
            HeadPosition = Vector3.zero;
        }
    }

    public class TrackingData
    {
        public long TimestampMs { get; set; }
        public PoseData Pose { get; set; }
        public HandData LeftHand { get; set; }
        public HandData RightHand { get; set; }
        public FaceData Face { get; set; }
        public bool HasPose { get; set; }
        public bool HasLeftHand { get; set; }
        public bool HasRightHand { get; set; }
        public bool HasFace { get; set; }

        public float[] SegmentationMask { get; set; }
        public int SegmentationMaskWidth { get; set; }
        public int SegmentationMaskHeight { get; set; }
        public bool HasSegmentationMask { get; set; }
    }

    public enum Handedness { Left, Right }
}
