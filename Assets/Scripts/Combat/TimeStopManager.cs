using System;
using UnityEngine;

// Global "is time stopped" flag. Anything with a TimeStoppable component listens to it.
// Time.timeScale is NOT used, so the player (and anything without TimeStoppable) keeps moving.
public static class TimeStopManager
{
    public static bool IsTimeStopped { get; private set; }

    // true = time just stopped, false = time just resumed
    public static event Action<bool> TimeStopChanged;

    public static void SetTimeStopped(bool stopped)
    {
        if (IsTimeStopped == stopped) return;

        IsTimeStopped = stopped;
        TimeStopChanged?.Invoke(stopped);
    }

    // Reset static state when entering play mode (needed if Domain Reload is disabled).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        IsTimeStopped = false;
        TimeStopChanged = null;
    }
}
