using System;
using MediaPipeMotionCapture.Visualization;
using UnityEngine;
    
namespace MediaPipeMotionCapture.ARFoundation.Samples
{

    public enum DebugInfoType
    {
        None,
        XROrigin,
        ARCamera,
        XRCpuImage,
        BodyAnchorTracker,
        MotionActorHips,
    }

    public class DebugPresenter : MonoBehaviour
    {
        [SerializeField] private DebugUIView _debugUIView;
        [SerializeField] private GameObject _xrOrigin;
        [SerializeField] private ARCamera _arCamera;
        [SerializeField] private ARDepthBodyAnchorTracker _bodyAnchorTracker;
        [SerializeField] private MediaPipeMotionActor _motionActor;

        private int _debugInfoTypeCount;
        private DebugInfoType _currentDebugInfo = DebugInfoType.None;

        void Awake()
        {
            _debugInfoTypeCount = Enum.GetValues(typeof(DebugInfoType)).Length;
            _debugUIView.OnDebugButtonClicked += ChangeDebugInfo;
        }

        void OnDestroy()
        {
            _debugUIView.OnDebugButtonClicked -= ChangeDebugInfo;
        }

        void ChangeDebugInfo()
        {
            _currentDebugInfo = (DebugInfoType)(((int)_currentDebugInfo + 1) % _debugInfoTypeCount);
        }

        void Update()
        {
            if (_currentDebugInfo == DebugInfoType.None)
            {
                _debugUIView.SetButtonText("Show XROrigin Info");
                _debugUIView.SetDebugInfo("Debug Info", "None");
            }
            else if (_currentDebugInfo == DebugInfoType.XROrigin)
            {
                _debugUIView.SetButtonText("Show Next Info");
                var debugTitle = "XROrigin Info";
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Position: {_xrOrigin.transform.position}");
                sb.AppendLine($"Rotation: {_xrOrigin.transform.rotation.eulerAngles}");
                _debugUIView.SetDebugInfo(debugTitle, sb.ToString());
            }
            else if (_currentDebugInfo == DebugInfoType.ARCamera)
            {
                _debugUIView.SetButtonText("Show Next Info");
                var debugTitle = "ARCamera Info";
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Position: {_arCamera.transform.position}");
                sb.AppendLine($"Rotation: {_arCamera.transform.rotation.eulerAngles}");
                _debugUIView.SetDebugInfo(debugTitle, sb.ToString());
            }
            else if (_currentDebugInfo == DebugInfoType.XRCpuImage)
            {
                _debugUIView.SetButtonText("Show Next Info");
                var debugTitle = "XRCpuImage Info";
                var sb = new System.Text.StringBuilder();
                if (_arCamera.TryAcquireLatestCpuImage(out var cpuImage))
                {
                    sb.AppendLine($"Width: {cpuImage.width}");
                    sb.AppendLine($"Height: {cpuImage.height}");
                    cpuImage.Dispose();
                }
                else
                {
                    sb.AppendLine("Failed to acquire CPU image.");
                }
                _debugUIView.SetDebugInfo(debugTitle, sb.ToString());
            }
            else if (_currentDebugInfo == DebugInfoType.BodyAnchorTracker)
            {
                _debugUIView.SetButtonText("Show Next Info");
                var debugTitle = "BodyAnchorTracker Info";
                var sb = new System.Text.StringBuilder();
                var anchorType = _bodyAnchorTracker.ActiveAnchorType;
                sb.AppendLine($"Anchor Type: {anchorType}");
                sb.AppendLine($"Position: {_bodyAnchorTracker.AnchorWorldPosition}");
                _debugUIView.SetDebugInfo(debugTitle, sb.ToString());
            }
            else if (_currentDebugInfo == DebugInfoType.MotionActorHips)
            {
                _debugUIView.SetButtonText("Show Next Info");
                var debugTitle = "MotionActor Hips Info";
                var sb = new System.Text.StringBuilder();
                if (_motionActor.TryGetBoneTransform(HumanBodyBones.Hips, out var hipsTransform))
                {
                    sb.AppendLine($"Position: {hipsTransform.position}");
                    sb.AppendLine($"Rotation: {hipsTransform.rotation.eulerAngles}");
                }
                else
                {
                    sb.AppendLine("Hips bone not found.");
                }
                _debugUIView.SetDebugInfo(debugTitle, sb.ToString());
            }   
        }
    }
}
