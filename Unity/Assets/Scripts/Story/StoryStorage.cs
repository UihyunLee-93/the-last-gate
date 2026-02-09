using System.IO;
using UnityEngine;

public static class StoryStorage
{
    private const string FileName = "story_package.json";

    public static string GetPath()
        => Path.Combine(Application.persistentDataPath, FileName);

    public static void Save(StoryPackage data)
    {
        var json = JsonUtility.ToJson(data, true);
        File.WriteAllText(GetPath(), json);
        Debug.Log($"[StoryStorage] Saved: {GetPath()}");
    }

    public static bool TryLoad(out StoryPackage data)
    {
        var path = GetPath();
        if (!File.Exists(path))
        {
            data = null;
            return false;
        }

        var json = File.ReadAllText(path);
        data = JsonUtility.FromJson<StoryPackage>(json);
        Debug.Log($"[StoryStorage] Loaded: {path}");
        return data != null;
    }

    public static void Delete()
    {
        var path = GetPath();
        if (File.Exists(path)) File.Delete(path);
        Debug.Log($"[StoryStorage] Deleted: {path}");
    }
}