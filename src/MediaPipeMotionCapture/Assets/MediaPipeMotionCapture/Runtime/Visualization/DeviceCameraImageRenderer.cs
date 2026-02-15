using UnityEngine;
using UnityEngine.UI;

namespace MediaPipeMotionCapture
{
    public class DeviceCameraImageRenderer : MonoBehaviour
    {
        [SerializeField] private DeviceCameraBase _deviceCameraComponent;
        [SerializeField] private RawImage _outputImage;

        private IDeviceCamera _deviceCamera;

        void Start()
        {
            _deviceCamera = _deviceCameraComponent.GetComponent<IDeviceCamera>();
            if (_deviceCamera == null)
            {
                DebugLogger.LogError($"[{nameof(DeviceCameraImageRenderer)}] DeviceCamera component does not implement IDeviceCamera.");
                enabled = false;
                return;
            }

            if (_outputImage == null)
            {
                DebugLogger.LogError($"[{nameof(DeviceCameraImageRenderer)}] Output image is not assigned.");
                enabled = false;
                return;
            }
        }

        void LateUpdate()
        {
            if (_deviceCamera == null || _outputImage == null) return;

            var texture = _deviceCamera.ImageTexture;
            if (texture != null)
            {
                _outputImage.texture = texture;
                _outputImage.enabled = true;
            }
            else
            {
                _outputImage.enabled = false;
            }
        }
    }
}
