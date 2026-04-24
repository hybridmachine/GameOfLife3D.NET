using System.Text.Json;

namespace GameOfLife3D.NET.IO;

/// <summary>
/// Persists pattern-library user state (recently-used list, last category) to a
/// small JSON file next to the executable. Failures are silent — losing state
/// is never worth crashing the app.
/// </summary>
public sealed class PatternLibraryState
{
    private const string FileName = "library.state.json";
    private const int MaxRecent = 8;

    public List<string> RecentIds { get; set; } = new();
    public string? LastCategory { get; set; }

    public static string FilePath =>
        Path.Combine(AppContext.BaseDirectory, FileName);

    public static PatternLibraryState Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new PatternLibraryState();

            string json = File.ReadAllText(FilePath);
            var loaded = JsonSerializer.Deserialize<PatternLibraryState>(json);
            return loaded ?? new PatternLibraryState();
        }
        catch
        {
            return new PatternLibraryState();
        }
    }

    public void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"PatternLibraryState: save failed: {ex.Message}");
        }
    }

    public void MarkUsed(string id)
    {
        RecentIds.Remove(id);
        RecentIds.Insert(0, id);
        if (RecentIds.Count > MaxRecent)
            RecentIds.RemoveRange(MaxRecent, RecentIds.Count - MaxRecent);
        Save();
    }
}
