using UnityEngine;

[System.Serializable]
public struct PlayerSnapshot
{
    //Primary Key
    public float timestamp;

    //Position
    public Vector3 position;

    //Buttons (If needed later)
    public bool isAttacking;
    public bool isInteracting;

    //Animation parameters
    public float lastInputX;
    public float lastInputY;
    public float inputX;
    public float inputY;
    public bool isWalking;
}