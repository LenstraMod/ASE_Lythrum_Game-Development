using UnityEngine;

// A collectible memory shard with a unique id. Bobs up and down, and starts the pickup sequence
// (ShardManager) when the player walks into its trigger. Removes itself if it was already collected.
[RequireComponent(typeof(Collider2D))]
public class MemoryShard : MonoBehaviour
{
    [SerializeField] string id = "";              // unique, e.g. "town_01" or "dungeon_boss_drop"
    [SerializeField] float bobHeight = 0.12f;
    [SerializeField] float bobSpeed = 2f;
    [SerializeField] float vanishTime = 0.25f;

    public string Id => id;

    Vector3 basePosition;

    public void SetId(string newId) => id = newId;

    void Reset() => GetComponent<Collider2D>().isTrigger = true;

    void Start()
    {
        basePosition = transform.position;

        if (string.IsNullOrEmpty(id)) Debug.LogWarning("[MemoryShard] " + name + " has no id.", this);
        else if (ShardManager.IsCollected(id)) Destroy(gameObject);
    }

    void Update()
    {
        transform.position = basePosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")) ShardManager.Pickup(this);
    }

    // Called by the manager: no more triggers while the camera sequence runs, and when it is done the shard shrinks away.
    public void DisableTrigger() => GetComponent<Collider2D>().enabled = false;
    public void Consume() => StartCoroutine(VanishRoutine());

    System.Collections.IEnumerator VanishRoutine()
    {
        Vector3 start = transform.localScale;
        for (float t = 0f; t < vanishTime; t += Time.deltaTime)
        {
            transform.localScale = Vector3.Lerp(start, Vector3.zero, Mathf.SmoothStep(0f, 1f, t / vanishTime));
            yield return null;
        }
        Destroy(gameObject);
    }
}
