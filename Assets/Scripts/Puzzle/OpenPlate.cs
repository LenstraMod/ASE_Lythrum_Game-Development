using UnityEngine;
using UnityEngine.Tilemaps; // Needed for Tilemaps

public class OpenPlate : MonoBehaviour {

    [Header("Doors to Open")]
    public DoorController[] targetDoors; 
    
    [Header("Pressure Plate Tiles")]
    public Tilemap tilemap;
    public TileBase unpressedTile; // The tile when nothing is on it
    public TileBase pressedTile;   // The tile when it is stepped on

    [Header("Who can press this?")]
    [Tooltip("Type the tags of objects allowed to press this plate (e.g., Player, Box, Enemy)")]
    public string[] allowedTags = {"Player", "Shadow", "Box"};

    private Vector3Int cellPosition;

    private bool doorOpened = false; 

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
            // 1. Change the pressure plate tile to look "pressed"
                tilemap.SetTile(cellPosition, pressedTile);
            
            if (!doorOpened) 
            {
                // 2. Open all connected doors
                foreach (DoorController door in targetDoors)
                {
                    if (door != null) door.Open();
                }
                doorOpened = true;
            } else 
            {
                 // 2. Close all connected doors
                foreach (DoorController door in targetDoors)
                {
                    if (door != null) door.Close();
                }
                doorOpened = false;
            }
        }
    }

    void OnTriggerExit2D(Collider2D other) 
    {
        if (!other.isTrigger && IsAllowedToPress(other.tag)) 
        {
            //Change the pressure plate tile back to "unpressed"
            tilemap.SetTile(cellPosition, unpressedTile);
            
        }
    }
}