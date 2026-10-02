using UnityEngine;

public class Spike : MonoBehaviour
{
    public int damageAmount = 1;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            PlayerHealth player = collision.GetComponent<PlayerHealth>();
            
            if (player != null)
            {
                player.TakeDamage(damageAmount);
            }
        }
    }
}