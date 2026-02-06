using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Projectiles;

public class DisappearWhenPlayerNotInArea : MonoBehaviour
{
    public GameObject objectToDisappear;
    public BoxCollider areaTrigger;

    private Transform player;

    void Update() {
        player = gameObject.GetComponentInParent<Gameplay>().GetLocalPlayer().GetComponent<Player>().ActiveAgent.transform;


        Debug.Log(player.position);
        if (areaTrigger.bounds.Contains(player.position)) {
            objectToDisappear.SetActive(true);
        } else {
            objectToDisappear.SetActive(false);
        }
    }
}
