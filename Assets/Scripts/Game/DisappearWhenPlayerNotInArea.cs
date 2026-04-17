using UnityEngine;
using Projectiles;

/// <summary>
/// Tracks whether the local player is currently standing inside one of the two spawn-area
/// trigger volumes. Exposes <see cref="IsLocalPlayerInside"/> as a static flag used by the
/// Tab-to-swap-character input gate.
///
/// This component lives inside the Gameplay prefab, so <c>GetComponentInParent&lt;Gameplay&gt;()</c>
/// resolves at runtime once the Gameplay network object is spawned.
/// </summary>
public class DisappearWhenPlayerNotInArea : MonoBehaviour
{
    public static bool IsLocalPlayerInside { get; private set; }

    public GameObject objectToDisappear;
    public BoxCollider areaTrigger1;
    public BoxCollider areaTrigger2;

    void Update()
    {
        var gameplay = GetComponentInParent<Gameplay>();
        var activeAgent = gameplay != null ? gameplay.GetLocalPlayer()?.ActiveAgent : null;
        if (activeAgent == null)
        {
            IsLocalPlayerInside = false;
            if (objectToDisappear != null) objectToDisappear.SetActive(false);
            return;
        }

        Vector3 playerPos = activeAgent.transform.position;
        bool inArea = IsPointInsideBox(areaTrigger1, playerPos) || IsPointInsideBox(areaTrigger2, playerPos);
        IsLocalPlayerInside = inArea;

        if (objectToDisappear != null)
            objectToDisappear.SetActive(inArea);
    }

    /// <summary>
    /// Checks if a world-space point is inside a BoxCollider. Rotation-safe (uses the
    /// collider's local space instead of the world-axis-aligned `bounds`).
    /// </summary>
    private static bool IsPointInsideBox(BoxCollider box, Vector3 worldPoint)
    {
        if (box == null || box.enabled == false || box.gameObject.activeInHierarchy == false)
            return false;

        Vector3 local = box.transform.InverseTransformPoint(worldPoint) - box.center;
        Vector3 half = box.size * 0.5f;
        return Mathf.Abs(local.x) <= half.x
            && Mathf.Abs(local.y) <= half.y
            && Mathf.Abs(local.z) <= half.z;
    }

    void OnDisable()
    {
        IsLocalPlayerInside = false;
    }
}
