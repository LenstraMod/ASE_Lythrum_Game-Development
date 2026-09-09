using System.Collections.Generic;
using UnityEngine;

public class PlayerRecord : MonoBehaviour
{
    public GameObject shadowPrefab;
    public float recordDuration = 10f; 
    public Animator playerAnimator;
    
    private List<PlayerSnapshot> recordedData = new List<PlayerSnapshot>();
    private bool isRecording = false;
    private float recordTimer = 0f;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E) && !isRecording)
        {
            StartRecording();
        }

        if (isRecording)
        {
            recordTimer += Time.deltaTime;
            
            // Capture the current frame's data
            PlayerSnapshot currentFrame = new PlayerSnapshot
            {
                // Primary Key
                timestamp = recordTimer,

                // Position
                position = transform.position,

                // Button Pressed
                isAttacking = Input.GetButtonDown("Fire1"), // Replace with your attack input
                isInteracting = Input.GetKeyDown(KeyCode.F), // Replace with your interact input

                // Animation parameters
                lastInputX = playerAnimator.GetFloat("LastInputX"),
                lastInputY = playerAnimator.GetFloat("LastInputY"),
                inputX = playerAnimator.GetFloat("InputX"),
                inputY = playerAnimator.GetFloat("InputY"),
                isWalking = playerAnimator.GetBool("isWalking")
            };
            
            recordedData.Add(currentFrame);

            if (recordTimer >= recordDuration)
            {
                StopRecordingAndSpawnShadow();
            }
        }
    }

    void StartRecording()
    {
        isRecording = true;
        recordTimer = 0f;
        recordedData.Clear();
        Debug.Log("Recording started!");
    }

    void StopRecordingAndSpawnShadow()
    {
        isRecording = false;
        Debug.Log("Recording stopped. Spawning shadow...");
        
        GameObject shadow = Instantiate(shadowPrefab, recordedData[0].position, Quaternion.identity);
        shadow.GetComponent<ShadowPlayback>().Initialize(new List<PlayerSnapshot>(recordedData));
    }
}