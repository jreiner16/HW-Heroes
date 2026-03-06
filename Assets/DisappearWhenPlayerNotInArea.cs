using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Projectiles;

public class DisappearWhenPlayerNotInArea : MonoBehaviour
{
    public static bool IsLocalPlayerInside { get; private set; }

    public GameObject objectToDisappear;
    public BoxCollider areaTrigger1;
    public BoxCollider areaTrigger2;

    private Transform player;

    void Update() {
        var activeAgent = gameObject.GetComponentInParent<Gameplay>().GetLocalPlayer()?.GetComponent<Player>()?.ActiveAgent;
        if (activeAgent == null)
        {
            IsLocalPlayerInside = false;
            if (objectToDisappear != null) objectToDisappear.SetActive(false);
            return;
        }

        player = activeAgent.transform;
        bool inArea = areaTrigger1.bounds.Contains(player.position) || areaTrigger2.bounds.Contains(player.position);
        IsLocalPlayerInside = inArea;

        if (objectToDisappear != null)
            objectToDisappear.SetActive(inArea);
    }

    void OnDisable()
    {
        IsLocalPlayerInside = false;
    }
}
