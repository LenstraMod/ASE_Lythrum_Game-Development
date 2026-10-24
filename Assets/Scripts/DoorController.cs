using UnityEngine;
using UnityEngine.Tilemaps;

public class DoorController : MonoBehaviour {
    [Header("Tilemap Settings")]
    public Tilemap tilemap;
    public TileBase openTile;
    public TileBase closedTile;

    [Header("Key Settings")]
    public bool requiresKey;     // The Checkbox!
    public Key requiredKey;      // The slot to drag your Key object into

    private Vector3Int cellPosition;
    public bool isOpen = false;

    private void Start() {
        cellPosition = tilemap.WorldToCell(transform.position);
        
        // (Optional) Remove the dummy sprite you used for level design when the game starts
        if (GetComponent<SpriteRenderer>() != null) {
            Destroy(GetComponent<SpriteRenderer>());
        }

        // Draw the closed door tile immediately when the game starts
        tilemap.SetTile(cellPosition, closedTile); 
    }

    // This allows the door to detect when the player bumps into it
    private void OnTriggerEnter2D(Collider2D other) {
        if (other.CompareTag("Player") && !isOpen) {
            
            // If the door needs a key, check if the player has the right one
            if (requiresKey) {
                if (requiredKey != null && requiredKey.isCollected) {
                    // Unlock success! Destroy the key so it's "used" and open the door
                    Destroy(requiredKey.gameObject);
                    Open();
                }
            }
        }
    }

    public void Open() {
        isOpen = true;
        tilemap.SetTile(cellPosition, openTile);
        if (!isOpen) AudioManager.Play(Sfx.DoorOpen);
    }

    public void Close() {
        isOpen = false;
        tilemap.SetTile(cellPosition, closedTile);
        if (isOpen) AudioManager.Play(Sfx.DoorClose);
    }
}