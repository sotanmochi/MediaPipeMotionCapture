using UnityEngine;
using UnityEngine.UI;

namespace MediaPipeMotionCapture.Visualization
{
    /// <summary>
    /// カメラ画像上に NormalizedLandmarks（ポーズ・手）をオーバーレイ描画するコンポーネント。
    /// RawImage（カメラプレビュー）の子として配置し、同じサイズの RectTransform を持たせて使用する。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class LandmarkOverlayRenderer : Graphic
    {
        [Header("Data Source")]
        [SerializeField] private MotionCaptureTaskRunner _motionCaptureTaskRunner;

        [Header("Camera")]
        [SerializeField] private bool _isFrontCamera;

        [Header("Pose Settings")]
        [SerializeField] private Color _poseLandmarkColor = new Color(0f, 1f, 0.5f, 1f);
        [SerializeField] private Color _poseConnectionColor = new Color(0f, 0.8f, 0.4f, 0.8f);
        [SerializeField] private float _poseLandmarkSize = 4f;
        [SerializeField] private float _poseConnectionWidth = 2f;
        [SerializeField] private float _poseVisibilityThreshold = 0.5f;

        [Header("Hand Settings")]
        [SerializeField] private Color _leftHandColor = new Color(1f, 0.4f, 0.4f, 1f);
        [SerializeField] private Color _rightHandColor = new Color(0.4f, 0.4f, 1f, 1f);
        [SerializeField] private float _handLandmarkSize = 3f;
        [SerializeField] private float _handConnectionWidth = 1.5f;

        public bool IsFrontCamera
        {
            get => _isFrontCamera;
            set => _isFrontCamera = value;
        }

        private PoseData _poseData;
        private HandData _leftHandData;
        private HandData _rightHandData;
        private bool _hasPose;
        private bool _hasLeftHand;
        private bool _hasRightHand;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_motionCaptureTaskRunner != null)
            {
                _motionCaptureTaskRunner.OnTrackingDataUpdated += OnTrackingDataUpdated;
            }
        }

        protected override void OnDisable()
        {
            if (_motionCaptureTaskRunner != null)
            {
                _motionCaptureTaskRunner.OnTrackingDataUpdated -= OnTrackingDataUpdated;
            }
            base.OnDisable();
        }

        private void OnTrackingDataUpdated(TrackingData data)
        {
            _hasPose = data.HasPose;
            _hasLeftHand = data.HasLeftHand;
            _hasRightHand = data.HasRightHand;
            _poseData = data.Pose;
            _leftHandData = data.LeftHand;
            _rightHandData = data.RightHand;

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            var rect = rectTransform.rect;

            if (_hasPose && _poseData != null)
            {
                DrawConnections(vh, _poseData.NormalizedLandmarks, _poseData.Visibility,
                    PoseConnections.Connections, _poseConnectionColor, _poseConnectionWidth,
                    _poseVisibilityThreshold, rect);
                DrawLandmarks(vh, _poseData.NormalizedLandmarks, _poseData.Visibility,
                    _poseLandmarkColor, _poseLandmarkSize,
                    _poseVisibilityThreshold, rect);
            }

            if (_hasLeftHand && _leftHandData != null)
            {
                DrawConnections(vh, _leftHandData.NormalizedLandmarks, null,
                    HandConnections.Connections, _leftHandColor, _handConnectionWidth,
                    0f, rect);
                DrawLandmarks(vh, _leftHandData.NormalizedLandmarks, null,
                    _leftHandColor, _handLandmarkSize,
                    0f, rect);
            }

            if (_hasRightHand && _rightHandData != null)
            {
                DrawConnections(vh, _rightHandData.NormalizedLandmarks, null,
                    HandConnections.Connections, _rightHandColor, _handConnectionWidth,
                    0f, rect);
                DrawLandmarks(vh, _rightHandData.NormalizedLandmarks, null,
                    _rightHandColor, _handLandmarkSize,
                    0f, rect);
            }
        }

        /// <summary>
        /// NormalizedLandmark ([0,1], 左上原点, Y下向き) を RectTransform のローカル座標に変換する。
        /// </summary>
        private Vector2 NormalizedToLocal(Vector3 normalizedLandmark, Rect rect)
        {
            float nx = normalizedLandmark.x;
            float ny = normalizedLandmark.y;

            // WIP
            TransformToScreenCoordinates(ref nx, ref ny, _isFrontCamera);

            float x = rect.xMin + nx * rect.width;
            float y = rect.yMax - ny * rect.height; // Y反転: MediaPipe Y下向き → UI Y上向き
            return new Vector2(x, y);
        }

        /// <summary>
        /// TODO: Fix bugs
        /// </summary>
        private static void TransformToScreenCoordinates(ref float nx, ref float ny, bool isFrontCamera)
        {
            if (isFrontCamera)
            {
                // ミラー済みセンサー座標 → 画面座標: unmirror → rotate → mirror
                switch (Screen.orientation)
                {
                    case ScreenOrientation.LandscapeRight:
                        // ミラー済みセンサー座標がそのまま画面座標と一致
                        //
                        nx = 1f - nx;
                        ny = ny;
                        break;
                    case ScreenOrientation.Portrait:
                        // (nx, ny) → (ny, 1-nx)
                        // var tmpP = nx;
                        // nx = ny;
                        // ny = 1f - tmpP;
                        var tmpP = nx;
                        nx = ny;
                        ny = tmpP;
                        break;
                    case ScreenOrientation.LandscapeLeft:
                        // (nx, ny) → (1-nx, 1-ny)
                        // nx = 1f - nx;
                        // ny = 1f - ny;
                        nx = nx;
                        ny = 1f - ny;
                        break;
                    case ScreenOrientation.PortraitUpsideDown:
                        // (nx, ny) → (1-ny, nx)
                        // var tmpU = nx;
                        // nx = 1f - ny;
                        // ny = tmpU;
                        nx = 1f - ny;
                        ny = 1f - nx;
                        break;
                    default:
                        break;
                }
            }
            else
            {
                // センサー座標 → 画面座標: 回転のみ
                switch (Screen.orientation)
                {
                    case ScreenOrientation.LandscapeLeft:
                        // センサーと一致。変換不要
                        break;
                    case ScreenOrientation.Portrait:
                        // 90度回転: (nx, ny) → (1-ny, nx)
                        var tmpP = nx;
                        nx = 1f - ny;
                        ny = tmpP;
                        break;
                    case ScreenOrientation.LandscapeRight:
                        // 180度回転: (nx, ny) → (1-nx, 1-ny)
                        nx = 1f - nx;
                        ny = 1f - ny;
                        break;
                    case ScreenOrientation.PortraitUpsideDown:
                        // 270度回転: (nx, ny) → (ny, 1-nx)
                        var tmpU = nx;
                        nx = ny;
                        ny = 1f - tmpU;
                        break;
                    default:
                        break;
                }
            }
        }

        private void DrawLandmarks(VertexHelper vh, Vector3[] landmarks, float[] visibility,
            Color color, float size, float visibilityThreshold, Rect rect)
        {
            float halfSize = size;

            for (int i = 0; i < landmarks.Length; i++)
            {
                if (visibility != null && visibility[i] < visibilityThreshold)
                    continue;

                var pos = NormalizedToLocal(landmarks[i], rect);
                AddQuad(vh, pos, halfSize, color);
            }
        }

        private void DrawConnections(VertexHelper vh, Vector3[] landmarks, float[] visibility,
            (int, int)[] connections, Color color, float width, float visibilityThreshold, Rect rect)
        {
            float halfWidth = width * 0.5f;

            for (int i = 0; i < connections.Length; i++)
            {
                int startIdx = connections[i].Item1;
                int endIdx = connections[i].Item2;

                if (visibility != null &&
                    (visibility[startIdx] < visibilityThreshold || visibility[endIdx] < visibilityThreshold))
                    continue;

                var startPos = NormalizedToLocal(landmarks[startIdx], rect);
                var endPos = NormalizedToLocal(landmarks[endIdx], rect);

                AddLine(vh, startPos, endPos, halfWidth, color);
            }
        }

        private void AddQuad(VertexHelper vh, Vector2 center, float halfSize, Color color)
        {
            int vertIndex = vh.currentVertCount;

            vh.AddVert(new Vector3(center.x - halfSize, center.y - halfSize, 0f), color, Vector4.zero);
            vh.AddVert(new Vector3(center.x - halfSize, center.y + halfSize, 0f), color, Vector4.zero);
            vh.AddVert(new Vector3(center.x + halfSize, center.y + halfSize, 0f), color, Vector4.zero);
            vh.AddVert(new Vector3(center.x + halfSize, center.y - halfSize, 0f), color, Vector4.zero);

            vh.AddTriangle(vertIndex, vertIndex + 1, vertIndex + 2);
            vh.AddTriangle(vertIndex, vertIndex + 2, vertIndex + 3);
        }

        private void AddLine(VertexHelper vh, Vector2 start, Vector2 end, float halfWidth, Color color)
        {
            int vertIndex = vh.currentVertCount;

            var dir = (end - start).normalized;
            var normal = new Vector2(-dir.y, dir.x) * halfWidth;

            vh.AddVert(new Vector3(start.x + normal.x, start.y + normal.y, 0f), color, Vector4.zero);
            vh.AddVert(new Vector3(start.x - normal.x, start.y - normal.y, 0f), color, Vector4.zero);
            vh.AddVert(new Vector3(end.x - normal.x, end.y - normal.y, 0f), color, Vector4.zero);
            vh.AddVert(new Vector3(end.x + normal.x, end.y + normal.y, 0f), color, Vector4.zero);

            vh.AddTriangle(vertIndex, vertIndex + 1, vertIndex + 2);
            vh.AddTriangle(vertIndex, vertIndex + 2, vertIndex + 3);
        }
    }
}
