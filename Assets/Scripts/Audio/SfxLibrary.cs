using System;
using UnityEngine;

// Every sound the game can play by name. Add a new entry here, then map clips to it in the
// SfxLibrary asset (Assets/Resources/SfxLibrary.asset, built by Lythrum > Setup > Audio).
public enum Sfx
{
    PlayerFootstep,
    PlayerSlash,
    PlayerAreaSlash,
    PlayerHurt,
    PlayerDeath,
    HitImpact,
    HitImpactHeavy,
    EnemyAttack,
    EnemyDeath,
    TimeStopStart,
    TimeStopEnd,
    DoorOpen,
    DoorClose,
    PlatePress,
    PlateRelease,
    SpikeHit,
    Checkpoint,
    RecordStart,
    ShadowSpawn,
    ShadowEnd,
    PlayerSlashHeavy,
    BossSlam,
    BossQuake,
    BossRoar,
    BossFire,
    BossFearCatch,
}

[CreateAssetMenu(menuName = "Lythrum/Sfx Library")]
public class SfxLibrary : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public Sfx id;
        [Tooltip("One is picked at random each time, so repeated sounds don't feel robotic.")]
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Pitch is randomised between these two values.")]
        public Vector2 pitchRange = new Vector2(0.95f, 1.05f);
    }

    public Entry[] entries;
}
