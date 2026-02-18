using System.Collections.Generic;
using UnityEngine;

namespace MediaPipeMotionCapture
{
    /// <summary>
    /// 標準プロポーションのヒューマノイドスケルトン (Transform 階層) を生成し、
    /// T-Pose を適用して Avatar を構築するユーティリティ。
    /// </summary>
    public static class MediaPipeSkeletonBuilder
    {
        public class BuildResult
        {
            public GameObject SkeletonRoot;
            public Avatar Avatar;
            public Dictionary<HumanBodyBones, Transform> BoneMap;
        }

        /// <summary>
        /// 仮想ヒューマノイドスケルトンを生成し、T-Pose を適用して Avatar を構築する。
        /// </summary>
        public static BuildResult Build()
        {
            var root = new GameObject("MediaPipeSkeleton");
            var boneMap = new Dictionary<HumanBodyBones, Transform>();
            var boneTransforms = new Dictionary<HumanBodyBones, Transform>();

            // ボーン定義テーブルに基づいて Transform 階層を生成
            foreach (var def in BoneDefinitions)
            {
                var boneObj = new GameObject(def.Name);

                Transform parent;
                if (def.ParentBone == HumanBodyBones.LastBone)
                {
                    // ルート直下
                    parent = root.transform;
                }
                else
                {
                    parent = boneTransforms[def.ParentBone];
                }

                boneObj.transform.SetParent(parent, false);
                boneObj.transform.localPosition = def.LocalPosition;
                boneObj.transform.localRotation = Quaternion.identity;
                boneObj.transform.localScale = Vector3.one;

                boneTransforms[def.Bone] = boneObj.transform;
                boneMap[def.Bone] = boneObj.transform;
            }

            // T-Pose を適用
            HumanAvatarSkeletonUtility.ApplyTPose(boneMap, adjustHeightToGround: true);

            // Avatar を構築
            var avatar = HumanAvatarBuilder.BuildHumanAvatar(root, boneMap);

            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                DebugLogger.LogError("[MediaPipeSkeletonBuilder] Failed to build a valid human avatar.");
                Object.Destroy(root);
                return null;
            }

            avatar.name = "MediaPipeAvatar";

            DebugLogger.Log($"[MediaPipeSkeletonBuilder] Skeleton built successfully. Bones: {boneMap.Count}, Avatar valid: {avatar.isValid}");

            return new BuildResult
            {
                SkeletonRoot = root,
                Avatar = avatar,
                BoneMap = boneMap,
            };
        }

        // ボーン定義: HumanBodyBones.LastBone を親なし (ルート直下) として使用
        private struct BoneDefinition
        {
            public HumanBodyBones Bone;
            public HumanBodyBones ParentBone;
            public string Name;
            public Vector3 LocalPosition;

            public BoneDefinition(HumanBodyBones bone, HumanBodyBones parentBone, string name, Vector3 localPosition)
            {
                Bone = bone;
                ParentBone = parentBone;
                Name = name;
                LocalPosition = localPosition;
            }
        }

        // 身長 ~1.7m 基準の標準ヒューマノイドプロポーション
        private static readonly BoneDefinition[] BoneDefinitions = new[]
        {
            // --- 体幹 ---
            new BoneDefinition(HumanBodyBones.Hips,       HumanBodyBones.LastBone,   "Hips",       new Vector3(0, 1.0f, 0)),
            new BoneDefinition(HumanBodyBones.Spine,      HumanBodyBones.Hips,       "Spine",      new Vector3(0, 0.1f, 0)),
            new BoneDefinition(HumanBodyBones.Chest,      HumanBodyBones.Spine,      "Chest",      new Vector3(0, 0.15f, 0)),
            new BoneDefinition(HumanBodyBones.Neck,       HumanBodyBones.Chest,      "Neck",       new Vector3(0, 0.2f, 0)),
            new BoneDefinition(HumanBodyBones.Head,       HumanBodyBones.Neck,       "Head",       new Vector3(0, 0.1f, 0)),

            // --- 左腕 ---
            new BoneDefinition(HumanBodyBones.LeftShoulder,  HumanBodyBones.Chest,        "LeftShoulder",  new Vector3(-0.05f, 0.18f, 0)),
            new BoneDefinition(HumanBodyBones.LeftUpperArm,  HumanBodyBones.LeftShoulder, "LeftUpperArm",  new Vector3(-0.1f, 0, 0)),
            new BoneDefinition(HumanBodyBones.LeftLowerArm,  HumanBodyBones.LeftUpperArm, "LeftLowerArm",  new Vector3(-0.28f, 0, 0)),
            new BoneDefinition(HumanBodyBones.LeftHand,      HumanBodyBones.LeftLowerArm, "LeftHand",      new Vector3(-0.25f, 0, 0)),

            // --- 右腕 ---
            new BoneDefinition(HumanBodyBones.RightShoulder,  HumanBodyBones.Chest,         "RightShoulder",  new Vector3(0.05f, 0.18f, 0)),
            new BoneDefinition(HumanBodyBones.RightUpperArm,  HumanBodyBones.RightShoulder, "RightUpperArm",  new Vector3(0.1f, 0, 0)),
            new BoneDefinition(HumanBodyBones.RightLowerArm,  HumanBodyBones.RightUpperArm, "RightLowerArm",  new Vector3(0.28f, 0, 0)),
            new BoneDefinition(HumanBodyBones.RightHand,      HumanBodyBones.RightLowerArm, "RightHand",      new Vector3(0.25f, 0, 0)),

            // --- 左脚 ---
            new BoneDefinition(HumanBodyBones.LeftUpperLeg,  HumanBodyBones.Hips,         "LeftUpperLeg",  new Vector3(-0.1f, 0, 0)),
            new BoneDefinition(HumanBodyBones.LeftLowerLeg,  HumanBodyBones.LeftUpperLeg, "LeftLowerLeg",  new Vector3(0, -0.4f, 0)),
            new BoneDefinition(HumanBodyBones.LeftFoot,      HumanBodyBones.LeftLowerLeg, "LeftFoot",      new Vector3(0, -0.42f, 0)),
            new BoneDefinition(HumanBodyBones.LeftToes,      HumanBodyBones.LeftFoot,     "LeftToes",      new Vector3(0, 0, 0.1f)),

            // --- 右脚 ---
            new BoneDefinition(HumanBodyBones.RightUpperLeg,  HumanBodyBones.Hips,          "RightUpperLeg",  new Vector3(0.1f, 0, 0)),
            new BoneDefinition(HumanBodyBones.RightLowerLeg,  HumanBodyBones.RightUpperLeg, "RightLowerLeg",  new Vector3(0, -0.4f, 0)),
            new BoneDefinition(HumanBodyBones.RightFoot,      HumanBodyBones.RightLowerLeg, "RightFoot",      new Vector3(0, -0.42f, 0)),
            new BoneDefinition(HumanBodyBones.RightToes,      HumanBodyBones.RightFoot,     "RightToes",      new Vector3(0, 0, 0.1f)),

            // --- 左手 指 ---
            new BoneDefinition(HumanBodyBones.LeftThumbProximal,       HumanBodyBones.LeftHand,                 "LeftThumbProximal",       new Vector3(-0.02f, 0, 0.02f)),
            new BoneDefinition(HumanBodyBones.LeftThumbIntermediate,   HumanBodyBones.LeftThumbProximal,        "LeftThumbIntermediate",   new Vector3(-0.03f, 0, 0.01f)),
            new BoneDefinition(HumanBodyBones.LeftThumbDistal,         HumanBodyBones.LeftThumbIntermediate,    "LeftThumbDistal",         new Vector3(-0.02f, 0, 0.005f)),
            new BoneDefinition(HumanBodyBones.LeftIndexProximal,       HumanBodyBones.LeftHand,                 "LeftIndexProximal",       new Vector3(-0.08f, 0, 0.015f)),
            new BoneDefinition(HumanBodyBones.LeftIndexIntermediate,   HumanBodyBones.LeftIndexProximal,        "LeftIndexIntermediate",   new Vector3(-0.04f, 0, 0)),
            new BoneDefinition(HumanBodyBones.LeftIndexDistal,         HumanBodyBones.LeftIndexIntermediate,    "LeftIndexDistal",         new Vector3(-0.025f, 0, 0)),
            new BoneDefinition(HumanBodyBones.LeftMiddleProximal,      HumanBodyBones.LeftHand,                 "LeftMiddleProximal",      new Vector3(-0.08f, 0, 0)),
            new BoneDefinition(HumanBodyBones.LeftMiddleIntermediate,  HumanBodyBones.LeftMiddleProximal,       "LeftMiddleIntermediate",  new Vector3(-0.045f, 0, 0)),
            new BoneDefinition(HumanBodyBones.LeftMiddleDistal,        HumanBodyBones.LeftMiddleIntermediate,   "LeftMiddleDistal",        new Vector3(-0.03f, 0, 0)),
            new BoneDefinition(HumanBodyBones.LeftRingProximal,        HumanBodyBones.LeftHand,                 "LeftRingProximal",        new Vector3(-0.075f, 0, -0.015f)),
            new BoneDefinition(HumanBodyBones.LeftRingIntermediate,    HumanBodyBones.LeftRingProximal,         "LeftRingIntermediate",    new Vector3(-0.04f, 0, 0)),
            new BoneDefinition(HumanBodyBones.LeftRingDistal,          HumanBodyBones.LeftRingIntermediate,     "LeftRingDistal",          new Vector3(-0.025f, 0, 0)),
            new BoneDefinition(HumanBodyBones.LeftLittleProximal,      HumanBodyBones.LeftHand,                 "LeftLittleProximal",      new Vector3(-0.065f, 0, -0.03f)),
            new BoneDefinition(HumanBodyBones.LeftLittleIntermediate,  HumanBodyBones.LeftLittleProximal,       "LeftLittleIntermediate",  new Vector3(-0.03f, 0, 0)),
            new BoneDefinition(HumanBodyBones.LeftLittleDistal,        HumanBodyBones.LeftLittleIntermediate,   "LeftLittleDistal",        new Vector3(-0.02f, 0, 0)),

            // --- 右手 指 ---
            new BoneDefinition(HumanBodyBones.RightThumbProximal,      HumanBodyBones.RightHand,                "RightThumbProximal",      new Vector3(0.02f, 0, 0.02f)),
            new BoneDefinition(HumanBodyBones.RightThumbIntermediate,  HumanBodyBones.RightThumbProximal,       "RightThumbIntermediate",  new Vector3(0.03f, 0, 0.01f)),
            new BoneDefinition(HumanBodyBones.RightThumbDistal,        HumanBodyBones.RightThumbIntermediate,   "RightThumbDistal",        new Vector3(0.02f, 0, 0.005f)),
            new BoneDefinition(HumanBodyBones.RightIndexProximal,      HumanBodyBones.RightHand,                "RightIndexProximal",      new Vector3(0.08f, 0, 0.015f)),
            new BoneDefinition(HumanBodyBones.RightIndexIntermediate,  HumanBodyBones.RightIndexProximal,       "RightIndexIntermediate",  new Vector3(0.04f, 0, 0)),
            new BoneDefinition(HumanBodyBones.RightIndexDistal,        HumanBodyBones.RightIndexIntermediate,   "RightIndexDistal",        new Vector3(0.025f, 0, 0)),
            new BoneDefinition(HumanBodyBones.RightMiddleProximal,     HumanBodyBones.RightHand,                "RightMiddleProximal",     new Vector3(0.08f, 0, 0)),
            new BoneDefinition(HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleProximal,      "RightMiddleIntermediate", new Vector3(0.045f, 0, 0)),
            new BoneDefinition(HumanBodyBones.RightMiddleDistal,       HumanBodyBones.RightMiddleIntermediate,  "RightMiddleDistal",       new Vector3(0.03f, 0, 0)),
            new BoneDefinition(HumanBodyBones.RightRingProximal,       HumanBodyBones.RightHand,                "RightRingProximal",       new Vector3(0.075f, 0, -0.015f)),
            new BoneDefinition(HumanBodyBones.RightRingIntermediate,   HumanBodyBones.RightRingProximal,        "RightRingIntermediate",   new Vector3(0.04f, 0, 0)),
            new BoneDefinition(HumanBodyBones.RightRingDistal,         HumanBodyBones.RightRingIntermediate,    "RightRingDistal",         new Vector3(0.025f, 0, 0)),
            new BoneDefinition(HumanBodyBones.RightLittleProximal,     HumanBodyBones.RightHand,                "RightLittleProximal",     new Vector3(0.065f, 0, -0.03f)),
            new BoneDefinition(HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleProximal,      "RightLittleIntermediate", new Vector3(0.03f, 0, 0)),
            new BoneDefinition(HumanBodyBones.RightLittleDistal,       HumanBodyBones.RightLittleIntermediate,  "RightLittleDistal",       new Vector3(0.02f, 0, 0)),
        };
    }
}
