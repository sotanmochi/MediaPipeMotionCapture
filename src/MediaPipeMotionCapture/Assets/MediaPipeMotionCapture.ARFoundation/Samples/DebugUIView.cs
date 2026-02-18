using System;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;

namespace MediaPipeMotionCapture.ARFoundation.Samples
{
    public class DebugUIView : MonoBehaviour
    {
        [SerializeField] private Button _debugButton;
        [SerializeField] private Text _buttonText;
        [SerializeField] private Text _debugTitle;
        [SerializeField] private Text _debugText;
        [SerializeField] private Text _frameRateText;

        public event Action OnDebugButtonClicked;

        void Awake()
        {
            _debugButton.onClick.AddListener(HandleDebugButtonClick);
        }

        void OnDestroy()
        {
            _debugButton?.onClick.RemoveAllListeners();
        }

        void Update()
        {
            if (_frameRateText != null)
            {
                _frameRateText.text = $"FPS: {1.0f / Time.deltaTime:F1}";
            }
        }

        public void SetDebugInfo(string title, string info)
        {
            if (_debugTitle != null)
            {
                _debugTitle.text = title;
            }
            if (_debugText != null)
            {
                _debugText.text = info;
            }
        }

        public void SetButtonText(string text)
        {
            if (_buttonText != null)
            {
                _buttonText.text = text;
            }
        }

        private void HandleDebugButtonClick()
        {
            OnDebugButtonClicked?.Invoke();
        }
    }
}
