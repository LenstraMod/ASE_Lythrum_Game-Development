using UnityEngine;
using UnityEngine.Tilemaps;

public class OpenPlate : MonoBehaviour {

    [Header("Doors to Open")]
    public DoorController[] targetDoors; 
    
    [Header("Pressure Plate Tiles")]
    public Tilemap tilemap;
    public TileBase unpressedTile; 
    public TileBase pressedTile;   

    [Header("Who can press this?")]
    public string[] allowedTags = {"Player", "Shadow", "Box"};

    private Vector3Int cellPosition;

    private void Start() {
        cellPosition = tilemap.WorldToCell(transform.position);
        tilemap.SetTile(cellPosition, unpressedTile);
        
        if (GetComponent<SpriteRenderer>() != null) {
            Destroy(GetComponent<SpriteRenderer>());
        }
    }

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
        if (!other.isTrigger && IsAllowedToPress(other.tag)) 
        {
            // 1. Change the pressure plate tile to look "pressed"
            tilemap.SetTile(cellPosition, pressedTile);
            
            // 2. Check the value directly from the DoorController
            foreach (DoorController door in targetDoors) {
                if (door != null) {
                    if (door.isOpen) {
                        door.Close(); // If it's already open, close it
                    } else {
                        door.Open();  // If it's closed, open it
                    }
                }
            }
        }
    }

    void OnTriggerExit2D(Collider2D other) 
    {
        if (!other.isTrigger && IsAllowedToPress(other.tag)) 
        {
            // 3. Change the pressure plate tile back to "unpressed"
            tilemap.SetTile(cellPosition, unpressedTile);
        }
    }
}