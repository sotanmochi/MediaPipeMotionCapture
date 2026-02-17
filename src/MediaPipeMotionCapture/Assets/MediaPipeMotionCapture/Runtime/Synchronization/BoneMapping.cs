using UnityEngine;

namespace MediaPipeMotionCapture
{
    /// <summary>
    /// MediaPipe ランドマークインデックスと Unity HumanBodyBones の対応を定義する。
    /// </summary>
    public struct LandmarkBoneMapping
    {
        public HumanBodyBones Bone;
        public int ParentLandmarkIndex;
        public int ChildLandmarkIndex;
    }

    /// <summary>
    /// MediaPipe Pose ランドマーク → HumanBodyBones のマッピングテーブル。
    /// </summary>
    public static class BoneMappingTable
    {
        // 上半身: Shoulder → Elbow → Wrist
        public static readonly LandmarkBoneMapping[] UpperBody = new[]
        {
            new LandmarkBoneMapping { Bone = HumanBodyBones.LeftUpperArm,  ParentLandmarkIndex = 11, ChildLandmarkIndex = 13 },
            new LandmarkBoneMapping { Bone = HumanBodyBones.LeftLowerArm,  ParentLandmarkIndex = 13, ChildLandmarkIndex = 15 },
            new LandmarkBoneMapping { Bone = HumanBodyBones.RightUpperArm, ParentLandmarkIndex = 12, ChildLandmarkIndex = 14 },
            new LandmarkBoneMapping { Bone = HumanBodyBones.RightLowerArm, ParentLandmarkIndex = 14, ChildLandmarkIndex = 16 },
        };

        // 下半身: Hip → Knee → Ankle
        public static readonly LandmarkBoneMapping[] LowerBody = new[]
        {
            new LandmarkBoneMapping { Bone = HumanBodyBones.LeftUpperLeg,  ParentLandmarkIndex = 23, ChildLandmarkIndex = 25 },
            new LandmarkBoneMapping { Bone = HumanBodyBones.LeftLowerLeg,  ParentLandmarkIndex = 25, ChildLandmarkIndex = 27 },
            new LandmarkBoneMapping { Bone = HumanBodyBones.RightUpperLeg, ParentLandmarkIndex = 24, ChildLandmarkIndex = 26 },
            new LandmarkBoneMapping { Bone = HumanBodyBones.RightLowerLeg, ParentLandmarkIndex = 26, ChildLandmarkIndex = 28 },
        };

        // 手: Wrist, Pinky, Index, Thumb の3点から手の向きを計算する
        public static readonly HandBoneMapping[] Hands = new[]
        {
            new HandBoneMapping { Bone = HumanBodyBones.LeftHand,  WristIndex = 15, PinkyIndex = 17, IndexIndex = 19 },
            new HandBoneMapping { Bone = HumanBodyBones.RightHand, WristIndex = 16, PinkyIndex = 18, IndexIndex = 20 },
        };

        // Pose ランドマークの主要インデックス
        public const int LeftShoulder = 11;
        public const int RightShoulder = 12;
        public const int LeftHip = 23;
        public const int RightHip = 24;
    }

    /// <summary>
    /// MediaPipe Hand ランドマーク (21点) → HumanBodyBones 指関節のマッピングテーブル。
    /// NOTE: 各指は Proximal → Intermediate → Distal の順序であること。
    /// 親関節の回転を基準に子関節の回転を計算するため、親が先に更新されている必要がある。
    /// </summary>
    public static class HandBoneMappingTable
    {
        // MediaPipe Hand ランドマークインデックス
        public const int Wrist = 0;
        public const int ThumbCMC = 1;
        public const int ThumbMCP = 2;
        public const int ThumbIP = 3;
        public const int ThumbTip = 4;
        public const int IndexMCP = 5;
        public const int IndexPIP = 6;
        public const int IndexDIP = 7;
        public const int IndexTip = 8;
        public const int MiddleMCP = 9;
        public const int MiddlePIP = 10;
        public const int MiddleDIP = 11;
        public const int MiddleTip = 12;
        public const int RingMCP = 13;
        public const int RingPIP = 14;
        public const int RingDIP = 15;
        public const int RingTip = 16;
        public const int PinkyMCP = 17;
        public const int PinkyPIP = 18;
        public const int PinkyDIP = 19;
        public const int PinkyTip = 20;

        // 左手の指関節マッピング
        public static readonly LandmarkBoneMapping[] LeftFingers = new[]
        {
            // 親指
            new LandmarkBoneMapping { Bone = HumanBodyBones.LeftThumbProximal,     ParentLandmarkIndex = ThumbCMC, ChildLandmarkIndex = ThumbMCP },
            new LandmarkBoneMapping { Bone = HumanBodyBones.LeftThumbIntermediate, ParentLandmarkIndex = ThumbMCP, ChildLandmarkIndex = ThumbIP },
            new LandmarkBoneMapping { Bone = HumanBodyBones.LeftThumbDistal,       ParentLandmarkIndex = ThumbIP,  ChildLandmarkIndex = ThumbTip },
            // 人差し指
            new LandmarkBoneMapping { Bone = HumanBodyBones.LeftIndexProximal,     ParentLandmarkIndex = IndexMCP, ChildLandmarkIndex = IndexPIP },
            new LandmarkBoneMapping { Bone = HumanBodyBones.LeftIndexIntermediate, ParentLandmarkIndex = IndexPIP, ChildLandmarkIndex = IndexDIP },
            new LandmarkBoneMapping { Bone = HumanBodyBones.LeftIndexDistal,       ParentLandmarkIndex = IndexDIP, ChildLandmarkIndex = IndexTip },
            // 中指
            new LandmarkBoneMapping { Bone = HumanBodyBones.LeftMiddleProximal,     ParentLandmarkIndex = MiddleMCP, ChildLandmarkIndex = MiddlePIP },
            new LandmarkBoneMapping { Bone = HumanBodyBones.LeftMiddleIntermediate, ParentLandmarkIndex = MiddlePIP, ChildLandmarkIndex = MiddleDIP },
            new LandmarkBoneMapping { Bone = HumanBodyBones.LeftMiddleDistal,       ParentLandmarkIndex = MiddleDIP, ChildLandmarkIndex = MiddleTip },
            // 薬指
            new LandmarkBoneMapping { Bone = HumanBodyBones.LeftRingProximal,     ParentLandmarkIndex = RingMCP, ChildLandmarkIndex = RingPIP },
            new LandmarkBoneMapping { Bone = HumanBodyBones.LeftRingIntermediate, ParentLandmarkIndex = RingPIP, ChildLandmarkIndex = RingDIP },
            new LandmarkBoneMapping { Bone = HumanBodyBones.LeftRingDistal,       ParentLandmarkIndex = RingDIP, ChildLandmarkIndex = RingTip },
            // 小指
            new LandmarkBoneMapping { Bone = HumanBodyBones.LeftLittleProximal,     ParentLandmarkIndex = PinkyMCP, ChildLandmarkIndex = PinkyPIP },
            new LandmarkBoneMapping { Bone = HumanBodyBones.LeftLittleIntermediate, ParentLandmarkIndex = PinkyPIP, ChildLandmarkIndex = PinkyDIP },
            new LandmarkBoneMapping { Bone = HumanBodyBones.LeftLittleDistal,       ParentLandmarkIndex = PinkyDIP, ChildLandmarkIndex = PinkyTip },
        };

        // 右手の指関節マッピング
        public static readonly LandmarkBoneMapping[] RightFingers = new[]
        {
            // 親指
            new LandmarkBoneMapping { Bone = HumanBodyBones.RightThumbProximal,     ParentLandmarkIndex = ThumbCMC, ChildLandmarkIndex = ThumbMCP },
            new LandmarkBoneMapping { Bone = HumanBodyBones.RightThumbIntermediate, ParentLandmarkIndex = ThumbMCP, ChildLandmarkIndex = ThumbIP },
            new LandmarkBoneMapping { Bone = HumanBodyBones.RightThumbDistal,       ParentLandmarkIndex = ThumbIP,  ChildLandmarkIndex = ThumbTip },
            // 人差し指
            new LandmarkBoneMapping { Bone = HumanBodyBones.RightIndexProximal,     ParentLandmarkIndex = IndexMCP, ChildLandmarkIndex = IndexPIP },
            new LandmarkBoneMapping { Bone = HumanBodyBones.RightIndexIntermediate, ParentLandmarkIndex = IndexPIP, ChildLandmarkIndex = IndexDIP },
            new LandmarkBoneMapping { Bone = HumanBodyBones.RightIndexDistal,       ParentLandmarkIndex = IndexDIP, ChildLandmarkIndex = IndexTip },
            // 中指
            new LandmarkBoneMapping { Bone = HumanBodyBones.RightMiddleProximal,     ParentLandmarkIndex = MiddleMCP, ChildLandmarkIndex = MiddlePIP },
            new LandmarkBoneMapping { Bone = HumanBodyBones.RightMiddleIntermediate, ParentLandmarkIndex = MiddlePIP, ChildLandmarkIndex = MiddleDIP },
            new LandmarkBoneMapping { Bone = HumanBodyBones.RightMiddleDistal,       ParentLandmarkIndex = MiddleDIP, ChildLandmarkIndex = MiddleTip },
            // 薬指
            new LandmarkBoneMapping { Bone = HumanBodyBones.RightRingProximal,     ParentLandmarkIndex = RingMCP, ChildLandmarkIndex = RingPIP },
            new LandmarkBoneMapping { Bone = HumanBodyBones.RightRingIntermediate, ParentLandmarkIndex = RingPIP, ChildLandmarkIndex = RingDIP },
            new LandmarkBoneMapping { Bone = HumanBodyBones.RightRingDistal,       ParentLandmarkIndex = RingDIP, ChildLandmarkIndex = RingTip },
            // 小指
            new LandmarkBoneMapping { Bone = HumanBodyBones.RightLittleProximal,     ParentLandmarkIndex = PinkyMCP, ChildLandmarkIndex = PinkyPIP },
            new LandmarkBoneMapping { Bone = HumanBodyBones.RightLittleIntermediate, ParentLandmarkIndex = PinkyPIP, ChildLandmarkIndex = PinkyDIP },
            new LandmarkBoneMapping { Bone = HumanBodyBones.RightLittleDistal,       ParentLandmarkIndex = PinkyDIP, ChildLandmarkIndex = PinkyTip },
        };
    }

    /// <summary>
    /// 手首の回転を計算するためのマッピング。
    /// Wrist, Pinky, Index の3点から手の向き (forward + up) を算出する。
    /// </summary>
    public struct HandBoneMapping
    {
        public HumanBodyBones Bone;
        public int WristIndex;
        public int PinkyIndex;
        public int IndexIndex;
    }
}
