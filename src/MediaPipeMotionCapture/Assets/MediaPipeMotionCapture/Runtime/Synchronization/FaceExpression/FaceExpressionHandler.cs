using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MediaPipeMotionCapture
{
    public sealed class FaceExpressionHandler
    {
        private readonly Dictionary<FaceExpressionKey, IFaceExpressionTarget> _faceExpressionTargets = new();

        public FaceExpressionHandler(Transform targetTransform)
        {
            var skinnedMeshRenderers = targetTransform.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();

            var faceExpressionKeys = Enum.GetValues(typeof(FaceExpressionKey)).Cast<FaceExpressionKey>();

            foreach (var faceExpressionKey in faceExpressionKeys)
            {
                var faceExpressionKeyName = faceExpressionKey.ToString();

                // FaceBlendShapeTarget
                foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
                {
                    var blendShapeIndex = skinnedMeshRenderer.sharedMesh.GetBlendShapeIndex(faceExpressionKeyName);
                    if (blendShapeIndex >= 0)
                    {
                        _faceExpressionTargets[faceExpressionKey] = new FaceBlendShapeTarget(skinnedMeshRenderer, blendShapeIndex);
                    }
                }
            }
        }

        public void SetWeight(FaceExpressionKey faceExpressionKey, float value)
        {
            if (_faceExpressionTargets.TryGetValue(faceExpressionKey, out var faceExpressionTarget))
            {
                faceExpressionTarget.SetWeight(value);
            }
        }
    }
}
