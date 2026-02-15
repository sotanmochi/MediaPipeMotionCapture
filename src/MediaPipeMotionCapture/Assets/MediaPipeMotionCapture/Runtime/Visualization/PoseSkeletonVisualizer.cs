using UnityEngine;

namespace MediaPipeMotionCapture.Visualization
{
    public class PoseSkeletonVisualizer : MonoBehaviour
    {
        [Header("Joint Settings")]
        [SerializeField] private float _jointRadius = 0.02f;
        [SerializeField] private Material _jointMaterial;

        [Header("Bone Settings")]
        [SerializeField] private float _boneWidth = 0.008f;
        [SerializeField] private Material _boneMaterial;

        [Header("Display Settings")]
        [SerializeField] private float _visibilityThreshold = 0.5f;
        [SerializeField] private Vector3 _positionOffset = new Vector3(0, 0, 0f);

        private GameObject[] _joints;
        private LineRenderer[] _bones;
        private bool _initialized;

        void Awake()
        {
            InitializeJointsAndBones();
        }

        private void InitializeJointsAndBones()
        {
            _joints = new GameObject[Converter.PoseLandmarkCount];
            for (int i = 0; i < Converter.PoseLandmarkCount; i++)
            {
                var joint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                joint.name = $"Joint_{i}";
                joint.transform.SetParent(transform, false);
                joint.transform.localScale = Vector3.one * _jointRadius * 2f;

                var collider = joint.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                if (_jointMaterial != null)
                {
                    joint.GetComponent<Renderer>().material = _jointMaterial;
                }

                joint.SetActive(false);
                _joints[i] = joint;
            }

            var connections = PoseConnections.Connections;
            _bones = new LineRenderer[connections.Length];
            for (int i = 0; i < connections.Length; i++)
            {
                var boneObj = new GameObject($"Bone_{connections[i].Item1}_{connections[i].Item2}");
                boneObj.transform.SetParent(transform, false);

                var lr = boneObj.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.startWidth = _boneWidth;
                lr.endWidth = _boneWidth;
                lr.useWorldSpace = true;
                lr.numCapVertices = 2;

                if (_boneMaterial != null)
                {
                    lr.material = _boneMaterial;
                }

                boneObj.SetActive(false);
                _bones[i] = lr;
            }

            _initialized = true;
        }

        /// <summary>
        /// ポーズデータでスケルトンの表示を更新する
        /// </summary>
        public void UpdatePose(PoseData poseData)
        {
            if (!_initialized || poseData == null) return;

            for (int i = 0; i < Converter.PoseLandmarkCount; i++)
            {
                float vis = poseData.Visibility[i];
                bool visible = vis >= _visibilityThreshold;

                _joints[i].SetActive(visible);
                if (visible)
                {
                    _joints[i].transform.position = poseData.WorldLandmarks[i] + _positionOffset;
                }
            }

            var connections = PoseConnections.Connections;
            for (int i = 0; i < connections.Length; i++)
            {
                int startIdx = connections[i].Item1;
                int endIdx = connections[i].Item2;

                bool boneVisible =
                    poseData.Visibility[startIdx] >= _visibilityThreshold &&
                    poseData.Visibility[endIdx] >= _visibilityThreshold;

                _bones[i].gameObject.SetActive(boneVisible);
                if (boneVisible)
                {
                    _bones[i].SetPosition(0, poseData.WorldLandmarks[startIdx] + _positionOffset);
                    _bones[i].SetPosition(1, poseData.WorldLandmarks[endIdx] + _positionOffset);
                }
            }
        }

        /// <summary>
        /// 全ての関節とボーンを非表示にする
        /// </summary>
        public void Hide()
        {
            if (!_initialized) return;

            for (int i = 0; i < _joints.Length; i++)
            {
                _joints[i].SetActive(false);
            }
            for (int i = 0; i < _bones.Length; i++)
            {
                _bones[i].gameObject.SetActive(false);
            }
        }
    }
}
