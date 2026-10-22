using UnityEngine;
using UnityEngine.Tilemaps; // Needed for Tilemaps

public class PressurePlate : MonoBehaviour {

    [Header("Doors to Open")]
    public DoorController[] targetDoors; 
    
    [Header("Pressure Plate Tiles")]
    public Tilemap tilemap;
    public TileBase unpressedTile; // The tile when nothing is on it
    public TileBase pressedTile;   // The tile when it is stepped on

    [Header("Who can press this?")]
    [Tooltip("Type the tags of objects allowed to press this plate (e.g., Player, Box, Enemy)")]
    public string[] allowedTags = {"Player", "Shadow", "Box"};

    private int objectsOnPlate = 0; 
    private Vector3Int cellPosition;

    private void Start() {
        // Find exactly which grid cell this plate is sitting on
        cellPosition = tilemap.WorldToCell(transform.position);

        // Draw the unpressed plate tile immediately when the game starts
        tilemap.SetTile(cellPosition, unpressedTile);

        //Destroy Scene Preview
        Destroy(GetComponent<SpriteRenderer>());    
    }

    // A helper rule to check if the object has a matching tag
    private bool IsAllowedToPress(string tagToCheck) {
        foreach (string tag in allowedTags) {
            if (tagToCheck == tag) {
                return true;
            }
        }
        return false;
    }

    void OnTriggerEnter2D(Collider2D other) 
    {
        // Check if the object has an allowed tag AND is a solid object (not another trigger)
        if (!other.isTrigger && IsAllowedToPress(other.tag)) 
        {
            objectsOnPlate++;
            
            if (objectsOnPlate == 1) 
            {
                // 1. Change the pressure plate tile to look "pressed"
                tilemap.SetTile(cellPosition, pressedTile);

                // 2. Open all connected doors
                foreach (DoorController door in targetDoors)
                {
                    if (door != null) door.Open();
                }
            }
        }
    }

    void OnTriggerExit2D(Collider2D other) 
    {
        if (!other.isTrigger && IsAllowedToPress(other.tag)) 
        {
            objectsOnPlate--;
            
            if (objectsOnPlate <= 0) 
            {
                objectsOnPlate = 0; // Reset to 0 just in case
                
                // 1. Change the pressure plate tile back to "unpressed"
                tilemap.SetTile(cellPosition, unpressedTile);

                // 2. Close all connected doors
                foreach (DoorController door in targetDoors)
                {
                    if (door != null) door.Close();
                }
            }
        }
    }
}