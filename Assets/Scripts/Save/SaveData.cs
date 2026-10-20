using System;
using UnityEngine;

// Isi satu slot save. Disimpan sebagai JSON oleh SaveSystem.
[Serializable]
public class SaveData
{
    public string sceneName;
    public Vector3 checkpoint;
    public int health;
    public float playTime;
    public long savedAtTicks;

    public DateTime SavedAt => new DateTime(savedAtTicks);
}
