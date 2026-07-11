using System.Text.Json;

namespace GameOfLife3D.NET.IO;

/// <summary>
/// An entry in the user's persisted material library.
/// </summary>
public sealed class MaterialLibraryEntry
{
    public string Name { get; set; } = "";
    public string FilePath { get; set; } = "";

    /// <summary>
    /// Set to <c>false</c> at load time when the file no longer exists.
    /// The entry is kept so the user can see what is missing and remove it.
    /// </summary>
    public bool FileExists { get; set; } = true;
}

/// <summary>
/// Persists the user's PBR material library to a small JSON file next to the
/// executable. Mirrors the pattern used by <see cref="PatternLibraryState"/>;
/// failures are silent so loss of state never crashes the app.
/// </summary>
public sealed class MaterialLibraryState
{
    private const string FileName = "materials.state.json";

    public List<MaterialLibraryEntry> Materials { get; set; } = [];

    public static string FilePath =>
        Path.Combine(AppContext.BaseDirectory, FileName);

    public static MaterialLibraryState Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new MaterialLibraryState();

            string json = File.ReadAllText(FilePath);
            var loaded = JsonSerializer.Deserialize<MaterialLibraryState>(json);
            var state = loaded ?? new MaterialLibraryState();

            // Validate file existence and flag missing entries.
            foreach (var entry in state.Materials)
                entry.FileExists = File.Exists(entry.FilePath);

            return state;
        }
        catch
        {
            return new MaterialLibraryState();
        }
    }

    public void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(this,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"MaterialLibraryState: save failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds a new entry (or updates an existing entry with the same path).
    /// Persists the updated state.
    /// </summary>
    public void AddOrUpdate(string name, string filePath)
    {
        var existing = Materials.FirstOrDefault(m =>
            string.Equals(m.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            existing.Name = name;
            existing.FileExists = true;
        }
        else
        {
            Materials.Add(new MaterialLibraryEntry
            {
                Name = name,
                FilePath = filePath,
                FileExists = true,
            });
        }
        Save();
    }

    /// <summary>Removes the entry with the given file path. Persists the update.</summary>
    public void Remove(string filePath)
    {
        int removed = Materials.RemoveAll(m =>
            string.Equals(m.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        if (removed > 0) Save();
    }

    /// <summary>Renames the entry with the given file path. Persists the update.</summary>
    public void Rename(string filePath, string newName)
    {
        var entry = Materials.FirstOrDefault(m =>
            string.Equals(m.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        if (entry == null) return;
        entry.Name = newName;
        Save();
    }
}
