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

    public static bool ValidatePattern(bool[,]? pattern)
    {
        return pattern != null && pattern.GetLength(0) > 0 && pattern.GetLength(1) > 0;
    }
}
