namespace GameOfLife3D.NET.Engine;

public sealed class PatternLoader
{
    private readonly Dictionary<string, PatternInfo> _builtIn = new();

    public PatternLoader()
    {
        InitializeBuiltInPatterns();
    }

    private void InitializeBuiltInPatterns()
    {
        _builtIn["r-pentomino"] = new PatternInfo("R-pentomino",
            "A methuselah that evolves for 1103 generations",
            new bool[,]
            {
                { false, true, true },
                { true, true, false },
                { false, true, false },
            });

        _builtIn["glider"] = new PatternInfo("Glider",
            "A simple spaceship that travels diagonally",
            new bool[,]
            {
                { false, true, false },
                { false, false, true },
                { true, true, true },
            });

        _builtIn["blinker"] = new PatternInfo("Blinker",
            "A simple oscillator with period 2",
            new bool[,]
            {
                { true, true, true },
            });

        _builtIn["pulsar"] = new PatternInfo("Pulsar",
            "A period 3 oscillator",
            new bool[,]
            {
                { false, false, true, true, true, false, false, false, true, true, true, false, false },
                { false, false, false, false, false, false, false, false, false, false, false, false, false },
                { true, false, false, false, false, true, false, true, false, false, false, false, true },
                { true, false, false, false, false, true, false, true, false, false, false, false, true },
                { true, false, false, false, false, true, false, true, false, false, false, false, true },
                { false, false, true, true, true, false, false, false, true, true, true, false, false },
                { false, false, false, false, false, false, false, false, false, false, false, false, false },
                { false, false, true, true, true, false, false, false, true, true, true, false, false },
                { true, false, false, false, false, true, false, true, false, false, false, false, true },
                { true, false, false, false, false, true, false, true, false, false, false, false, true },
                { true, false, false, false, false, true, false, true, false, false, false, false, true },
                { false, false, false, false, false, false, false, false, false, false, false, false, false },
                { false, false, true, true, true, false, false, false, true, true, true, false, false },
            });

        _builtIn["glider-gun"] = new PatternInfo("Gosper Glider Gun",
            "The first known finite pattern with unbounded growth",
            new bool[,]
            {
                { false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, true, false, false, false, false, false, false, false, false, false, false, false },
                { false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, true, false, true, false, false, false, false, false, false, false, false, false, false, false },
                { false, false, false, false, false, false, false, false, false, false, false, false, true, true, false, false, false, false, false, false, true, true, false, false, false, false, false, false, false, false, false, false, false, false, true, true },
                { false, false, false, false, false, false, false, false, false, false, false, true, false, false, false, true, false, false, false, false, true, true, false, false, false, false, false, false, false, false, false, false, false, false, true, true },
                { true, true, false, false, false, false, false, false, false, false, true, false, false, false, false, false, true, false, false, false, true, true, false, false, false, false, false, false, false, false, false, false, false, false, false, false },
                { true, true, false, false, false, false, false, false, false, false, true, false, false, false, true, false, true, true, false, false, false, false, true, false, true, false, false, false, false, false, false, false, false, false, false, false },
                { false, false, false, false, false, false, false, false, false, false, true, false, false, false, false, false, true, false, false, false, false, false, false, false, true, false, false, false, false, false, false, false, false, false, false, false },
                { false, false, false, false, false, false, false, false, false, false, false, true, false, false, false, true, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false },
                { false, false, false, false, false, false, false, false, false, false, false, false, true, true, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false },
            },
            "Bill Gosper");
    }

    public bool[,]? GetBuiltInPattern(string name) =>
        _builtIn.TryGetValue(name, out var info) ? info.Pattern : null;

    public PatternInfo? GetBuiltInPatternInfo(string name) =>
        _builtIn.TryGetValue(name, out var info) ? info : null;

    public IReadOnlyList<PatternInfo> GetAllBuiltInPatterns() => [.. _builtIn.Values];

    public IReadOnlyDictionary<string, PatternInfo> GetBuiltInPatternMap() => _builtIn;

    public static bool[,] ParseRLE(string rleContent)
    {
        var lines = rleContent.Split('\n').Select(l => l.Trim()).ToList();

        int width = 0, height = 0;

        foreach (var line in lines)
        {
            if (line.StartsWith("x ", StringComparison.OrdinalIgnoreCase))
            {
                var match = System.Text.RegularExpressions.Regex.Match(line,
                    @"x\s*=\s*(\d+),\s*y\s*=\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    width = int.Parse(match.Groups[1].Value);
                    height = int.Parse(match.Groups[2].Value);
                }
                break;
            }
        }

        if (width == 0 || height == 0)
            throw new FormatException("Invalid RLE format: missing dimensions");

        var rleData = string.Concat(lines.Where(l =>
            !l.StartsWith('#') &&
            !l.StartsWith("x ", StringComparison.OrdinalIgnoreCase) &&
            l.Length > 0));

        var pattern = new bool[height, width];
        int x = 0, y = 0;
        string countStr = "";

        foreach (char c in rleData)
        {
            if (c >= '0' && c <= '9')
            {
                countStr += c;
                continue;
            }

            int repeat = countStr.Length == 0 ? 1 : int.Parse(countStr);
            countStr = "";

            switch (c)
            {
                case 'b':
                    x += repeat;
                    break;
                case 'o':
                    for (int j = 0; j < repeat; j++)
                    {
                        if (x < width && y < height)
                            pattern[y, x] = true;
                        x++;
                    }
                    break;
                case '$':
                    y += repeat;
                    x = 0;
                    break;
                case '!':
                    return pattern;
            }

            if (x >= width)
            {
                x = 0;
                y++;
            }
        }

        return pattern;
    }

    public static string ExportRLE(bool[,] grid, string ruleString)
    {
        int rows = grid.GetLength(0);
        int cols = grid.GetLength(1);

        // Find bounding box of live cells
        int minRow = rows, maxRow = -1, minCol = cols, maxCol = -1;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (grid[r, c])
                {
                    if (r < minRow) minRow = r;
                    if (r > maxRow) maxRow = r;
                    if (c < minCol) minCol = c;
                    if (c > maxCol) maxCol = c;
                }
            }
        }

        // No live cells — produce minimal valid RLE
        if (maxRow < 0)
            return $"#C Exported from GameOfLife3D.NET\nx = 0, y = 0, rule = {ruleString}\n!\n";

        int width = maxCol - minCol + 1;
        int height = maxRow - minRow + 1;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("#C Exported from GameOfLife3D.NET");
        sb.AppendLine($"x = {width}, y = {height}, rule = {ruleString}");

        // Encode pattern data with run-length encoding
        var line = new System.Text.StringBuilder();
        int pendingNewlines = 0;

        for (int r = minRow; r <= maxRow; r++)
        {
            // Flush pending empty rows as counted $
            if (pendingNewlines > 0)
            {
                AppendRun(line, '$', pendingNewlines, sb);
                pendingNewlines = 0;
            }

            // Run-length encode this row within the bounding box columns
            int c = minCol;
            while (c <= maxCol)
            {
                bool state = grid[r, c];
                int count = 0;
                while (c <= maxCol && grid[r, c] == state)
                {
                    count++;
                    c++;
                }

                // Skip trailing dead cells at end of row
                if (!state && c > maxCol)
                    break;

                AppendRun(line, state ? 'o' : 'b', count, sb);
            }

            // End of row (except last row, which ends with !)
            if (r < maxRow)
                pendingNewlines = 1;
        }

        // Flush remaining line content and terminate
        if (line.Length > 0)
            sb.Append(line);
        sb.AppendLine("!");

        return sb.ToString();
    }

    private static void AppendRun(System.Text.StringBuilder line, char tag, int count, System.Text.StringBuilder output)
    {
        string token = count > 1 ? $"{count}{tag}" : $"{tag}";

        // Wrap lines at 70 characters
        if (line.Length + token.Length > 70)
        {
            output.AppendLine(line.ToString());
            line.Clear();
        }

        line.Append(token);
    }

    public static bool ValidatePattern(bool[,]? pattern)
    {
        return pattern != null && pattern.GetLength(0) > 0 && pattern.GetLength(1) > 0;
    }
}
