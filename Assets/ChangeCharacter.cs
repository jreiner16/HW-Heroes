using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChangeCharacter : MonoBehaviour
{
    public Transform[] characters;
    private int currentCharacter = 0;

    public void NextCharacter(bool previous)
    {
        if (previous)
            currentCharacter--;
        else
            currentCharacter++;
        if (currentCharacter >= characters.Length)
            currentCharacter = 0;
        else if (currentCharacter < 0)
            currentCharacter = characters.Length - 1;
        foreach (Transform character in characters)
        {
            character.gameObject.SetActive(false);
        }
        characters[currentCharacter].gameObject.SetActive(true);
    }
}
