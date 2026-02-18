using UnityEngine;

namespace MediaPipeMotionCapture
{
    /// <summary>
    /// 仮想スケルトンの Transform 階層をシーンビューおよびゲームビューで可視化するデバッグコンポーネント。
    /// アタッチされた GameObject を起点に、子 Transform の親子関係をラインと球で描画する。
    /// </summary>
    public class SkeletonDebugVisualizer : MonoBehaviour
    {
        [Header("Colors")]
        [SerializeField] private Color _boneColor = Color.green;
        [SerializeField] private Color _jointColor = Color.yellow;

        [Header("Sizes")]
        [SerializeField] private float _jointRadius = 0.015f;

        private Material _lineMaterial;
        private Mesh _jointMesh;

        private void Awake()
        {
            CreateLineMaterial();
            CreateJointMesh();
        }

        private void OnDestroy()
        {
            if (_lineMaterial != null)
            {
                Destroy(_lineMaterial);
            }
            if (_jointMesh != null)
            {
                Destroy(_jointMesh);
            }
        }

        private void CreateLineMaterial()
        {
            var shader = Shader.Find("Hidden/Internal-Colored");
            _lineMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _lineMaterial.SetInt("_ZWrite", 0);
        }

        private void CreateJointMesh()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _jointMesh = go.GetComponent<MeshFilter>().mesh;
            Destroy(go);
        }

        private void OnRenderObject()
        {
            if (!Application.isPlaying) return;
            if (_lineMaterial == null) return;

            // ボーンのライン描画
            _lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(GL.LINES);
            GL.Color(_boneColor);
            DrawBoneLines(transform);
            GL.End();
            GL.PopMatrix();

            // ジョイントの球描画
            DrawJointSpheres(transform);
        }

        private void DrawBoneLines(Transform parent)
        {
            foreach (Transform child in parent)
            {
                GL.Vertex(parent.position);
                GL.Vertex(child.position);
                DrawBoneLines(child);
            }
        }

        private void DrawJointSpheres(Transform parent)
        {
            if (_jointMesh == null || _lineMaterial == null) return;

            var matrix = Matrix4x4.TRS(parent.position, Quaternion.identity, Vector3.one * _jointRadius * 2f);
            _lineMaterial.SetPass(0);
            Graphics.DrawMeshNow(_jointMesh, matrix);

            foreach (Transform child in parent)
            {
                DrawJointSpheres(child);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            DrawGizmosHierarchy(transform);
        }

        private void DrawGizmosHierarchy(Transform parent)
        {
            Gizmos.color = _jointColor;
            Gizmos.DrawSphere(parent.position, _jointRadius);

            foreach (Transform child in parent)
            {
                Gizmos.color = _boneColor;
                Gizmos.DrawLine(parent.position, child.position);
                DrawGizmosHierarchy(child);
            }
        }
#endif
    }
}
