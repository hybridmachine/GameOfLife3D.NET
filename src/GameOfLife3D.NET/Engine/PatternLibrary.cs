using System.Reflection;

namespace GameOfLife3D.NET.Engine;

/// <summary>
/// Indexes all bundled RLE patterns shipped as embedded resources under
/// GameOfLife3D.NET.Patterns.*.rle. Headers are parsed eagerly at startup
/// (cheap); full cell grids are decoded lazily on first load so 100+ patterns
/// don't pay memory cost upfront.
/// </summary>
public sealed class PatternLibrary
{
    // Match any embedded resource under a "Patterns." logical path ending in .rle.
    // Resource names look like "GameOfLife3D.NET.Patterns.block.rle".
    private const string ResourceMarker = ".Patterns.";
    private const string ResourceSuffix = ".rle";
    private const string DefaultCategory = "misc";

    private readonly Dictionary<string, PatternMetadata> _byId = new();
    private readonly Dictionary<string, bool[,]> _patternCache = new();
    private readonly List<PatternMetadata> _ordered = new();
    private readonly Assembly _assembly;

    public IReadOnlyList<PatternMetadata> All => _ordered;

    public IReadOnlyList<string> Categories { get; private set; } = Array.Empty<string>();

    public PatternLibrary(Assembly? assembly = null)
    {
        _assembly = assembly ?? Assembly.GetExecutingAssembly();
        LoadFromEmbeddedResources();
    }

    private void LoadFromEmbeddedResources()
    {
        foreach (string resourceName in _assembly.GetManifestResourceNames())
        {
            if (!resourceName.EndsWith(ResourceSuffix, StringComparison.Ordinal))
                continue;
            if (resourceName.IndexOf(ResourceMarker, StringComparison.Ordinal) < 0)
                continue;

            try
            {
                string rle = ReadResource(resourceName);
                var header = PatternLoader.ParseRleHeaderOnly(rle);

                if (header.Width <= 0 || header.Height <= 0)
                    continue;

                string id = DeriveId(resourceName);
                string displayName = header.Name ?? ToTitleCase(id);
                string category = header.Category ?? DefaultCategory;

                var metadata = new PatternMetadata(
                    id,
                    displayName,
                    category,
                    header.Width,
                    header.Height,
                    header.Period,
                    header.Author,
                    header.Description,
                    resourceName);

                _byId[id] = metadata;
                _ordered.Add(metadata);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"PatternLibrary: failed to index {resourceName}: {ex.Message}");
            }
        }

        _ordered.Sort((a, b) =>
        {
            int catCmp = string.Compare(a.Category, b.Category, StringComparison.OrdinalIgnoreCase);
            return catCmp != 0 ? catCmp : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        Categories = _ordered
            .Select(p => p.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public PatternMetadata? Get(string id) => _byId.TryGetValue(id, out var m) ? m : null;

    /// <summary>
    /// Returns the decoded cell grid for a pattern, loading and caching it on
    /// first call. Returns null if the pattern is missing or malformed.
    /// </summary>
    public bool[,]? GetPattern(string id)
    {
        if (_patternCache.TryGetValue(id, out var cached))
            return cached;

        if (!_byId.TryGetValue(id, out var metadata))
            return null;

        try
        {
            string rle = ReadResource(metadata.ResourcePath);
            var (pattern, _) = PatternLoader.ParseRLEWithHeader(rle);
            _patternCache[id] = pattern;
            return pattern;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"PatternLibrary: failed to load pattern {id}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Returns patterns matching all of the given filters. Any filter left at
    /// its default (null / empty / 0) is ignored.
    /// </summary>
    public IEnumerable<PatternMetadata> Search(
        string? query = null,
        string? category = null,
        int? periodMin = null,
        int? periodMax = null,
        int? maxSize = null)
    {
        string? lowerQuery = string.IsNullOrWhiteSpace(query) ? null : query.Trim().ToLowerInvariant();

        foreach (var p in _ordered)
        {
            if (!string.IsNullOrEmpty(category)
                && !string.Equals(p.Category, category, StringComparison.OrdinalIgnoreCase))
                continue;

            if (lowerQuery != null)
            {
                bool nameMatch = p.Name.ToLowerInvariant().Contains(lowerQuery);
                bool idMatch = p.Id.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase);
                bool authorMatch = p.Author != null
                    && p.Author.ToLowerInvariant().Contains(lowerQuery);
                if (!(nameMatch || idMatch || authorMatch))
                    continue;
            }

            if (periodMin.HasValue && p.Period is int pp1 && pp1 < periodMin.Value)
                continue;
            if (periodMax.HasValue && p.Period is int pp2 && pp2 > periodMax.Value)
                continue;

            if (maxSize.HasValue && (p.Width > maxSize.Value || p.Height > maxSize.Value))
                continue;

            yield return p;
        }
    }

    private string ReadResource(string resourceName)
    {
        using var stream = _assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Embedded resource not found: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string DeriveId(string resourceName)
    {
        // "<ns>.Patterns.gosper-glider-gun.rle" → "gosper-glider-gun".
        // Resources with nested folders under Patterns (e.g. "...Patterns.oscillators.beacon.rle")
        // still map to the last segment before the .rle suffix.
        int markerIdx = resourceName.IndexOf(ResourceMarker, StringComparison.Ordinal);
        string afterMarker = markerIdx >= 0
            ? resourceName[(markerIdx + ResourceMarker.Length)..]
            : resourceName;
        string trimmed = afterMarker.EndsWith(ResourceSuffix, StringComparison.Ordinal)
            ? afterMarker[..^ResourceSuffix.Length]
            : afterMarker;
        int lastDot = trimmed.LastIndexOf('.');
        return lastDot >= 0 ? trimmed[(lastDot + 1)..] : trimmed;
    }

    private static string ToTitleCase(string id)
    {
        var parts = id.Split('-', '_');
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0) continue;
            parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i][1..];
        }
        return string.Join(' ', parts);
    }
}
