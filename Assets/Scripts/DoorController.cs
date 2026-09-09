using UnityEngine;

public class DoorController : MonoBehaviour {
    public Collider2D doorCollider;
    public SpriteRenderer doorSprite;
    
    public Sprite openSprite;
    public Sprite closedSprite;

    public void Open() {
        doorCollider.enabled = false; // Let player walk through
        doorSprite.sprite = openSprite;
    }

    public void Close() {
        doorCollider.enabled = true; // Block the player
        doorSprite.sprite = closedSprite;
    }
}