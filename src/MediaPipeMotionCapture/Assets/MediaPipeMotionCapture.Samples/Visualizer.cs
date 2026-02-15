using MediaPipeMotionCapture.Visualization;
using UnityEngine;

namespace MediaPipeMotionCapture.Samples
{
    public class Visualizer : MonoBehaviour
    {
        [SerializeField] private MotionCaptureTaskRunner _motionCaptureTaskRunner;

        [Header("Visualization")]
        [SerializeField] private PoseSkeletonVisualizer _skeletonVisualizer;
        [SerializeField] private HandSkeletonVisualizer _leftHandVisualizer;
        [SerializeField] private HandSkeletonVisualizer _rightHandVisualizer;

        private void OnEnable()
        {
            _motionCaptureTaskRunner.OnTrackingDataUpdated += OnTrackingDataUpdated;
        }

        private void OnDisable()
        {
            _motionCaptureTaskRunner.OnTrackingDataUpdated -= OnTrackingDataUpdated;
        }

        private void OnTrackingDataUpdated(TrackingData data)
        {
            if (data.HasPose)
                _skeletonVisualizer.UpdatePose(data.Pose);
            else
                _skeletonVisualizer.Hide();

            if (data.HasLeftHand)
                _leftHandVisualizer.UpdateHand(data.LeftHand);
            else
                _leftHandVisualizer.Hide();

            if (data.HasRightHand)
                _rightHandVisualizer.UpdateHand(data.RightHand);
            else
                _rightHandVisualizer.Hide();
        }
    }    
}
