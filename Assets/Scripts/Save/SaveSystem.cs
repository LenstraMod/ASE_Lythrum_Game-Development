using System;
using System.IO;
using UnityEngine;

// Baca/tulis slot save ke file JSON di Application.persistentDataPath.
public static class SaveSystem
{
    public const int SlotCount = 3;

    static string PathFor(int slot) => Path.Combine(Application.persistentDataPath, "save_slot" + (slot + 1) + ".json");

    public static bool Exists(int slot) => File.Exists(PathFor(slot));

    public static SaveData Load(int slot)
    {
        string path = PathFor(slot);
        if (!File.Exists(path)) return null;

        try
        {
            return JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogWarning("Gagal membaca save slot " + (slot + 1) + ": " + e.Message);
            return null;
        }
    }

    public static bool Write(int slot, SaveData data)
    {
        string path = PathFor(slot);
        string temp = path + ".tmp";

        try
        {
            // Tulis ke file sementara dulu supaya save lama tidak rusak kalau penulisan gagal di tengah jalan.
            File.WriteAllText(temp, JsonUtility.ToJson(data, true));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("Gagal menyimpan slot " + (slot + 1) + ": " + e.Message);
            return false;
        }
    }

    public static void Delete(int slot)
    {
        string path = PathFor(slot);
        if (File.Exists(path)) File.Delete(path);
    }

    public static bool HasAnySave() => LatestSlot() >= 0;

    // Slot dengan waktu save paling baru, atau -1 kalau belum ada save sama sekali.
    public static int LatestSlot()
    {
        int latest = -1;
        long latestTicks = long.MinValue;

        for (int i = 0; i < SlotCount; i++)
        {
            SaveData data = Load(i);
            if (data != null && data.savedAtTicks > latestTicks)
            {
                latest = i;
                latestTicks = data.savedAtTicks;
            }
        }

        return latest;
    }
}
