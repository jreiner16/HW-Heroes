using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Projectiles;

public class DisappearWhenPlayerNotInArea : MonoBehaviour
{
    public GameObject objectToDisappear;
    public BoxCollider areaTrigger1;
    public BoxCollider areaTrigger2;

    private Transform player;

    void Update() {
        player = gameObject.GetComponentInParent<Gameplay>().GetLocalPlayer().GetComponent<Player>().ActiveAgent.transform;


        if (areaTrigger1.bounds.Contains(player.position) || areaTrigger2.bounds.Contains(player.position)) {
            objectToDisappear.SetActive(true);
        } else {
            objectToDisappear.SetActive(false);
        }
    }
}
