using UnityEngine;

// Marks an area where the map setup is allowed to place enemies. If a scene has at least one EnemyZone,
// enemies are only placed inside the zones (the City keeps its town safe and fills the meadow with slimes).
// Created from the "enemy_zone" lines in Assets/Maps/Data/*.txt.
public class EnemyZone : MonoBehaviour
{
    public Vector2 size = new Vector2(10f, 10f);

    public Bounds WorldBounds => new Bounds(transform.position, new Vector3(size.x, size.y, 100f));

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);
        Gizmos.DrawWireCube(transform.position, new Vector3(size.x, size.y, 0f));
    }
}
