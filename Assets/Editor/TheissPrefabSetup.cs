using UnityEngine;
using UnityEditor;

namespace Projectiles.Editor
{
    /// <summary>
    /// Sets up all ability components on Theiss_Agent.prefab:
    ///   - TheissShieldAbility   (RMB — active when Sugar Rush is OFF)
    ///   - TheissJumpSmashAbility (RMB — active when Sugar Rush is ON)
    ///   - TheissSugarRushAbility (E key)
    ///
    /// Safe to re-run — updates values even if components already exist.
    /// Run via: Tools > Theiss > Setup Abilities on Prefab
    /// Delete this file once the prefab is fully configured.
    /// </summary>
    public static class TheissPrefabSetup
    {
        private const string PrefabPath      = "Assets/Prefabs/Players/Theiss/Theiss_Agent.prefab";
        private const string ShieldPath      = "Assets/Prefabs/Players/Theiss/TheissShield.prefab";
        private const string ShieldPrevPath  = "Assets/Prefabs/Players/Theiss/TheissShieldPreview.prefab";

        [MenuItem("Tools/Theiss/Setup Abilities on Prefab")]
        public static void SetupAbilities()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[TheissPrefabSetup] Could not find prefab at: {PrefabPath}");
                return;
            }

            var shieldWallPrefab    = AssetDatabase.LoadAssetAtPath<TheissShieldWall>(ShieldPath);
            var shieldPreviewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShieldPrevPath);

            using (var scope = new PrefabUtility.EditPrefabContentsScope(PrefabPath))
            {
                var root = scope.prefabContentsRoot;

                // --- TheissShieldAbility (RMB, normal mode) ---
                var shield = root.GetComponent<TheissShieldAbility>();
                if (shield == null)
                {
                    shield = root.AddComponent<TheissShieldAbility>();
                    Debug.Log("[TheissPrefabSetup] Added TheissShieldAbility.");
                }
                else
                {
                    Debug.Log("[TheissPrefabSetup] TheissShieldAbility already present — updating values.");
                }
                var soShield = new SerializedObject(shield);
                soShield.FindProperty("_cooldown").floatValue           = 12f;
                soShield.FindProperty("_spawnDistance").floatValue      = 2.5f;
                soShield.FindProperty("_spawnHeightOffset").floatValue  = 0f;
                soShield.FindProperty("_previewAlpha").floatValue       = 0.35f;
                if (shieldWallPrefab != null)
                    soShield.FindProperty("_shieldPrefab").objectReferenceValue = shieldWallPrefab;
                else
                    Debug.LogWarning($"[TheissPrefabSetup] Could not find shield wall prefab at: {ShieldPath}");
                if (shieldPreviewPrefab != null)
                    soShield.FindProperty("_shieldPreviewPrefab").objectReferenceValue = shieldPreviewPrefab;
                else
                    Debug.LogWarning($"[TheissPrefabSetup] Could not find shield preview prefab at: {ShieldPrevPath}");
                soShield.ApplyModifiedProperties();

                // --- TheissJumpSmashAbility (RMB, Sugar Rush mode) ---
                var jumpSmash = root.GetComponent<TheissJumpSmashAbility>();
                if (jumpSmash == null)
                {
                    jumpSmash = root.AddComponent<TheissJumpSmashAbility>();
                    Debug.Log("[TheissPrefabSetup] Added TheissJumpSmashAbility.");
                }
                else
                {
                    Debug.Log("[TheissPrefabSetup] TheissJumpSmashAbility already present — updating values.");
                }
                var soJump = new SerializedObject(jumpSmash);
                soJump.FindProperty("_cooldown").floatValue         = 15f;
                soJump.FindProperty("_launchImpulse").floatValue    = 9f;
                soJump.FindProperty("_hangGravity").floatValue      = 1.5f;
                soJump.FindProperty("_hangDuration").floatValue     = 0.6f;
                soJump.FindProperty("_descendGravity").floatValue   = 55f;
                soJump.FindProperty("_maxDescendTime").floatValue   = 5f;
                soJump.FindProperty("_smashRadius").floatValue      = 5f;
                soJump.FindProperty("_smashDamage").floatValue      = 80f;
                soJump.FindProperty("_knockbackForce").floatValue   = 8f;
                soJump.FindProperty("_verticalKnockback").floatValue = 4f;
                soJump.ApplyModifiedProperties();

                // --- TheissSugarRushAbility (E key) ---
                var sugarRush = root.GetComponent<TheissSugarRushAbility>();
                if (sugarRush == null)
                {
                    sugarRush = root.AddComponent<TheissSugarRushAbility>();
                    Debug.Log("[TheissPrefabSetup] Added TheissSugarRushAbility.");
                }
                else
                {
                    Debug.Log("[TheissPrefabSetup] TheissSugarRushAbility already present — updating values.");
                }
                var soSugar = new SerializedObject(sugarRush);
                soSugar.FindProperty("_cooldown").floatValue         = 20f;
                soSugar.FindProperty("_duration").floatValue         = 10f;
                soSugar.FindProperty("_shieldHpFraction").floatValue = 0.3f;
                soSugar.FindProperty("_damageReduction").floatValue  = 0.2f;
                soSugar.FindProperty("_speedMultiplier").floatValue  = 1.15f;
                soSugar.ApplyModifiedProperties();
            }

            Debug.Log("[TheissPrefabSetup] Done! Theiss_Agent prefab updated successfully.");
        }
    }
}
