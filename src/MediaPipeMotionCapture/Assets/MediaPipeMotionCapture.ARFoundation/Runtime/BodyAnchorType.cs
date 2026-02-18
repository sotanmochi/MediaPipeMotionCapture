using UnityEngine;

namespace MediaPipeMotionCapture.ARFoundation
{
    /// <summary>
    /// トラッキング対象の関節種別。
    /// 優先度順（数値が大きいほど優先度が高い）に定義する。
    /// </summary>
    public enum BodyAnchorType
    {
        None = 0,
        Head = 1,
        Shoulder = 2,
        Hips = 3,
    }

    /// <summary>
    /// 1つの関節のトラッキング結果。
    /// </summary>
    public struct BodyAnchorResult
    {
        public BodyAnchorType AnchorType;
        public Vector3 WorldPosition;
        public bool IsValid;
        public float Confidence;
    }
}
