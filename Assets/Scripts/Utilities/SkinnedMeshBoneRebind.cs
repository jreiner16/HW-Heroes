using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// At Awake, finds all SkinnedMeshRenderers in this GameObject's children and
/// rebinds their bones to matching transforms found in the parent character's
/// animated skeleton (located via the nearest parent Animator).
/// Place this on the root of any clothing/accessory prefab attached to a character.
///
/// Uses Animator.GetBoneTransform() rather than a hierarchy walk so that duplicate
/// bone names from other clothing sub-skeletons are never accidentally chosen.
/// </summary>
public class SkinnedMeshBoneRebind : MonoBehaviour
{
    void Awake()
    {
        var animator = GetComponentInParent<Animator>();
        if (animator == null) return;

        // Build the map exclusively from the humanoid avatar's defined bones.
        // This guarantees we always target the character's animated transforms,
        // even when sibling clothing prefabs contain skeleton copies with identical bone names.
        var boneMap = new Dictionary<string, Transform>();
        foreach (HumanBodyBones bone in System.Enum.GetValues(typeof(HumanBodyBones)))
        {
            if (bone == HumanBodyBones.LastBone) continue;
            var t = animator.GetBoneTransform(bone);
            if (t != null && !boneMap.ContainsKey(t.name))
                boneMap[t.name] = t;
        }

        // Also register the animator root itself in case it's referenced as rootBone
        if (!boneMap.ContainsKey(animator.transform.name))
            boneMap[animator.transform.name] = animator.transform;

        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var newBones = new Transform[smr.bones.Length];
            for (int i = 0; i < smr.bones.Length; i++)
            {
                if (smr.bones[i] != null && boneMap.TryGetValue(smr.bones[i].name, out var match))
                    newBones[i] = match;
                else
                    newBones[i] = smr.bones[i];
            }
            smr.bones = newBones;

            if (smr.rootBone != null && boneMap.TryGetValue(smr.rootBone.name, out var rootMatch))
                smr.rootBone = rootMatch;
        }
    }
}
