using System.Collections.Generic;
using UnityEngine;

public class ShadowPlayback : MonoBehaviour
{
    private List<PlayerSnapshot> playbackData;
    private bool isPlaying = false;
    private float playbackTimer = 0f;
    private int currentIndex = 0;

    public Animator shadowAnimator;

    public void Initialize(List<PlayerSnapshot> data)
    {
        playbackData = data;
        isPlaying = true;
        playbackTimer = 0f;
        currentIndex = 0;
    }

    void Update()
    {
        if (!isPlaying || playbackData == null || playbackData.Count == 0) return;

        playbackTimer += Time.deltaTime;

        // Fast-forward through the list until we match the current playback time
        while (currentIndex < playbackData.Count - 1 && playbackData[currentIndex + 1].timestamp <= playbackTimer)
        {
            currentIndex++;
            ExecuteActions(playbackData[currentIndex]);
        }

        // Smoothly interpolate position between the current snapshot and the next one
        if (currentIndex < playbackData.Count - 1)
        {
            PlayerSnapshot currentSnap = playbackData[currentIndex];
            PlayerSnapshot nextSnap = playbackData[currentIndex + 1];
            
            float timeBetween = nextSnap.timestamp - currentSnap.timestamp;
            float timePassed = playbackTimer - currentSnap.timestamp;
            float lerpPercent = timePassed / timeBetween;

            transform.position = Vector3.Lerp(currentSnap.position, nextSnap.position, lerpPercent);

            shadowAnimator.SetFloat("LastInputX", currentSnap.lastInputX);
            shadowAnimator.SetFloat("LastInputY", currentSnap.lastInputY);
            shadowAnimator.SetFloat("InputX", currentSnap.inputX);
            shadowAnimator.SetFloat("InputY", currentSnap.inputY);
            shadowAnimator.SetBool("isWalking", currentSnap.isWalking);
        }
        else
        {
            // Playback finished
            Destroy(gameObject);
        }
    }

    void ExecuteActions(PlayerSnapshot snapshot)
    {
        if (snapshot.isAttacking)
        {
            Debug.Log("Shadow is attacking!");
            // Trigger shadow attack animation/logic here
        }

        if (snapshot.isInteracting)
        {
            Debug.Log("Shadow is interacting!");
            // Trigger shadow interact logic here
        }
    }
}