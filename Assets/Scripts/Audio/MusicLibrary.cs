using System;
using UnityEngine;

// Which music plays in which scene. Built by Lythrum > Setup > Audio (Assets/Resources/MusicLibrary.asset);
// each scene can play several looping layers at once (e.g. a drone plus room ambience).
[CreateAssetMenu(menuName = "Lythrum/Music Library")]
public class MusicLibrary : ScriptableObject
{
    [Serializable]
    public class Layer
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.5f;
        [Tooltip("Tick for room/wind/drone sounds: the layer then follows the Ambience slider instead of Music.")]
        public bool ambience;
    }

    [Serializable]
    public class SceneMusic
    {
        [Tooltip("Scene name exactly as in the Build Settings, e.g. Desert.")]
        public string sceneName;
        public Layer[] layers;
        [Tooltip("Seconds to crossfade from the previous scene's music.")]
        public float fadeSeconds = 2f;
    }

    public SceneMusic[] scenes;
}
