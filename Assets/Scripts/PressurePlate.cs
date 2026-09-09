using UnityEngine;

public class PressurePlate : MonoBehaviour {

    public DoorController targetDoor; 
    private int objectsOnPlate = 0; 

    void OnTriggerEnter2D(Collider2D other) 
    {
        if (other.CompareTag("Player")) 
        {
            objectsOnPlate++;
            targetDoor.Open();
        }
    }

    void OnTriggerExit2D(Collider2D other) 
    {
        if (other.CompareTag("Player")) 
        {
            objectsOnPlate--;
        }
    }

    private void Update()
    {   
        if (objectsOnPlate <= 0)
        {
            targetDoor.Close();
        }
    }
}