using UnityEngine;
using UnityEditor;

namespace Projectiles.Editor
{
    /// <summary>
    /// One-shot editor utility that swaps Theiss_Agent's ability components:
    ///   - Removes  TheissShieldAbility   (firewall — replaced by Jump Smash)
    ///   - Adds     TheissJumpSmashAbility (Q key)
    ///   - Adds     TheissSugarRushAbility (E key)
    ///
    /// Run via: Tools > Theiss > Setup Abilities on Prefab
    /// Safe to re-run — it skips steps that are already done.
    /// Delete this file once the prefab is updated.
    /// </summary>
    public static class TheissPrefabSetup
    {
        private const string PrefabPath = "Assets/Prefabs/Players/Theiss/Theiss_Agent.prefab";

        [MenuItem("Tools/Theiss/Setup Abilities on Prefab")]
        public static void SetupAbilities()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[TheissPrefabSetup] Could not find prefab at: {PrefabPath}");
                return;
            }

            using (var scope = new PrefabUtility.EditPrefabContentsScope(PrefabPath))
            {
                var root = scope.prefabContentsRoot;

                // --- Remove TheissShieldAbility (the old firewall) ---
                var shield = root.GetComponent<TheissShieldAbility>();
                if (shield != null)
                {
                    Object.DestroyImmediate(shield);
                    Debug.Log("[TheissPrefabSetup] Removed TheissShieldAbility.");
                }
                else
                {
                    Debug.Log("[TheissPrefabSetup] TheissShieldAbility not present — skipping removal.");
                }

                // --- Add TheissJumpSmashAbility (Q key) ---
                if (root.GetComponent<TheissJumpSmashAbility>() == null)
                {
                    root.AddComponent<TheissJumpSmashAbility>();
                    Debug.Log("[TheissPrefabSetup] Added TheissJumpSmashAbility.");
                }
                else
                {
                    Debug.Log("[TheissPrefabSetup] TheissJumpSmashAbility already present — skipping.");
                }

                // --- Add TheissSugarRushAbility (E key) ---
                if (root.GetComponent<TheissSugarRushAbility>() == null)
                {
                    root.AddComponent<TheissSugarRushAbility>();
                    Debug.Log("[TheissPrefabSetup] Added TheissSugarRushAbility.");
                }
                else
                {
                    Debug.Log("[TheissPrefabSetup] TheissSugarRushAbility already present — skipping.");
                }
            }

            Debug.Log("[TheissPrefabSetup] Done! Theiss_Agent prefab updated successfully.");
        }
    }
}
