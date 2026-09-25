using System.IO;

namespace FortniteTracker.Desktop;

/// <summary>Your goals, stored as-is in goals.json (the UI owns the format and computes progress).</summary>
public sealed class GoalStore
{
    private readonly string _path;

    public GoalStore(string directory)
    {
        _path = Path.Combine(directory, "goals.json");
        try
        {
            if (File.Exists(_path)) Json = File.ReadAllText(_path);
        }
        catch (IOException)
        {
        }
    }

    public string Json { get; private set; } = "[]";

    public void Save(string json)
    {
        Json = json;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, json);
    }
}
