using UnityEngine;

namespace MediaPipeMotionCapture
{
    public sealed class FaceBlendShapeTarget : IFaceExpressionTarget
    {
        private readonly SkinnedMeshRenderer _skinnedMeshRenderer;
        private readonly int _blendShapeIndex;

        public FaceBlendShapeTarget(SkinnedMeshRenderer skinnedMeshRenderer, int blendShapeIndex)
        {
            _skinnedMeshRenderer = skinnedMeshRenderer;
            _blendShapeIndex = blendShapeIndex;
        }

        public void SetWeight(float value)
        {
            _skinnedMeshRenderer.SetBlendShapeWeight(_blendShapeIndex, value);
        }
    }
}
