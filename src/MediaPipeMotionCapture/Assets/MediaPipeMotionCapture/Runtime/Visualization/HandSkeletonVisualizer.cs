using UnityEngine;

namespace MediaPipeMotionCapture.Visualization
{
    public class HandSkeletonVisualizer : MonoBehaviour
    {
        [Header("Joint Settings")]
        [SerializeField] private float _jointRadius = 0.008f;
        [SerializeField] private Material _jointMaterial;

        [Header("Bone Settings")]
        [SerializeField] private float _boneWidth = 0.005f;
        [SerializeField] private Material _boneMaterial;

        [Header("Display Settings")]
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
            _joints = new GameObject[Converter.HandLandmarkCount];
            for (int i = 0; i < Converter.HandLandmarkCount; i++)
            {
                var joint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                joint.name = $"HandJoint_{i}";
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

            var connections = HandConnections.Connections;
            _bones = new LineRenderer[connections.Length];
            for (int i = 0; i < connections.Length; i++)
            {
                var boneObj = new GameObject($"HandBone_{connections[i].Item1}_{connections[i].Item2}");
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
        /// 手のランドマークデータでスケルトンの表示を更新する
        /// </summary>
        public void UpdateHand(HandData handData)
        {
            if (!_initialized || handData == null) return;

            for (int i = 0; i < Converter.HandLandmarkCount; i++)
            {
                _joints[i].SetActive(true);
                _joints[i].transform.position = handData.Landmarks[i] + _positionOffset;
            }

            var connections = HandConnections.Connections;
            for (int i = 0; i < connections.Length; i++)
            {
                int startIdx = connections[i].Item1;
                int endIdx = connections[i].Item2;

                _bones[i].gameObject.SetActive(true);
                _bones[i].SetPosition(0, handData.Landmarks[startIdx] + _positionOffset);
                _bones[i].SetPosition(1, handData.Landmarks[endIdx] + _positionOffset);
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
