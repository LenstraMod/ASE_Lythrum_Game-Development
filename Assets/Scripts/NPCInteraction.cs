using UnityEngine;

public class NPCInteraction : MonoBehaviour
{
    [Header("NPC Data")]
    public Sprite npcProfile;
    [TextArea(3, 5)]
    public string[] dialogueLines;
    
    private bool playerInRange = false;

    void Update()
    {
        if (playerInRange && Input.GetKeyDown(KeyCode.F))
        {
            // If the UI is off, start dialogue. If it's on, advance to the next line.
            if (!DialogueManager.instance.dialogueUI.activeInHierarchy)
            {
                DialogueManager.instance.StartDialogue(dialogueLines, npcProfile);
            }
            else
            {
                DialogueManager.instance.AdvanceDialogue();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            // Force close dialogue if player walks away mid-conversation
            DialogueManager.instance.dialogueUI.SetActive(false); 
        }
    }
}