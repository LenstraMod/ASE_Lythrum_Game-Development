using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager instance;

    [Header("UI Elements")]
    public GameObject dialogueUI;
    public Image profileImage;
    public TextMeshProUGUI dialogueText;
    
    [Header("Settings")]
    public float typingSpeed = 0.05f;

    private string[] currentLines;
    private int currentLineIndex;
    private bool isTyping;

    void Awake()
    {
        // Singleton pattern to access this script from anywhere
        if (instance == null) instance = this;
    }

    public void StartDialogue(string[] lines, Sprite profile)
    {
        currentLines = lines;
        currentLineIndex = 0;
        profileImage.sprite = profile;
        dialogueUI.SetActive(true);
        StartCoroutine(TypeLine());
    }

    public void AdvanceDialogue()
    {
        // Prevent skipping to the next line while still typing
        if (isTyping) return; 

        currentLineIndex++;
        if (currentLineIndex < currentLines.Length)
        {
            StartCoroutine(TypeLine());
        }
        else
        {
            dialogueUI.SetActive(false); // Close dialogue when out of lines
        }
    }

    IEnumerator TypeLine()
    {
        isTyping = true;
        dialogueText.text = "";
        
        foreach (char letter in currentLines[currentLineIndex].ToCharArray())
        {
            dialogueText.text += letter;
            yield return new WaitForSeconds(typingSpeed);
        }
        
        isTyping = false;
    }
}