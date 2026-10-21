using UnityEngine;

public class DoorController : MonoBehaviour {
    public Collider2D doorCollider;
    public SpriteRenderer doorSprite;
    
    public Sprite openSprite;
    public Sprite closedSprite;

    // Open()/Close() can be called every frame (PressurePlate does), so only play the sound when the state changes.
    private bool isOpen;

    public void Open() {
        doorCollider.enabled = false; // Let player walk through
        doorSprite.sprite = openSprite;
        if (!isOpen) AudioManager.Play(Sfx.DoorOpen);
        isOpen = true;
    }

    public void Close() {
        doorCollider.enabled = true; // Block the player
        doorSprite.sprite = closedSprite;
        if (isOpen) AudioManager.Play(Sfx.DoorClose);
        isOpen = false;
    }
}